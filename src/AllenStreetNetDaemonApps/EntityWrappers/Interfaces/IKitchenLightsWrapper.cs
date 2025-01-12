namespace AllenStreetNetDaemonApps.EntityWrappers.Interfaces;

public interface IKitchenLightsWrapper
{
    void TurnOnKitchenLightsFromMotion();
    void TurnOffKitchenLightsFromMotion();

    public Task SetKitchenLightsBrighter();
    public Task SetKitchenLightsDimmer();

    public Task SetKitchenLightsToPurpleScene();

    public Task ModifyCeilingLightsBrightnessBy(int brightnessModifier);

    public Task SetKitchenLightsToWarmWhite();
    public Task SetKitchenLightsToEspressoMachineScene();
}