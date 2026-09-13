using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Helpdesk.Infrastructure.Storage;

/// <summary>Stores private attachment bytes outside the static web root.</summary>
public sealed class TicketAttachmentFileStore : IHostedService
{
    private readonly string _root;
    private readonly string _legacyRoot;

    public TicketAttachmentFileStore(IHostEnvironment environment, IOptions<StorageOptions> options)
    {
        var storageRoot = string.IsNullOrWhiteSpace(options.Value.RootPath)
            ? Path.Combine(environment.ContentRootPath, "storage")
            : options.Value.RootPath;
        _root = Path.GetFullPath(Path.Combine(storageRoot, "attachments"));
        var webRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot"));
        if (_root.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Private attachment storage must be outside wwwroot.");
        _legacyRoot = Path.Combine(webRoot, "attachments");
    }

    public string GetWritePath(string fileName)
    {
        ValidateFileName(fileName);
        Directory.CreateDirectory(_root);
        return Path.Combine(_root, fileName);
    }

    public string? GetReadPath(string fileName)
    {
        if (!IsSafeFileName(fileName))
            return null;
        var path = Path.Combine(_root, fileName);
        if (File.Exists(path))
            return path;
        // Existing installations retain access during migration through the authorized API.
        var legacyPath = Path.Combine(_legacyRoot, fileName);
        return File.Exists(legacyPath) ? legacyPath : null;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_root);
        if (!Directory.Exists(_legacyRoot))
            return;

        foreach (var source in Directory.EnumerateFiles(_legacyRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destination = GetWritePath(Path.GetFileName(source));
            if (File.Exists(destination))
            {
                await using var existingStream = File.OpenRead(destination);
                await using var legacyStream = File.OpenRead(source);
                var existing = await SHA256.HashDataAsync(existingStream, cancellationToken);
                var legacy = await SHA256.HashDataAsync(legacyStream, cancellationToken);
                if (!CryptographicOperations.FixedTimeEquals(existing, legacy))
                    throw new IOException("An attachment storage migration found conflicting files.");
            }
            else
            {
                // Complete the copy before removing the legacy file; supports different volumes.
                var temporary = destination + ".migrating";
                File.Copy(source, temporary, overwrite: true);
                File.Move(temporary, destination);
            }
            File.Delete(source);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool IsSafeFileName(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName) && fileName is not "." and not ".." &&
        !fileName.Contains('/') && !fileName.Contains('\\') && !Path.IsPathRooted(fileName);

    private static void ValidateFileName(string fileName)
    {
        if (!IsSafeFileName(fileName))
            throw new ArgumentException("An attachment storage name must be a single file name.", nameof(fileName));
    }
}
