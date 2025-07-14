using System.Diagnostics;
using System.Linq;
using AllenStreetNetDaemonApps.EntityWrappers.Lights.Interfaces;
using AllenStreetNetDaemonApps.Models;
using AllenStreetNetDaemonApps.Utilities.NotificationUtilities;
using HomeAssistantGenerated;
using NetDaemon.Extensions.Scheduler;
using Newtonsoft.Json;

namespace AllenStreetNetDaemonApps.LightControllers.EspressoLightOnPowerUsage;

[NetDaemonApp]
public class EspressoLightOnPowerUsage
{
    private readonly IKitchenLightsWrapper _kitchenLightsWrapper;
    private readonly ILogger _logger;
    private readonly Entities _entities;
    private DateTimeOffset _bulbLastOnAt = DateTimeOffset.MinValue;
    
    public EspressoLightOnPowerUsage(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger, IKitchenLightsWrapper kitchenLightsWrapper)
    {
        _logger = logger;
        _kitchenLightsWrapper = kitchenLightsWrapper;
        
        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        // _logger = new LoggerConfiguration()
        //     .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
        //     .MinimumLevel.Information()
        //     .WriteTo.Console()
        //     .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
        //     .CreateLogger();
        
        _entities = new Entities(ha);
        
        _logger.Debug("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        //ha.Events.Where(e => e.EventType == "state_changed").Subscribe(HandleKitchenMotion);
        
        scheduler.RunEvery(TimeSpan.FromSeconds(3), checkCurrentEspressoPowerUsage); 
    }
    
    // private void checkCurrentWasherPowerUsage()
    private void checkCurrentEspressoPowerUsage()
    {
        var espressoCurrentWattsRaw = _entities.Sensor.EspressoMachineMeteringPlugElectricConsumptionW.State;
        
        if (espressoValueInvalid(espressoCurrentWattsRaw)) return;
        
        if (espressoCurrentWattsRaw is null) return;
        var espressoWatts = (double)espressoCurrentWattsRaw;
        
        handleEspressoStateUpdate(espressoWatts);
    }

    private void handleEspressoStateUpdate(double espressoWatts)
    {
        _logger.Information("Checking bulbLastOnAt: {LastOnAt} vs DateTimeOffset.Now - TimeSpan.FromMinutes(5): {FiveMinutesAgo}", _bulbLastOnAt, DateTimeOffset.Now - TimeSpan.FromMinutes(5));
        if (_bulbLastOnAt >  DateTimeOffset.Now - TimeSpan.FromMinutes(5)) return;
        
        _logger.Information("bulbLastOnAt was more than five minutes ago, so Checking espresso watts: {CurrentWatts}", espressoWatts);
        if (espressoWatts < 50) return;

        var isEspressoBulbOn = _kitchenLightsWrapper.IsEspressoBulbOn();
        _logger.Information("Was over threshold, checking if espresso bulb is on: {IsBulbOn}", isEspressoBulbOn);
        if (isEspressoBulbOn) return;

        _logger.Information("Bulb was not on, setting espresso scene now");
        
        _kitchenLightsWrapper.SetKitchenLightsToEspressoMachineScene();
    }

    private bool espressoValueInvalid(double? washerCurrentWattsRaw)
    {
        // if (_washerErrorCount > 10)
        // {
        //     _washerErrorCount = 0;
        //     _logger.Fatal("Washer returned bad info more than 10 times in a row");
        // }

        if (washerCurrentWattsRaw is null or < -1)
        {
            _logger.Error("Washer power usage is null or less than -1, skipping");
            
            //_washerErrorCount++;
            
            return true;
        }

        return false;
    }
}