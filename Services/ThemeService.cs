using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using PointManager.Models;

namespace PointManager.Services;

public static class ThemeService
{
    private static readonly SwatchesProvider Swatches = new();
    private static AppSettings? _monitored;
    private static FontFamily? _defaultFont;

    private const string RobotoPackUri =
        "pack://application:,,,/MaterialDesignThemes.Wpf;component/Resources/Roboto/#Roboto";

    /// <summary>
    /// The MaterialDesign default font, captured once (before any override) so that
    /// switching back to "默认" restores the original rather than the last custom font.
    /// </summary>
    public static FontFamily DefaultFont
    {
        get
        {
            if (_defaultFont != null) return _defaultFont;
            var app = System.Windows.Application.Current;
            _defaultFont = (app?.TryFindResource("MaterialDesignFont") as FontFamily)
                           ?? new FontFamily(RobotoPackUri);
            return _defaultFont;
        }
    }

    /// <summary>All primary swatches, for the settings color picker.</summary>
    public static IReadOnlyList<Swatch> PrimarySwatches => Swatches.Swatches.ToList();

    /// <summary>Swatches that provide an accent hue, for the secondary color picker.</summary>
    public static IReadOnlyList<Swatch> AccentSwatches =>
        Swatches.Swatches.Where(s => s.IsAccented && s.AccentExemplarHue != null).ToList();

    public static Color GetPrimaryColor(string? value)
    {
        if (TryParseHex(value, out var hex)) return hex;
        var sw = Swatches.Swatches.FirstOrDefault(s =>
            string.Equals(s.Name, value, StringComparison.OrdinalIgnoreCase));
        return sw?.ExemplarHue.Color ?? Colors.Indigo;
    }

    public static Color GetSecondaryColor(string? value)
    {
        if (TryParseHex(value, out var hex)) return hex;
        var sw = Swatches.Swatches.FirstOrDefault(s =>
            s.IsAccented && s.AccentExemplarHue != null &&
            string.Equals(s.Name, value, StringComparison.OrdinalIgnoreCase));
        return sw?.AccentExemplarHue?.Color ?? Color.FromRgb(0xFF, 0xC1, 0x07);
    }

    /// <summary>
    /// Parses a user-picked color stored as "#RRGGBB"/"#AARRGGBB". Only strings that
    /// begin with '#' are treated as hex, so swatch names like "Teal" still resolve to
    /// their MaterialDesign exemplar hue rather than the WPF named color.
    /// </summary>
    private static bool TryParseHex(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value) || value![0] != '#') return false;
        try
        {
            if (ColorConverter.ConvertFromString(value) is Color c)
            {
                color = c;
                return true;
            }
        }
        catch { /* not a valid hex color; fall back to swatch lookup */ }
        return false;
    }

    /// <summary>
    /// Reads the current Windows accent color (HKCU\...\DWM\ColorizationColor, 0xAARRGGBB).
    /// Returns null when it cannot be read, so callers can fall back to a swatch.
    /// </summary>
    public static Color? GetSystemAccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("ColorizationColor") is int argb)
            {
                // Force the alpha channel opaque; the stored alpha drives DWM transparency.
                return Color.FromRgb((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
            }
        }
        catch { /* fall through to null */ }
        return null;
    }

    public static void Apply(AppSettings settings)
    {
        var helper = new PaletteHelper();
        var theme = helper.GetTheme();

        IBaseTheme baseTheme = settings.IsDarkTheme
            ? new MaterialDesignDarkTheme()
            : new MaterialDesignLightTheme();

        theme.SetBaseTheme(baseTheme);

        // When following the system accent, both the primary and the accent track it
        // (falling back to the stored swatch if the system color can't be read).
        var systemAccent = settings.FollowSystemAccent ? GetSystemAccentColor() : null;
        var primary = systemAccent ?? GetPrimaryColor(settings.PrimaryColor);
        var secondary = systemAccent ?? GetSecondaryColor(settings.SecondaryColor);

        theme.SetPrimaryColor(primary);
        theme.SetSecondaryColor(secondary);

        helper.SetTheme(theme);
        ApplyContrastForegrounds();

        // Capture the pristine default font before overriding it, so switching back to
        // "默认" restores the original rather than the last custom font.
        var defaultFont = DefaultFont;
        var font = string.IsNullOrWhiteSpace(settings.FontFamily)
            ? defaultFont
            : new FontFamily(settings.FontFamily);

        // PointManagerFont drives our window style; MaterialDesignFont covers
        // MaterialDesign's own control styles that reference it.
        var resources = System.Windows.Application.Current?.Resources;
        if (resources != null)
        {
            resources["PointManagerFont"] = font;
            resources["MaterialDesignFont"] = font;
        }
    }

    /// <summary>
    /// MDIX picks the primary/secondary foreground by raw WCAG contrast, which lands on
    /// black for mid-dark saturated hues like the Windows accent #0078D4 and reads poorly
    /// on the colored title bars. Re-pick using perceived brightness so dark/saturated
    /// surfaces get white text and light surfaces keep black.
    /// </summary>
    private static void ApplyContrastForegrounds()
    {
        var resources = System.Windows.Application.Current?.Resources;
        if (resources is null) return;
        foreach (var hue in new[] { "Primary", "Secondary" })
        foreach (var shade in new[] { "Light", "Mid", "Dark" })
        {
            if (System.Windows.Application.Current!.TryFindResource($"{hue}Hue{shade}Brush")
                is not SolidColorBrush background) continue;
            resources[$"{hue}Hue{shade}ForegroundBrush"] =
                new SolidColorBrush(ContrastForeground(background.Color));
        }
    }

    private static Color ContrastForeground(Color background)
    {
        var brightness = (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255.0;
        return brightness > 0.5 ? Colors.Black : Colors.White;
    }

    /// <summary>
    /// Re-applies the theme whenever the user changes the Windows accent color or
    /// light/dark preference, so "跟随系统主题色" stays in sync while the app runs.
    /// </summary>
    public static void MonitorSystemTheme(AppSettings settings)
    {
        _monitored = settings;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        var settings = _monitored;
        if (settings is null) return;
        if (e.Category is not (UserPreferenceCategory.Color
            or UserPreferenceCategory.General
            or UserPreferenceCategory.Accessibility)) return;

        // The event can arrive on a non-UI thread; PaletteHelper must run on the UI thread.
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            try { Apply(settings); } catch { /* theming must never crash the app */ }
        }));
    }
}
