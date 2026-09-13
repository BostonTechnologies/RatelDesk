using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Helpdesk.API.Bootstrap;

/// <summary>
/// Persists bootstrap-managed runtime secrets outside application configuration.
/// Deployment-provided values take precedence and never flow through this store.
/// </summary>
public static class BootstrapRuntimeSecretStore
{
    private const string ImageSigningSecretFileName = "image-signing-secret";
    private const string ImageSigningSecretPurpose = "RatelDesk.Bootstrap.ImageSigningSecret.v1";

    public static string GetOrCreateImageSigningSecret(
        BootstrapOptions options,
        string keyRingPath,
        string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyRingPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        Directory.CreateDirectory(options.StateDirectory);
        Directory.CreateDirectory(keyRingPath);
        var secretPath = Path.Combine(options.StateDirectory, ImageSigningSecretFileName);
        var protector = DataProtectionProvider.Create(
                new DirectoryInfo(keyRingPath),
                configuration => configuration.SetApplicationName(applicationName))
            .CreateProtector(ImageSigningSecretPurpose);

        if (File.Exists(secretPath))
        {
            return protector.Unprotect(File.ReadAllText(secretPath, Encoding.UTF8));
        }

        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var protectedSecret = protector.Protect(secret);
        try
        {
            using var stream = new FileStream(secretPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(protectedSecret);
            writer.Flush();
            stream.Flush(flushToDisk: true);
            SetOwnerReadWriteOnly(secretPath);
            return secret;
        }
        catch (IOException) when (File.Exists(secretPath))
        {
            return protector.Unprotect(File.ReadAllText(secretPath, Encoding.UTF8));
        }
    }

    private static void SetOwnerReadWriteOnly(string path)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
