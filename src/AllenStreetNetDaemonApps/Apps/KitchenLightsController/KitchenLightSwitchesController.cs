using AllenStreetNetDaemonApps.Internal.Interfaces;

namespace AllenStreetNetDaemonApps.Apps.KitchenLightsController;

[NetDaemonApp]
public class KitchenLightSwitchesController
{
    private readonly IKitchenLightsControl _kitchenLightsControl;
    private readonly ILogger _logger;
    
    private readonly Entities _entities;

    private readonly Entity[] _kitchenCeilingLightsEntities;

    public KitchenLightSwitchesController(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger, IKitchenLightsControl kitchenLightsControl)
    {
        _kitchenLightsControl = kitchenLightsControl;
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        ha.Events.Where(e => e.EventType == "zwave_js_value_notification").Subscribe(async (e) => await HandleKitchenSwitchButtons(e));
        
        _kitchenCeilingLightsEntities = GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.KitchenCeilingLights);
    }

    private async Task HandleKitchenSwitchButtons(Event eventToCheck)
    {
        var dataElement = eventToCheck.DataElement;

        if (dataElement is null) return;
        
        _logger.Debug("Raw JSON: {EventData}", dataElement.Value.ToString());

        var zWaveEvent = dataElement.Value.Deserialize<ZWaveDataElementValue>();

        if (zWaveEvent is null) return;

        if (!EventWasFromKitchenMainSwitch(zWaveEvent)) return;
        
        _logger.Verbose("Passed filters for coming from main kitchen switch");
        
        // If it passes filters
        await SetKitchenLightsFrom(zWaveEvent);
    }

    private bool EventWasFromKitchenMainSwitch(ZWaveDataElementValue zWaveEvent)
    {
        // Some filtering for making sure it's from the kitchen switch and the right event type

        // ReSharper disable once ReplaceWithSingleAssignment.True cause this is easier to read imho
        var passingFilters = true;

        if (zWaveEvent.Domain != "zwave_js") 
            passingFilters = false;
        
        if (zWaveEvent.DeviceId != SECRETS.KitchenMainSwitchZWaveDeviceId)              // Kitchen switch ID is b63490e5f9fb0cbd32588f893de0bdca
            passingFilters = false;
        
        if (zWaveEvent.Value != "KeyPressed")
            passingFilters = false;

        return passingFilters;
    }

    private async Task SetKitchenLightsFrom(ZWaveDataElementValue zWaveEvent)
    {
        if (zWaveEvent.CommandClassName != "Central Scene") return;
        
        _logger.Verbose("Detected as incoming central scene change");
        
        if (zWaveEvent.Label == "Scene 001") 
            await _kitchenLightsControl.SetKitchenLightsBrighter();
        
        if (zWaveEvent.Label == "Scene 002") 
            await _kitchenLightsControl.SetKitchenLightsToPurpleScene();
        
        if (zWaveEvent.Label == "Scene 003") 
            await _kitchenLightsControl.SetKitchenLightsDimmer();
        
        if (zWaveEvent.Label == "Scene 004") 
            await _kitchenLightsControl.SetKitchenLightsToEspressoMachineScene();
        
        // Event for main button BUT this fires when main button is turning lights off AND when main button turning lights on
        // if (zWaveEvent.CommandClassName == "Scene 005")
    }


}