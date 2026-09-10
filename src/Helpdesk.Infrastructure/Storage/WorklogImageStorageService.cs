using System.Text.RegularExpressions;
using Helpdesk.Application.WorkLogs;

namespace Helpdesk.Infrastructure.Storage;

public sealed class WorklogImageStorageService(IInlineImageStorageService inlineImageStorageService) : IWorklogImageStorageService
{
    private static readonly Regex InlineImageRegex = new(
        "<img\\b[^>]*?\\bsrc\\s*=\\s*[\"'](?<src>data:image/(?<type>[a-zA-Z0-9.+-]+);base64,(?<data>[^\"']+))[\"'][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IInlineImageStorageService _inlineImageStorageService = inlineImageStorageService;

    public async Task<string> ExtractAndStoreImagesAsync(string worklogId, string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var result = html;
        var matches = InlineImageRegex.Matches(html);
        foreach (Match match in matches)
        {
            var format = match.Groups["type"].Value;
            var base64 = match.Groups["data"].Value;

            byte[] bytes;
            try
            {
                bytes = System.Convert.FromBase64String(base64);
            }
            catch
            {
                continue;
            }

            var extension = ResolveExtension(format);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var replacement = await _inlineImageStorageService.SaveWorklogInlineImageAsync(worklogId, fileName, bytes);
            result = result.Replace(match.Groups["src"].Value, replacement, StringComparison.Ordinal);
        }

        return result;
    }

    private static string ResolveExtension(string format) =>
        format.ToLowerInvariant() switch
        {
            "jpeg" or "jpg" => ".jpg",
            "gif" => ".gif",
            "webp" => ".webp",
            "bmp" => ".bmp",
            "svg+xml" => ".svg",
            _ => ".png"
        };

}
