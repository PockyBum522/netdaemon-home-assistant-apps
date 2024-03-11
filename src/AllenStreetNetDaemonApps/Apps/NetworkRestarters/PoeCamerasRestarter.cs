using Renci.SshNet;
using Renci.SshNet.Common;

namespace AllenStreetNetDaemonApps.Apps.NetworkRestarters;

[NetDaemonApp]
public class PoeCamerasRestarter
{
    private readonly IHaContext _ha;
    private readonly ILogger _logger;
    private readonly Entities _entities;
    
    private readonly InputBooleanEntity _camerasRestartToggle;

    private bool _camerasAreRestarting;
    
    public PoeCamerasRestarter(IHaContext ha, INetDaemonScheduler scheduler)
    {
        _ha = ha;

        _entities = new Entities(_ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();

        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        _camerasRestartToggle = _entities.InputBoolean.RestartPoeCameras;
        
        // ReSharper disable once AsyncVoidLambda because I'm fairly sure it is useless
        scheduler.RunEvery(TimeSpan.FromSeconds(2), async () => await CheckAllToggles());
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
    }
    
    private async Task CheckAllToggles()
    {
        await Task.Delay(500);
        
        if (_camerasRestartToggle.State != "on")
        {
            _camerasAreRestarting = false;
            return;
        }
        
        await Task.Delay(500);

        if (_camerasAreRestarting == false)
        {
            _camerasAreRestarting = true;
            
            await Task.Delay(TimeSpan.FromSeconds(5));
            
            // Second check after 5s to make sure they really meant to
            if (_camerasRestartToggle.State != "on") return;
        
            // Leave this for lazy threading block
            await Task.Delay(TimeSpan.FromSeconds(5));

            await TurnOffShopNetworkEquipment();
            await TurnOffLaundryRoomNetworkEquipment();
            await TurnOffNetworkClosetCamsPoeSwitch();
            
            await Task.Delay(TimeSpan.FromSeconds(20));
            
            await TurnOnShopNetworkEquipment();
            await TurnOnLaundryRoomNetworkEquipment();
            await TurnOnNetworkClosetCamsPoeSwitch();
            
            // After we've finished, reset dashboard controls and lazy thread safety bool:
            _camerasRestartToggle.TurnOff();
            _camerasAreRestarting = false;

            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
    
    private async Task TurnOffShopNetworkEquipment()
    {
        var entityToWork = _entities.Switch.NetworkPowerShop;
            
        _logger.Information("_entities.Switch.NetworkPowerShop State: {State}", entityToWork.State);

        _logger.Information("Turning off: _entities.Switch.NetworkPowerShop");
            
        entityToWork.TurnOff();   
            
        await Task.Delay(TimeSpan.FromSeconds(5));
            
        _logger.Information("_entities.Switch.NetworkPowerShop State: {State}", entityToWork.State);
    }
    
    private async Task TurnOnShopNetworkEquipment()
    {
        var entityToWork = _entities.Switch.NetworkPowerShop;
            
        _logger.Information("_entities.Switch.NetworkPowerShop State: {State}", entityToWork.State);

        _logger.Information("Turning on: _entities.Switch.NetworkPowerShop");
            
        entityToWork.TurnOn();   
            
        await Task.Delay(TimeSpan.FromSeconds(5));
            
        _logger.Information("_entities.Switch.NetworkPowerShop State: {State}", entityToWork.State);
    }
    
    private async Task TurnOffLaundryRoomNetworkEquipment()
    {
        var entityToWork = _entities.Light.NetworkPowerLaundryRoomEquipment;

        _logger.Information("_entities.Light.NetworkPowerLaundryRoomEquipment State: {State}", entityToWork.State);

        _logger.Information("Turning off: _entities.Light.NetworkPowerLaundryRoomEquipment");
            
        entityToWork.TurnOff();      // Accidentally got dimmers so it's a light not a switch, still works.
        
        await Task.Delay(TimeSpan.FromSeconds(5));
        
        _logger.Information("_entities.Light.NetworkPowerLaundryRoomEquipment State: {State}", entityToWork.State);
    }
    
    private async Task TurnOffNetworkClosetCamsPoeSwitch()
    {
        var entityToWork = _entities.Switch.NetworkPowerNetworkClosetPoeCameras4PortSwitch;
        
        _logger.Information("_entities.Switch.NetworkPowerNetworkClosetPoeCameras4PortSwitch2 State: {State}", entityToWork.State);

        _logger.Information("Turning off: _entities.Switch.NetworkPowerNetworkClosetPoeCameras4PortSwitch2");
            
        entityToWork.TurnOff();

        await Task.Delay(TimeSpan.FromSeconds(5));

        _logger.Information("_entities.Switch.NetworkPowerNetworkClosetPoeCameras4PortSwitch2 State: {State}", entityToWork.State);
    }
    
    private async Task TurnOnLaundryRoomNetworkEquipment()
    {
        var entityToWork = _entities.Light.NetworkPowerLaundryRoomEquipment;

        _logger.Information("_entities.Light.NetworkPowerLaundryRoomEquipment State: {State}", entityToWork.State);

        _logger.Information("Turning on: _entities.Light.NetworkPowerLaundryRoomEquipment");
            
        entityToWork.TurnOn();      // Accidentally got dimmers so it's a light not a switch, still works.

        await Task.Delay(TimeSpan.FromSeconds(5));

        _logger.Information("_entities.Light.NetworkPowerLaundryRoomEquipment State: {State}", entityToWork.State);
    }
    
    private async Task TurnOnNetworkClosetCamsPoeSwitch()
    {
        var entityToWork = _entities.Switch.NetworkPowerNetworkClosetPoeCameras4PortSwitch;
        
        _logger.Information("_entities.Switch.NetworkPowerNetworkClosetPoeCameras4PortSwitch2 State: {State}", entityToWork.State);

        _logger.Information("Turning on: _entities.Switch.NetworkPowerNetworkClosetPoeCameras4PortSwitch2");
            
        entityToWork.TurnOn();

        await Task.Delay(TimeSpan.FromSeconds(5));

        _logger.Information("_entities.Switch.NetworkPowerNetworkClosetPoeCameras4PortSwitch2 State: {State}", entityToWork.State);
    }
}
