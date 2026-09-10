using Helpdesk.Application.Services.EmailTemplates;
using Helpdesk.Infrastructure.EmailTemplates;

namespace Helpdesk.Tests.Infrastructure.Templates;

public class TemplateEngineTests
{
    private readonly TemplateEngine _engine = new();

    [Fact]
    public void Render_EncodesDoubleMustache_AndLeavesTripleRaw()
    {
        var model = new { Name = "John <Doe>", Html = "<strong>safe</strong>" };

        var rendered = _engine.Render("{{Name}} {{{Html}}}", model);

        Assert.Equal("John &lt;Doe&gt; <strong>safe</strong>", rendered);
    }

    [Fact]
    public void Render_SupportsDotPaths()
    {
        var model = new { Customer = new { Name = "Alice" } };

        var rendered = _engine.Render("Hello {{Customer.Name}}", model);

        Assert.Equal("Hello Alice", rendered);
    }

    [Fact]
    public void Render_IfBlock_UsesTruthyRules()
    {
        var renderedTrue = _engine.Render("A{{#if Enabled}}B{{/if}}C", new { Enabled = true });
        var renderedFalse = _engine.Render("A{{#if Enabled}}B{{/if}}C", new { Enabled = false });

        Assert.Equal("ABC", renderedTrue);
        Assert.Equal("AC", renderedFalse);
    }

    [Fact]
    public void Render_EachBlock_SupportsThisAndIndex()
    {
        var model = new
        {
            Items = new[]
            {
                new { Name = "One" },
                new { Name = "Two" }
            }
        };

        var rendered = _engine.Render("{{#each Items}}[{{@index}}:{{this.Name}}]{{/each}}", model);

        Assert.Equal("[0:One][1:Two]", rendered);
    }

    [Fact]
    public void Render_SupportsNestedBlocks()
    {
        var model = new
        {
            Outer = true,
            Items = new[] { 1, 2, 0 }
        };

        var rendered = _engine.Render("{{#if Outer}}{{#each Items}}{{#if this}}X{{/if}}{{/each}}{{/if}}", model);

        Assert.Equal("XX", rendered);
    }

    [Fact]
    public void Render_UnknownToken_Ignore_ReturnsEmpty()
    {
        var rendered = _engine.Render("Hello {{Missing}}", new { });

        Assert.Equal("Hello ", rendered);
    }

    [Fact]
    public void Render_UnknownToken_Keep_KeepsOriginalTag()
    {
        var rendered = _engine.Render(
            "Hello {{ Missing }}",
            new { },
            new TemplateEngineOptions { UnknownTokenBehavior = UnknownTokenBehavior.Keep });

        Assert.Equal("Hello {{ Missing }}", rendered);
    }

    [Fact]
    public void Render_UnknownToken_Throw_ThrowsTemplateRenderException()
    {
        var ex = Assert.Throws<TemplateRenderException>(() =>
            _engine.Render(
                "{{Missing}}",
                new { },
                new TemplateEngineOptions { UnknownTokenBehavior = UnknownTokenBehavior.Throw }));

        Assert.Contains("Missing", ex.Message);
    }

    [Fact]
    public void Render_MalformedTemplate_ThrowsTemplateParseException()
    {
        Assert.Throws<TemplateParseException>(() => _engine.Render("{{#if A}}x", new { A = true }));
        Assert.Throws<TemplateParseException>(() => _engine.Render("{{#if A}}{{/each}}", new { A = true }));
    }

    [Fact]
    public void Render_EnforcesOutputCap()
    {
        var ex = Assert.Throws<TemplateRenderException>(() =>
            _engine.Render(
                "{{#each Items}}abcd{{/each}}",
                new { Items = Enumerable.Range(0, 10).ToArray() },
                new TemplateEngineOptions { MaxOutputChars = 20 }));

        Assert.Contains("MaxOutputChars", ex.Message);
    }

    [Fact]
    public void Render_SupportsEscapedMustache()
    {
        var rendered = _engine.Render("\\{{Name}} and {{Name}}", new { Name = "A" });
        Assert.Equal("{{Name}} and A", rendered);
    }
}
