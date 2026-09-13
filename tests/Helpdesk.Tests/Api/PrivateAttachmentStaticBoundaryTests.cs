using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace Helpdesk.Tests.Api;

public partial class IncidentCcRecipientsEndpointsTests
{
    [Fact]
    public async Task LegacyAttachmentStaticPathCannotBypassTicketAuthorization()
    {
        var root = Path.Combine(Path.GetTempPath(), "rateldesk-static-boundary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "attachments"));
        await File.WriteAllTextAsync(Path.Combine(root, "attachments", "private.txt"), "private ticket content");
        try
        {
            using var factory = _factory.WithWebHostBuilder(builder => builder.UseWebRoot(root));
            using var client = factory.CreateClient();
            var response = await client.GetAsync("/attachments/private.txt");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("private ticket content", await response.Content.ReadAsStringAsync());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
