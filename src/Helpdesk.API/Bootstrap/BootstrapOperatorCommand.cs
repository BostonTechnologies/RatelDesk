using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Helpdesk.API.Bootstrap;

/// <summary>
/// Operator-only diagnostics. Read commands do not initialize bootstrap state,
/// touch the key ring, connect to a database, or start an HTTP listener.
/// </summary>
public static class BootstrapOperatorCommand
{
    public const string ShowCode = "--show-setup-code";
    public const string Status = "--setup-status";
    public const string RotateCode = "--rotate-setup-code";

    public static bool IsRequested(string[] args) =>
        args.Any(argument => argument is ShowCode or Status or RotateCode ||
            argument.StartsWith(ShowCode + "=", StringComparison.Ordinal) ||
            argument.StartsWith(Status + "=", StringComparison.Ordinal) ||
            argument.StartsWith(RotateCode + "=", StringComparison.Ordinal));

    /// <summary>
    /// Preserve the host's content-root environment precedence. The shipped
    /// image resolves relative paths from its /app working directory even when
    /// appsettings are mounted at another content root. Native deployments with
    /// a different working directory should configure absolute bootstrap, data,
    /// and key-ring paths for operator commands.
    /// </summary>
    public static string ResolveContentRoot(string applicationDirectory, string? dotnetContentRoot, string? aspnetCoreContentRoot) =>
        Path.GetFullPath(dotnetContentRoot ?? aspnetCoreContentRoot ?? applicationDirectory, applicationDirectory);

    /// <returns>An exit code, or null when a validated rotation should continue through normal startup reconciliation.</returns>
    public static async Task<int?> ExecuteAsync(
        string[] args,
        IConfiguration configuration,
        BootstrapOptions options,
        TextWriter output,
        TextWriter error,
        CancellationToken token = default)
    {
        if (args.Length != 1 || args[0] is not (ShowCode or Status or RotateCode))
        {
            await error.WriteLineAsync($"Use one operator command: {ShowCode}, {Status}, or {RotateCode}. Configure this command with the same environment settings as the running API.");
            return 1;
        }

        var status = args[0] == Status;
        try
        {
            var stateDirectory = Path.GetFullPath(options.StateDirectory);
            var skipStartup = configuration.GetValue<bool>("Helpdesk:SkipDatabaseStartup");
            if (status)
            {
                var version = typeof(BootstrapOperatorCommand).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
                await output.WriteLineAsync($"RatelDesk API version/build: {version}");
                await output.WriteLineAsync($"Bootstrap state directory: {stateDirectory}");
                await output.WriteLineAsync($"Helpdesk:SkipDatabaseStartup: {skipStartup}");
                await output.WriteLineAsync("Status reads existing local state; it does not connect to the database or check HTTP readiness.");
            }

            if (skipStartup)
            {
                if (status) await output.WriteLineAsync("Setup state: Disabled (database startup is skipped)");
                await error.WriteLineAsync("First-run setup is disabled by Helpdesk__SkipDatabaseStartup=true. Remove that test-only setting and restart the API, then run --setup-status again.");
                return 1;
            }

            // Do not acquire a lease for an absent descriptor: lease acquisition
            // creates its directory, which would hide a wrong mount/configuration.
            var descriptorPath = Path.Combine(stateDirectory, "descriptor.json");
            if (!File.Exists(descriptorPath))
            {
                if (status) await output.WriteLineAsync("Setup state: Missing");
                await error.WriteLineAsync("No bootstrap descriptor exists in the configured state directory. Verify that this is the running API container, that API and Web use the same release, and that Bootstrap__StateDirectory and its volume match the API deployment. Start the API normally, then run --setup-status again. No setup state was created by this command.");
                return 1;
            }

            await using var lease = await BootstrapOperationLease.AcquireAsync(stateDirectory, token);
            var descriptor = await ReadDescriptorAsync(descriptorPath, token);
            if (descriptor is null)
            {
                if (status) await output.WriteLineAsync("Setup state: Invalid");
                await error.WriteLineAsync("The existing bootstrap descriptor is invalid or unsupported. Restore the matching bootstrap volume and inspect API startup logs; this command cannot create or reset an installation.");
                return 1;
            }

            if (status) await output.WriteLineAsync($"Setup state: {descriptor.State}");
            if (descriptor.State is BootstrapState.Ready or BootstrapState.RecoveryRequired)
            {
                if (status) await output.WriteLineAsync("Setup code: Unavailable (setup is closed)");
                if (descriptor.State == BootstrapState.Ready)
                {
                    if (!status) await error.WriteLineAsync("Setup is already complete. Sign in with the administrator created during setup; a new setup code cannot be issued. If Web still shows setup, check that API and Web use the same release and that Web targets this API.");
                    return status ? 0 : 1;
                }
                await error.WriteLineAsync("This instance requires recovery. Restore its matching database, bootstrap state, and key-ring volumes, then restart the API. Setup codes cannot reopen an initialized instance.");
                return 1;
            }

            // Rotation still goes through normal startup reconciliation before
            // it may replace a code, so an interrupted database commit is checked.
            if (args[0] == RotateCode) return null;

            var code = await ReadCurrentCodeAsync(descriptor, options, token);
            if (status)
            {
                await output.WriteLineAsync($"Setup code: {code.Source}");
                if (code.Value is not null)
                    await output.WriteLineAsync("Next step: run dotnet /app/Helpdesk.API.dll --show-setup-code, then paste its output into /setup.");
            }
            else if (code.Value is not null)
            {
                await output.WriteLineAsync(code.Value);
            }

            if (code.Value is not null) return 0;
            await error.WriteLineAsync("The current setup code is missing or does not match the saved installation. Run dotnet /app/Helpdesk.API.dll --rotate-setup-code in this API container, then paste the new code into /setup. Rotation replaces the old code and any earlier unlocked setup session.");
            return 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or FormatException or InvalidOperationException)
        {
            // Exception details may contain credentials or descriptor content.
            await error.WriteLineAsync("Setup state could not be read. Check the API container's Bootstrap__StateDirectory, its volume permissions, and whether another setup operation is running, then run --setup-status again. No setup state was reset.");
            return 1;
        }
    }

    private static async Task<BootstrapDescriptor?> ReadDescriptorAsync(string path, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        var descriptor = await JsonSerializer.DeserializeAsync<BootstrapDescriptor>(stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web), token);
        if (descriptor is null || descriptor.Version != 1 || descriptor.InstanceId == Guid.Empty ||
            !Enum.IsDefined(descriptor.State) || descriptor.SetupCodeCreatedAtUtc == default ||
            descriptor.SetupCodeHash is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit))
            return null;
        return descriptor;
    }

    private static async Task<(string? Value, string Source)> ReadCurrentCodeAsync(
        BootstrapDescriptor descriptor, BootstrapOptions options, CancellationToken token)
    {
        var configuredCode = options.SetupCode?.Trim().TrimStart('\uFEFF');
        var configuredMatches = Matches(configuredCode, descriptor.SetupCodeHash);
        // A rotation writes a generated file and supersedes the initial
        // environment-provided value. Only a matching source may be returned.
        var codePath = Path.Combine(options.StateDirectory, "setup-code");
        if (File.Exists(codePath))
        {
            var fileCode = (await File.ReadAllTextAsync(codePath, token)).Trim().TrimStart('\uFEFF');
            if (Matches(fileCode, descriptor.SetupCodeHash)) return (fileCode, "Available (generated file)");
        }
        if (configuredMatches) return (configuredCode, "Available (Bootstrap:SetupCode configuration)");
        return (null, string.IsNullOrWhiteSpace(configuredCode) && !File.Exists(codePath)
            ? "Missing (no current code source)" : "Stale (available sources do not match saved state)");
    }

    private static bool Matches(string? code, string hash) =>
        !string.IsNullOrWhiteSpace(code) && CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(code)), Convert.FromHexString(hash));
}
