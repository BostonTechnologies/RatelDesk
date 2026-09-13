using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Helpdesk.Tests.Api;

internal static class IsolatedTestStorage
{
    // API hosted services must exercise real storage startup without writing to
    // the container path, the checkout, or another test host's attachment files.
    public static IWebHostBuilder UseIsolatedTestStorage(this IWebHostBuilder builder)
    {
        var root = Path.Combine(Path.GetTempPath(), "rateldesk-api-test-storage", Guid.NewGuid().ToString("N"));
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["StorageOptions:RootPath"] = root }));
        builder.ConfigureServices(services =>
            services.AddSingleton<IHostedService>(provider =>
                new StorageCleanup(root, provider.GetRequiredService<IHostApplicationLifetime>())));
        return builder;
    }

    private sealed class StorageCleanup(string root, IHostApplicationLifetime lifetime) : IHostedService, IDisposable
    {
        private readonly object _cleanupLock = new();
        private CancellationTokenRegistration _stopped;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // app.Run also disposes the provider on its entry-point thread.
            // Use the awaited stopped notification so cleanup has completed
            // when WebApplicationFactory returns from stopping the host.
            _stopped = lifetime.ApplicationStopped.Register(DeleteStorage);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose()
        {
            _stopped.Dispose();
            DeleteStorage();
        }

        private void DeleteStorage()
        {
            lock (_cleanupLock)
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }
    }
}
