extern alias NewWeb;

using System.Net;
using System.Text;
using NewWeb::HelpDesk.NewWeb.Services;

namespace Helpdesk.Tests.NewWeb;

public sealed class SetupInitializationErrorTests
{
    [Fact]
    public async Task Time_zone_problem_identifies_step_three_without_blame_on_the_password()
    {
        using var response = Problem("""
            {"code":"setup_validation_failed","errors":{"timeZoneId":["The API could not load the selected time zone. Install tzdata."]},"traceId":"0TEST:000001"}
            """);
        var message = await SetupInitializationError.ReadAsync(response, CancellationToken.None);
        Assert.Contains("Step 3 — Time zone", message);
        Assert.Contains("Install tzdata", message);
        Assert.Contains("API log reference: 0TEST:000001", message);
        Assert.DoesNotContain("passphrase", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Password_problem_identifies_step_four()
    {
        using var response = Problem("""
            {"code":"setup_validation_failed","errors":{"password":["Use at least 15 characters."]}}
            """);
        var message = await SetupInitializationError.ReadAsync(response, CancellationToken.None);
        Assert.Contains("Step 4 — Passphrase", message);
        Assert.Contains("15 characters", message);
        Assert.DoesNotContain("Time zone", message);
    }

    [Theory]
    [InlineData("<html>upstream password=private</html>")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"title\":\"private\",\"detail\":\"password=private\"}")]
    [InlineData("{\"code\":\"setup_initialization_failed\",\"detail\":\"password=private\"}")]
    [InlineData("{\"code\":\"unknown\",\"errors\":{\"password\":[\"private\"]}}")]
    public async Task Unexpected_responses_do_not_expose_raw_proxy_or_database_errors(string body)
    {
        using var response = Problem(body);
        var message = await SetupInitializationError.ReadAsync(response, CancellationToken.None);
        Assert.Contains("API setup status", message);
        Assert.DoesNotContain("private", message);
    }

    [Fact]
    public async Task Oversized_response_is_not_displayed()
    {
        using var response = Problem(new string('x', 20_000));
        var message = await SetupInitializationError.ReadAsync(response, CancellationToken.None);
        Assert.True(message.Length < 200);
    }

    [Fact]
    public async Task Unknown_fields_and_unsafe_trace_references_are_omitted()
    {
        using var response = Problem("""
            {"code":"setup_validation_failed","errors":{"connectionString":["private"]},"traceId":"<script>private</script>"}
            """);
        var message = await SetupInitializationError.ReadAsync(response, CancellationToken.None);
        Assert.DoesNotContain("private", message);
        Assert.DoesNotContain("<script>", message);
    }

    [Fact]
    public async Task Storage_preflight_failure_points_to_database_requirements()
    {
        using var response = Problem("""{"code":"setup_storage_preflight_failed"}""");
        Assert.Contains("vector and pg_trgm", await SetupInitializationError.ReadAsync(response, CancellationToken.None));
    }

    private static HttpResponseMessage Problem(string body) => new(HttpStatusCode.BadRequest)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/problem+json")
    };
}
