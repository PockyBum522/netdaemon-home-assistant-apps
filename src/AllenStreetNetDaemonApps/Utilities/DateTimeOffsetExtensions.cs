namespace AllenStreetNetDaemonApps.Utilities;

public static class DateTimeOffsetExtensions
{
    public static string GetTimeOnly(this DateTimeOffset dateTimeOffset)
    {
        return dateTimeOffset.ToString("HH:mm:ss");
    }
}