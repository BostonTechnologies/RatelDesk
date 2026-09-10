using Helpdesk.Application.Services.AI;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Helpdesk.Tests.Application.Services;

public class AiProviderServiceTests
{
    private static (HelpdeskDbContext ctx, IAiClient client, ISecretProtector protector, AiProviderService svc) CreateService()
    {
        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenant = Substitute.For<ITenantContext>();
        var ctx = new HelpdeskDbContext(options, tenant, new HttpContextAccessor());
        var client = Substitute.For<IAiClient>();
        var protector = Substitute.For<ISecretProtector>();
        protector.Protect(Arg.Any<string>()).Returns(ci => $"enc-{ci.Arg<string>()}");
        var logger = Substitute.For<ILogger<AiProviderService>>();
        var svc = new AiProviderService(ctx, protector, client, logger);
        return (ctx, client, protector, svc);
    }

    [Fact]
    public async Task Crud_Operations_Encrypt_ApiKey()
    {
        var (ctx, client, protector, svc) = CreateService();
        var provider = new AiProvider { Name = "Test", ProviderType = AiProviderType.OpenAI, BaseUrl = "https://api" };

        await svc.CreateAsync(provider, "key", CancellationToken.None);
        var created = await svc.GetAsync(provider.Id, CancellationToken.None);
        Assert.NotNull(created);
        Assert.Equal("enc-key", created!.ApiKeyEncrypted);
        protector.Received(1).Protect("key");

        provider.Name = "Updated";
        await svc.UpdateAsync(provider, "new", CancellationToken.None);
        var updated = await svc.GetAsync(provider.Id, CancellationToken.None);
        Assert.Equal("Updated", updated!.Name);
        Assert.Equal("enc-new", updated.ApiKeyEncrypted);
        protector.Received().Protect("new");

        var deleted = await svc.DeleteAsync(provider.Id, CancellationToken.None);
        Assert.True(deleted);
        Assert.Empty(ctx.AiProviders);
    }


    [Fact]
    public async Task TestAsync_ReturnsFalse_WhenClientFails()
    {
        var (ctx, client, _, svc) = CreateService();
        var provider = new AiProvider { Name = "Test", ProviderType = AiProviderType.OpenAI, BaseUrl = "https://api" };
        await svc.CreateAsync(provider, "key", CancellationToken.None);

        client.TestAsync(Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns(new AiProviderConnectionTestResult(false, Message: "failed"));

        var result = await svc.TestAsync(provider.Id, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal("failed", result.Message);
    }
}
