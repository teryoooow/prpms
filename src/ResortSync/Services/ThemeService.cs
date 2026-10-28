using MaterialDesignThemes.Wpf;

namespace ResortSync.Services;

/// <summary>Applies the MaterialDesign light/dark base theme at runtime.</summary>
public interface IThemeService
{
    /// <summary>themeName: "Dark", "Light", or "System" (follows Windows).</summary>
    void Apply(string themeName);

    string Current { get; }
}

public sealed class ThemeService : IThemeService
{
    public string Current { get; private set; } = "System";

    public void Apply(string themeName)
    {
        Current = themeName;

        var baseTheme = themeName switch
        {
            "Dark" => BaseTheme.Dark,
            "Light" => BaseTheme.Light,
            // "System": read the Windows per-app light/dark preference.
            _ => IsWindowsAppsDark() ? BaseTheme.Dark : BaseTheme.Light
        };

        // MD 5.x API: read the active Theme, swap its base (Light/Dark), re-apply.
        // The primary/secondary palette is preserved.
        var palette = new PaletteHelper();
        var theme = palette.GetTheme();
        theme.SetBaseTheme(baseTheme);
        palette.SetTheme(theme);
    }

    /// <summary>Reads HKCU ...\Personalize\AppsUseLightTheme (1 = light, 0 = dark).</summary>
    private static bool IsWindowsAppsDark()
    {
        try
        {
            return Microsoft.Win32.Registry.GetValue(
                       @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                       "AppsUseLightTheme",
                       1) is int value && value == 0;
        }
        catch
        {
            return true; // registry unavailable → assume dark (app default)
        }
    }
}
