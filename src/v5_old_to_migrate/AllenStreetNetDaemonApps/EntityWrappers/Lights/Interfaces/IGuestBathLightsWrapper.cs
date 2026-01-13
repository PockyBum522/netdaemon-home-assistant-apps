using System.Threading.Tasks;

namespace AllenStreetNetDaemonApps.EntityWrappers.Lights.Interfaces;

public interface IGuestBathLightsWrapper
{
    Task TurnOffGuestBathLights();
    Task SetGuestBathLightsBrighter();
    Task SetGuestBathLightsDimmer();
    Task SetGuestBathLightsToWarmWhiteScene();
    Task ModifyCeilingLightsBrightnessBy(int brightnessModifier);
    Task SetGuestBathLightsDimRed();
    
    bool AreAnyAboveMirrorLightsOn();
}