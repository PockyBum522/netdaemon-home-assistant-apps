using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AllenStreetNetDaemonApps.EntityWrappers.Lights.Interfaces;
using AllenStreetNetDaemonApps.Models;
using AllenStreetNetDaemonApps.Utilities;
using HomeAssistantGenerated;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel.Entities;

namespace AllenStreetNetDaemonApps.EntityWrappers.Lights;

public class GuestBathLightsWrapper : IGuestBathLightsWrapper
{
    private readonly ILogger _logger;
    
    private readonly Entities _entities;

    private readonly Entity[] _guestBathCeilingLightsEntities;

    public GuestBathLightsWrapper(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger)
    {
        _logger = logger;
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        // _logger = new LoggerConfiguration()
        //     .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
        //     .MinimumLevel.Information()
        //     .WriteTo.Console()
        //     .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
        //     .CreateLogger();
        
        _logger.Debug("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);

        _guestBathCeilingLightsEntities = GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.GuestBathroomLightsGroup);
    }
    
    public bool AreAnyAboveMirrorLightsOn()
    {
        var foundOneOn = false;
        
        foreach (var light in _guestBathCeilingLightsEntities)
        {
            _logger.Debug("Walking all above mirror lights: {Name}.State: {State}", light.EntityId, light.State);
            
            if (light.State == "on")
                foundOneOn = true;
        }

        return foundOneOn;
    }
    
    public async Task TurnOffGuestBathLights()
    {
        _logger.Debug("Running {NameOfThis}", nameof(TurnOffGuestBathLights));
        
        var anyLightsAreOn = _guestBathCeilingLightsEntities.Any(l => l.State == "on");
        
        if (!anyLightsAreOn) return;
        
        _logger.Debug("Turning off GuestBath lights because there was no motion and at least one light state was on");
        
        foreach (var ceilingLight in _guestBathCeilingLightsEntities)
            ceilingLight.CallService("light.turn_off");

        // Now turn off the native group
        _entities.Light.GuestBathroomLightsGroup.TurnOff();

        await Task.Delay(1000);
        
        foreach (var light in _guestBathCeilingLightsEntities)
        {
            await Task.Delay(1000);
            
            if (!light.IsOn()) continue;
            
            light.CallService("light.turn_off");
        }
    }
    
    public async Task SetGuestBathLightsBrighter()
    {
        if (_entities.Switch.SceneControllerMainLightswitchInGuestBathroom.IsOn())
            await ModifyCeilingLightsBrightnessBy(20);
    }

    public async Task SetGuestBathLightsDimmer()
    {
        if (AreAnyAboveMirrorLightsOn())
        {
            await ModifyCeilingLightsBrightnessBy(-20);   
        }
        else
        {
            await TurnMainRelayOn(CustomColors.WarmWhite(20));
            
            _guestBathCeilingLightsEntities.CallService("turn_on", CustomColors.WarmWhite(20) );
        }
    }

    public async Task SetGuestBathLightsToWarmWhiteScene()
    {
        _entities.Switch.SceneControllerMainLightswitchInGuestBathroom0x43Button1IndicationBinary.TurnOff();
        
        _logger.Debug("Setting warm white scene");
        
        if (_entities.Switch.SceneControllerMainLightswitchInGuestBathroom.IsOff())
        {
            await TurnMainRelayOn(CustomColors.WarmWhite());
            return;
        }

        _guestBathCeilingLightsEntities.CallService("turn_on", CustomColors.WarmWhite() );
    }

    public async Task SetGuestBathLightsDimRed()
    {
        _logger.Debug("Setting dime red scene");

        if (_entities.Switch.SceneControllerMainLightswitchInGuestBathroom.IsOff())
        {
            await TurnMainRelayOn(CustomColors.RedDim());
            return;
        }

        _guestBathCeilingLightsEntities.CallService("turn_on", CustomColors.RedDim() );
    }
    
    public async Task ModifyCeilingLightsBrightnessBy(int brightnessModifier)
    {
        foreach (var ceilingLight in _guestBathCeilingLightsEntities)
        {
            var lightAttributesDict = (Dictionary<string,object>?)ceilingLight.Attributes;
            
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

            await Task.Delay(50);
        }
    }

    private async Task allGuestBathLightsOnWithBrightness(int brightPercent)
    {
        for (var i = 0; i < 5; i++)
        {           
            await Task.Delay(200);

            _guestBathCeilingLightsEntities.CallService("turn_on", new { brightness_pct = brightPercent } );
        }
    }
    
    private async Task TurnMainRelayOn(object turnOnStateData)
    {
        _entities.Switch.SceneControllerMainLightswitchInGuestBathroom.TurnOn();
        
        await Task.Delay(300);

        for (var i = 0; i < 2; i++)
        {
            _guestBathCeilingLightsEntities.CallService("turn_on", turnOnStateData );
            await Task.Delay(100);
        }
        
        // Make sure it got set
        await Task.Delay(500);
        
        for (var i = 0; i < 2; i++)
        {
            _guestBathCeilingLightsEntities.CallService("turn_on", turnOnStateData );
            await Task.Delay(500);
        }
    }
}