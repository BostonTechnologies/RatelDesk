using System.Globalization;
using System.Text;
using MudBlazor;
using MudBlazor.Utilities;

namespace HelpDesk.NewWeb.Themes;

/// <summary>
/// Emits the palette variables needed before the interactive MudBlazor provider can run.
/// The values are read from <see cref="RatelDeskTheme"/>, which remains the sole palette source.
/// </summary>
public static class RatelDeskPrepaintTheme
{
    private static readonly RatelDeskTheme Theme = new();

    public static string Css { get; } = BuildCss();

    private static string BuildCss()
    {
        var css = new StringBuilder();
        AppendPalette(css, "html[data-helpdesk-theme=\"light\"]", Theme.PaletteLight, "light");
        AppendPalette(css, "html[data-helpdesk-theme=\"dark\"]", Theme.PaletteDark, "dark");

        // JavaScript cannot read a stored override when it is disabled. System is the safe fallback.
        css.Append("@media (prefers-color-scheme: dark){");
        AppendPalette(css, "html:not([data-helpdesk-theme])", Theme.PaletteDark, "dark");
        css.Append('}');
        css.Append("@media (prefers-color-scheme: light){");
        AppendPalette(css, "html:not([data-helpdesk-theme])", Theme.PaletteLight, "light");
        css.Append('}');
        return css.ToString();
    }

    private static void AppendPalette(StringBuilder css, string selector, Palette palette, string colorScheme)
    {
        css.Append(selector).Append("{color-scheme:").Append(colorScheme).Append(";background:").Append(Color(palette.Background)).Append(';');

        AppendColor(css, "black", palette.Black);
        AppendColor(css, "white", palette.White);
        AppendColorFamily(css, "primary", palette.Primary, palette.PrimaryContrastText, palette.PrimaryDarken, palette.PrimaryLighten, palette.HoverOpacity);
        AppendColorFamily(css, "secondary", palette.Secondary, palette.SecondaryContrastText, palette.SecondaryDarken, palette.SecondaryLighten, palette.HoverOpacity);
        AppendColorFamily(css, "tertiary", palette.Tertiary, palette.TertiaryContrastText, palette.TertiaryDarken, palette.TertiaryLighten, palette.HoverOpacity);
        AppendColorFamily(css, "info", palette.Info, palette.InfoContrastText, palette.InfoDarken, palette.InfoLighten, palette.HoverOpacity);
        AppendColorFamily(css, "success", palette.Success, palette.SuccessContrastText, palette.SuccessDarken, palette.SuccessLighten, palette.HoverOpacity);
        AppendColorFamily(css, "warning", palette.Warning, palette.WarningContrastText, palette.WarningDarken, palette.WarningLighten, palette.HoverOpacity);
        AppendColorFamily(css, "error", palette.Error, palette.ErrorContrastText, palette.ErrorDarken, palette.ErrorLighten, palette.HoverOpacity);
        AppendColorFamily(css, "dark", palette.Dark, palette.DarkContrastText, palette.DarkDarken, palette.DarkLighten, palette.HoverOpacity);

        AppendColor(css, "text-primary", palette.TextPrimary, includeRgb: true);
        AppendColor(css, "text-secondary", palette.TextSecondary, includeRgb: true);
        AppendColor(css, "text-disabled", palette.TextDisabled, includeRgb: true);
        AppendColor(css, "action-default", palette.ActionDefault);
        Append(css, "action-default-hover", palette.ActionDefault.SetAlpha(palette.HoverOpacity).ToString(MudColorOutputFormats.RGBA));
        AppendColor(css, "action-disabled", palette.ActionDisabled);
        AppendColor(css, "action-disabled-background", palette.ActionDisabledBackground);
        AppendColor(css, "surface", palette.Surface, includeRgb: true);
        AppendColor(css, "background", palette.Background);
        AppendColor(css, "background-gray", palette.BackgroundGray);
        AppendColor(css, "drawer-background", palette.DrawerBackground);
        AppendColor(css, "drawer-text", palette.DrawerText);
        AppendColor(css, "drawer-icon", palette.DrawerIcon);
        AppendColor(css, "appbar-background", palette.AppbarBackground);
        AppendColor(css, "appbar-text", palette.AppbarText);
        AppendColor(css, "lines-default", palette.LinesDefault);
        AppendColor(css, "lines-inputs", palette.LinesInputs);
        AppendColor(css, "table-lines", palette.TableLines);
        AppendColor(css, "table-striped", palette.TableStriped);
        AppendColor(css, "table-hover", palette.TableHover);
        AppendColor(css, "divider", palette.Divider, includeRgb: true);
        AppendColor(css, "divider-light", palette.DividerLight);
        AppendColor(css, "skeleton", palette.Skeleton);
        AppendColor(css, "gray-default", palette.GrayDefault);
        AppendColor(css, "gray-light", palette.GrayLight);
        AppendColor(css, "gray-lighter", palette.GrayLighter);
        AppendColor(css, "gray-dark", palette.GrayDark);
        AppendColor(css, "gray-darker", palette.GrayDarker);
        AppendColor(css, "overlay-dark", palette.OverlayDark);
        AppendColor(css, "overlay-light", palette.OverlayLight);
        Append(css, "border-opacity", palette.BorderOpacity.ToString(CultureInfo.InvariantCulture));
        css.Append("--mud-ripple-color:var(--mud-palette-text-primary);");
        css.Append('}');
    }

    private static void AppendColorFamily(StringBuilder css, string name, MudColor color, MudColor contrast, string darken, string lighten, double hoverOpacity)
    {
        AppendColor(css, name, color, includeRgb: true);
        AppendColor(css, $"{name}-text", contrast);
        Append(css, $"{name}-darken", darken);
        Append(css, $"{name}-lighten", lighten);
        Append(css, $"{name}-hover", color.SetAlpha(hoverOpacity).ToString(MudColorOutputFormats.RGBA));
    }

    private static void AppendColor(StringBuilder css, string name, MudColor color, bool includeRgb = false)
    {
        Append(css, name, Color(color));
        if (includeRgb)
        {
            Append(css, $"{name}-rgb", color.ToString(MudColorOutputFormats.ColorElements));
        }
    }

    private static void Append(StringBuilder css, string name, string value) =>
        css.Append("--mud-palette-").Append(name).Append(':').Append(value).Append(';');

    private static string Color(MudColor color) => color.ToString(MudColorOutputFormats.RGBA);
}
