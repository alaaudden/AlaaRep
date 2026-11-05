using System.Windows;
using System.Windows.Media;

namespace AlaaRep.Takeoff.Wpf;

public enum AppTheme
{
    Dark,
    Light,
    Desert,
    Sea
}

public static class ThemeManager
{
    public static void ApplyTheme(Window window, AppTheme theme)
    {
        var res = window.Resources;

        switch (theme)
        {
            case AppTheme.Light:
                SetBrush(res, "BgPrimaryBrush", Color.FromRgb(241, 245, 249));   // Slate 100
                SetBrush(res, "BgSecondaryBrush", Color.FromRgb(255, 255, 255)); // White
                SetBrush(res, "BgTertiaryBrush", Color.FromRgb(226, 232, 240));  // Slate 200
                SetBrush(res, "BorderBrush", Color.FromRgb(203, 213, 225));      // Slate 300
                SetBrush(res, "TextMainBrush", Color.FromRgb(15, 23, 42));       // Slate 900
                SetBrush(res, "TextMutedBrush", Color.FromRgb(100, 116, 139));   // Slate 500
                SetBrush(res, "AccentColorBrush", Color.FromRgb(37, 99, 235));   // Blue 600
                SetBrush(res, "ViewportBgBrush", Color.FromRgb(248, 250, 252));
                break;

            case AppTheme.Desert:
                SetBrush(res, "BgPrimaryBrush", Color.FromRgb(245, 235, 224));   // Warm desert sand
                SetBrush(res, "BgSecondaryBrush", Color.FromRgb(253, 251, 247)); // Parchment white
                SetBrush(res, "BgTertiaryBrush", Color.FromRgb(227, 213, 202));  // Terracotta clay
                SetBrush(res, "BorderBrush", Color.FromRgb(212, 163, 115));      // Camel bronze
                SetBrush(res, "TextMainBrush", Color.FromRgb(67, 40, 24));       // Deep espresso
                SetBrush(res, "TextMutedBrush", Color.FromRgb(127, 85, 57));     // Sienna
                SetBrush(res, "AccentColorBrush", Color.FromRgb(217, 119, 6));   // Desert amber
                SetBrush(res, "ViewportBgBrush", Color.FromRgb(250, 244, 238));
                break;

            case AppTheme.Sea:
                SetBrush(res, "BgPrimaryBrush", Color.FromRgb(11, 25, 44));      // Deep oceanic navy
                SetBrush(res, "BgSecondaryBrush", Color.FromRgb(30, 62, 98));    // Marine blue
                SetBrush(res, "BgTertiaryBrush", Color.FromRgb(45, 85, 125));    // Wave teal
                SetBrush(res, "BorderBrush", Color.FromRgb(0, 141, 218));        // Electric cyan
                SetBrush(res, "TextMainBrush", Color.FromRgb(245, 247, 250));    // Sea foam white
                SetBrush(res, "TextMutedBrush", Color.FromRgb(147, 197, 253));   // Soft aqua
                SetBrush(res, "AccentColorBrush", Color.FromRgb(65, 176, 110));  // Marine emerald
                SetBrush(res, "ViewportBgBrush", Color.FromRgb(7, 15, 26));
                break;

            case AppTheme.Dark:
            default:
                SetBrush(res, "BgPrimaryBrush", Color.FromRgb(15, 23, 42));      // Slate 900
                SetBrush(res, "BgSecondaryBrush", Color.FromRgb(30, 41, 59));    // Slate 800
                SetBrush(res, "BgTertiaryBrush", Color.FromRgb(51, 65, 85));     // Slate 700
                SetBrush(res, "BorderBrush", Color.FromRgb(51, 65, 85));
                SetBrush(res, "TextMainBrush", Color.FromRgb(248, 250, 252));    // White
                SetBrush(res, "TextMutedBrush", Color.FromRgb(148, 163, 184));   // Slate 400
                SetBrush(res, "AccentColorBrush", Color.FromRgb(59, 130, 246));  // Blue 500
                SetBrush(res, "ViewportBgBrush", Color.FromRgb(6, 9, 19));
                break;
        }

        window.Background = (Brush)res["BgPrimaryBrush"];
    }

    private static void SetBrush(ResourceDictionary res, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        res[key] = brush;
    }
}
