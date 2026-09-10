using Helpdesk.Application.Sla;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using NSubstitute;

namespace Helpdesk.Tests.Application.Sla;

public class RecipientResolverTests
{
    [Fact]
    public async Task ResolveEmailsAsync_ResolvesRoleAndEmailTargets()
    {
        var users = Substitute.For<IRepository<User>>();
        users.GetAllAsync().Returns(new List<User>
        {
            new() { Id = "1", OrganizationId = "tenant-1", Role = "Technician", Email = "tech@a.com" },
            new() { Id = "2", OrganizationId = "tenant-1", Role = "Manager", Email = "mgr@a.com" },
            new() { Id = "3", OrganizationId = "tenant-2", Role = "Technician", Email = "other@a.com" }
        });

        var sut = new RecipientResolver(users);

        var emails = await sut.ResolveEmailsAsync("tenant-1", new List<RecipientTarget>
        {
            new() { Type = RecipientTargetType.Role, Value = "Technician" },
            new() { Type = RecipientTargetType.Email, Value = "direct@a.com" },
            new() { Type = RecipientTargetType.Group, Value = "GroupA" }
        });

        Assert.Contains("tech@a.com", emails);
        Assert.Contains("direct@a.com", emails);
        Assert.DoesNotContain("other@a.com", emails);
    }
}
