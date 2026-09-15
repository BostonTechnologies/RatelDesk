namespace HelpDesk.NewWeb.Components.Pages.Admin.User;

public sealed class TenantOrganizationSelection
{
    public string? SelectedOrganizationId { get; private set; }

    public bool ApplyRouteParameter(string? organizationId, IEnumerable<string> availableOrganizationIds)
    {
        var availableIds = availableOrganizationIds.ToArray();
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            if (!availableIds.Contains(SelectedOrganizationId, StringComparer.OrdinalIgnoreCase))
            {
                SelectedOrganizationId = availableIds.FirstOrDefault();
            }

            return true;
        }

        SelectedOrganizationId = availableIds.FirstOrDefault(id =>
            string.Equals(id, organizationId, StringComparison.OrdinalIgnoreCase));
        return !string.IsNullOrWhiteSpace(SelectedOrganizationId);
    }

    public void Select(string organizationId) => SelectedOrganizationId = organizationId;

    public string Path(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(SelectedOrganizationId))
        {
            throw new InvalidOperationException("An organization must be selected before a tenant request can be created.");
        }

        return $"api/v1/tenant-admin/organizations/{Uri.EscapeDataString(SelectedOrganizationId)}/{relativePath.TrimStart('/')}";
    }
}
