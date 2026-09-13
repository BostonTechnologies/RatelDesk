using Helpdesk.Application.Services.Branding;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Helpdesk.Application.WorkLogs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Helpdesk.Infrastructure.Storage;

public sealed class InlineImageStorageService(
    IHostEnvironment env,
    IOptions<StorageOptions> options,
    IImageLinkSigner imageLinkSigner,
    ILogger<InlineImageStorageService> logger,
    IInstanceBrandingProvider? brandingProvider = null) : IInlineImageStorageService
{
    private readonly string _storageRoot = string.IsNullOrWhiteSpace(options.Value.RootPath)
        ? Path.Combine(env.ContentRootPath, "storage")
        : options.Value.RootPath;
    private readonly string _publicApiBaseUrl = options.Value.PublicApiBaseUrl ?? string.Empty;
    private readonly IImageLinkSigner _imageLinkSigner = imageLinkSigner;
    private readonly ILogger<InlineImageStorageService> _logger = logger;

    public async Task<string> SaveIncidentInlineImageAsync(
        string incidentId,
        string filename,
        byte[] content)
    {
        var safeIncidentId = SanitizePathSegment(incidentId);
        var safeName = Path.GetFileName((filename ?? string.Empty).Trim().Replace('\\', '/'));
        var basename = Regex.Replace(Path.GetFileNameWithoutExtension(safeName), "[^a-zA-Z0-9_-]", "-");
        basename = string.IsNullOrWhiteSpace(basename) ? "image" : basename[..Math.Min(basename.Length, 80)];
        var extension = Path.GetExtension(safeName).ToLowerInvariant();
        extension = Regex.IsMatch(extension, @"^\.[a-z0-9]{1,10}$") ? extension : string.Empty;
        var safeFilename = $"{basename}-{Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()}{extension}";
        var root = Path.Combine(_storageRoot, "incidents", safeIncidentId, "inline");
        Directory.CreateDirectory(root);

        var fullPath = Path.Combine(root, safeFilename);
        await File.WriteAllBytesAsync(fullPath, content);
        _logger.LogInformation("Saved incident inline image. IncidentId={IncidentId} Path={Path}", safeIncidentId, fullPath);

        var expires = DateTimeOffset.UtcNow.AddHours(24);
        var token = _imageLinkSigner.GenerateToken("incident", safeIncidentId, safeFilename, expires);
        var relative =
            $"/api/incidents/{Uri.EscapeDataString(safeIncidentId)}/images/{Uri.EscapeDataString(safeFilename)}?token={Uri.EscapeDataString(token)}";

        return await PublicResourceUrl.BuildAsync(relative, _publicApiBaseUrl, brandingProvider);
    }

    public async Task<string> SaveWorklogInlineImageAsync(
        string worklogId,
        string filename,
        byte[] content)
    {
        var safeWorklogId = SanitizePathSegment(worklogId);
        var safeFilename = SanitizeFileName(filename);
        var root = Path.Combine(_storageRoot, "worklogs", safeWorklogId, "inline");
        Directory.CreateDirectory(root);

        var fullPath = Path.Combine(root, safeFilename);
        await File.WriteAllBytesAsync(fullPath, content);
        _logger.LogInformation("Saved worklog inline image. WorklogId={WorklogId} Path={Path}", safeWorklogId, fullPath);

        var expires = DateTimeOffset.UtcNow.AddHours(24);
        var token = _imageLinkSigner.GenerateToken("worklog", safeWorklogId, safeFilename, expires);
        var relative =
            $"/api/worklogs/{Uri.EscapeDataString(safeWorklogId)}/images/{Uri.EscapeDataString(safeFilename)}?token={Uri.EscapeDataString(token)}";

        return await PublicResourceUrl.BuildAsync(relative, _publicApiBaseUrl, brandingProvider);
    }

    private static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        var clean = Regex.Replace(value, "[^a-zA-Z0-9_-]", "-");
        return string.IsNullOrWhiteSpace(clean) ? "unknown" : clean;
    }

    private static string SanitizeFileName(string value)
    {
        var fileName = Path.GetFileName(string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim());
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = Guid.NewGuid().ToString("N");

        fileName = fileName.Replace('|', '-');
        return fileName;
    }
}
