namespace AllenStreetNetDaemonApps.Utilities;

public static class TimeRangeHelpers
{
    public static bool IsNightTime()
    {
        // These should handle 2300, 0000, 0100 on up to 0600, but not outside of those
        
        if (DateTimeOffset.Now.Hour == 23) return true;
        if (DateTimeOffset.Now.Hour < 7) return true;
        
        return false;
    }
}