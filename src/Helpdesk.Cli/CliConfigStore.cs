using System.Text.Json;

internal sealed class CliConfigStore
{
    public CliConfigStore(string? explicitPath = null)
    {
        Path = ExpandPath(explicitPath) ?? DefaultPath;
    }

    public string Path { get; }

    public static string DefaultPath
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
            {
                home = Environment.GetEnvironmentVariable("HOME") ?? ".";
            }

            return System.IO.Path.Combine(home, ".config", "helpdesk", "cli.json");
        }
    }

    public CliConfig Load()
    {
        if (!File.Exists(Path))
        {
            return CliConfig.Empty;
        }

        var json = File.ReadAllText(Path);
        return JsonSerializer.Deserialize<CliConfig>(json, CliConfig.JsonOptions) ?? CliConfig.Empty;
    }

    public void Save(CliConfig config)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(Path, JsonSerializer.Serialize(config, CliConfig.JsonOptions) + Environment.NewLine);
        TryRestrictPermissions(Path);
    }

    private static string? ExpandPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (path.StartsWith("~/", StringComparison.Ordinal))
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path[2..]);
        }

        return path;
    }

    private static void TryRestrictPermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // Best effort only; container filesystems may not support chmod.
            }
        }
    }
}
