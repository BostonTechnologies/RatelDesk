extern alias NewWeb;

using NewWeb::HelpDesk.NewWeb.Models;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Helpdesk.Tests.NewWeb;

public class AuthentikOidcOptionsValidatorTests
{
    [Fact]
    public void Validate_Succeeds_When_All_Required_Values_Are_Present()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AUTHENTIK_CLIENT_SECRET"] = "env-secret"
            })
            .Build();

        var validator = new AuthentikOidcOptionsValidator(configuration);
        var options = new AuthentikOidcOptions
        {
            Authority = "https://id.example.com/application/o/rateldesk/",
            ClientId = "client-id",
            ApiScope = "helpdesk-api",
            CallbackPath = "/signin-authentik",
            SignedOutCallbackPath = "/signout-authentik"
        };

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_Fails_When_Required_Values_Are_Missing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var validator = new AuthentikOidcOptionsValidator(configuration);
        var options = new AuthentikOidcOptions
        {
            Authority = "not-a-uri",
            CallbackPath = "signin-authentik",
            SignedOutCallbackPath = "signout-authentik"
        };

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("Authority", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("ClientId", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("AUTHENTIK_CLIENT_SECRET", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("ApiScope", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_Fails_With_Legacy_Azure_Config_When_Authentik_Is_Not_Configured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AZURE_CLIENT_SECRET"] = "azure-secret",
                ["Authentication:Azure:Authority"] = "https://login.microsoftonline.com/tenant/v2.0",
                ["Authentication:Azure:ClientId"] = "azure-client-id",
                ["Authentication:Azure:ApiScope"] = "api://helpdesk/access_as_user",
                ["Authentication:Azure:CallbackPath"] = "/signin-azure",
                ["Authentication:Azure:SignedOutCallbackPath"] = "/signout-azure"
            })
            .Build();

        var validator = new AuthentikOidcOptionsValidator(configuration);
        var options = new AuthentikOidcOptions
        {
            Authority = "https://id.example.com/application/o/rateldesk/",
            CallbackPath = "/signin-authentik",
            SignedOutCallbackPath = "/signout-authentik"
        };

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("ClientId", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("AUTHENTIK_CLIENT_SECRET", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("ApiScope", StringComparison.Ordinal));
    }
}
