using System;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HomeAssistantGenerated;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using NetdaemonApps.Models;
using NetdaemonApps.Utilities;
using Serilog;

namespace NetdaemonApps.apps.KitchenLightsController;

[NetDaemonApp]
public class KitchenLightsController
{
    private readonly ILogger _logger;
    
    private readonly Entities _entities;

    private readonly Entity[] _kitchenCeilingLightsEntities;
    
    public KitchenLightsController(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger)
    {
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Verbose()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        ha.Events.Where(e => e.EventType == "zwave_js_value_notification").Subscribe(async (e) => await HandleKitchenSwitchButtons(e));

        _kitchenCeilingLightsEntities = GroupUtilities.GetEntitiesFromGroup(ha, _entities.Group.GroupKitchenCeilingLights);

        //scheduler.RunIn(TimeSpan.FromSeconds(0.5), async () => await SetKitchenLightsDimmer());
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
            await SetKitchenLightsBrighter();
        
        if (zWaveEvent.Label == "Scene 002") 
            await SetKitchenLightsToPurpleScene();
        
        if (zWaveEvent.Label == "Scene 003") 
            await SetKitchenLightsDimmer();
        
        if (zWaveEvent.Label == "Scene 004") 
            await SetKitchenLightsToEspressoMachineScene();
        
        // Event for main button BUT this fires when main button is turning lights off AND when main button turning lights on
        // if (zWaveEvent.CommandClassName == "Scene 005")
    }

    private async Task SetKitchenLightsBrighter()
    {
        if (_entities.Switch.KitchenMainLightswitch.IsOn())
            await ModifyCeilingLightsBrightnessBy(20);
        
        if (_entities.Switch.KitchenMainLightswitch.IsOff())
            await TurnMainRelayOn(CustomColors.WarmWhite());
    }

    private async Task SetKitchenLightsDimmer()
    {
        if (_entities.Switch.KitchenMainLightswitch.IsOn())
            await ModifyCeilingLightsBrightnessBy(-20);
        
        if (_entities.Switch.KitchenMainLightswitch.IsOff())
            await TurnMainRelayOn(CustomColors.WarmWhite(20));
    }

    private async Task SetKitchenLightsToPurpleScene()
    {
        _logger.Debug("Setting purple scene");

        // Handle when kitchen main relay was off, turning on and try to not blind people with defaults
        if (_entities.Switch.KitchenMainLightswitch.IsOff())
            await TurnMainRelayOn(CustomColors.AlyssaPurple());

        // Then set the right colors, whether main relay was on or not
        _kitchenCeilingLightsEntities.CallService("turn_on", CustomColors.AlyssaPurple() );
    }

    private Task ModifyCeilingLightsBrightnessBy(int brightnessModifier)
    {
        foreach (var ceilingLight in _kitchenCeilingLightsEntities)
        {
            var lightAttributesDict = (System.Collections.Generic.Dictionary<string,object>?)ceilingLight.Attributes;
            
            if (lightAttributesDict is null)
                throw new Exception("lightAttributesDict is null");

            var currentLightBrightness = decimal.Parse(lightAttributesDict["brightness"].ToString() ?? "0");

            var currentLightBrightnessPercent = currentLightBrightness.Map(0, 255, 0, 100); 
            
            var newLightBrightness = (int)currentLightBrightnessPercent + brightnessModifier;

            if (newLightBrightness > 100)
                newLightBrightness = 100;
            
            if (newLightBrightness < 0)
                newLightBrightness = 0;
            
            _logger.Information("Current brightness: {Bright} and new brightness will be: {NewBright}", currentLightBrightness, newLightBrightness);
            
            ceilingLight.CallService("turn_on", new { brightness_pct = newLightBrightness } );

            // await Task.Delay(100);
        }

        return Task.CompletedTask;
    }

    private async Task SetKitchenLightsToEspressoMachineScene()
    {
        // Handle when kitchen main relay was off, turning on and try to not blind people with defaults
        if (_entities.Switch.KitchenMainLightswitch.IsOff())
            await TurnMainRelayOn(CustomColors.WarmWhite(20));
        
        // Then set the right colors, whether main relay was on or not
        await TurnMainRelayOn(CustomColors.WarmWhite(20));
    }


    private async Task TurnMainRelayOn(object turnOnStateData)
    {
        _entities.Switch.KitchenMainLightswitch.TurnOn();

        for (var i = 0; i < 5; i++)
        {
            _kitchenCeilingLightsEntities.CallService("turn_on", turnOnStateData );

            await Task.Delay(500);
        }
    }
}