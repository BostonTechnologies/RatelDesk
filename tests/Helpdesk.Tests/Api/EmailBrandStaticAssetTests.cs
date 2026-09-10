using System.Net;
using Helpdesk.API;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Helpdesk.Tests.Api;

public class EmailBrandStaticAssetTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EmailBrandStaticAssetTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DefaultEmailBrandLogo_IsServedWithoutAuthentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/email-brand/rateldesk-mark.svg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
    }
}
