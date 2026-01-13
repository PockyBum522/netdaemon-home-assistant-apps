namespace AllenStreetNetDaemonApps.Utilities;

public static class Extensions
{
    public static decimal Map(this decimal value, decimal fromSource, decimal toSource, decimal fromTarget, decimal toTarget)
    {
        return (value - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget;
    }
    
    public static int Map(this int value, int fromSource, int toSource, int fromTarget, int toTarget)
    {
        var convertedValue = ((decimal)value).Map(fromSource, toSource, fromTarget, toTarget);

        return (int)Math.Round(convertedValue);
    }
}