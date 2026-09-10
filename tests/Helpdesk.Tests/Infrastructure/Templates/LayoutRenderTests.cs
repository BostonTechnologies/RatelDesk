using Helpdesk.Application.Services.EmailTemplates;
using Helpdesk.Infrastructure.EmailTemplates;

namespace Helpdesk.Tests.Infrastructure.Templates;

public class LayoutRenderTests
{
    private readonly EmailTemplateRenderer _renderer = new(new TemplateEngine());

    [Fact]
    public void Render_InsertsBody_AndResolvesBrandTokens()
    {
        var template = "<p>Hello {{USER_NAME}}</p>";
        var context = new EmailTemplateContext
        {
            UserName = "Jane",
            LayoutHtml = "<header>{{BRAND_NAME}}</header><main>{{{BODY}}}</main><footer>{{PRIMARY_COLOR}} {{{LOGO_HTML}}} {{{FOOTER_HTML}}}</footer>",
            BrandName = "Acme & Co",
            PrimaryColor = "#112233",
            LogoHtml = "<img src=\"/logo.png\" />",
            FooterHtml = "<p>Footer</p>"
        };

        var rendered = _renderer.Render(template, context);

        Assert.Contains("<header>Acme &amp; Co</header>", rendered);
        Assert.Contains("<main><p>Hello Jane</p></main>", rendered);
        Assert.Contains("#112233", rendered);
        Assert.Contains("<img src=\"/logo.png\" />", rendered);
        Assert.Contains("<p>Footer</p>", rendered);
    }

    [Fact]
    public void Render_WithoutLayout_ReturnsBodyOnly()
    {
        var rendered = _renderer.Render("<p>{{USER_NAME}}</p>", new EmailTemplateContext { UserName = "J" });

        Assert.Equal("<p>J</p>", rendered);
    }

    [Fact]
    public void Render_PreservesSanitizedBodyMarkupInsideLayout()
    {
        var sanitizedBody = "<p>Safe <strong>message</strong></p>";
        var rendered = _renderer.Render(
            "{{{UPDATE_MESSAGE}}}",
            new EmailTemplateContext
            {
                UpdateMessageHtml = sanitizedBody,
                LayoutHtml = "<section>{{{BODY}}}</section>"
            });

        Assert.Equal($"<section>{sanitizedBody}</section>", rendered);
    }
}
