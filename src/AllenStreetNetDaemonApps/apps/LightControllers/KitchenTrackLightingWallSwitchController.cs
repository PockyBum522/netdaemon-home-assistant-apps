using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AllenStreetNetDaemonApps.EntityWrappers.Interfaces;
using AllenStreetNetDaemonApps.Models;
using HomeAssistantGenerated;
using NetDaemon.Extensions.Scheduler;

namespace AllenStreetNetDaemonApps.LightControllers;

[NetDaemonApp]
public class KitchenTrackLightingWallSwitchController
{
    private readonly IHaContext _ha;
    private readonly IKitchenLightsWrapper _kitchenLightsWrapper;
    private readonly ILogger _logger;
    
    private readonly Entities _entities;

    // private readonly Entity[] _kitchenCeilingLightsEntities;

    public KitchenTrackLightingWallSwitchController(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger, IKitchenLightsWrapper kitchenLightsWrapper)
    {
        _ha = ha;
        _logger = logger;
        _kitchenLightsWrapper = kitchenLightsWrapper;
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        // _logger = new LoggerConfiguration()
        //     .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
        //     .MinimumLevel.Information()
        //     .WriteTo.Console()
        //     .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
        //     .CreateLogger();
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        ha.Events.Where(e => e.EventType == "zwave_js_value_notification").Subscribe(async (e) => await HandleKitchenSwitchButtons(e));
        
        //_kitchenCeilingLightsEntities = GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.KitchenCeilingLights);
        
        // Make the four buttons have the correct colors, resends every once in a blue moon just in case something interrupted power
        scheduler.RunIn(TimeSpan.FromSeconds(10), async () => await InitializeSceneControllerSwitchFourButtonLights());
        scheduler.RunEvery(TimeSpan.FromMinutes(122),  async () => await InitializeSceneControllerSwitchFourButtonLights());
    }

    private async Task InitializeSceneControllerSwitchFourButtonLights()
    {
        // Delay so all scene controller inits aren't sending tons of z-wave messages at the same time
        await Task.Delay(TimeSpan.FromSeconds(20));
        
        var buttonOneColor = "select.kitchen_main_left_track_lighting_lightswitch_scene_controller_led_indicator_color_button_1";
        var buttonTwoColor = "select.kitchen_main_left_track_lighting_lightswitch_scene_controller_led_indicator_color_button_2";
        var buttonThreeColor = "select.kitchen_main_left_track_lighting_lightswitch_scene_controller_led_indicator_color_button_3";
        var buttonFourColor = "select.kitchen_main_left_track_lighting_lightswitch_scene_controller_led_indicator_color_button_4";
        
        var buttonOneBrightness = "select.kitchen_main_left_track_lighting_lightswitch_scene_controller_led_indicator_brightness_button_1";
        var buttonTwoBrightness = "select.kitchen_main_left_track_lighting_lightswitch_scene_controller_led_indicator_brightness_button_2";
        var buttonThreeBrightness = "select.kitchen_main_left_track_lighting_lightswitch_scene_controller_led_indicator_brightness_button_3";
        var buttonFourBrightness = "select.kitchen_main_left_track_lighting_lightswitch_scene_controller_led_indicator_brightness_button_4";
        
        var buttonOneIndicatorBehavior = "select.kitchen_main_left_track_lighting_lightswitch_scene_controller_led_indicator_button_1";
        var buttonTwoIndicatorBehavior = "select.kitchen_main_left_track_lighting_lightswitch_scene_controller_led_indicator_button_2";
        var buttonThreeIndicatorBehavior = "select.kitchen_main_left_track_lighting_lightswitch_scene_controller_led_indicator_button_3";
        var buttonFourIndicatorBehavior = "select.kitchen_main_left_track_lighting_lightswitch_scene_controller_led_indicator_button_4";
        
        // White, Blue, Green, Red, Magenta, Yellow, Cyan
        // Bright (100%), Medium (60%), Low (30%)
        
        // Set button 1 color and brightness
        _ha.CallService("select", "select_option", data: new { option = "Yellow", entity_id = buttonOneColor });
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        _ha.CallService("select", "select_option", data: new { option = "Medium (60%)", entity_id = buttonOneBrightness });
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        _ha.CallService("select", "select_option", data: new { option = "Always on", entity_id = buttonOneIndicatorBehavior });
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        
        // Set button 2 color and brightness
        _ha.CallService("select", "select_option", data: new { option = "White", entity_id = buttonTwoColor });
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        _ha.CallService("select", "select_option", data: new { option = "Bright (100%)", entity_id = buttonTwoBrightness });
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        _ha.CallService("select", "select_option", data: new { option = "Always on", entity_id = buttonTwoIndicatorBehavior });
        await Task.Delay(TimeSpan.FromSeconds(0.5));

        // Set button 3 color and brightness
        _ha.CallService("select", "select_option", data: new { option = "Green", entity_id = buttonThreeColor });
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        _ha.CallService("select", "select_option", data: new { option = "Medium (60%)", entity_id = buttonThreeBrightness });
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        _ha.CallService("select", "select_option", data: new { option = "Always on", entity_id = buttonThreeIndicatorBehavior });
        await Task.Delay(TimeSpan.FromSeconds(0.5));

        // Set button 4 color and brightness
        _ha.CallService("select", "select_option", data: new { option = "White", entity_id = buttonFourColor });
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        _ha.CallService("select", "select_option", data: new { option = "Low (30%)", entity_id = buttonFourBrightness });
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        _ha.CallService("select", "select_option", data: new { option = "Always on", entity_id = buttonFourIndicatorBehavior });
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        
        // Actually button 1 of the bottom 4
        _entities.Switch.SceneControllerKitchenMainLightswitchLeftSide0x43Button1IndicationBinary.TurnOn();
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        
        // Actually button 2 of the bottom 4
        _entities.Switch.SceneControllerKitchenMainLightswitchLeftSide0x45Button3IndicationBinary.TurnOn();
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        
        // Actually button 3 of the bottom 4
        _entities.Switch.SceneControllerKitchenMainLightswitchLeftSide0x46Button4IndicationBinary.TurnOn();
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        
        // Actually button 4 of the bottom 4
        _entities.Switch.SceneControllerKitchenMainLightswitchLeftSide0x47Button5IndicationBinary.TurnOn();
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
        
        // Kitchen track lighting (Left) switch ID is 9020618a0e003b1271f9949d91919564
        if (zWaveEvent.DeviceId != "9020618a0e003b1271f9949d91919564")              
            passingFilters = false;
        
        if (zWaveEvent.Value != "KeyPressed")
            passingFilters = false;

        return passingFilters;
    }

    private async Task SetKitchenLightsFrom(ZWaveDataElementValue zWaveEvent)
    {
        if (zWaveEvent.CommandClassName != "Central Scene") return;
        
        _logger.Verbose("Detected as incoming central scene change");

        switch (zWaveEvent.Label)
        {
            case "Scene 001":
                await _kitchenLightsWrapper.SetKitchenLightsToWarmWhite();
                break;
            
            case "Scene 002":
                await _kitchenLightsWrapper.SetKitchenLightsBrighter();
                break;
            
            case "Scene 003":
                await _kitchenLightsWrapper.SetKitchenLightsToEspressoMachineScene();
                break;
            
            case "Scene 004":
                await _kitchenLightsWrapper.SetKitchenLightsDimmer();
                break;
        }

        // Event for main button BUT this fires when main button is turning lights off AND when main button turning lights on
        // if (zWaveEvent.CommandClassName == "Scene 005")
    }


}