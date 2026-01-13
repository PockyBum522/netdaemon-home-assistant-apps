namespace AllenStreetNetDaemonApps;

public static class SharedState
{
    public static class MotionTimeouts
    {
        public const int KitchenLightsTimeoutMinutes = 6;
    }
    
    public static class MotionSensors
    {
        public static DateTimeOffset KitchenMotionLastSeenAt { get; set; }
    }
}