using Dodo.Primitives;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Shared.Models;

public class OrganizationSupportCoverage
{
    public string Id { get; set; } = Uuid.CreateVersion7().ToString();
    public string CustomerOrganizationId { get; set; } = string.Empty;
    public string ProviderOrganizationId { get; set; } = string.Empty;
    public string SupportGroupId { get; set; } = string.Empty;
    public SupportCoverageRole Role { get; set; } = SupportCoverageRole.Primary;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedUtc { get; set; }
}
