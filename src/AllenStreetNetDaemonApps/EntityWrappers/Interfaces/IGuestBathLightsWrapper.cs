namespace AllenStreetNetDaemonApps.EntityWrappers;

public interface IGuestBathLightsWrapper
{
    Task TurnOffGuestBathLights();
    Task SetGuestBathLightsBrighter();
    Task SetGuestBathLightsDimmer();
    Task SetGuestBathLightsToWarmWhiteScene();
    Task ModifyCeilingLightsBrightnessBy(int brightnessModifier);
    Task SetGuestBathLightsDimRed();
}