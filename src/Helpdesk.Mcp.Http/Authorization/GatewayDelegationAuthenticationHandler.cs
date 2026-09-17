using System.Security.Claims;
using System.Text.Json;
using Helpdesk.AgentClient;
using Helpdesk.Mcp.Http.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Helpdesk.Mcp.Http.Authorization;

/// <summary>
/// Authenticates a locally paired HTTP MCP caller by exchanging its MCP-only
/// opaque credential at the configured API. The inbound credential is used
/// only for that narrow exchange and is never retained or sent to business
/// endpoints.
/// </summary>
public sealed class GatewayDelegationAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder,
    IHttpClientFactory clients,
    IOptions<HelpdeskMcpHttpOptions> mcpOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "HelpdeskMcpGateway";
    public const string ExecutionTokenItemKey = "RatelDesk.Mcp.ExecutionToken";
    public const string DelegationClientName = "Helpdesk.Mcp.Http.Delegation";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer rdk_", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        try
        {
            using var delegationRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/mcp/execution-token");
            delegationRequest.Headers.TryAddWithoutValidation("Authorization", authorization);
            delegationRequest.Headers.TryAddWithoutValidation("X-RatelDesk-Mcp-Resource", mcpOptions.Value.PublicResourceUri.TrimEnd('/'));
            using var response = await clients.CreateClient(DelegationClientName)
                .SendAsync(delegationRequest, HttpCompletionOption.ResponseHeadersRead, Context.RequestAborted)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return AuthenticateResult.Fail("The MCP credential could not be delegated for this gateway.");

            await using var stream = await response.Content.ReadAsStreamAsync(Context.RequestAborted).ConfigureAwait(false);
            var delegated = await JsonSerializer.DeserializeAsync<DelegatedCredential>(stream, SerializerOptions, Context.RequestAborted).ConfigureAwait(false);
            if (delegated is null || string.IsNullOrWhiteSpace(delegated.AccessToken) || delegated.ExpiresAtUtc <= DateTimeOffset.UtcNow ||
                string.IsNullOrWhiteSpace(delegated.UserId))
            {
                return AuthenticateResult.Fail("The MCP gateway received an invalid execution credential.");
            }

            Context.Items[ExecutionTokenItemKey] = delegated.AccessToken;
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, delegated.UserId),
                new(ClaimTypes.Name, delegated.Name ?? delegated.UserId),
                new("auth_mode", "mcp"),
                new("token_use", "mcp_gateway")
            };
            if (!string.IsNullOrWhiteSpace(delegated.PrimaryOrganizationId))
                claims.Add(new Claim("organization_id", delegated.PrimaryOrganizationId));
            foreach (var permission in delegated.Permissions ?? [])
                claims.Add(new Claim("delegated_permission", permission));
            foreach (var organizationId in delegated.AllowedOrganizationIds ?? [])
                claims.Add(new Claim("allowed_organization_id", organizationId));

            var identity = new ClaimsIdentity(claims, SchemeName, ClaimTypes.Name, ClaimTypes.Role);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
        }
        catch (OperationCanceledException) when (Context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            return AuthenticateResult.Fail("The MCP gateway could not reach the delegation endpoint.");
        }
    }

    private sealed record DelegatedCredential(
        string AccessToken,
        DateTimeOffset ExpiresAtUtc,
        string UserId,
        string? Name,
        string? PrimaryOrganizationId,
        string[]? Permissions,
        string[]? AllowedOrganizationIds);
}

/// <summary>Gets only the API-issued execution token for the active request.</summary>
public sealed class GatewayExecutionTokenProvider(IHttpContextAccessor httpContextAccessor) : IAgentAccessTokenProvider
{
    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = httpContextAccessor.HttpContext?.Items[GatewayDelegationAuthenticationHandler.ExecutionTokenItemKey] as string;
        return string.IsNullOrWhiteSpace(token)
            ? Task.FromException<string>(new AgentClientValidationException("No delegated MCP execution credential is available for this request."))
            : Task.FromResult(token);
    }
}
