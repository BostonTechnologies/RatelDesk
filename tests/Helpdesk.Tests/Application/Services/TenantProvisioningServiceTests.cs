using Helpdesk.Application.Services.Tenants;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using NSubstitute;
using Xunit;

namespace Helpdesk.Tests.Application.Services;

public class TenantProvisioningServiceTests
{
    [Fact]
    public async Task GetOrCreateOrganizationByDomainAsync_CreatesOrganization_AndSeedsAiKbSettings()
    {
        var orgRepo = Substitute.For<IRepository<Organization>>();
        orgRepo.GetAllAsync().Returns(Task.FromResult<IEnumerable<Organization>>(Array.Empty<Organization>()));
        orgRepo.CreateAsync(Arg.Any<Organization>())
            .Returns(call =>
            {
                var created = call.Arg<Organization>();
                created.Id = "org-1";
                return created;
            });
        var customerRepo = Substitute.For<IRepository<Customer>>();
        var aiKbRepo = Substitute.For<IRepository<OrganizationAiKbSettings>>();
        aiKbRepo.GetAllAsync().Returns(Task.FromResult<IEnumerable<OrganizationAiKbSettings>>(Array.Empty<OrganizationAiKbSettings>()));

        var sut = new TenantProvisioningService(orgRepo, customerRepo, aiKbRepo);

        var org = await sut.GetOrCreateOrganizationByDomainAsync(" Example.COM ");

        Assert.Equal("org-1", org.Id);
        Assert.Equal("example.com", org.Name);
        Assert.Equal("example.com", org.DnsName);
        await aiKbRepo.Received(1).CreateAsync(Arg.Is<OrganizationAiKbSettings>(x =>
            x.OrganizationId == "org-1" &&
            x.EnableAiSearch == false &&
            x.EnableAiAnswers == false));
    }

    [Fact]
    public async Task GetOrCreateOrganizationByDomainAsync_ReturnsExistingOrganization_ByDnsName()
    {
        var existing = new Organization { Id = "org-2", Name = "existing", DnsName = "contoso.com" };
        var orgRepo = Substitute.For<IRepository<Organization>>();
        orgRepo.GetAllAsync().Returns(Task.FromResult<IEnumerable<Organization>>(new[] { existing }));
        var customerRepo = Substitute.For<IRepository<Customer>>();
        var aiKbRepo = Substitute.For<IRepository<OrganizationAiKbSettings>>();
        aiKbRepo.GetAllAsync().Returns(Task.FromResult<IEnumerable<OrganizationAiKbSettings>>(new[]
        {
            new OrganizationAiKbSettings { OrganizationId = "org-2" }
        }));

        var sut = new TenantProvisioningService(orgRepo, customerRepo, aiKbRepo);

        var org = await sut.GetOrCreateOrganizationByDomainAsync("CONTOSO.COM");

        Assert.Equal(existing, org);
        await orgRepo.DidNotReceive().CreateAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task GetOrCreateCustomerAsync_UpdatesExistingCustomerName_AndOrganization()
    {
        var existingCustomer = new Customer
        {
            Id = "cust-1",
            Email = "person@contoso.com",
            Name = "Old Name",
            OrganizationId = "org-old"
        };
        var orgRepo = Substitute.For<IRepository<Organization>>();
        orgRepo.GetAllAsync().Returns(Task.FromResult<IEnumerable<Organization>>(new[]
        {
            new Organization { Id = "org-3", Name = "contoso.com", DnsName = "contoso.com" }
        }));
        var customerRepo = Substitute.For<IRepository<Customer>>();
        customerRepo.GetAllAsync().Returns(Task.FromResult<IEnumerable<Customer>>(new[] { existingCustomer }));
        var aiKbRepo = Substitute.For<IRepository<OrganizationAiKbSettings>>();
        aiKbRepo.GetAllAsync().Returns(Task.FromResult<IEnumerable<OrganizationAiKbSettings>>(new[]
        {
            new OrganizationAiKbSettings { OrganizationId = "org-3" }
        }));

        var sut = new TenantProvisioningService(orgRepo, customerRepo, aiKbRepo);

        var (customer, isNew) = await sut.GetOrCreateCustomerAsync("Person@Contoso.com", "New Name", "contoso.com");

        Assert.False(isNew);
        Assert.Equal(existingCustomer, customer);
        Assert.Equal("New Name", customer.Name);
        Assert.Equal("org-3", customer.OrganizationId);
        await customerRepo.Received(1).UpdateAsync(existingCustomer);
    }
}
