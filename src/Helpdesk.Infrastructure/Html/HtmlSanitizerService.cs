using Ganss.Xss;
using Helpdesk.Application.WorkLogs;

namespace Helpdesk.Infrastructure.Html;

public sealed class HtmlSanitizerService : IHtmlSanitizerService
{
    private readonly HtmlSanitizer _sanitizer = BuildSanitizer();

    public string Sanitize(string html)
    {
        return _sanitizer.Sanitize(html ?? string.Empty);
    }

    private static HtmlSanitizer BuildSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.Add("img");
        sanitizer.AllowedTags.Add("table");
        sanitizer.AllowedTags.Add("thead");
        sanitizer.AllowedTags.Add("tbody");
        sanitizer.AllowedTags.Add("tr");
        sanitizer.AllowedTags.Add("td");
        sanitizer.AllowedTags.Add("th");

        sanitizer.AllowedAttributes.Add("src");
        sanitizer.AllowedAttributes.Add("style");
        sanitizer.AllowedAttributes.Add("class");
        sanitizer.AllowedAttributes.Add("alt");
        sanitizer.AllowedAttributes.Add("width");
        sanitizer.AllowedAttributes.Add("height");

        sanitizer.AllowedSchemes.Add("data");

        return sanitizer;
    }
}
