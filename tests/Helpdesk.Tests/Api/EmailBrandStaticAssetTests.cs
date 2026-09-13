using System.Net;
using Helpdesk.API;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Helpdesk.Tests.Api;

public class EmailBrandStaticAssetTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EmailBrandStaticAssetTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseIsolatedTestStorage());
    }

    [Fact]
    public async Task DefaultEmailBrandPngs_AreServedWithoutAuthentication()
    {
        var client = _factory.CreateClient();

        var markResponse = await client.GetAsync("/email-brand/rateldesk-email-mark.png");
        var wordmarkResponse = await client.GetAsync("/email-brand/rateldesk-email-wordmark.png");

        Assert.Equal(HttpStatusCode.OK, markResponse.StatusCode);
        Assert.Equal("image/png", markResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, wordmarkResponse.StatusCode);
        Assert.Equal("image/png", wordmarkResponse.Content.Headers.ContentType?.MediaType);
    }
}
