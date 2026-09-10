using Helpdesk.Shared.DTOs.Customer;
using Helpdesk.Shared.Models;

namespace Helpdesk.Tests.Shared;

public class CustomerDtoTests
{
    [Fact]
    public void IsEnabled_DefaultsTrue()
    {
        var dto = new CustomerDto();
        Assert.True(dto.IsEnabled);
        Assert.Equal(EntityState.Enabled, dto.State);
    }

    [Fact]
    public void IsEnabled_SetTrue_SetsStateEnabled()
    {
        var dto = new CustomerDto();
        dto.IsEnabled = true;
        Assert.Equal(EntityState.Enabled, dto.State);
    }

    [Fact]
    public void IsEnabled_SetFalse_SetsStateBlocked()
    {
        var dto = new CustomerDto();
        dto.IsEnabled = false;
        Assert.Equal(EntityState.Blocked, dto.State);
    }
}
