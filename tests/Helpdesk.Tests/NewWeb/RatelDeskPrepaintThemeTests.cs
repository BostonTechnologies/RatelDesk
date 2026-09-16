extern alias NewWeb;

using NewWeb::HelpDesk.NewWeb.Themes;
using MudBlazor;
using MudBlazor.Utilities;

namespace Helpdesk.Tests.NewWeb;

public sealed class RatelDeskPrepaintThemeTests
{
    [Fact]
    public void Prepaint_css_uses_the_canonical_light_and_dark_palette_values()
    {
        var css = RatelDeskPrepaintTheme.Css;
        var theme = new RatelDeskTheme();

        Assert.Contains("html[data-helpdesk-theme=\"light\"]", css);
        Assert.Contains($"--mud-palette-background:{Color(theme.PaletteLight.Background)};", css);
        Assert.Contains($"--mud-palette-surface:{Color(theme.PaletteLight.Surface)};", css);
        Assert.Contains($"--mud-palette-text-primary:{Color(theme.PaletteLight.TextPrimary)};", css);
        Assert.Contains($"--mud-palette-appbar-background:{Color(theme.PaletteLight.AppbarBackground)};", css);
        Assert.Contains($"--mud-palette-divider:{Color(theme.PaletteLight.Divider)};", css);

        Assert.Contains("html[data-helpdesk-theme=\"dark\"]", css);
        Assert.Contains($"--mud-palette-background:{Color(theme.PaletteDark.Background)};", css);
        Assert.Contains($"--mud-palette-surface:{Color(theme.PaletteDark.Surface)};", css);
        Assert.Contains($"--mud-palette-text-primary:{Color(theme.PaletteDark.TextPrimary)};", css);
        Assert.Contains($"--mud-palette-appbar-background:{Color(theme.PaletteDark.AppbarBackground)};", css);
        Assert.Contains($"--mud-palette-divider:{Color(theme.PaletteDark.Divider)};", css);
        Assert.Contains($"--mud-palette-overlay-dark:{Color(theme.PaletteDark.OverlayDark)};", css);
    }

    [Fact]
    public void Prepaint_css_has_a_system_fallback_when_javascript_is_unavailable()
    {
        Assert.Contains("@media (prefers-color-scheme: dark){html:not([data-helpdesk-theme])", RatelDeskPrepaintTheme.Css);
        Assert.Contains("@media (prefers-color-scheme: light){html:not([data-helpdesk-theme])", RatelDeskPrepaintTheme.Css);
    }

    private static string Color(MudColor color) => color.ToString(MudColorOutputFormats.RGBA);
}
