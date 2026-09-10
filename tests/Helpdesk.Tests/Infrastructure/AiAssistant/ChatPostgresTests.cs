using System.Text.Json;
using Helpdesk.Application.Events;
using Helpdesk.Application.AiAssistant.Chat;
using Helpdesk.Infrastructure.AiAssistant.Chat;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.AiAssistant.Chat;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Helpdesk.Tests.Infrastructure.AiAssistant;

public sealed class ChatPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg16").Build();
    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await using var db = Context();
        await db.Database.EnsureCreatedAsync();
        db.Organizations.Add(new Organization { Id = "org", Name = "Test organization" });
        await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => container.DisposeAsync().AsTask();
    public HelpdeskDbContext Context(string organization = "org")
    {
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(organization);
        return Context(tenant);
    }
    public HelpdeskDbContext Context(ITenantContext tenant) => new(new DbContextOptionsBuilder<HelpdeskDbContext>().UseNpgsql(container.GetConnectionString(), x => x.UseVector()).Options, tenant, new HttpContextAccessor());
    public AiAssistantChatStore Store(HelpdeskDbContext db, string organization = "org")
    {
        var tenant = Substitute.For<ITenantContext>(); tenant.TenantId.Returns(organization);
        var correlation = Substitute.For<ICorrelationContext>(); correlation.GetCorrelationId().Returns("test-chat");
        return new(db, tenant, Substitute.For<IDomainEventPublisher>(), correlation, Microsoft.Extensions.Logging.Abstractions.NullLogger<AiAssistantChatStore>.Instance);
    }
    public async Task<(string Ticket, Guid Conversation)> CreateAsync()
    {
        await using var db = Context();
        var ticket = new Incident { Id = Guid.NewGuid().ToString(), Title = "Test", OrganizationId = "org", TrackingId = Guid.NewGuid().ToString() };
        db.Add(ticket); await db.SaveChangesAsync();
        var snapshot = await Store(db).LoadAsync("incidents", ticket.Id, null, 0, "operator", default);
        return (ticket.Id, snapshot.ConversationId);
    }
}

public sealed class ChatPostgresTests(ChatPostgresFixture fixture) : IClassFixture<ChatPostgresFixture>
{
    [Fact]
    public async Task ConcurrentIdenticalResolutionsCreateOnlyOneSuccessor()
    {
        var (ticket, conversation) = await fixture.CreateAsync();
        var message = Guid.NewGuid();
        await using (var db = fixture.Context())
        {
            await fixture.Store(db).AcceptMessageAsync("incidents", ticket, new(conversation, message, "Uncertain"), "operator", default);
            var chat = await db.Set<AiAssistantChatConversation>().SingleAsync(x => x.Id == conversation);
            chat.State = ChatState.DeliveryUnknown;
            await db.SaveChangesAsync();
        }
        var request = new ChatAbandonRequest(conversation, Guid.NewGuid(), message, true);
        async Task ResolveAsync()
        {
            await using var db = fixture.Context();
            await fixture.Store(db).AbandonAsync("incidents", ticket, request, "operator", default);
        }
        await Task.WhenAll(ResolveAsync(), ResolveAsync());
        await using var check = fixture.Context();
        Assert.Single(await check.Set<AiAssistantChatEvent>().Where(x => x.ConversationId == conversation && x.Type == "delivery_abandoned").ToListAsync());
        Assert.Equal(2, await check.Set<AiAssistantChatConversation>().CountAsync(x => x.TicketId == ticket));
        await Assert.ThrowsAsync<ChatConflictException>(() => fixture.Store(check).AbandonAsync("incidents", ticket, request, "different-operator", default));
    }

    [Fact]
    public async Task StreamedDeltasProduceOneCanonicalMessageAndSafeToolMetadata()
    {
        await using var services = new ServiceCollection().AddScoped(_ => fixture.Context())
            .AddSingleton(Substitute.For<IDomainEventPublisher>())
            .AddSingleton(Substitute.For<ICorrelationContext>()).BuildServiceProvider();
        var client = new FailingChatClient { FailSend = false };
        var factory = Substitute.For<IAiAssistantChatClientFactory>();
        factory.Create().Returns(client);
        var feed = new ChatLiveFeed();
        using var manager = new AiAssistantChatSessionManager(services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new AiAssistantChatOptions { Enabled = true }), feed,
            NullLogger<AiAssistantChatSessionManager>.Instance, factory);
        await manager.StartAsync(default);
        try
        {
            var (ticket, conversation) = await fixture.CreateAsync();
            var request = new ChatMessageRequest(conversation, Guid.NewGuid(), "Hello");
            await using (var db = fixture.Context())
                await fixture.Store(db).AcceptMessageAsync("incidents", ticket, request, "operator", default);
            var reader = feed.Subscribe(conversation);
            try
            {
                await manager.SendAsync(conversation, request.ClientMessageId, request.Text, default);
                await client.EmitAsync(new { type = "text_delta", sessionId = "signalr/test", text = "Hel" });
                await client.EmitAsync(new { type = "text_delta", sessionId = "signalr/test", text = "lo" });
                await client.EmitAsync(new { type = "tool_call", sessionId = "signalr/test", toolName = "safe_tool", callId = "call", argumentsJson = "NEVER_PERSIST_SECRET" });
                await client.EmitAsync(new { type = "tool_result", sessionId = "signalr/test", toolName = "safe_tool", callId = "call", result = "NEVER_PERSIST_SECRET" });
                await client.EmitAsync(new { type = "text", sessionId = "signalr/test", text = "Hello" });
                await client.EmitAsync(new { type = "turn_completed", sessionId = "signalr/test" });
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                while (await reader.WaitToReadAsync(timeout.Token))
                {
                    while (reader.TryRead(out _)) { /* Drain wake-ups before the authoritative read. */ }
                    await using var db = fixture.Context();
                    if ((await db.Set<AiAssistantChatConversation>().SingleAsync(x => x.Id == conversation, timeout.Token)).State != ChatState.Idle) continue;
                    var events = await db.Set<AiAssistantChatEvent>().Where(x => x.ConversationId == conversation).ToListAsync(timeout.Token);
                    Assert.Equal("Hello", Assert.Single(events, x => x.Type == "assistant").Text);
                    Assert.DoesNotContain(events, x => x.Type == "text_delta");
                    Assert.DoesNotContain("NEVER_PERSIST_SECRET", JsonSerializer.Serialize(events));
                    return;
                }
                Assert.Fail("The chat feed closed before turn completion.");
            }
            finally { feed.Unsubscribe(conversation, reader); }
        }
        finally { await manager.StopAsync(default); }
    }

    [Fact]
    public async Task AmbiguousSendIsDurableAndNeverAutomaticallyResent()
    {
        await using var services = new ServiceCollection()
            .AddScoped(_ => fixture.Context())
            .BuildServiceProvider();
        var client = new FailingChatClient();
        var factory = Substitute.For<IAiAssistantChatClientFactory>();
        factory.Create().Returns(client);
        using var manager = new AiAssistantChatSessionManager(services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new AiAssistantChatOptions { Enabled = true }), new ChatLiveFeed(),
            NullLogger<AiAssistantChatSessionManager>.Instance, factory);
        await manager.StartAsync(default);
        try
        {
            var (ticket, conversation) = await fixture.CreateAsync();
            var message = new ChatMessageRequest(conversation, Guid.NewGuid(), "Hello");
            await using (var db = fixture.Context())
                Assert.True(await fixture.Store(db).AcceptMessageAsync("incidents", ticket, message, "operator", default));
            await manager.SendAsync(conversation, message.ClientMessageId, message.Text, default);
            await using var check = fixture.Context();
            Assert.Equal(ChatState.DeliveryUnknown, (await check.Set<AiAssistantChatConversation>().SingleAsync(x => x.Id == conversation)).State);
            Assert.Equal(1, client.SendCount);
            Assert.False(await fixture.Store(check).AcceptMessageAsync("incidents", ticket, message, "operator", default));
            Assert.Equal(1, client.SendCount);
            Assert.Contains("helpdeskOperator", client.SentText);
            Assert.Contains("operator", client.SentText);
        }
        finally { await manager.StopAsync(default); }
    }

    [Fact]
    public async Task SecondEnabledApiCannotStartAgainstSameDatabase()
    {
        await using var services = new ServiceCollection().AddScoped(_ => fixture.Context()).BuildServiceProvider();
        AiAssistantChatSessionManager Create() => new(services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new AiAssistantChatOptions { Enabled = true }), new ChatLiveFeed(),
            NullLogger<AiAssistantChatSessionManager>.Instance, Substitute.For<IAiAssistantChatClientFactory>());
        using var first = Create();
        using var second = Create();
        await first.StartAsync(default);
        try { await Assert.ThrowsAsync<InvalidOperationException>(() => second.StartAsync(default)); }
        finally { await first.StopAsync(default); }
    }

    private sealed class FailingChatClient : IAiAssistantChatClient
    {
        private Func<JsonElement, Task>? output;
        public bool FailSend { get; init; } = true;
        public bool IsConnected => true;
        public int SendCount { get; private set; }
        public string SentText { get; private set; } = string.Empty;
        public Task<SessionEnsureResult> ConnectAsync(string? sessionId, Func<JsonElement, Task> output, CancellationToken ct)
        {
            this.output = output;
            return Task.FromResult(new SessionEnsureResult(sessionId ?? "signalr/test", sessionId is null));
        }
        public Task EmitAsync(object value) => output!(JsonSerializer.SerializeToElement(value));
        public Task SendAsync(string sessionId, string text, CancellationToken ct)
        {
            SendCount++;
            SentText = text;
            return FailSend ? Task.FromException(new IOException("Acknowledgement lost")) : Task.CompletedTask;
        }
        public Task RespondAsync(string sessionId, string callId, string key, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ConcurrentMessagesAdmitExactlyOneTurn()
    {
        var (ticket, conversation) = await fixture.CreateAsync();
        async Task<bool> Submit()
        {
            await using var db = fixture.Context();
            try { return await fixture.Store(db).AcceptMessageAsync("incidents", ticket, new(conversation, Guid.NewGuid(), "Hello"), "operator", default); }
            catch (ChatConflictException) { return false; }
        }
        var outcomes = await Task.WhenAll(Submit(), Submit());
        Assert.Single(outcomes, x => x);
        await using var check = fixture.Context();
        var events = await check.Set<AiAssistantChatEvent>().Where(x => x.ConversationId == conversation).OrderBy(x => x.Sequence).ToListAsync();
        Assert.Equal(new long[] { 1, 2 }, events.Select(x => x.Sequence));
        Assert.Single(events, x => x.Type == "operator");
    }

    [Fact]
    public async Task IdenticalIdempotencyKeyReturnsExistingWhileProcessing()
    {
        var (ticket, conversation) = await fixture.CreateAsync();
        var request = new ChatMessageRequest(conversation, Guid.NewGuid(), "Hello");
        await using (var first = fixture.Context()) Assert.True(await fixture.Store(first).AcceptMessageAsync("incidents", ticket, request, "operator", default));
        await using var next = fixture.Context();
        Assert.False(await fixture.Store(next).AcceptMessageAsync("incidents", ticket, request, "operator", default));
        Assert.Single(await next.Set<AiAssistantChatEvent>().Where(x => x.ConversationId == conversation && x.ClientMessageId == request.ClientMessageId).ToListAsync());
    }

    [Fact]
    public async Task CompetingApprovalResponsesHaveOneWinner()
    {
        var (ticket, conversation) = await fixture.CreateAsync();
        await using (var setup = fixture.Context())
        {
            var chat = await setup.Set<AiAssistantChatConversation>().SingleAsync(x => x.Id == conversation);
            chat.State = ChatState.AwaitingApproval;
            setup.Add(new AiAssistantChatInteraction { ConversationId = conversation, CallId = "call", OptionsJson = JsonSerializer.Serialize(new[] { new ChatOption("deny", "Deny") }) });
            await setup.SaveChangesAsync();
        }
        async Task<bool> Answer(string actor)
        {
            await using var db = fixture.Context();
            try { await fixture.Store(db).AcceptApprovalAsync("incidents", ticket, "call", new(conversation, "deny"), actor, default); return true; }
            catch (ChatConflictException) { return false; }
        }
        Assert.Single(await Task.WhenAll(Answer("one"), Answer("two")), x => x);
        await using var check = fixture.Context();
        Assert.Single(await check.Set<AiAssistantChatEvent>().Where(x => x.ConversationId == conversation && x.Type == "approval_response").ToListAsync());
    }

    [Fact]
    public async Task CursorReplaySurvivesFreshContextAndRejectsCrossTenant()
    {
        var (ticket, conversation) = await fixture.CreateAsync();
        await using (var db = fixture.Context()) await fixture.Store(db).AcceptMessageAsync("incidents", ticket, new(conversation, Guid.NewGuid(), "Hello"), "operator", default);
        await using (var db = fixture.Context())
        {
            var snapshot = await fixture.Store(db).LoadAsync("incidents", ticket, conversation, 1, "operator", default);
            Assert.Equal(2, Assert.Single(snapshot.Events).Sequence);
            await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store(db).LoadAsync("incidents", ticket, conversation, 100, "operator", default));
        }
        await using var other = fixture.Context("other");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Store(other, "other").LoadAsync("incidents", ticket, conversation, 0, "other-operator", default));
    }
}
