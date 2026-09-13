namespace HelpDesk.NewWeb.Services;

/// <summary>Forwards only the current browser's local ticket, including ASP.NET cookie chunks.</summary>
public static class LocalSessionCookieForwarder
{
    public static string? GetHeader(HttpContext context, string cookieName)
    {
        if (!context.Request.Cookies.TryGetValue(cookieName, out var value) || string.IsNullOrWhiteSpace(value))
            return null;

        return string.Join("; ", context.Request.Cookies
            .Where(cookie => string.Equals(cookie.Key, cookieName, StringComparison.Ordinal) ||
                (cookie.Key.StartsWith(cookieName + "C", StringComparison.Ordinal) &&
                 int.TryParse(cookie.Key.AsSpan(cookieName.Length + 1), out var chunk) && chunk > 0))
            .Select(cookie => $"{cookie.Key}={cookie.Value}"));
    }
}
