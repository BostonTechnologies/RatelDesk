using System.Text.RegularExpressions;

namespace Helpdesk.Application.AiAssistant.Chat;

public static partial class ChatOutputSafety
{
    public static string Identifier(string? text) => text is not null && text.Length <= 100 && IdentifierPattern().IsMatch(text) ? text : "tool";
    public static string FileName(string? text)
    {
        var name = (text ?? "artifact").Replace('\\', '/').Split('/').Last();
        return name.Length <= 128 && FileNamePattern().IsMatch(name) ? name : "artifact";
    }
    public static string MimeType(string? text) => text is not null && text.Length <= 100 && MimePattern().IsMatch(text) ? text : "application/octet-stream";
    [GeneratedRegex("^[A-Za-z0-9_.-]+$")] private static partial Regex IdentifierPattern();
    [GeneratedRegex("^[A-Za-z0-9_. -]+$")] private static partial Regex FileNamePattern();
    [GeneratedRegex("^[A-Za-z0-9.+-]+/[A-Za-z0-9.+-]+$")] private static partial Regex MimePattern();
}
