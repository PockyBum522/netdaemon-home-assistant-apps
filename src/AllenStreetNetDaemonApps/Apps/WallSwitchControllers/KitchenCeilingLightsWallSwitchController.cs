using AllenStreetNetDaemonApps.EntityWrappers.Interfaces;

namespace AllenStreetNetDaemonApps.Apps.WallSwitchControllers;

[NetDaemonApp]
public class KitchenCeilingLightsWallSwitchController
{
    private readonly IHaContext _ha;
    private readonly IKitchenLightsWrapper _kitchenLightsWrapper;
    private readonly ILogger _logger;
    
    private readonly Entities _entities;

    private readonly Entity[] _kitchenCeilingLightsEntities;

    public KitchenCeilingLightsWallSwitchController(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger, IKitchenLightsWrapper kitchenLightsWrapper)
    {
        _ha = ha;
        _kitchenLightsWrapper = kitchenLightsWrapper;
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
        
        // Make the four buttons have the correct colors, resends every once in a blue moon just in case something interrupted power
        scheduler.RunIn(TimeSpan.FromSeconds(10), async () => await InitializeSceneControllerSwitchFourButtonLights());
        scheduler.RunEvery(TimeSpan.FromMinutes(121),  async () => await InitializeSceneControllerSwitchFourButtonLights());
    }

    private async Task InitializeSceneControllerSwitchFourButtonLights()
    {
        var buttonOneColor = "select.kitchen_main_lightswitch_led_indicator_color_button_1";
        var buttonTwoColor = "select.kitchen_main_lightswitch_led_indicator_color_button_2";
        var buttonThreeColor = "select.kitchen_main_lightswitch_led_indicator_color_button_3";
        var buttonFourColor = "select.kitchen_main_lightswitch_led_indicator_color_button_4";
        
        var buttonOneBrightness = "select.kitchen_main_lightswitch_led_indicator_brightness_button_1";
        var buttonTwoBrightness = "select.kitchen_main_lightswitch_led_indicator_brightness_button_2";
        var buttonThreeBrightness = "select.kitchen_main_lightswitch_led_indicator_brightness_button_3";
        var buttonFourBrightness = "select.kitchen_main_lightswitch_led_indicator_brightness_button_4";
        
        // White, Blue, Green, Red, Magenta, Yellow, Cyan
        // Bright (100%), Medium (60%), Low (30%)
        
        // Set button 1 color and brightness
        _ha.CallService("select", "select_option", data: new { option = "White", entity_id = buttonOneColor });
        _ha.CallService("select", "select_option", data: new { option = "Medium (60%)", entity_id = buttonOneBrightness });
        
        // Set button 2 color and brightness
        await Task.Delay(TimeSpan.FromSeconds(0.3));
        _ha.CallService("select", "select_option", data: new { option = "Magenta", entity_id = buttonTwoColor });
        _ha.CallService("select", "select_option", data: new { option = "Bright (100%)", entity_id = buttonTwoBrightness });

        // Set button 3 color and brightness
        await Task.Delay(TimeSpan.FromSeconds(0.3));
        _ha.CallService("select", "select_option", data: new { option = "White", entity_id = buttonThreeColor });
        _ha.CallService("select", "select_option", data: new { option = "Low (30%)", entity_id = buttonThreeBrightness });

        // Set button 4 color and brightness
        await Task.Delay(TimeSpan.FromSeconds(0.3));
        _ha.CallService("select", "select_option", data: new { option = "Yellow", entity_id = buttonFourColor });
        _ha.CallService("select", "select_option", data: new { option = "Bright (100%)", entity_id = buttonFourBrightness });
        
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        // Actually button 1 of the bottom 4
        _entities.Switch.KitchenMainLightswitchButton2IndicationBinary.TurnOn();
        
        // Actually button 2 of the bottom 4
        _entities.Switch.KitchenMainLightswitchButton3IndicationBinary.TurnOn();
        
        // Actually button 3 of the bottom 4
        _entities.Switch.KitchenMainLightswitchButton4IndicationBinary.TurnOn();
        
        // Actually button 4 of the bottom 4
        _entities.Switch.KitchenMainLightswitchButton5IndicationBinary.TurnOn();
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
        
        _logger.Information("zWaveEvent.DeviceId: {ZWaveEventDeviceId}", zWaveEvent.DeviceId);
        
        // Kitchen main (Right) switch ID is 803011e6d9bc48770c9a90e3fb819463
        if (zWaveEvent.DeviceId != "803011e6d9bc48770c9a90e3fb819463")              
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
        {
            await _kitchenLightsWrapper.SetKitchenLightsBrighter();
            
            // Actually button 1 of the bottom 4
            _entities.Switch.KitchenMainLightswitchButton2IndicationBinary.TurnOn();
        }

        if (zWaveEvent.Label == "Scene 002")
        {
            await _kitchenLightsWrapper.SetKitchenLightsToPurpleScene();  
            
            // Actually button 2 of the bottom 4
            _entities.Switch.KitchenMainLightswitchButton3IndicationBinary.TurnOn();
        }

        if (zWaveEvent.Label == "Scene 003")
        {
            await _kitchenLightsWrapper.SetKitchenLightsDimmer();
            
            // Actually button 3 of the bottom 4
            _entities.Switch.KitchenMainLightswitchButton4IndicationBinary.TurnOn();
        }

        if (zWaveEvent.Label == "Scene 004")
        {
            await _kitchenLightsWrapper.SetKitchenLightsToWarmWhite();
            
            // Actually button 4 of the bottom 4
            _entities.Switch.KitchenMainLightswitchButton5IndicationBinary.TurnOn();
        } 
        
        // Event for main button BUT this fires when main button is turning lights off AND when main button turning lights on
        // if (zWaveEvent.CommandClassName == "Scene 005")
    }


}