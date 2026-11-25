namespace AllenStreetNetDaemonApps.Models;

public static class CustomColors
{
    public static object AlyssaPurple(int brightnessPercent = 100) =>
        new { hs_color = new[] { 263, 62 }, brightness_pct = brightnessPercent };
    
    public static object DimRed(int brightnessPercent = 20) =>
        new { hs_color = new[] { 360, 100 }, brightness_pct = brightnessPercent };

    public static object VeryWarmWhite(int brightnessPercent = 100) =>
        new { color_temp_kelvin = 2864, brightness_pct = brightnessPercent };
}

public class CustomColorsTemperature(int brightnessPct)
{
    // Temperature was throwing as invalid in HA service call. Figure out how this should be set or just convert everything to a different color space
    //public int Temperature { get; private set; } = colorTemperature;
    public int BrightnessPct { get; private set; } = brightnessPct;
}