using Microsoft.AspNetCore.StaticFiles;

namespace Helpdesk.Infrastructure.Storage;

public static class ContentTypeHelper
{
    private static readonly FileExtensionContentTypeProvider Provider = new();

    public static string GetContentType(string filename)
    {
        return Provider.TryGetContentType(filename, out var contentType)
            ? contentType
            : "application/octet-stream";
    }
}
