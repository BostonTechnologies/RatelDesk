using System.Reflection;
using System.Text.Json.Serialization;

namespace Helpdesk.Shared.Build;

public sealed record BuildInfo(
    string Version,
    string CommitHash,
    string BuildTimestamp,
    string AssemblyName,
    string Environment)
{
    [JsonIgnore]
    public string DisplayVersion => $"v{Version}";

    [JsonIgnore]
    public string ShortCommitHash => CommitHash.Length > 7 ? CommitHash[..7] : CommitHash;

    [JsonIgnore]
    public string BuildTimestampDisplay => DateTimeOffset.TryParse(BuildTimestamp, out var timestamp)
        ? timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture)
        : BuildTimestamp;
}

public static class BuildInfoProvider
{
    public static BuildInfo FromAssembly(Assembly assembly, string environment)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var version = SemanticVersion(informationalVersion, assembly.GetName().Version);
        var sourceRevision = AssemblyMetadata(assembly, "SourceRevisionId")
            ?? SourceRevisionFromInformationalVersion(informationalVersion)
            ?? "unknown";

        return new BuildInfo(
            Version: version,
            CommitHash: sourceRevision,
            BuildTimestamp: AssemblyMetadata(assembly, "BuildTimestamp") ?? "unknown",
            AssemblyName: assembly.GetName().Name ?? "unknown",
            Environment: environment);
    }

    private static string SemanticVersion(string? informationalVersion, Version? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+', 2, StringSplitOptions.TrimEntries)[0];
        }

        return assemblyVersion?.ToString(3) ?? "dev";
    }

    private static string? SourceRevisionFromInformationalVersion(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return null;
        }

        var parts = informationalVersion.Split('+', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : null;
    }

    private static string? AssemblyMetadata(Assembly assembly, string key) => assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))?
        .Value;
}
