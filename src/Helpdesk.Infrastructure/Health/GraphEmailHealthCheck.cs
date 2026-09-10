using Helpdesk.Application.Services.Email;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Helpdesk.Infrastructure.Health;

public sealed class GraphEmailHealthCheck : IHealthCheck
{
    private readonly IEmailService _email;

    public GraphEmailHealthCheck(IEmailService email)
    {
        _email = email;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var ok = await _email.TestApiConnectionAsync();

        return ok
            ? HealthCheckResult.Healthy("Graph email operational")
            : HealthCheckResult.Unhealthy("Graph email not initialized");
    }
}
