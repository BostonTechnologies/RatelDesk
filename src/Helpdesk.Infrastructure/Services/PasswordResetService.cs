using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;

namespace Helpdesk.Infrastructure.Services;

public interface IPasswordResetService
{
    Task<string> GenerateTokenAsync(string email);
    Task<bool> ValidateTokenAsync(string email, string token);
    Task InvalidateTokenAsync(string token);
}

public class PasswordResetService(IRepository<PasswordResetToken> tokens) : IPasswordResetService
{
    public async Task<string> GenerateTokenAsync(string email)
    {
        var token = new PasswordResetToken
        {
            Id = Guid.NewGuid().ToString(),
            Email = email,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        await tokens.CreateAsync(token);
        return token.Id;
    }

    public async Task<bool> ValidateTokenAsync(string email, string token)
    {
        var stored = await tokens.GetAsync(token);
        return stored is not null && stored.Email == email && stored.ExpiresAt > DateTime.UtcNow;
    }

    public Task InvalidateTokenAsync(string token)
        => tokens.DeleteAsync(token);
}
