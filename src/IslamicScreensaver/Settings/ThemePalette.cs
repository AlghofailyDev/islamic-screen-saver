using System.Windows;
using System.Windows.Media;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Settings;
using Microsoft.Win32;

namespace IslamicScreensaver.Settings;

/// <summary>
/// Swaps the settings window between the light and dark palettes. Only the brushes named in
/// <c>Theme.xaml</c> change; every page refers to them by key, so nothing else needs to know.
/// </summary>
internal static class ThemePalette
{
    private static readonly (string Key, string Light, string Dark)[] Colours =
    [
        ("AccentBrush",            "#1F7A63", "#3FA98C"),
        ("AccentHoverBrush",       "#1A6A56", "#4FBC9D"),
        ("AccentPressedBrush",     "#155A49", "#2F8E73"),
        ("AccentSoftBrush",        "#141F7A63", "#243FA98C"),
        ("TextPrimaryBrush",       "#1B1B1B", "#F0F0F0"),
        ("TextSecondaryBrush",     "#5D5D5D", "#A8A8A8"),
        ("TextDisabledBrush",      "#9D9D9D", "#6E6E6E"),
        ("CardBackgroundBrush",    "#FBFBFB", "#2B2B2B"),
        ("CardBorderBrush",        "#0F000000", "#14FFFFFF"),
        ("DividerBrush",           "#0F000000", "#14FFFFFF"),
        ("NavSelectedBrush",       "#0B000000", "#1AFFFFFF"),
        ("NavHoverBrush",          "#08000000", "#12FFFFFF"),
        ("ControlBorderBrush",     "#8A8A8A", "#7A7A7A"),
        ("SuccessBrush",           "#1F7A63", "#4FBC9D"),
        ("WarningBrush",           "#9D5D00", "#E0A34A"),
        ("InputBackgroundBrush",   "#FFFFFF", "#333333"),
        ("InputBorderBrush",       "#E5E5E5", "#3D3D3D"),
        ("ButtonHoverBrush",       "#F0F0F0", "#383838"),
        ("ButtonPressedBrush",     "#E6E6E6", "#404040"),
    ];

    private static readonly (string Key, string Light1, string Light2, string Dark1, string Dark2) Window =
        ("WindowBackgroundBrush", "#F5F6F7", "#EFF2F1", "#1F2220", "#1A1D1C");

    /// <summary>Applies a theme to the application's resources and returns the one actually used.</summary>
    public static AppTheme Apply(AppTheme theme)
    {
        var dark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => WindowsPrefersDark(),
        };

        var resources = Application.Current.Resources;
        foreach (var (key, light, darkValue) in Colours)
            resources[key] = Frozen(new SolidColorBrush(Parse(dark ? darkValue : light)));

        var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        gradient.GradientStops.Add(new GradientStop(Parse(dark ? Window.Dark1 : Window.Light1), 0));
        gradient.GradientStops.Add(new GradientStop(Parse(dark ? Window.Dark2 : Window.Light2), 1));
        resources[Window.Key] = Frozen(gradient);

        return dark ? AppTheme.Dark : AppTheme.Light;
    }

    /// <summary>Windows' own "app mode" preference, which the System option follows.</summary>
    public static bool WindowsPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception ex)
        {
            Log.Error("Could not read the Windows theme preference; using light.", ex);
            return false;
        }
    }

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
