namespace AllenStreetNetDaemonApps.Apps;

public static class SharedState
{
    public static class MotionSensors
    {
        public static DateTimeOffset LastMotionInKitchenAt { get; set; } = DateTime.MinValue;
        public static DateTimeOffset LastMotionInFrontRoomAt { get; set; } = DateTime.MinValue;
        public static DateTimeOffset LastMotionInGuestBathAt { get; set; } = DateTime.MinValue;
    }
}