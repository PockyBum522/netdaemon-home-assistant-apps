using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AllenStreetNetDaemonApps.EntityWrappers.Lights.Interfaces;
using AllenStreetNetDaemonApps.Models;
using HomeAssistantGenerated;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel.Entities;

namespace AllenStreetNetDaemonApps.LightControllers.SceneChanges;

[NetDaemonApp]
public class GuestBathSceneController
{
    private readonly IHaContext _ha;
    private readonly IGuestBathLightsWrapper _guestBathLightsWrapper;
    private readonly ILogger _logger;
    
    private readonly Entities _entities;

    public GuestBathSceneController(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger, IGuestBathLightsWrapper guestBathLightsWrapper)
    {
        _ha = ha;
        _logger = logger;
        _guestBathLightsWrapper = guestBathLightsWrapper;
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        // _logger = new LoggerConfiguration()
        //     .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
        //     .MinimumLevel.Information()
        //     .WriteTo.Console()
        //     .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
        //     .CreateLogger();
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        ha.Events.Where(e => e.EventType == "zwave_js_value_notification").Subscribe(async (e) => await HandleGuestBathSwitchButtons(e));

        // Make the four buttons have the correct colors, resends every once in a blue moon just in case something interrupted power
        scheduler.RunIn(TimeSpan.FromSeconds(10), async () => await InitializeSceneControllerSwitchFourButtonLights());
        scheduler.RunEvery(TimeSpan.FromMinutes(121), async () => await InitializeSceneControllerSwitchFourButtonLights());
    }

    private async Task InitializeSceneControllerSwitchFourButtonLights()
    {
        // Delay so all scene controller inits aren't sending tons of z-wave messages at the same time
        await Task.Delay(TimeSpan.FromSeconds(10));
        
        var buttonOneColor = "select.guest_bath_main_lightswitch_scene_controller_led_indicator_color_button_1";
        var buttonTwoColor = "select.guest_bath_main_lightswitch_scene_controller_led_indicator_color_button_2";
        var buttonThreeColor = "select.guest_bath_main_lightswitch_scene_controller_led_indicator_color_button_3";
        var buttonFourColor = "select.guest_bath_main_lightswitch_scene_controller_led_indicator_color_button_4";
        
        var buttonOneBrightness = "select.guest_bath_main_lightswitch_scene_controller_led_indicator_brightness_button_1";
        var buttonTwoBrightness = "select.guest_bath_main_lightswitch_scene_controller_led_indicator_brightness_button_2";
        var buttonThreeBrightness = "select.guest_bath_main_lightswitch_scene_controller_led_indicator_brightness_button_3";
        var buttonFourBrightness = "select.guest_bath_main_lightswitch_scene_controller_led_indicator_brightness_button_4";
        
        var buttonOneIndicatorBehavior = "select.guest_bath_main_lightswitch_scene_controller_led_indicator_button_1";
        var buttonTwoIndicatorBehavior = "select.guest_bath_main_lightswitch_scene_controller_led_indicator_button_2";
        var buttonThreeIndicatorBehavior = "select.guest_bath_main_lightswitch_scene_controller_led_indicator_button_3";
        var buttonFourIndicatorBehavior = "select.guest_bath_main_lightswitch_scene_controller_led_indicator_button_4";
        
        // White, Blue, Green, Red, Magenta, Yellow, Cyan
        // Bright (100%), Medium (60%), Low (30%)
        
        if (_entities.Switch.SceneControllerMainLightswitchInGuestBathroom.IsOff())
        {
            _entities.Switch.SceneControllerMainLightswitchInGuestBathroom.TurnOn();
            
            await Task.Delay(TimeSpan.FromSeconds(3));

            await _guestBathLightsWrapper.TurnOffGuestBathLights();
        }
        
        // Disable local relay control on startup
        _ha.CallService("select", "select_option", data: new { option = "Local control disabled", entity_id = "select.guest_bath_main_lightswitch_scene_controller_relay_control" });
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        
        // Set button 1 color and brightness
        _ha.CallService("select", "select_option", data: new { option = "Red", entity_id = buttonOneColor });
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
        _ha.CallService("select", "select_option", data: new { option = "Yellow", entity_id = buttonThreeColor });
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
        _entities.Switch.SceneControllerMainLightswitchInGuestBathroom0x43Button1IndicationBinary.TurnOn();
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        
        // Actually button 2 of the bottom 4
        _entities.Switch.SceneControllerMainLightswitchInGuestBathroom0x45Button3IndicationBinary.TurnOn();
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        
        // Actually button 3 of the bottom 4
        _entities.Switch.SceneControllerMainLightswitchInGuestBathroom0x46Button4IndicationBinary.TurnOn();
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        
        // Actually button 4 of the bottom 4
        _entities.Switch.SceneControllerMainLightswitchInGuestBathroom0x47Button5IndicationBinary.TurnOn();
    }

    private async Task HandleGuestBathSwitchButtons(Event eventToCheck)
    {
        var dataElement = eventToCheck.DataElement;

        if (dataElement is null) return;
        
        //_logger.Debug("Raw JSON: {EventData}", dataElement.Value.ToString());

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
        
        if (zWaveEvent.DeviceId != "372b93b10e93ac6b91b297ee09918cb9")              // GuestBath scene controller switch ID is 372b93b10e93ac6b91b297ee09918cb9
            passingFilters = false;
        
        if (zWaveEvent.Value != "KeyPressed")
            passingFilters = false;

        return passingFilters;
    }

    private async Task SetGuestBathLightsFrom(ZWaveDataElementValue zWaveEvent)
    {
        if (zWaveEvent.CommandClassName != "Central Scene") return;
        
        //_logger.Verbose("Detected: as incoming central scene change");

        switch (zWaveEvent.Label)
        {
            // Big relay button
            case "Scene 005":
                if (_guestBathLightsWrapper.AreAnyAboveMirrorLightsOn())
                {
                    _entities.Switch.SceneControllerMainLightswitchInGuestBathroom0x43Button1IndicationBinary.TurnOn();
                    await _guestBathLightsWrapper.TurnOffGuestBathLights();
                }
                else
                {
                    await _guestBathLightsWrapper.SetGuestBathLightsToWarmWhiteScene();
                }
                break;

            case "Scene 001":
                await _guestBathLightsWrapper.SetGuestBathLightsDimRed();
                break;

            case "Scene 002":
                await _guestBathLightsWrapper.SetGuestBathLightsBrighter();
                break;
            
            case "Scene 003":
                await _guestBathLightsWrapper.SetGuestBathLightsToWarmWhiteScene();
                break;
            
            case "Scene 004":
                await _guestBathLightsWrapper.SetGuestBathLightsDimmer();
                break;
        }
    }
}
