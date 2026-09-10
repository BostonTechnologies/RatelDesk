namespace Helpdesk.Infrastructure.Storage;

public sealed class StorageOptions
{
    public string RootPath { get; init; } = "/app/storage";

    public string PublicApiBaseUrl { get; init; } = string.Empty;

    public string ImageSigningSecret { get; init; } = string.Empty;
}
