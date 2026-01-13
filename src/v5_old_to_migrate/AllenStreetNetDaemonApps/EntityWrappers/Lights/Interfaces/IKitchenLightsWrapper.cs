using System.Threading.Tasks;

namespace AllenStreetNetDaemonApps.EntityWrappers.Lights.Interfaces;

public interface IKitchenLightsWrapper
{
    bool AreAnyCeilingLightsOn();
    bool IsEspressoBulbOn();
    
    void TurnOnKitchenLightsFromMotion();
    void TurnOffKitchenLightsFromMotion();

    public Task SetKitchenLightsBrighter();
    public Task SetKitchenLightsDimmer();

    public Task SetKitchenLightsToPurpleScene();
    
    public Task SetKitchenCeilingLightsOff();

    public Task ModifyCeilingLightsBrightnessBy(int brightnessModifier);

    public Task SetKitchenLightsToWarmWhite();
    public Task SetKitchenLightsToEspressoMachineScene();
}