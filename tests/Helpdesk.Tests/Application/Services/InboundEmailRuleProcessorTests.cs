using Helpdesk.Application.Services.Email;
using Helpdesk.Infrastructure.Email;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.EmailRules;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Helpdesk.Tests.Application.Services;

public class InboundEmailRuleProcessorTests
{
    [Fact]
    public async Task ProcessAsync_EvaluatesTenantRulesBeforeGlobalRules()
    {
        await using var db = CreateDb();
        db.InboundEmailRules.Add(new InboundEmailRule
        {
            Id = "global",
            ScopeType = InboundEmailRuleScopeType.Global,
            Enabled = true,
            Priority = 1,
            Name = "Global",
            ConditionsJson = "[]",
            ActionsJson = """[{"type":0,"actionKey":"global"}]"""
        });
        db.InboundEmailRules.Add(new InboundEmailRule
        {
            Id = "tenant",
            ScopeType = InboundEmailRuleScopeType.Tenant,
            TenantId = "tenant-1",
            Enabled = true,
            Priority = 99,
            Name = "Tenant",
            ConditionsJson = "[]",
            ActionsJson = """[{"type":0,"actionKey":"tenant"}]"""
        });
        await db.SaveChangesAsync();

        var executor = Substitute.For<IInboundEmailActionExecutor>();
        var calls = new List<string>();
        executor.ExecuteAsync(
                Arg.Any<InboundEmailRule>(),
                Arg.Do<InboundEmailRuleActionConfig>(x => calls.Add(x.ActionKey!)),
                Arg.Any<InboundEmailContext>(),
                Arg.Any<ForwardedEmailParseResult?>(),
                Arg.Any<CancellationToken>())
            .Returns(new InboundEmailRuleProcessingResult(false, false, null));

        var processor = new InboundEmailRuleProcessor(
            db,
            Substitute.For<IForwardedEmailParser>(),
            executor,
            NullLogger<InboundEmailRuleProcessor>.Instance);

        await processor.ProcessAsync(Context(mailboxTenantId: "tenant-1"));

        Assert.Equal(["tenant", "global"], calls);
    }

    [Fact]
    public async Task ProcessAsync_StopProcessing_ReturnsHandled()
    {
        await using var db = CreateDb();
        db.InboundEmailRules.Add(new InboundEmailRule
        {
            Id = "rule-1",
            ScopeType = InboundEmailRuleScopeType.Global,
            Enabled = true,
            Priority = 1,
            StopProcessing = true,
            Name = "Rule",
            ConditionsJson = "[]",
            ActionsJson = """[{"type":0,"actionKey":"create"}]"""
        });
        await db.SaveChangesAsync();

        var executor = Substitute.For<IInboundEmailActionExecutor>();
        executor.ExecuteAsync(
                Arg.Any<InboundEmailRule>(),
                Arg.Any<InboundEmailRuleActionConfig>(),
                Arg.Any<InboundEmailContext>(),
                Arg.Any<ForwardedEmailParseResult?>(),
                Arg.Any<CancellationToken>())
            .Returns(new InboundEmailRuleProcessingResult(true, true, new Incident { Id = "inc-1", TrackingId = "INC-1" }));

        var processor = new InboundEmailRuleProcessor(
            db,
            Substitute.For<IForwardedEmailParser>(),
            executor,
            NullLogger<InboundEmailRuleProcessor>.Instance);

        var result = await processor.ProcessAsync(Context());

        Assert.True(result.Handled);
        Assert.True(result.StopDefaultProcessing);
        Assert.Equal("inc-1", result.Ticket!.Id);
    }

    [Fact]
    public async Task ProcessAsync_ForwardedButMissingRequester_StillExecutesActionForAuditAndStop()
    {
        await using var db = CreateDb();
        db.InboundEmailRules.Add(new InboundEmailRule
        {
            Id = "rule-1",
            ScopeType = InboundEmailRuleScopeType.Global,
            Enabled = true,
            Priority = 1,
            StopProcessing = true,
            Name = "Rule",
            ConditionsJson = """[{"type":2},{"type":3}]""",
            ActionsJson = """[{"type":0,"actionKey":"create"}]"""
        });
        await db.SaveChangesAsync();

        var parser = Substitute.For<IForwardedEmailParser>();
        parser.Parse(Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new ForwardedEmailParseResult(
                ForwardedEmailParseStatus.MissingOriginalSender,
                null,
                null,
                null,
                null,
                "Printer",
                null,
                null,
                0.3));
        var executor = Substitute.For<IInboundEmailActionExecutor>();
        executor.ExecuteAsync(
                Arg.Any<InboundEmailRule>(),
                Arg.Any<InboundEmailRuleActionConfig>(),
                Arg.Any<InboundEmailContext>(),
                Arg.Any<ForwardedEmailParseResult?>(),
                Arg.Any<CancellationToken>())
            .Returns(new InboundEmailRuleProcessingResult(true, true, null));

        var processor = new InboundEmailRuleProcessor(
            db,
            parser,
            executor,
            NullLogger<InboundEmailRuleProcessor>.Instance);

        var result = await processor.ProcessAsync(Context());

        Assert.True(result.Handled);
        await executor.Received(1).ExecuteAsync(
            Arg.Any<InboundEmailRule>(),
            Arg.Any<InboundEmailRuleActionConfig>(),
            Arg.Any<InboundEmailContext>(),
            Arg.Any<ForwardedEmailParseResult?>(),
            Arg.Any<CancellationToken>());
    }

    private static HelpdeskDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HelpdeskDbContext(options, Substitute.For<ITenantContext>(), new HttpContextAccessor());
    }

    private static InboundEmailContext Context(string? mailboxTenantId = null) => new(
        "message-1",
        "graph-1",
        null,
        mailboxTenantId,
        "support@example.com",
        "tech@example.com",
        "Tech",
        [],
        [],
        "Forwarded",
        "",
        "",
        DateTimeOffset.UtcNow,
        new Dictionary<string, string>(),
        []);
}
