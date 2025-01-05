using AllenStreetNetDaemonApps.Apps.FrontDoorCameraMotion;

namespace AllenStreetNetDaemonApps.Apps.Initializers;

[NetDaemonApp]
public class ThermostatInitializer
{
    private readonly IHaContext _ha;
    private Serilog.ILogger _logger;
    private Entities _entities;
    
    public ThermostatInitializer(IHaContext ha, INetDaemonScheduler scheduler)
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
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        scheduler.RunIn(TimeSpan.FromSeconds(MqttWatcher.MqttSetupDelaySeconds + 10), SetThermostatOnceTemperatureFetched);
    }

    private void SetThermostatOnceTemperatureFetched()
    {
        var thermostatWrapper = new ThermostatWrapper(_logger, _ha);
            
        thermostatWrapper.RestoreSavedModeToThermostat();
    }
}