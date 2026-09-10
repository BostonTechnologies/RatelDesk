using System.Text.Json.Nodes;

internal sealed class CliValidationException(string message, JsonObject? details = null) : Exception(message)
{
    public JsonObject? Details { get; } = details;
}

internal sealed class CliRemoteException(string code, string message, int statusCode, string? responseBody) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public string? ResponseBody { get; } = responseBody;
}
