namespace Helpdesk.Infrastructure.Persistence.Connectivity;

public class M2MConnectivitySettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; } = false; // default disabled
    public string? RemoteBaseUrl { get; set; }
    public string? RemoteAudience { get; set; }
    public string? RemoteSystemName { get; set; }
    public string? RemoteTokenEndpoint { get; set; }
    public string? RemoteAuthority { get; set; }
    public string? ClientId { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
