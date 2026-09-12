namespace Helpdesk.API.Endpoints.Authentication;

public sealed class LocalAuthenticationOptions
{
    public const string Scheme = "RatelDeskLocal";

    public const string SectionName = "Authentication";

    public string Mode { get; init; } = "Oidc";

    public bool AllowInsecureLocalhost { get; init; }

    public bool SupportsLocalAccounts => string.Equals(Mode, "Local", StringComparison.OrdinalIgnoreCase)
                                      || string.Equals(Mode, "Hybrid", StringComparison.OrdinalIgnoreCase);
}
