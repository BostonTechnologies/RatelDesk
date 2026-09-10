using Helpdesk.Infrastructure.Email;
using Xunit;

namespace Helpdesk.Tests.Infrastructure.Email;

public class EmailAddressGuardTests
{
    [Theory]
    [InlineData("helpdesk@example.com", "helpdesk@example.com")]
    [InlineData(" HELPDESK@EXAMPLE.COM ", "helpdesk@example.com")]
    [InlineData("helpdesk@example.com", " HELPDESK@EXAMPLE.COM ")]
    public void IsSameAddress_ReturnsTrue_ForNormalizedMailboxAddress(
        string address,
        string mailboxAddress)
    {
        var result = EmailAddressGuard.IsSameAddress(address, mailboxAddress);

        Assert.True(result);
    }

    [Theory]
    [InlineData("customer@example.com", "helpdesk@example.com")]
    [InlineData("", "helpdesk@example.com")]
    [InlineData("customer@example.com", "")]
    public void IsSameAddress_ReturnsFalse_ForUnrelatedOrMissingAddress(
        string address,
        string mailboxAddress)
    {
        var result = EmailAddressGuard.IsSameAddress(address, mailboxAddress);

        Assert.False(result);
    }
}
