using Helpdesk.Application.Services.Branding;
using Helpdesk.Infrastructure.Branding;
using Helpdesk.Shared.Models;
using Helpdesk.Shared.Services;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace Helpdesk.Tests.Infrastructure.Branding;

public sealed class InstanceBrandingProviderTests
{
    [Fact]
    public async Task GetEffectiveAsync_UsesRatelDeskDefaults_WhenNothingIsConfigured()
    {
        var provider = CreateProvider(new ConfigurationBuilder().Build(), out _);

        var effective = await provider.GetEffectiveAsync();

        Assert.Equal("RatelDesk", effective.ApplicationName);
        Assert.Equal("/branding/rateldesk-mark.webp", effective.CompactLogoUrl);
        Assert.Equal("RatelDesk", effective.EmailFromDisplayName);
    }

    [Fact]
    public async Task GetAdministrationAsync_UsesDeploymentValueOverPersistedValue_AndMarksItManaged()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Branding:ApplicationName"] = "Acme Service Desk"
        }).Build();
        var provider = CreateProvider(configuration, out var stored);
        stored.Value = new InstanceBranding { ApplicationName = "Database Desk", OrganizationName = "Acme IT" };

        var administration = await provider.GetAdministrationAsync();

        Assert.Equal("Acme Service Desk", administration.Effective.ApplicationName);
        Assert.Equal("Acme IT", administration.Effective.OrganizationName);
        var applicationName = Assert.Single(administration.Fields, x => x.Name == nameof(InstanceBranding.ApplicationName));
        Assert.Equal(InstanceBrandingValueSource.Environment, applicationName.Source);
        Assert.False(applicationName.IsAdminEditable);
    }

    [Fact]
    public async Task SaveAsync_PersistsEditableValues_AndImmediatelyReturnsTheirEffectiveValue()
    {
        var provider = CreateProvider(new ConfigurationBuilder().Build(), out var stored);
        var update = new InstanceBrandingUpdate(
            "Acme Service Desk", "Acme IT", "https://support.example.test", null, null,
            "help@example.test", null, null, null, "Acme Service Desk", "Here to help");

        var administration = await provider.SaveAsync(update);

        Assert.Equal("Acme Service Desk", stored.Value?.ApplicationName);
        Assert.Equal("https://support.example.test", administration.Effective.ApplicationUrl);
        Assert.Equal(InstanceBrandingValueSource.Database,
            Assert.Single(administration.Fields, x => x.Name == nameof(InstanceBranding.ApplicationUrl)).Source);
    }

    private static InstanceBrandingProvider CreateProvider(IConfiguration configuration, out BrandingState stored)
    {
        stored = new BrandingState();
        var state = stored;
        var repository = Substitute.For<IRepository<InstanceBranding>>();
        repository.GetAsync("1").Returns(_ => Task.FromResult(state.Value));
        repository.CreateAsync(Arg.Any<InstanceBranding>()).Returns(call =>
        {
            state.Value = call.Arg<InstanceBranding>();
            return Task.FromResult(state.Value);
        });
        repository.UpdateAsync(Arg.Any<InstanceBranding>()).Returns(call =>
        {
            state.Value = call.Arg<InstanceBranding>();
            return Task.FromResult<InstanceBranding?>(state.Value);
        });
        return new InstanceBrandingProvider(repository, configuration);
    }

    private sealed class BrandingState
    {
        public InstanceBranding? Value { get; set; }
    }
}
