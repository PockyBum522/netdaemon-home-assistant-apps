using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AllenStreetNetDaemonApps.EntityWrappers.Lights.Interfaces;
using AllenStreetNetDaemonApps.Models;
using AllenStreetNetDaemonApps.Utilities;
using HomeAssistantGenerated;
using NetDaemon.HassModel.Entities;

namespace AllenStreetNetDaemonApps.EntityWrappers.Lights;

public class GuestBathLightsWrapper : IGuestBathLightsWrapper
{
    private readonly ILogger<GuestBathLightsWrapper> _logger;
    
    private Entities? _entities;

    private Entity[]? _guestBathCeilingLightsEntities;

    public GuestBathLightsWrapper(ILogger<GuestBathLightsWrapper> logger)
    {
        _logger = logger;
        
        var namespaceBuiltString = TrimmedNamespaceBuilder.GetTrimmedName(this);
        
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Initializing {NamespaceBuildString} v0.02", namespaceBuiltString);

        
    }
    
    public bool AreAnyAboveMirrorLightsOn(IHaContext ha)
    {
        _entities ??= new Entities(ha);
        _guestBathCeilingLightsEntities ??= GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.GuestBathroomLightsGroup);
        
        var foundOneOn = false;
        
        foreach (var light in _guestBathCeilingLightsEntities)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Walking all above mirror lights: {Name}.State: {State}", light.EntityId, light.State);
            
            if (light.State == "on")
                foundOneOn = true;
        }

        return foundOneOn;
    }
    
    public async Task TurnOffGuestBathLights(IHaContext ha)
    {
        _entities ??= new Entities(ha);
        _guestBathCeilingLightsEntities ??= GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.GuestBathroomLightsGroup);
        
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Running {NameOfThis}", nameof(TurnOffGuestBathLights));
        
        var anyLightsAreOn = _guestBathCeilingLightsEntities.Any(l => l.State == "on");
        
        if (!anyLightsAreOn) return;
        
        _logger.LogDebug("Turning off GuestBath lights because there was no motion and at least one light state was on");
        
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
    
    public async Task SetGuestBathLightsBrighter(IHaContext ha)
    {
        _entities ??= new Entities(ha);
        _guestBathCeilingLightsEntities ??= GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.GuestBathroomLightsGroup);
        
        if (_entities.Switch.SceneControllerMainLightswitchInGuestBathroom.IsOn())
            await ModifyCeilingLightsBrightnessBy(ha, 20);
    }

    public async Task SetGuestBathLightsDimmer(IHaContext ha)
    {
        _entities ??= new Entities(ha);
        _guestBathCeilingLightsEntities ??= GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.GuestBathroomLightsGroup);
        
        if (AreAnyAboveMirrorLightsOn(ha))
        {
            await ModifyCeilingLightsBrightnessBy(ha, -20);   
        }
        else
        {
            await TurnMainRelayOn(ha, CustomColors.WarmWhite(20));
            
            _guestBathCeilingLightsEntities.CallService("turn_on", CustomColors.WarmWhite(20) );
        }
    }

    public async Task SetGuestBathLightsToWarmWhiteScene(IHaContext ha)
    {
        _entities ??= new Entities(ha);
        _guestBathCeilingLightsEntities ??= GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.GuestBathroomLightsGroup);
        
        _entities.Switch.SceneControllerMainLightswitchInGuestBathroom0x43Button1IndicationBinary.TurnOff();
        
        _logger.LogDebug("Setting warm white scene");
        
        if (_entities.Switch.SceneControllerMainLightswitchInGuestBathroom.IsOff())
        {
            await TurnMainRelayOn(ha, CustomColors.WarmWhite());
            return;
        }

        _guestBathCeilingLightsEntities.CallService("turn_on", CustomColors.WarmWhite() );
    }

    public async Task SetGuestBathLightsDimRed(IHaContext ha)
    {
        _entities ??= new Entities(ha);
        _guestBathCeilingLightsEntities ??= GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.GuestBathroomLightsGroup);
        
        _logger.LogDebug("Setting dim red scene");

        if (_entities.Switch.SceneControllerMainLightswitchInGuestBathroom.IsOff())
        {
            await TurnMainRelayOn(ha, CustomColors.RedDim());
            return;
        }

        _guestBathCeilingLightsEntities.CallService("turn_on", CustomColors.RedDim() );
    }
    
    public async Task ModifyCeilingLightsBrightnessBy(IHaContext ha, int brightnessModifier)
    {
        _entities ??= new Entities(ha);
        _guestBathCeilingLightsEntities ??= GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.GuestBathroomLightsGroup);
        
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
            
            _logger.LogInformation("Current brightness: {Bright} and new brightness will be: {NewBright}", currentLightBrightness, newLightBrightness);
            
            ceilingLight.CallService("turn_on", new { brightness_pct = newLightBrightness } );

            await Task.Delay(50);
        }
    }

    private async Task allGuestBathLightsOnWithBrightness(IHaContext ha, int brightPercent)
    {
        _entities ??= new Entities(ha);
        _guestBathCeilingLightsEntities ??= GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.GuestBathroomLightsGroup);
        
        for (var i = 0; i < 5; i++)
        {           
            await Task.Delay(200);

            _guestBathCeilingLightsEntities.CallService("turn_on", new { brightness_pct = brightPercent } );
        }
    }
    
    private async Task TurnMainRelayOn(IHaContext ha, object turnOnStateData)
    {
        _entities ??= new Entities(ha);
        _guestBathCeilingLightsEntities ??= GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.GuestBathroomLightsGroup);
        
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