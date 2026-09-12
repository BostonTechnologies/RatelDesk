using Helpdesk.API.Bootstrap;

namespace Helpdesk.Tests.Api;

public sealed class BootstrapStateStoreTests
{
    [Fact]
    public async Task Generated_operator_code_unlocks_a_new_unconfigured_descriptor()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rateldesk-bootstrap-{Guid.NewGuid():N}");
        try
        {
            var store = new FileBootstrapStateStore(new BootstrapOptions { StateDirectory = directory });
            var descriptor = await store.LoadOrCreateAsync();
            var setupCode = await File.ReadAllTextAsync(Path.Combine(directory, "setup-code"));
            var sessions = new BootstrapSessionService();

            Assert.Equal(BootstrapState.Unconfigured, descriptor.State);
            Assert.NotEqual(setupCode.Trim(), descriptor.SetupCodeHash);
            Assert.False(sessions.TryCreate(descriptor, "incorrect", out _));
            Assert.True(sessions.TryCreate(descriptor, setupCode, out var session));
            Assert.True(sessions.IsValid(session));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Caller_supplied_code_is_hashed_without_creating_a_retrieval_file()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rateldesk-bootstrap-{Guid.NewGuid():N}");
        try
        {
            var store = new FileBootstrapStateStore(new BootstrapOptions
            {
                StateDirectory = directory,
                SetupCode = "operator-provided-code"
            });

            var descriptor = await store.LoadOrCreateAsync();
            var updated = await store.UpdateAsync(current => current with
            {
                State = BootstrapState.Configuring,
                Provider = "Sqlite",
                OperationId = Guid.NewGuid()
            });

            Assert.Equal(BootstrapState.Configuring, updated.State);
            Assert.Equal("Sqlite", updated.Provider);
            Assert.NotEqual("operator-provided-code", descriptor.SetupCodeHash);
            Assert.False(File.Exists(Path.Combine(directory, "setup-code")));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
