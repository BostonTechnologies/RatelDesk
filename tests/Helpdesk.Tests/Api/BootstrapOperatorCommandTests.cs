using System.Diagnostics;
using System.Text.Json;
using Helpdesk.API.Bootstrap;
using Microsoft.Extensions.Configuration;

namespace Helpdesk.Tests.Api;

public sealed class BootstrapOperatorCommandTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"rateldesk-operator-command-{Guid.NewGuid():N}");

    [Theory]
    [InlineData("DOTNET_CONTENTROOT", false)]
    [InlineData("ASPNETCORE_CONTENTROOT", false)]
    [InlineData("DOTNET_CONTENTROOT", true)]
    [InlineData("ASPNETCORE_CONTENTROOT", true)]
    public async Task Executable_uses_mounted_appsettings_and_original_application_relative_storage_from_another_shell_directory(
        string contentRootVariable, bool relativeContentRoot)
    {
        var applicationDirectory = AppContext.BaseDirectory;
        var configDirectory = Path.Combine(directory, "mounted-config");
        var shellDirectory = Path.Combine(directory, "shell-directory");
        Directory.CreateDirectory(configDirectory);
        Directory.CreateDirectory(shellDirectory);
        var options = Options("expected-configured-setup-code");
        await new FileBootstrapStateStore(options).LoadOrCreateAsync();
        await File.WriteAllTextAsync(Path.Combine(configDirectory, "appsettings.json"), JsonSerializer.Serialize(new
        {
            Bootstrap = new
            {
                StateDirectory = Path.GetRelativePath(applicationDirectory, options.StateDirectory),
                SetupCode = options.SetupCode
            },
            // A read command must not attempt to use this deliberately unusable database.
            ConnectionStrings = new { HelpdeskDb = "must-not-connect" }
        }));
        await File.WriteAllTextAsync(Path.Combine(shellDirectory, "appsettings.json"), "{\"Bootstrap\":{\"SetupCode\":\"wrong-shell-config\"}}");
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            WorkingDirectory = shellDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(typeof(BootstrapOperatorCommand).Assembly.Location);
        start.ArgumentList.Add(BootstrapOperatorCommand.ShowCode);
        foreach (var key in start.Environment.Keys.Where(key =>
            key.StartsWith("Bootstrap__", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("Helpdesk__SkipDatabaseStartup", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("DOTNET_CONTENTROOT", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("ASPNETCORE_CONTENTROOT", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.Environment.Remove(key);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["DOTNET_ENVIRONMENT"] = "Production";
        start.Environment[contentRootVariable] = relativeContentRoot
            ? Path.GetRelativePath(applicationDirectory, configDirectory) : configDirectory;
        if (contentRootVariable == "DOTNET_CONTENTROOT")
            start.Environment["ASPNETCORE_CONTENTROOT"] = shellDirectory;

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, process.ExitCode);
            Assert.Equal(options.SetupCode, (await output).Trim());
            Assert.Empty(await error);
            Assert.False(File.Exists(Path.Combine(shellDirectory, "descriptor.json")));
            Assert.False(File.Exists(Path.Combine(configDirectory, "descriptor.json")));
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    [Fact]
    public async Task Show_returns_the_current_generated_code_from_a_custom_directory_without_starting_the_database()
    {
        var options = Options();
        var store = new FileBootstrapStateStore(options);
        await store.LoadOrCreateAsync();
        var descriptorBefore = await File.ReadAllBytesAsync(DescriptorPath);
        var expectedCode = await File.ReadAllTextAsync(CodePath);
        var configuration = Config(("Database:Provider", "PostgreSql"),
            ("ConnectionStrings:HelpdeskDb", "intentionally invalid; must never be used"));

        var result = await RunAsync(BootstrapOperatorCommand.ShowCode, options, configuration);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(expectedCode, result.Output.Trim());
        Assert.Empty(result.Error);
        Assert.Equal(descriptorBefore, await File.ReadAllBytesAsync(DescriptorPath));
        Assert.False(Directory.Exists(options.DataDirectory));
        Assert.False(Directory.Exists(Path.Combine(directory, "keys")));
    }

    [Fact]
    public async Task Show_returns_a_deployment_supplied_code_when_no_generated_file_exists()
    {
        var options = Options("deployment-code-for-test");
        await new FileBootstrapStateStore(options).LoadOrCreateAsync();

        var result = await RunAsync(BootstrapOperatorCommand.ShowCode, options);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(options.SetupCode, result.Output.Trim());
        Assert.False(File.Exists(CodePath));
    }

    [Fact]
    public async Task Show_uses_the_rotated_code_instead_of_a_stale_environment_value()
    {
        var options = Options("original-deployment-code");
        var store = new FileBootstrapStateStore(options);
        await store.LoadOrCreateAsync();
        var rotated = await store.RotateSetupCodeAsync();

        var result = await RunAsync(BootstrapOperatorCommand.ShowCode, options);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(rotated, result.Output.Trim());
        Assert.DoesNotContain(options.SetupCode!, result.Output + result.Error);
    }

    [Fact]
    public async Task Show_uses_matching_configuration_when_a_stale_file_exists()
    {
        var options = Options("current-deployment-code");
        await new FileBootstrapStateStore(options).LoadOrCreateAsync();
        await File.WriteAllTextAsync(CodePath, "stale-file-code");

        var result = await RunAsync(BootstrapOperatorCommand.ShowCode, options);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(options.SetupCode, result.Output.Trim());
        Assert.Equal("stale-file-code", await File.ReadAllTextAsync(CodePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_stale_code_fails_with_rotation_instructions_and_preserves_state(bool stale)
    {
        var options = Options();
        await new FileBootstrapStateStore(options).LoadOrCreateAsync();
        var descriptorBefore = await File.ReadAllBytesAsync(DescriptorPath);
        if (stale) await File.WriteAllTextAsync(CodePath, "stale-test-value");
        else File.Delete(CodePath);

        var result = await RunAsync(BootstrapOperatorCommand.ShowCode, options);

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("--rotate-setup-code", result.Error);
        Assert.DoesNotContain("stale-test-value", result.Error);
        Assert.Equal(descriptorBefore, await File.ReadAllBytesAsync(DescriptorPath));
    }

    [Theory]
    [InlineData(BootstrapOperatorCommand.ShowCode)]
    [InlineData(BootstrapOperatorCommand.Status)]
    [InlineData(BootstrapOperatorCommand.RotateCode)]
    public async Task Missing_descriptor_never_creates_state_or_a_code(string command)
    {
        var result = await RunAsync(command, Options());

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("No bootstrap descriptor", result.Error);
        Assert.Contains("API and Web use the same release", result.Error);
        Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData("{\"secret\":\"never-echo-this-value\"")]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task Malformed_descriptor_fails_without_echoing_descriptor_contents(string contents)
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(DescriptorPath, contents);

        var result = await RunAsync(BootstrapOperatorCommand.ShowCode, Options());

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.DoesNotContain("never-echo-this-value", result.Error);
        Assert.Equal(contents, await File.ReadAllTextAsync(DescriptorPath));
        Assert.False(File.Exists(CodePath));
    }

    [Fact]
    public async Task Descriptor_with_unknown_version_or_state_is_not_used_for_code_retrieval()
    {
        var options = Options();
        var store = new FileBootstrapStateStore(options);
        await store.LoadOrCreateAsync();
        await store.UpdateAsync(current => current with { Version = 2 });
        Assert.Equal(1, (await RunAsync(BootstrapOperatorCommand.ShowCode, options)).ExitCode);
        await store.UpdateAsync(current => current with { Version = 1, State = (BootstrapState)20 });
        Assert.Equal(1, (await RunAsync(BootstrapOperatorCommand.ShowCode, options)).ExitCode);
    }

    [Theory]
    [InlineData(BootstrapState.Ready, BootstrapOperatorCommand.ShowCode)]
    [InlineData(BootstrapState.Ready, BootstrapOperatorCommand.RotateCode)]
    [InlineData(BootstrapState.RecoveryRequired, BootstrapOperatorCommand.ShowCode)]
    [InlineData(BootstrapState.RecoveryRequired, BootstrapOperatorCommand.RotateCode)]
    public async Task Completed_and_recovery_instances_cannot_retrieve_or_rotate_codes(BootstrapState state, string command)
    {
        var options = Options("private-test-code");
        var store = new FileBootstrapStateStore(options);
        await store.LoadOrCreateAsync();
        await store.UpdateAsync(current => current with { State = state });
        var descriptorBefore = await File.ReadAllBytesAsync(DescriptorPath);

        var result = await RunAsync(command, options);

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.DoesNotContain(options.SetupCode!, result.Error);
        Assert.Equal(descriptorBefore, await File.ReadAllBytesAsync(DescriptorPath));
    }

    [Fact]
    public async Task Status_reports_ready_as_success_without_retrieving_a_code()
    {
        var options = Options("private-test-code");
        var store = new FileBootstrapStateStore(options);
        await store.LoadOrCreateAsync();
        await store.UpdateAsync(current => current with { State = BootstrapState.Ready });

        var result = await RunAsync(BootstrapOperatorCommand.Status, options);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Setup state: Ready", result.Output);
        Assert.Contains("Unavailable (setup is closed)", result.Output);
        Assert.DoesNotContain(options.SetupCode!, result.Output + result.Error);
    }

    [Fact]
    public async Task Status_reports_version_path_state_and_configuration_source_without_secrets()
    {
        var options = Options("private-test-code");
        var store = new FileBootstrapStateStore(options);
        var descriptor = await store.LoadOrCreateAsync();
        await store.UpdateAsync(current => current with
        {
            State = BootstrapState.Configuring,
            ProtectedPostgreSqlConnection = "private-protected-connection",
            ProtectedKeyRingProof = "private-key-ring-proof"
        });
        var descriptorBefore = await File.ReadAllBytesAsync(DescriptorPath);
        var configuration = Config(("ConnectionStrings:HelpdeskDb", "private-database-connection"));

        var result = await RunAsync(BootstrapOperatorCommand.Status, options, configuration);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("RatelDesk API version/build:", result.Output);
        Assert.Contains($"Bootstrap state directory: {directory}", result.Output);
        Assert.Contains("Setup state: Configuring", result.Output);
        Assert.Contains("Helpdesk:SkipDatabaseStartup: False", result.Output);
        Assert.Contains("Available (Bootstrap:SetupCode configuration)", result.Output);
        Assert.DoesNotContain("private-", result.Output + result.Error);
        Assert.DoesNotContain(descriptor.SetupCodeHash, result.Output + result.Error);
        Assert.Equal(descriptorBefore, await File.ReadAllBytesAsync(DescriptorPath));
    }

    [Theory]
    [InlineData(BootstrapOperatorCommand.ShowCode)]
    [InlineData(BootstrapOperatorCommand.Status)]
    [InlineData(BootstrapOperatorCommand.RotateCode)]
    public async Task Skipped_database_startup_reports_how_to_enable_setup_without_creating_state(string command)
    {
        var result = await RunAsync(command, Options(), Config(("Helpdesk:SkipDatabaseStartup", "true")));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Helpdesk__SkipDatabaseStartup=true", result.Error);
        Assert.Contains("restart the API", result.Error);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task Rotation_preflight_only_continues_for_an_existing_open_descriptor()
    {
        var options = Options();
        var store = new FileBootstrapStateStore(options);
        await store.LoadOrCreateAsync();
        File.Delete(CodePath);
        var descriptorBefore = await File.ReadAllBytesAsync(DescriptorPath);

        var result = await RunAsync(BootstrapOperatorCommand.RotateCode, options);

        Assert.Null(result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Empty(result.Error);
        Assert.False(File.Exists(CodePath));
        Assert.Equal(descriptorBefore, await File.ReadAllBytesAsync(DescriptorPath));
    }

    [Fact]
    public async Task Malformed_command_exits_instead_of_starting_the_server()
    {
        var args = new[] { BootstrapOperatorCommand.ShowCode, "extra" };
        Assert.True(BootstrapOperatorCommand.IsRequested(args));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await BootstrapOperatorCommand.ExecuteAsync(args, Config(), Options(), output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("Use one operator command", error.ToString());
        Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData("--show-setup-code=value")]
    [InlineData("--setup-status=value")]
    [InlineData("--rotate-setup-code=value")]
    public async Task Known_commands_with_unexpected_values_are_rejected_before_host_startup(string argument)
    {
        Assert.True(BootstrapOperatorCommand.IsRequested([argument]));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await BootstrapOperatorCommand.ExecuteAsync([argument], Config(), Options(), output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("Use one operator command", error.ToString());
        Assert.False(Directory.Exists(directory));
    }

    private string DescriptorPath => Path.Combine(directory, "descriptor.json");
    private string CodePath => Path.Combine(directory, "setup-code");
    private BootstrapOptions Options(string? code = null) => new()
    {
        StateDirectory = directory,
        DataDirectory = Path.Combine(directory, "data"),
        SetupCode = code
    };

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(value => value.Key, value => value.Value)).Build();

    private static async Task<(int? ExitCode, string Output, string Error)> RunAsync(
        string command, BootstrapOptions options, IConfiguration? configuration = null)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await BootstrapOperatorCommand.ExecuteAsync([command], configuration ?? Config(), options, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
