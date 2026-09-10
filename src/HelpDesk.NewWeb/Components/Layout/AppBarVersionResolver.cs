using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace HelpDesk.NewWeb.Components.Layout;

public static class AppBarVersionResolver
{
    public static AppBarVersionDetails ResolveDetails(IConfiguration configuration, Assembly? assembly = null)
    {
        var assemblyVersion = ResolveAssemblyVersion(assembly);
        var source = FirstNonEmpty(
            ("RATELDESK_BUILD_VERSION", configuration["RATELDESK_BUILD_VERSION"]),
            ("AppBar:BuildVersion", configuration["AppBar:BuildVersion"]),
            ("BUILD_VERSION", configuration["BUILD_VERSION"]),
            ("APP_VERSION", configuration["APP_VERSION"]),
            ("VERSION", configuration["VERSION"]),
            ("OTEL_SERVICE_VERSION", configuration["OTEL_SERVICE_VERSION"]),
            ("AssemblyInformationalVersion", assemblyVersion));
        var value = source.Value ?? "dev";

        return new AppBarVersionDetails(
            ServiceName: "HelpDesk.NewWeb",
            DisplayVersion: Format(value),
            FullVersion: value.Trim(),
            Source: source.Name ?? "Fallback",
            AssemblyVersion: assembly?.GetName().Version?.ToString() ?? "unknown");
    }

    private static string? ResolveAssemblyVersion(Assembly? assembly)
    {
        if (assembly is null)
        {
            return null;
        }

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational;
        }

        return assembly.GetName().Version?.ToString();
    }

    private static string Format(string value)
    {
        var trimmed = Normalize(value);
        return trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"v{trimmed}";
    }

    private static string Normalize(string value)
    {
        var trimmed = value.Trim();
        var metadataStart = trimmed.IndexOf('+', StringComparison.Ordinal);
        if (metadataStart >= 0)
        {
            trimmed = trimmed[..metadataStart];
        }

        var semanticVersion = Regex.Match(trimmed, @"(?<version>\d+\.\d+\.\d+)");
        return semanticVersion.Success
            ? semanticVersion.Groups["version"].Value
            : trimmed;
    }

    private static (string? Name, string? Value) FirstNonEmpty(params (string Name, string? Value)[] values)
    {
        var value = values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value.Value));
        return string.IsNullOrWhiteSpace(value.Value)
            ? (null, null)
            : value;
    }
}

public sealed record AppBarVersionDetails(
    string ServiceName,
    string DisplayVersion,
    string FullVersion,
    string Source,
    string AssemblyVersion);
