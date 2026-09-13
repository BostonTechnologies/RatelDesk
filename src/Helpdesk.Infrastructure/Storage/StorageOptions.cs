namespace Helpdesk.Infrastructure.Storage;

public sealed class StorageOptions
{
    public string RootPath { get; init; } = "storage";

    public string PublicApiBaseUrl { get; init; } = string.Empty;

    public string ImageSigningSecret { get; init; } = string.Empty;

    public string ResolveRootPath(string contentRootPath)
    {
        var root = string.IsNullOrWhiteSpace(RootPath) ? "storage" : RootPath;
        return Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(contentRootPath, root));
    }
}
