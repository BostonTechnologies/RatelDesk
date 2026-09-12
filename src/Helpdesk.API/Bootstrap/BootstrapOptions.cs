namespace Helpdesk.API.Bootstrap;

public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string StateDirectory { get; init; } = "/var/lib/rateldesk/bootstrap";

    public string? SetupCode { get; init; }
}
