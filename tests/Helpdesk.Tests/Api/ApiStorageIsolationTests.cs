using Helpdesk.API;
using Helpdesk.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Helpdesk.Tests.Api;

public sealed class ApiStorageIsolationTests
{
    [Fact]
    public void ApiHostsHaveSeparateWritableStorageCleanedUpWithTheHost()
    {
        using var factory = new WebApplicationFactory<Program>();
        string firstRoot;
        string secondRoot;
        using (var first = factory.WithWebHostBuilder(builder =>
               {
                   builder.UseEnvironment("Development");
                   builder.UseIsolatedTestStorage();
               }))
        using (var second = factory.WithWebHostBuilder(builder =>
               {
                   builder.UseEnvironment("Development");
                   builder.UseIsolatedTestStorage();
               }))
        {
            using var firstClient = first.CreateClient();
            using var secondClient = second.CreateClient();
            firstRoot = first.Services.GetRequiredService<IOptions<StorageOptions>>().Value.RootPath;
            secondRoot = second.Services.GetRequiredService<IOptions<StorageOptions>>().Value.RootPath;
            Assert.NotEqual(firstRoot, secondRoot);
            Assert.StartsWith(Path.GetTempPath(), firstRoot);
            Assert.StartsWith(Path.GetTempPath(), secondRoot);
            Assert.True(Directory.Exists(Path.Combine(firstRoot, "attachments")));
            Assert.True(Directory.Exists(Path.Combine(secondRoot, "attachments")));

            File.WriteAllText(Path.Combine(firstRoot, "attachments", "private.txt"), "first host only");
            Assert.False(File.Exists(Path.Combine(secondRoot, "attachments", "private.txt")));
        }

        Assert.False(Directory.Exists(firstRoot));
        Assert.False(Directory.Exists(secondRoot));
    }
}
