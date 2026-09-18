using System.Security.Claims;
using System.Net;
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
    internal const int DefaultExchangeTimeoutSeconds = 20;
    internal const int MaximumDelegationResponseBytes = 16 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer rdk_", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Context.RequestAborted);
        deadline.CancelAfter(TimeSpan.FromSeconds(mcpOptions.Value.DelegationTimeoutSeconds));
        try
        {
            using var delegationRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/mcp/execution-token");
            delegationRequest.Headers.TryAddWithoutValidation("Authorization", authorization);
            delegationRequest.Headers.TryAddWithoutValidation("X-RatelDesk-Mcp-Resource", mcpOptions.Value.PublicResourceUri.TrimEnd('/'));
            using var response = await clients.CreateClient(DelegationClientName)
                .SendAsync(delegationRequest, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                .ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return AuthenticateResult.Fail("The MCP credential could not be delegated for this gateway.");
            if (!response.IsSuccessStatusCode)
                return AuthenticateResult.Fail("The MCP delegation service is temporarily unavailable.");

            var delegated = await ReadDelegatedCredentialAsync(response.Content, deadline.Token).ConfigureAwait(false);
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
        catch (OperationCanceledException)
        {
            return AuthenticateResult.Fail("The MCP delegation service timed out.");
        }
        catch (JsonException)
        {
            return AuthenticateResult.Fail("The MCP gateway received an invalid execution credential.");
        }
        catch (HttpRequestException)
        {
            return AuthenticateResult.Fail("The MCP gateway could not reach the delegation endpoint.");
        }
    }

    private static async Task<DelegatedCredential?> ReadDelegatedCredentialAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaximumDelegationResponseBytes)
            throw new JsonException("The delegation response exceeds the permitted size.");

        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        while (true)
        {
            var count = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (count == 0)
                break;

            if (buffer.Length + count > MaximumDelegationResponseBytes)
                throw new JsonException("The delegation response exceeds the permitted size.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }

        return JsonSerializer.Deserialize<DelegatedCredential>(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)), SerializerOptions);
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
