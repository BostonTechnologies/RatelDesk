using System.Net;
using System.Text.Json;
using Helpdesk.Application.Services.AI;
using Helpdesk.Shared.Enums;
using Helpdesk.Shared.Models;
using NSubstitute;

namespace Helpdesk.Tests.Application.Services;

public class OpenAiCompatibleClientTests
{
    [Fact]
    public async Task ListModelsAsync_UsesApiRoot_WhenBaseUrlEndsWithApi()
    {
        Uri? seenUri = null;
        var client = CreateClient(request =>
        {
            seenUri = request.RequestUri;
            var json = JsonSerializer.Serialize(new { data = new[] { new { id = "qwen2.5-coder:7b" } } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        });

        var models = await client.ListModelsAsync(CreateProvider("https://ai.example.com/api"), CancellationToken.None);

        Assert.Contains("qwen2.5-coder:7b", models);
        Assert.Equal("https://ai.example.com/api/models", seenUri?.ToString());
    }

    [Fact]
    public async Task ListModelsAsync_UsesV1Root_WhenBaseUrlEndsWithV1()
    {
        Uri? seenUri = null;
        var client = CreateClient(request =>
        {
            seenUri = request.RequestUri;
            var json = JsonSerializer.Serialize(new { data = new[] { new { id = "gpt-4o-mini" } } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        });

        var models = await client.ListModelsAsync(CreateProvider("https://api.openai.com/v1"), CancellationToken.None);

        Assert.Contains("gpt-4o-mini", models);
        Assert.Equal("https://api.openai.com/v1/models", seenUri?.ToString());
    }

    [Fact]
    public async Task ListModelsAsync_FallsBack_FromV1_ToApi_ForBareRoot()
    {
        var seenUris = new List<string>();
        var client = CreateClient(request =>
        {
            seenUris.Add(request.RequestUri!.ToString());
            if (request.RequestUri!.ToString().EndsWith("/v1/models", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"detail\":\"missing\"}")
                };
            }

            var json = JsonSerializer.Serialize(new { data = new[] { new { id = "qwen2.5-coder:7b" } } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        });

        var models = await client.ListModelsAsync(CreateProvider("https://ai.example.com"), CancellationToken.None);

        Assert.Contains("qwen2.5-coder:7b", models);
        Assert.Equal(
            ["https://ai.example.com/v1/models", "https://ai.example.com/api/models"],
            seenUris);
    }

    [Fact]
    public async Task TestAsync_ReturnsDiagnostics_OnFailure()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"detail\":\"API key not allowed to access this endpoint.\"}")
        });

        var result = await client.TestAsync(CreateProvider("https://ai.example.com/api"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("https://ai.example.com/api/models", result.AttemptedUrl);
        Assert.Equal(403, result.StatusCode);
        Assert.Contains("API key not allowed", result.Message);
    }

    private static OpenAiCompatibleClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("OpenAICompatible")
            .Returns(new HttpClient(new TestHandler(handler)));

        var protector = Substitute.For<ISecretProtector>();
        protector.Unprotect("enc-key").Returns("plain-token");

        return new OpenAiCompatibleClient(factory, protector);
    }

    private static AiProvider CreateProvider(string baseUrl) => new()
    {
        Id = Guid.NewGuid(),
        Name = "OpenWebUI",
        ProviderType = AiProviderType.OpenAICompatible,
        BaseUrl = baseUrl,
        ApiKeyEncrypted = "enc-key",
        DefaultModel = "qwen2.5-coder:7b",
        IsEnabled = true
    };

    private sealed class TestHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
