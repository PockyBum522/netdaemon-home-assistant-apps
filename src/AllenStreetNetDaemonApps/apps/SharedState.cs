namespace AllenStreetNetDaemonApps;

public static class SharedState
{
    public static class MotionTimeouts
    {
        public const int KitchenLightsTimeoutMinutes = 90;
    }
    
    public static class MotionSensors
    {
        public static DateTimeOffset KitchenMotionLastSeenAt { get; set; }
    }

    public static class Locks
    {
        public static DateTimeOffset FrontDoorToLockAt { get; set; } = DateTimeOffset.MinValue;
        public static DateTimeOffset BackDoorToLockAt { get; set; } = DateTimeOffset.MinValue;
        
        public static DateTimeOffset DoorLastNotifiedOfProblemAt { get; set; } = DateTimeOffset.MinValue;
    }
}