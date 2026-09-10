using Helpdesk.Shared.Models;

namespace Helpdesk.Application.Services.Tenants;

public interface ITenantProvisioningService
{
    Task<Organization> GetOrCreateOrganizationByDomainAsync(string domain);

    Task<(Customer customer, bool isNew)> GetOrCreateCustomerAsync(
        string email,
        string name,
        string domain);
}
