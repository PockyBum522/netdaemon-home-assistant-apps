namespace AllenStreetNetDaemonApps.Internal.Interfaces;

public interface IKitchenLightsControl
{
    void TurnOnKitchenLightsFromMotion();
    void TurnOffKitchenLightsFromMotion();

    public Task SetKitchenLightsBrighter();
    public Task SetKitchenLightsDimmer();

    public Task SetKitchenLightsToPurpleScene();

    public Task ModifyCeilingLightsBrightnessBy(int brightnessModifier);

    public Task SetKitchenLightsToEspressoMachineScene();
}