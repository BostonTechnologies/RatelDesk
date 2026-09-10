using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;

namespace Helpdesk.Infrastructure.Services;

/// <summary>
/// Defines methods for generating, validating, and invalidating two-factor authentication (2FA) codes.
/// </summary>
/// <remarks>This service is designed to support two-factor authentication workflows by providing functionality to
/// generate unique codes, validate them against user input, and invalidate codes when they are no longer needed. It is
/// typically used in scenarios where an additional layer of security is required for user authentication.</remarks>
public interface ITwoFactorService
{
    Task<string> GenerateCodeAsync(string email);
    Task<bool> ValidateCodeAsync(string email, string code);
    Task InvalidateCodeAsync(string code);
}

/// <summary>
/// Provides functionality for generating, validating, and invalidating two-factor authentication (2FA) codes.
/// </summary>
/// <remarks>This service is designed to manage time-sensitive two-factor authentication codes associated with
/// user email addresses. It supports generating new codes, validating existing codes, and invalidating codes when they
/// are no longer needed.</remarks>
/// <param name="codes"></param>
public class TwoFactorService(IRepository<TwoFactorCode> codes) : ITwoFactorService
{
    public async Task<string> GenerateCodeAsync(string email)
    {
        var rnd = Random.Shared.Next(0, 999999);
        var code = rnd.ToString("D6");
        var entry = new TwoFactorCode
        {
            Id = code,
            Email = email,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        await codes.CreateAsync(entry);
        return code;
    }

    public async Task<bool> ValidateCodeAsync(string email, string code)
    {
        var stored = await codes.GetAsync(code);
        return stored is not null && stored.Email == email && stored.ExpiresAt > DateTime.UtcNow;
    }

    public Task InvalidateCodeAsync(string code)
        => codes.DeleteAsync(code);
}
