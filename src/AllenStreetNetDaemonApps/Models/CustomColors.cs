namespace AllenStreetNetDaemonApps.Models;

public static class CustomColors
{
    public static object AlyssaPurple(int brightnessPercent = 100) =>
        new { hs_color = new[] { 263, 62 }, brightness_pct = brightnessPercent };

    public static CustomColorsHs RedDim(int brightnessPercent = 5) => new CustomColorsHs([360, 100], brightnessPercent);

    public static object VeryWarmWhite(int brightnessPercent = 100) =>
        new { color_temp_kelvin = 2864, brightness_pct = brightnessPercent };
    
    public static CustomColorsTemperature WarmWhite(int brightnessPercent = 100) =>
        new CustomColorsTemperature(3205, brightnessPercent);
}

public class CustomColorsTemperature(int colorTemperature, int brightnessPct)
{
    public int Temperature { get; private set; } = colorTemperature;
    public int BrightnessPct { get; private set; } = brightnessPct;
}

public class CustomColorsHs(int[] hsColor, int brightnessPct)
{
    public int[] HsColor { get; private set; } = hsColor;
    public int BrightnessPct { get; private set; } = brightnessPct;
}