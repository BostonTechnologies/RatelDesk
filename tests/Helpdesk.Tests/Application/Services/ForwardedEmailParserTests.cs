using Helpdesk.Infrastructure.Email;
using Helpdesk.Shared.Enums;

namespace Helpdesk.Tests.Application.Services;

public class ForwardedEmailParserTests
{
    [Fact]
    public void Parse_OutlookPlainText_ReturnsOriginalSenderAndBody()
    {
        var parser = new ForwardedEmailParser();
        var text = """
            Please log this.

            -----Original Message-----
            From: Jane Requester <jane@example.com>
            Sent: Monday, July 1, 2026 10:15 AM
            To: Support <support@example.com>
            Subject: Printer down

            The printer is offline.
            """;

        var result = parser.Parse(null, text);

        Assert.Equal(ForwardedEmailParseStatus.Parsed, result.Status);
        Assert.Equal("jane@example.com", result.OriginalFromEmail);
        Assert.Equal("Jane Requester", result.OriginalFromDisplayName);
        Assert.Equal("Printer down", result.OriginalSubject);
        Assert.Contains("printer is offline", result.OriginalBodyText, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.HasConfidentRequester);
    }

    [Fact]
    public void Parse_OutlookHtml_ReturnsOriginalSender()
    {
        var parser = new ForwardedEmailParser();
        var html = """
            <div>Please log this.</div>
            <div>-----Original Message-----<br>
            From: Jane Requester &lt;jane@example.com&gt;<br>
            Sent: Monday, July 1, 2026 10:15 AM<br>
            To: Support &lt;support@example.com&gt;<br>
            Subject: Printer down<br><br>
            The printer is offline.</div>
            """;

        var result = parser.Parse(html, null);

        Assert.Equal(ForwardedEmailParseStatus.Parsed, result.Status);
        Assert.Equal("jane@example.com", result.OriginalFromEmail);
        Assert.True(result.HasConfidentRequester);
    }

    [Fact]
    public void Parse_MissingSender_DoesNotReturnConfidentRequester()
    {
        var parser = new ForwardedEmailParser();
        var text = """
            -----Original Message-----
            Subject: Printer down

            The printer is offline.
            """;

        var result = parser.Parse(null, text);

        Assert.Equal(ForwardedEmailParseStatus.MissingOriginalSender, result.Status);
        Assert.False(result.HasConfidentRequester);
    }

    [Fact]
    public void Parse_NotForwarded_ReturnsNotForwarded()
    {
        var parser = new ForwardedEmailParser();

        var result = parser.Parse(null, "Hello support");

        Assert.Equal(ForwardedEmailParseStatus.NotForwarded, result.Status);
    }
}
