namespace NetDaemonApps.Models;

public static class CustomColors
{
    public static object AlyssaPurple(int brightnessPercent = 100) =>
        new { hs_color = new[] { 263, 62 }, brightness_pct = brightnessPercent };

    public static object VeryWarmWhite(int brightnessPercent = 100) =>
        new { color_temp_kelvin = 2864, brightness_pct = brightnessPercent };
    
    public static object WarmWhite(int brightnessPercent = 100) =>
        new { color_temp_kelvin = 3205, brightness_pct = brightnessPercent };
}