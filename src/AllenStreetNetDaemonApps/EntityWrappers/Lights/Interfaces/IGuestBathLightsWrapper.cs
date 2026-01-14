using System.Threading.Tasks;

namespace AllenStreetNetDaemonApps.EntityWrappers.Lights.Interfaces;

public interface IGuestBathLightsWrapper
{
    void SetGuestBathLightsBrighter(IHaContext ha);
    Task SetGuestBathLightsDimmer(IHaContext ha);
    Task SetGuestBathLightsToWarmWhiteScene(IHaContext ha);
    void ModifyCeilingLightsBrightnessBy(IHaContext ha, int brightnessModifier);
    Task SetGuestBathLightsDimRed(IHaContext ha);
    
    bool AreAnyAboveMirrorLightsOn(IHaContext ha);
}