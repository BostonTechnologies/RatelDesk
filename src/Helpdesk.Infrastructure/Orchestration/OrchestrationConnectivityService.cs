using Helpdesk.Application.Orchestration;
using System.Diagnostics;
using Helpdesk.Infrastructure.Configuration;
using Helpdesk.Shared.DTOs.Orchestration;
using Microsoft.Extensions.Options;

namespace Helpdesk.Infrastructure.Orchestration;

public sealed class OrchestrationConnectivityService(
    IOptions<OrchestrationM2MOptions> options,
    IOptions<M2MClientOptions> m2mClientOptions,
    IOrchestrationTokenService orchestrationTokenService,
    IOrchestrationInternalClient orchestrationClient) : IOrchestrationConnectivityService
{
    private const string DefaultOrchestrationAudience = "external-orchestration-api";

    private readonly OrchestrationM2MOptions _options = options.Value;
    private readonly M2MClientOptions _m2mClientOptions = m2mClientOptions.Value;
    private readonly IOrchestrationTokenService _orchestrationTokenService = orchestrationTokenService;
    private readonly IOrchestrationInternalClient _orchestrationClient = orchestrationClient;

    public Task<OrchestrationConnectivitySettingsDto> GetOrchestrationSettingsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ToDto(ToResolvedSettings()));

    public async Task<OrchestrationConnectivityTestResultDto> TestOrchestrationConnectivityAsync(CancellationToken cancellationToken = default)
    {
        var settings = ToResolvedSettings();
        if (!settings.Enabled)
        {
            return new OrchestrationConnectivityTestResultDto
            {
                Success = false,
                Message = "External orchestration connectivity is disabled. Set a provider base URL to enable it.",
                Probes =
                [
                    CreateProbe(
                        "AcquireToken",
                        "-",
                        OrchestrationConnectivityTrafficLight.Amber,
                        "Connectivity is disabled.",
                        settings.RemoteSystemName),
                    CreateProbe(
                        "RemoteHealth",
                        "(disabled)",
                        OrchestrationConnectivityTrafficLight.Amber,
                        "Connectivity is disabled.",
                        settings.RemoteSystemName)
                ]
            };
        }

        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return new OrchestrationConnectivityTestResultDto
            {
                Success = false,
                Message = "BaseUrl is not configured.",
                Probes =
                [
                    CreateProbe(
                        "AcquireToken",
                        "-",
                        OrchestrationConnectivityTrafficLight.Amber,
                        "The provider base URL is missing.",
                        settings.RemoteSystemName),
                    CreateProbe(
                        "RemoteHealth",
                        "(disabled)",
                        OrchestrationConnectivityTrafficLight.Amber,
                        "The provider base URL is missing.",
                        settings.RemoteSystemName)
                ]
            };
        }

        var probes = new List<OrchestrationConnectivityProbeResultDto>();

        try
        {
            var tokenStopwatch = Stopwatch.StartNew();
            await _orchestrationTokenService.GetAccessTokenAsync(settings, cancellationToken);
            tokenStopwatch.Stop();

            probes.Add(new OrchestrationConnectivityProbeResultDto
            {
                ProbeName = "AcquireToken",
                Target = settings.TokenEndpoint ?? BuildTokenEndpoint(settings.Authority) ?? "-",
                Status = OrchestrationConnectivityTrafficLight.Green,
                LatencyMs = tokenStopwatch.ElapsedMilliseconds,
                Message = "Token acquired successfully.",
                RemoteSystemName = settings.RemoteSystemName,
                CheckedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            probes.Add(CreateProbe(
                "AcquireToken",
                settings.TokenEndpoint ?? BuildTokenEndpoint(settings.Authority) ?? "-",
                OrchestrationConnectivityTrafficLight.Red,
                Truncate(ex.Message, 256),
                settings.RemoteSystemName));

            return new OrchestrationConnectivityTestResultDto
            {
                Success = false,
                Message = Truncate(ex.Message, 256),
                Probes = probes
            };
        }

        try
        {
            var result = await _orchestrationClient.HealthAsync(settings, cancellationToken);
            probes.Add(new OrchestrationConnectivityProbeResultDto
            {
                ProbeName = "RemoteHealth",
                Target = BuildHealthEndpoint(settings),
                Status = result.Success
                    ? OrchestrationConnectivityTrafficLight.Green
                    : result.StatusCode is 401 or 403
                        ? OrchestrationConnectivityTrafficLight.Amber
                        : OrchestrationConnectivityTrafficLight.Red,
                HttpStatus = result.StatusCode,
                Message = result.Message,
                RemoteSystemName = settings.RemoteSystemName,
                CheckedAtUtc = DateTimeOffset.UtcNow
            });

            return new OrchestrationConnectivityTestResultDto
            {
                Success = result.Success,
                StatusCode = result.StatusCode,
                Message = result.Message,
                Probes = probes
            };
        }
        catch (Exception ex)
        {
            probes.Add(CreateProbe(
                "RemoteHealth",
                BuildHealthEndpoint(settings),
                OrchestrationConnectivityTrafficLight.Red,
                Truncate(ex.Message, 256),
                settings.RemoteSystemName));

            return new OrchestrationConnectivityTestResultDto
            {
                Success = false,
                Message = Truncate(ex.Message, 256),
                Probes = probes
            };
        }
    }

    public async Task<OrchestrationResolvedSettings> GetResolvedOrchestrationSettingsAsync(CancellationToken cancellationToken = default)
        => await Task.FromResult(ToResolvedSettings());

    private OrchestrationResolvedSettings ToResolvedSettings()
    {
        var baseUrl = Normalize(_options.BaseUrl);
        var authority = Normalize(_options.Authority)
            ?? baseUrl;
        var audience = Normalize(_options.Audience)
            ?? DefaultOrchestrationAudience;
        var scope = Normalize(_options.Scope) ?? audience;
        var tokenEndpoint = Normalize(_options.TokenEndpoint)
            ?? BuildTokenEndpoint(authority);
        var enabled = _options.Enabled || !string.IsNullOrWhiteSpace(baseUrl);

        return new OrchestrationResolvedSettings
        {
            Enabled = enabled,
            BaseUrl = baseUrl,
            Audience = audience,
            Authority = authority,
            TokenEndpoint = tokenEndpoint,
            Scope = scope,
            ClientId = Normalize(_m2mClientOptions.ClientId) ?? Normalize(_options.ClientId),
            ClientSecret = Normalize(_m2mClientOptions.ClientSecret) ?? Normalize(_options.ClientSecret),
            RemoteSystemName = Normalize(_options.ProviderName) ?? "External orchestration provider",
            HealthPath = NormalizePath(_options.HealthPath, "/api/v1/health"),
            IngestPath = NormalizePath(_options.IngestPath, "/api/v1/orchestration/ingest"),
            CatalogPath = NormalizePath(_options.CatalogPath, "/api/v1/orchestration/catalog"),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static OrchestrationConnectivitySettingsDto ToDto(OrchestrationResolvedSettings settings)
    {
        return new OrchestrationConnectivitySettingsDto
        {
            Enabled = settings.Enabled,
            BaseUrl = settings.BaseUrl,
            Audience = settings.Audience,
            Scope = settings.Scope,
            Authority = settings.Authority,
            TokenEndpoint = settings.TokenEndpoint,
            RemoteSystemName = settings.RemoteSystemName,
            UpdatedAtUtc = settings.UpdatedAtUtc
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizePath(string? value, string fallback)
    {
        var path = Normalize(value) ?? fallback;
        return path.StartsWith('/') ? path : $"/{path}";
    }

    private static string? BuildTokenEndpoint(string? authority)
    {
        if (string.IsNullOrWhiteSpace(authority))
        {
            return null;
        }

        return $"{authority.TrimEnd('/')}/connect/token";
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string BuildHealthEndpoint(OrchestrationResolvedSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return "(not set)";
        }

        return $"{settings.BaseUrl.TrimEnd('/')}{settings.HealthPath}";
    }

    private static OrchestrationConnectivityProbeResultDto CreateProbe(
        string probeName,
        string target,
        OrchestrationConnectivityTrafficLight status,
        string message,
        string? remoteSystemName,
        int? httpStatus = null,
        long? latencyMs = null)
    {
        return new OrchestrationConnectivityProbeResultDto
        {
            ProbeName = probeName,
            Target = target,
            Status = status,
            HttpStatus = httpStatus,
            LatencyMs = latencyMs,
            Message = message,
            RemoteSystemName = remoteSystemName,
            CheckedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
