using Helpdesk.Shared.Models;
using Helpdesk.Infrastructure.Services;
using Helpdesk.Shared.Services;
using Xunit;

namespace Helpdesk.Tests.Api;

public class PasswordResetServiceTests
{
    [Fact]
    public async Task GenerateAndValidateToken_Works()
    {
        var repo = new InMemoryRepository<PasswordResetToken>();
        var service = new PasswordResetService(repo);
        var token = await service.GenerateTokenAsync("user@test.com");
        var valid = await service.ValidateTokenAsync("user@test.com", token);
        Assert.True(valid);
        await service.InvalidateTokenAsync(token);
        var validAfter = await service.ValidateTokenAsync("user@test.com", token);
        Assert.False(validAfter);
    }
}
