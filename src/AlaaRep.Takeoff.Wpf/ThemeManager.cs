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

        Color bgPrimary, bgSecondary, bgTertiary, border, textMain, textMuted, accent, viewportBg;

        switch (theme)
        {
            case AppTheme.Light:
                bgPrimary = Color.FromRgb(248, 250, 252);     // Slate 50
                bgSecondary = Color.FromRgb(255, 255, 255);   // White
                bgTertiary = Color.FromRgb(241, 245, 249);    // Slate 100
                border = Color.FromRgb(203, 213, 225);        // Slate 300
                textMain = Color.FromRgb(15, 23, 42);         // Slate 900
                textMuted = Color.FromRgb(100, 116, 139);     // Slate 500
                accent = Color.FromRgb(37, 99, 235);          // Blue 600
                viewportBg = Color.FromRgb(241, 245, 249);
                break;

            case AppTheme.Desert:
                bgPrimary = Color.FromRgb(245, 235, 224);     // Warm sand
                bgSecondary = Color.FromRgb(253, 251, 247);   // Parchment
                bgTertiary = Color.FromRgb(237, 224, 212);    // Clay
                border = Color.FromRgb(212, 163, 115);        // Bronze camel
                textMain = Color.FromRgb(67, 40, 24);         // Dark espresso
                textMuted = Color.FromRgb(140, 94, 61);       // Sienna
                accent = Color.FromRgb(202, 103, 2);          // Terracotta
                viewportBg = Color.FromRgb(237, 224, 212);
                break;

            case AppTheme.Sea:
                bgPrimary = Color.FromRgb(11, 25, 44);        // Deep oceanic navy
                bgSecondary = Color.FromRgb(20, 48, 80);      // Marine blue
                bgTertiary = Color.FromRgb(30, 62, 98);       // Ocean surge
                border = Color.FromRgb(0, 141, 218);          // Cyan
                textMain = Color.FromRgb(245, 247, 250);      // White
                textMuted = Color.FromRgb(147, 197, 253);     // Soft aqua
                accent = Color.FromRgb(65, 176, 110);        // Sea green
                viewportBg = Color.FromRgb(7, 15, 26);
                break;

            case AppTheme.Dark:
            default:
                bgPrimary = Color.FromRgb(15, 23, 42);        // Slate 900
                bgSecondary = Color.FromRgb(30, 41, 59);      // Slate 800
                bgTertiary = Color.FromRgb(51, 65, 85);       // Slate 700
                border = Color.FromRgb(51, 65, 85);
                textMain = Color.FromRgb(248, 250, 252);      // White
                textMuted = Color.FromRgb(148, 163, 184);     // Slate 400
                accent = Color.FromRgb(59, 130, 246);        // Blue 500
                viewportBg = Color.FromRgb(10, 15, 29);
                break;
        }

        res["BgPrimaryBrush"] = FreezeBrush(bgPrimary);
        res["BgSecondaryBrush"] = FreezeBrush(bgSecondary);
        res["BgTertiaryBrush"] = FreezeBrush(bgTertiary);
        res["BorderBrush"] = FreezeBrush(border);
        res["TextMainBrush"] = FreezeBrush(textMain);
        res["TextMutedBrush"] = FreezeBrush(textMuted);
        res["AccentColorBrush"] = FreezeBrush(accent);
        res["ViewportBgBrush"] = FreezeBrush(viewportBg);

        // Update CadGridTileBrush for the selected theme
        var gridTile = new DrawingBrush
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 50, 50),
            ViewportUnits = BrushMappingMode.Absolute
        };
        var dg = new DrawingGroup();
        dg.Children.Add(new GeometryDrawing(FreezeBrush(viewportBg), null, new RectangleGeometry(new Rect(0, 0, 50, 50))));
        var gridPen = new Pen(FreezeBrush(Color.FromArgb(40, border.R, border.G, border.B)), 0.5);
        gridPen.Freeze();
        var linesGeo = new GeometryGroup();
        linesGeo.Children.Add(new LineGeometry(new System.Windows.Point(0, 0), new System.Windows.Point(50, 0)));
        linesGeo.Children.Add(new LineGeometry(new System.Windows.Point(0, 0), new System.Windows.Point(0, 50)));
        dg.Children.Add(new GeometryDrawing(null, gridPen, linesGeo));
        gridTile.Drawing = dg;
        gridTile.Freeze();
        res["CadGridTileBrush"] = gridTile;

        window.Background = (Brush)res["BgPrimaryBrush"];
    }

    private static SolidColorBrush FreezeBrush(Color color)
    {
        var b = new SolidColorBrush(color);
        b.Freeze();
        return b;
    }
}
