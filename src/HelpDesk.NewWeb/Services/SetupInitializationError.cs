using System.Net;
using System.Text.Json;

namespace HelpDesk.NewWeb.Services;

/// <summary>Formats the setup API's safe problem contract without showing raw proxy or database errors.</summary>
public static class SetupInitializationError
{
    public static async Task<string> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        const string fallback = "Initialization could not complete. Check the API setup status and logs before retrying.";
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return "Too many setup attempts. Wait a moment before retrying.";

        try
        {
            // Bound an unexpected proxy response; it must not become a page-sized error or leak raw HTML.
            await response.Content.LoadIntoBufferAsync(16_384);
            cancellationToken.ThrowIfCancellationRequested();
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = problem.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return fallback;
            var code = Text(root, "code");
            var reference = SafeReference(Text(root, "traceId"));
            if (code == "setup_validation_failed" && root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = new List<string>();
                foreach (var field in errors.EnumerateObject())
                {
                    var label = FieldLabel(field.Name);
                    if (label is null || field.Value.ValueKind != JsonValueKind.Array) continue;
                    foreach (var message in field.Value.EnumerateArray().Take(2))
                        if (message.ValueKind == JsonValueKind.String && message.GetString() is { Length: > 0 and <= 512 } text)
                            messages.Add($"{label}: {text}");
                }
                if (messages.Count > 0)
                    return string.Join(" ", messages) + reference;
            }

            // Only known server-side failures get specialized guidance. Never echo arbitrary detail/title fields.
            return (code switch
            {
                "setup_not_ready" => "The saved setup state changed. Check setup status, then unlock the wizard again if initialization is still incomplete.",
                "setup_database_in_use" => "The selected database already contains installation data. Check the API setup status and confirm that this is the intended database.",
                "setup_storage_preflight_failed" => "PostgreSQL did not pass setup checks. Verify connectivity, database permissions, and the vector and pg_trgm extensions.",
                _ => fallback
            }) + reference;
        }
        catch (Exception exception) when (exception is JsonException or HttpRequestException or InvalidOperationException)
        {
            return fallback;
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string SafeReference(string? traceId) =>
        traceId is { Length: > 0 and <= 128 } && traceId.All(character => char.IsAsciiLetterOrDigit(character) || character is ':' or '-' or '_' or '.')
            ? $" API log reference: {traceId}."
            : string.Empty;

    private static string? FieldLabel(string field) => field switch
    {
        "timeZoneId" => "Step 3 — Time zone",
        "applicationUrl" => "Step 3 — Public application URL",
        "organizationName" => "Step 3 — Initial organization",
        "email" => "Step 4 — Administrator email",
        "displayName" => "Step 4 — Administrator display name",
        "password" => "Step 4 — Passphrase",
        "administrator" => "Step 4 — Administrator account",
        "supportEmail" => "Step 5 — Support email",
        "supportUrl" => "Step 5 — Support URL",
        "logoUrl" => "Step 5 — Main logo URL",
        "compactLogoUrl" => "Step 5 — Compact logo URL",
        _ => null
    };
}
