using Helpdesk.Application.Services.Email;
using Helpdesk.Application.WorkLogs;
using Helpdesk.Infrastructure.Email;
using Helpdesk.Infrastructure.Storage;
using HtmlAgilityPack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Helpdesk.Tests.Application.Services;

public sealed class InboundInlineImageResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "helpdesk-783-" + Guid.NewGuid().ToString("N"));
    private readonly InlineImageStorageService _storage;
    private readonly InboundInlineImageResolver _resolver;
    private readonly ILogger<InboundInlineImageResolver> _logger = Substitute.For<ILogger<InboundInlineImageResolver>>();

    public InboundInlineImageResolverTests()
    {
        var options = Options.Create(new StorageOptions { RootPath = _root, ImageSigningSecret = "test-inline-image-secret" });
        _storage = new InlineImageStorageService(Substitute.For<IHostEnvironment>(), options,
            new ImageLinkSigner(options, NullLogger<ImageLinkSigner>.Instance), NullLogger<InlineImageStorageService>.Instance);
        _resolver = new InboundInlineImageResolver(_storage, _logger);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateNames_MapScreenshotAndSignatureByCid_RegardlessOfOrder(bool reverse)
    {
        var attachments = new[] { Attachment("body", [1, 2]), Attachment("signature", [3, 4]) };
        if (reverse) Array.Reverse(attachments);
        var result = await _resolver.ResolveAsync("incident", "<p>Screenshot</p><img src='cid:body'><p>Signature</p><img src='cid:signature'>", attachments);
        var sources = Sources(result.Html);
        Assert.NotEqual(sources[0], sources[1]);
        Assert.Equal(new byte[] { 1, 2 }, await ReadImage(sources[0]));
        Assert.Equal(new byte[] { 3, 4 }, await ReadImage(sources[1]));
        Assert.Equal(2, result.ConsumedAttachmentIndexes.Count);
    }

    [Fact]
    public async Task LaterReplyWithSameName_DoesNotOverwriteOriginalImage()
    {
        var original = await _resolver.ResolveAsync("incident", "<img src='cid:body'>", [Attachment("body", [1])]);
        var reply = await _resolver.ResolveAsync("incident", "<img src='cid:body'>", [Attachment("body", [2])]);
        Assert.NotEqual(Sources(original.Html)[0], Sources(reply.Html)[0]);
        Assert.Equal(new byte[] { 1 }, await ReadImage(Sources(original.Html)[0]));
        Assert.Equal(new byte[] { 2 }, await ReadImage(Sources(reply.Html)[0]));
    }

    [Theory]
    [InlineData("<IMG SRC = ' cid:&lt;BoDy&gt; '>")]
    [InlineData("<img src=cid:BODY>")]
    public async Task CidNormalization_ResolvesBracketWhitespaceCaseAndEntities(string html)
    {
        var result = await _resolver.ResolveAsync("incident", html, [Attachment(" <body> ", [1])]);
        Assert.Equal(new byte[] { 1 }, await ReadImage(Sources(result.Html)[0]));
        Assert.Single(result.ConsumedAttachmentIndexes);
    }

    [Fact]
    public async Task OnlyImageSourcesChange_AndUnreferencedAttachmentsRemainUnconsumed()
    {
        const string html = "<!doctype html><P title='cid:body'>cid:body &amp; text</P><!-- cid:body --><img src='cid:body' /><img src='https://example.com/a'><img src='data:image/png;base64,AA=='>";
        var result = await _resolver.ResolveAsync("incident", html, [Attachment("body", [1]), Attachment("download", [2])]);
        Assert.Equal(html.Replace("src='cid:body'", $"src='{Sources(result.Html)[0]}'"), result.Html);
        Assert.Equal(new[] { 0 }, result.ConsumedAttachmentIndexes);
    }

    [Fact]
    public async Task MissingCid_RemainsUnresolvedAndLogsWarning()
    {
        const string html = "<img src='cid:missing'>";
        var result = await _resolver.ResolveAsync("incident", html, [Attachment("signature", [2])]);
        Assert.Equal(html, result.Html);
        Assert.Empty(result.ConsumedAttachmentIndexes);
        Assert.Contains(_logger.ReceivedCalls(), call => call.GetMethodInfo().Name == "Log" && Equals(call.GetArguments()[0], LogLevel.Warning));
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task DifferentBytesWithDuplicateCid_AreAmbiguous()
    {
        const string html = "<img src='cid:body'><img src='cid:body'>";
        var result = await _resolver.ResolveAsync("incident", html, [Attachment("body", [1]), Attachment("BODY", [2])]);
        Assert.Equal(html, result.Html);
        Assert.Empty(result.ConsumedAttachmentIndexes);
        Assert.Contains(_logger.ReceivedCalls(), call => call.GetMethodInfo().Name == "Log" && Equals(call.GetArguments()[0], LogLevel.Warning));
    }

    [Fact]
    public async Task SingleInlineCandidate_DisambiguatesDuplicateCid()
    {
        var result = await _resolver.ResolveAsync("incident", "<img src='cid:body'>",
            [Attachment("body", [1]), Attachment("BODY", [2]) with { IsInline = true }]);
        Assert.Equal(new byte[] { 2 }, await ReadImage(Sources(result.Html)[0]));
        Assert.Equal(new[] { 1 }, result.ConsumedAttachmentIndexes);
    }

    [Fact]
    public async Task IdenticalDuplicateCidPayloads_AreConsumedWithoutAmbiguity()
    {
        var result = await _resolver.ResolveAsync("incident", "<img src='cid:body'>",
            [Attachment("body", [1]), Attachment("BODY", [1])]);
        Assert.Equal(new byte[] { 1 }, await ReadImage(Sources(result.Html)[0]));
        Assert.Equal(2, result.ConsumedAttachmentIndexes.Count);
    }

    [Fact]
    public async Task NullPayload_IsNotConsumed()
    {
        var result = await _resolver.ResolveAsync("incident", "<img src='cid:body'>", [Attachment("body", [1]) with { ContentBytes = null }]);
        Assert.Equal("<img src='cid:body'>", result.Html);
        Assert.Empty(result.ConsumedAttachmentIndexes);
    }

    [Theory]
    [InlineData("image001.png", ".png")]
    [InlineData("../../escape.PNG", ".png")]
    [InlineData("..\\..\\escape.png", ".png")]
    [InlineData("a|b weird name.svg", ".svg")]
    [InlineData("..", "")]
    [InlineData("", "")]
    [InlineData("/", "")]
    public async Task Storage_IsCollisionSafeIdempotentAndContained(string filename, string extension)
    {
        var first = await _storage.SaveIncidentInlineImageAsync("incident", filename, [1]);
        var second = await _storage.SaveIncidentInlineImageAsync("incident", filename, [2]);
        var repeated = await _storage.SaveIncidentInlineImageAsync("incident", filename, [1]);
        Assert.Equal(new Uri("https://test" + first).AbsolutePath, new Uri("https://test" + repeated).AbsolutePath);
        Assert.NotEqual(first, second);
        Assert.Equal(new byte[] { 1 }, await ReadImage(first));
        Assert.Equal(new byte[] { 2 }, await ReadImage(second));
        var files = Directory.GetFiles(_root, "*", SearchOption.AllDirectories);
        Assert.Equal(2, files.Length);
        Assert.All(files, file =>
        {
            Assert.Equal(Path.Combine(_root, "incidents", "incident", "inline"), Path.GetDirectoryName(file));
            Assert.Equal(extension, Path.GetExtension(file));
            Assert.Matches("^[a-zA-Z0-9_-]+(?:\\.[a-z0-9]+)?$", Path.GetFileName(file));
        });
    }

    private static InboundEmailAttachmentContext Attachment(string cid, byte[] bytes) => new("image001.png", "image/png", cid, bytes);
    private Task<byte[]> ReadImage(string url) => File.ReadAllBytesAsync(Path.Combine(_root, "incidents", "incident", "inline", Path.GetFileName(new Uri("https://test" + url).AbsolutePath)));
    private static string[] Sources(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        return document.DocumentNode.Descendants("img").Select(node => node.Attributes["src"].DeEntitizeValue).ToArray();
    }
    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
