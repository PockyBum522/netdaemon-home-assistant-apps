using System.Threading.Tasks;

namespace AllenStreetNetDaemonApps.EntityWrappers.Lights.Interfaces;

public interface IMasterBathLightsWrapper
{
    Task TurnOffMasterBathLights();
    Task SetMasterBathLightsBrighter();
    Task SetMasterBathLightsDimmer();
    void SetMasterBathLightsToWarmWhiteScene();
    Task ModifyCeilingLightsBrightnessBy(int brightnessModifier);
    void SetMasterBathLightsDimRed();
    
    bool AreAnyLightsOn();
}