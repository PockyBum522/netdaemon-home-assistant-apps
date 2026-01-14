using System.Threading.Tasks;

namespace AllenStreetNetDaemonApps.EntityWrappers.Lights.Interfaces;

public interface IGuestBathLightsWrapper
{
    Task SetGuestBathLightsBrighter(IHaContext ha);
    Task SetGuestBathLightsDimmer(IHaContext ha);
    Task SetGuestBathLightsToWarmWhiteScene(IHaContext ha);
    Task ModifyCeilingLightsBrightnessBy(IHaContext ha, int brightnessModifier);
    Task SetGuestBathLightsDimRed(IHaContext ha);
    
    bool AreAnyAboveMirrorLightsOn(IHaContext ha);
}