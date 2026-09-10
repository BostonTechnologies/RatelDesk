using System.Net;

namespace Helpdesk.Infrastructure.Auth.Authentik;

public sealed class AuthentikAdminRequestException(
    HttpStatusCode statusCode,
    string operation,
    string path,
    string? responseBody)
    : Exception(BuildMessage(statusCode, operation, responseBody))
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string Operation { get; } = operation;
    public string Path { get; } = path;
    public string? ResponseBody { get; } = responseBody;

    private static string BuildMessage(HttpStatusCode statusCode, string operation, string? responseBody)
    {
        var statusText = $"{(int)statusCode} {statusCode}";
        return string.IsNullOrWhiteSpace(responseBody)
            ? $"Authentik rejected {operation} ({statusText})."
            : $"Authentik rejected {operation} ({statusText}): {responseBody}";
    }
}
