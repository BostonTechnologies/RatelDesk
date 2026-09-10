using Helpdesk.Application.Services.EmailTemplates;
using Helpdesk.Infrastructure.EmailTemplates;

namespace Helpdesk.Tests.Infrastructure;

public class EmailTemplateRendererTests
{
    [Fact]
    public void Render_EncodesStandardFields_AndInjectsUpdateMessageHtml()
    {
        var renderer = new EmailTemplateRenderer(new TemplateEngine());
        var html = "<p>{{{USER_NAME}}}</p><p>{{{TICKET_REF}}}</p><a href=\"{{{TICKET_LINK}}}\">Link</a>{{{UPDATE_MESSAGE}}}";

        var rendered = renderer.Render(html, new EmailTemplateContext
        {
            UserName = "John <Doe>",
            TicketRef = "INC-1 & 2",
            TicketLink = "https://example.com/?a=1&b=2",
            UpdateMessageHtml = "<p>Safe <strong>update</strong></p>"
        });

        Assert.Contains("John &lt;Doe&gt;", rendered);
        Assert.Contains("INC-1 &amp; 2", rendered);
        Assert.Contains("https://example.com/?a=1&amp;b=2", rendered);
        Assert.Contains("<p>Safe <strong>update</strong></p>", rendered);
    }

    [Fact]
    public void Render_UsesUpdateMessageText_WhenHtmlIsMissing()
    {
        var renderer = new EmailTemplateRenderer(new TemplateEngine());

        var rendered = renderer.Render("{{{UPDATE_MESSAGE}}}", new EmailTemplateContext
        {
            UpdateMessageText = "Line 1\nLine 2"
        });

        Assert.Equal("Line 1<br />Line 2", rendered);
    }

    [Fact]
    public void Render_ReplacesTicketTypeTokens()
    {
        var renderer = new EmailTemplateRenderer(new TemplateEngine());

        var rendered = renderer.Render(
            "{{{TICKET_TYPE}}}|{{TicketType}}|{{{TICKET_TYPE_LOWER}}}|{{TicketTypeLower}}",
            new EmailTemplateContext
            {
                TicketType = "Request",
                TicketTypeLower = "request"
            });

        Assert.Equal("Request|Request|request|request", rendered);
    }
}
