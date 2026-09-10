using Helpdesk.Shared.DTOs.Organization;
using Helpdesk.Shared.Models;

namespace Helpdesk.Tests.Shared;

public class OrganizationDtoTests
{
    [Fact]
    public void IsEnabled_DefaultsTrue()
    {
        var dto = new OrganizationDto();
        Assert.True(dto.IsEnabled);
        Assert.Equal(EntityState.Enabled, dto.State);
    }

    [Fact]
    public void IsEnabled_SetTrue_SetsStateEnabled()
    {
        var dto = new OrganizationDto();
        dto.IsEnabled = true;
        Assert.Equal(EntityState.Enabled, dto.State);
    }

    [Fact]
    public void IsEnabled_SetFalse_SetsStateBlocked()
    {
        var dto = new OrganizationDto();
        dto.IsEnabled = false;
        Assert.Equal(EntityState.Blocked, dto.State);
    }
}
