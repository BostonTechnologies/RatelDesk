using Helpdesk.Infrastructure.Html;

namespace Helpdesk.Tests.Infrastructure.Templates;

public class TenantBrandingSanitizationTests
{
    [Fact]
    public void FooterHtml_Sanitization_RemovesScriptsAndInlineHandlers()
    {
        var sanitizer = new HtmlSanitizerService();
        var input = "<p>Footer</p><script>alert(1)</script><img src=\"x\" onerror=\"alert(2)\">";

        var sanitized = sanitizer.Sanitize(input);

        Assert.DoesNotContain("<script", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<p>Footer</p>", sanitized, StringComparison.OrdinalIgnoreCase);
    }
}
