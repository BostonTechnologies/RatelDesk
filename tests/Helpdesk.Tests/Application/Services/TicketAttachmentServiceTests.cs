using Helpdesk.Application.Services.Tickets;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.DTOs.Attachment;
using Helpdesk.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Helpdesk.Tests.Application.Services;

public class TicketAttachmentServiceTests
{
    [Fact]
    public async Task SaveAsync_PersistsAttachments()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var env = Substitute.For<IHostEnvironment>();
        env.ContentRootPath.Returns(tempRoot);

        var options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenant = Substitute.For<ITenantContext>();
        var context = new HelpdeskDbContext(options, tenant, new HttpContextAccessor());

        var service = new TicketAttachmentService(context, new Helpdesk.Infrastructure.Storage.TicketAttachmentFileStore(env, Microsoft.Extensions.Options.Options.Create(new Helpdesk.Infrastructure.Storage.StorageOptions { RootPath = Path.Combine(tempRoot, "storage") })));
        var uploads = new[]
        {
            new AttachmentUpload("test.txt", "text/plain", System.Text.Encoding.UTF8.GetBytes("hello"))
        };

        var result = await service.SaveAsync("ticket-1", uploads, "user-1", CancellationToken.None);

        Assert.Single(result);
        Assert.Single(context.Attachments);
        var saved = context.Attachments.First();
        var filePath = Path.Combine(tempRoot, "storage", "attachments", saved.FilePath);
        Assert.True(File.Exists(filePath));
    }
}
