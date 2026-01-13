namespace AllenStreetNetDaemonApps;

public static class SharedState
{
    public static class MotionSensors
    {
        public static DateTimeOffset LastMotionInKitchenAt { get; set; } = DateTime.MinValue;
        public static DateTimeOffset LastMotionInFrontRoomAt { get; set; } = DateTime.MinValue;
        public static DateTimeOffset LastMotionInGuestBathAt { get; set; } = DateTime.MinValue;
        public static DateTimeOffset LastMotionInMasterBathAt { get; set; } = DateTime.MinValue;
        public static DateTimeOffset LastMotionAtBackDoorAt { get; set; } = DateTime.MinValue;
    }

    public static class Timeouts
    {
        public static DateTimeOffset ExhaustFanInGuestBathTurnedOnAt { get; set; } = DateTime.MinValue;
    }
}