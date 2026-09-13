using Helpdesk.Infrastructure.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Helpdesk.Tests.Infrastructure;

public sealed class PrivateAttachmentStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rateldesk-attachments-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DefaultStorageBelongsToTheHostContentRoot()
    {
        var contentRoot = Path.Combine(_root, "native-host");
        var environment = Substitute.For<IHostEnvironment>();
        environment.ContentRootPath.Returns(contentRoot);
        var store = new TicketAttachmentFileStore(environment, Options.Create(new StorageOptions()));

        await store.StartAsync(CancellationToken.None);
        var path = store.GetWritePath("private.txt");
        Assert.Equal(Path.Combine(contentRoot, "storage", "attachments", "private.txt"), path);
        await File.WriteAllTextAsync(path, "persistent bytes");
        Assert.Equal(path, store.GetReadPath("private.txt"));
    }

    [Fact]
    public async Task LegacyFilesMoveIntoPersistentStorageAndSurviveHostRecreation()
    {
        var firstHost = Path.Combine(_root, "first-host");
        var legacy = Path.Combine(firstHost, "wwwroot", "attachments");
        Directory.CreateDirectory(legacy);
        var bytes = new byte[] { 1, 4, 9, 16 };
        await File.WriteAllBytesAsync(Path.Combine(legacy, "legacy.dat"), bytes);
        var store = Create(firstHost);
        Assert.Equal(Path.Combine(legacy, "legacy.dat"), store.GetReadPath("legacy.dat"));

        await store.StartAsync(CancellationToken.None);
        Assert.False(File.Exists(Path.Combine(legacy, "legacy.dat")));
        await File.WriteAllBytesAsync(store.GetWritePath("new.dat"), bytes);
        Directory.Delete(firstHost, recursive: true);

        var recreated = Create(Path.Combine(_root, "replacement-host"));
        await recreated.StartAsync(CancellationToken.None);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(recreated.GetReadPath("legacy.dat")!));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(recreated.GetReadPath("new.dat")!));
    }

    [Fact]
    public async Task MigrationConflictKeepsBothFilesAndFailsExplicitly()
    {
        var host = Path.Combine(_root, "host");
        var legacy = Path.Combine(host, "wwwroot", "attachments");
        Directory.CreateDirectory(legacy);
        await File.WriteAllTextAsync(Path.Combine(legacy, "same.txt"), "legacy");
        var store = Create(host);
        await File.WriteAllTextAsync(store.GetWritePath("same.txt"), "persistent");
        await Assert.ThrowsAsync<IOException>(() => store.StartAsync(CancellationToken.None));
        Assert.Equal("legacy", await File.ReadAllTextAsync(Path.Combine(legacy, "same.txt")));
        Assert.Equal("persistent", await File.ReadAllTextAsync(store.GetReadPath("same.txt")!));
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("..\\secret")]
    [InlineData("/etc/passwd")]
    public void AttachmentNamesCannotEscapeStorage(string name)
    {
        var store = Create(Path.Combine(_root, "host"));
        Assert.Null(store.GetReadPath(name));
        Assert.Throws<ArgumentException>(() => store.GetWritePath(name));
    }

    private TicketAttachmentFileStore Create(string contentRoot)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.ContentRootPath.Returns(contentRoot);
        return new TicketAttachmentFileStore(environment, Options.Create(new StorageOptions
        {
            RootPath = Path.Combine(_root, "persistent")
        }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
