using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Helpdesk.API.Bootstrap;

public sealed class FileBootstrapStateStore : IBootstrapStateStore
{
    private const string DescriptorFileName = "descriptor.json";
    private const string SetupCodeFileName = "setup-code";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly BootstrapOptions _options;

    public FileBootstrapStateStore(BootstrapOptions options)
    {
        _options = options;
    }

    public async Task<BootstrapDescriptor> LoadOrCreateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_options.StateDirectory);
            var existing = await ReadAsync(cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            if (File.Exists(Path.Combine(_options.StateDirectory, DescriptorFileName)))
            {
                throw new InvalidOperationException("The bootstrap descriptor is invalid and requires operator recovery.");
            }

            var setupCode = string.IsNullOrWhiteSpace(_options.SetupCode)
                ? GenerateSetupCode()
                : _options.SetupCode.Trim();
            var descriptor = new BootstrapDescriptor(
                Version: 1,
                InstanceId: Guid.NewGuid(),
                State: BootstrapState.Unconfigured,
                SetupCodeHash: Hash(setupCode),
                SetupCodeCreatedAtUtc: DateTimeOffset.UtcNow,
                Provider: null,
                SqlitePath: null,
                OperationId: null,
                CompletedAtUtc: null);

            await WriteAsync(descriptor, cancellationToken);
            if (string.IsNullOrWhiteSpace(_options.SetupCode))
            {
                await WriteOperatorSetupCodeAsync(setupCode, cancellationToken);
            }

            return descriptor;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BootstrapDescriptor> UpdateAsync(
        Func<BootstrapDescriptor, BootstrapDescriptor> update,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await ReadAsync(cancellationToken)
                ?? throw new InvalidOperationException("The bootstrap descriptor is missing.");
            var next = update(current);
            await WriteAsync(next, cancellationToken);
            if (next.State is BootstrapState.Ready)
            {
                var codePath = Path.Combine(_options.StateDirectory, SetupCodeFileName);
                if (File.Exists(codePath))
                {
                    File.Delete(codePath);
                }
            }
            return next;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> RotateSetupCodeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await ReadAsync(cancellationToken);
            if (current is null || current.State is BootstrapState.Ready or BootstrapState.RecoveryRequired)
            {
                return null;
            }

            var setupCode = GenerateSetupCode();
            var next = current with
            {
                SetupCodeHash = Hash(setupCode),
                SetupCodeCreatedAtUtc = DateTimeOffset.UtcNow
            };
            await WriteAsync(next, cancellationToken);
            await WriteOperatorSetupCodeAsync(setupCode, cancellationToken);
            return setupCode;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<BootstrapDescriptor?> ReadAsync(CancellationToken cancellationToken)
    {
        var descriptorPath = Path.Combine(_options.StateDirectory, DescriptorFileName);
        if (!File.Exists(descriptorPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(descriptorPath);
        return await JsonSerializer.DeserializeAsync<BootstrapDescriptor>(stream, JsonOptions, cancellationToken);
    }

    private async Task WriteAsync(BootstrapDescriptor descriptor, CancellationToken cancellationToken)
    {
        var descriptorPath = Path.Combine(_options.StateDirectory, DescriptorFileName);
        var temporaryPath = $"{descriptorPath}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, descriptor, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, descriptorPath, overwrite: true);
    }

    private async Task WriteOperatorSetupCodeAsync(string setupCode, CancellationToken cancellationToken)
    {
        var codePath = Path.Combine(_options.StateDirectory, SetupCodeFileName);
        await File.WriteAllTextAsync(codePath, setupCode, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(codePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static string GenerateSetupCode() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
