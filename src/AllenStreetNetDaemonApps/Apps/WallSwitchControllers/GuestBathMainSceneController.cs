using AllenStreetNetDaemonApps.EntityWrappers;
using AllenStreetNetDaemonApps.EntityWrappers.Interfaces;

namespace AllenStreetNetDaemonApps.Apps.WallSwitchControllers;

[NetDaemonApp]
public class GuestBathMainSceneController
{
    private readonly IHaContext _ha;
    private readonly IGuestBathLightsWrapper _guestBathLightsWrapper;
    private readonly ILogger _logger;
    
    private readonly Entities _entities;

    public GuestBathMainSceneController(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger, IGuestBathLightsWrapper guestBathLightsWrapper)
    {
        _ha = ha;
        _guestBathLightsWrapper = guestBathLightsWrapper;
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        ha.Events.Where(e => e.EventType == "zwave_js_value_notification").Subscribe(async (e) => await HandleGuestBathSwitchButtons(e));

        // Make the four buttons have the correct colors, resends every once in a blue moon just in case something interrupted power
        scheduler.RunIn(TimeSpan.FromSeconds(20), InitializeSceneControllerSwitchFourButtonLights);
        scheduler.RunEvery(TimeSpan.FromMinutes(120), InitializeSceneControllerSwitchFourButtonLights);
    }

    private void InitializeSceneControllerSwitchFourButtonLights()
    {
        var buttonOneColor = "select.guest_bathroom_scene_controller_light_switch_for_lights_above_mirror_led_indicator_color_button_1";
        var buttonTwoColor = "select.guest_bathroom_scene_controller_light_switch_for_lights_above_mirror_led_indicator_color_button_2";
        var buttonThreeColor = "select.guest_bathroom_scene_controller_light_switch_for_lights_above_mirror_led_indicator_color_button_3";
        var buttonFourColor = "select.guest_bathroom_scene_controller_light_switch_for_lights_above_mirror_led_indicator_color_button_4";
        
        var buttonOneBrightness = "select.guest_bathroom_scene_controller_light_switch_for_lights_above_mirror_led_indicator_brightness_button_1";
        var buttonTwoBrightness = "select.guest_bathroom_scene_controller_light_switch_for_lights_above_mirror_led_indicator_brightness_button_2";
        var buttonThreeBrightness = "select.guest_bathroom_scene_controller_light_switch_for_lights_above_mirror_led_indicator_brightness_button_3";
        var buttonFourBrightness = "select.guest_bathroom_scene_controller_light_switch_for_lights_above_mirror_led_indicator_brightness_button_4";
        
        // Relay indicator LED:
        // _entities.Switch.GuestBathroomSceneControllerLightSwitchForLightsAboveMirror0x43Button1IndicationBinary.TurnOff();
        //var buttonRelayIdString = "select.guest_bathroom_scene_controller_light_switch_for_lights_above_mirror_led_indicator_brightness_relay";
        
        
        // White, Blue, Green, Red, Magenta, Yellow, Cyan
        // Bright (100%), Medium (60%), Low (30%)
        
        // Set button 1 color and brightness
        _ha.CallService("select", "select_option", data: new { option = "White", entity_id = buttonOneColor });
        _ha.CallService("select", "select_option", data: new { option = "Medium (60%)", entity_id = buttonOneBrightness });
        
        // Set button 2 color and brightness
        _ha.CallService("select", "select_option", data: new { option = "Yellow", entity_id = buttonTwoColor });
        _ha.CallService("select", "select_option", data: new { option = "Medium (60%)", entity_id = buttonTwoBrightness });

        // Set button 3 color and brightness
        _ha.CallService("select", "select_option", data: new { option = "White", entity_id = buttonThreeColor });
        _ha.CallService("select", "select_option", data: new { option = "Low (30%)", entity_id = buttonThreeBrightness });

        // Set button 4 color and brightness
        _ha.CallService("select", "select_option", data: new { option = "Red", entity_id = buttonFourColor });
        _ha.CallService("select", "select_option", data: new { option = "Medium (60%)", entity_id = buttonFourBrightness });
        
        // Actually button 1 of the bottom 4
        _entities.Switch.GuestBathroomSceneControllerLightSwitchForLightsAboveMirror0x44Button2IndicationBinary.TurnOn();
        
        // Actually button 2 of the bottom 4
        _entities.Switch.GuestBathroomSceneControllerLightSwitchForLightsAboveMirror0x45Button3IndicationBinary.TurnOn();
        
        // Actually button 3 of the bottom 4
        _entities.Switch.GuestBathroomSceneControllerLightSwitchForLightsAboveMirror0x46Button4IndicationBinary.TurnOn();
        
        // Actually button 4 of the bottom 4
        _entities.Switch.GuestBathroomSceneControllerLightSwitchForLightsAboveMirror0x47Button5IndicationBinary.TurnOn();
    }

    private async Task HandleGuestBathSwitchButtons(Event eventToCheck)
    {
        var dataElement = eventToCheck.DataElement;

        if (dataElement is null) return;
        
        _logger.Debug("Raw JSON: {EventData}", dataElement.Value.ToString());

        var zWaveEvent = dataElement.Value.Deserialize<ZWaveDataElementValue>();

        if (zWaveEvent is null) return;

        if (!EventWasFromGuestBathMainSwitch(zWaveEvent)) return;
        
        _logger.Verbose("Passed filters for coming from main GuestBath switch");
        
        // If it passes filters
        await SetGuestBathLightsFrom(zWaveEvent);
    }

    private bool EventWasFromGuestBathMainSwitch(ZWaveDataElementValue zWaveEvent)
    {
        // Some filtering for making sure it's from the GuestBath switch and the right event type

        // ReSharper disable once ReplaceWithSingleAssignment.True cause this is easier to read imho
        var passingFilters = true;

        if (zWaveEvent.Domain != "zwave_js") 
            passingFilters = false;
        
        if (zWaveEvent.DeviceId != "92921047c95e1cdd1f804dd324163dc6")              // GuestBath scene controller switch ID is 92921047c95e1cdd1f804dd324163dc6
            passingFilters = false;
        
        if (zWaveEvent.Value != "KeyPressed")
            passingFilters = false;

        return passingFilters;
    }

    private async Task SetGuestBathLightsFrom(ZWaveDataElementValue zWaveEvent)
    {
        if (zWaveEvent.CommandClassName != "Central Scene") return;
        
        _logger.Verbose("Detected as incoming central scene change");

        if (zWaveEvent.Label == "Scene 001")
        {
            await _guestBathLightsWrapper.SetGuestBathLightsBrighter();
            
            // Actually button 1 of the bottom 4
            _entities.Switch.GuestBathroomSceneControllerLightSwitchForLightsAboveMirror0x44Button2IndicationBinary.TurnOn();
        }

        if (zWaveEvent.Label == "Scene 002")
        { 
            await _guestBathLightsWrapper.SetGuestBathLightsToWarmWhiteScene();
            
            // Actually button 2 of the bottom 4
            _entities.Switch.GuestBathroomSceneControllerLightSwitchForLightsAboveMirror0x45Button3IndicationBinary.TurnOn();
        }
        
        if (zWaveEvent.Label == "Scene 003")
        {
            await _guestBathLightsWrapper.SetGuestBathLightsDimmer();
            
            // Actually button 3 of the bottom 4
            _entities.Switch.GuestBathroomSceneControllerLightSwitchForLightsAboveMirror0x46Button4IndicationBinary.TurnOn();
        }

        if (zWaveEvent.Label == "Scene 004")
        {
            await _guestBathLightsWrapper.SetGuestBathLightsDimRed();
            
            // Actually button 4 of the bottom 4
            _entities.Switch.GuestBathroomSceneControllerLightSwitchForLightsAboveMirror0x47Button5IndicationBinary.TurnOn();
        }
        
        // Event for main button BUT this fires when main button is turning lights off AND when main button turning lights on
        // if (zWaveEvent.CommandClassName == "Scene 005")
    }
}
