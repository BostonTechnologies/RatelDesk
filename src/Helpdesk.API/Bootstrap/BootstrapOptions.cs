namespace Helpdesk.API.Bootstrap;

public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string StateDirectory { get; init; } = "/var/lib/rateldesk/bootstrap";

    public string DataDirectory { get; init; } = "/var/lib/rateldesk/data";

    public string? SetupCode { get; init; }

    /// <summary>
    /// Non-secret values owned by the deployment. Interactive setup displays
    /// these values but cannot replace them.
    /// </summary>
    public BootstrapInteractiveOptions Interactive { get; init; } = new();

    public BootstrapUnattendedOptions Unattended { get; init; } = new();
}

public sealed class BootstrapInteractiveOptions
{
    public string? OrganizationName { get; init; }

    public string? ApplicationName { get; init; }

    public string? ApplicationUrl { get; init; }

    public string? TimeZoneId { get; init; }
}

public sealed class BootstrapUnattendedOptions
{
    public string? Provider { get; init; }

    public string? SqlitePath { get; init; }

    public string? PostgreSqlConnectionString { get; init; }

    public string? Email { get; init; }

    public string? DisplayName { get; init; }

    public string? Password { get; init; }

    public string? OrganizationName { get; init; }

    public string? ApplicationName { get; init; }

    public string? ApplicationUrl { get; init; }

    public string? TimeZoneId { get; init; }
}
