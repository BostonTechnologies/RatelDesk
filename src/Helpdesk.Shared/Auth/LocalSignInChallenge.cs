namespace Helpdesk.Shared.Auth;

/// <summary>A short-lived proof of password verification, never an authenticated application session.</summary>
public sealed record LocalSignInChallenge(string UserId, string SecurityStamp, long AuthorizationRevision, bool RememberMe)
{
    public const string ProtectionPurpose = "RatelDesk.LocalSignInChallenge.v1";

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public static string CookieName(bool allowInsecureLocalhost) =>
        allowInsecureLocalhost ? "RatelDesk.LocalChallenge" : "__Host-RatelDesk.LocalChallenge";
}
