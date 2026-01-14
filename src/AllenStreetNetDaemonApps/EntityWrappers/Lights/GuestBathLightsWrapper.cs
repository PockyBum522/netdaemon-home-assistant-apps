using System.Collections.Generic;
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
    
    public void SetGuestBathLightsBrighter(IHaContext ha)
    {
        _entities ??= new Entities(ha);
        _guestBathCeilingLightsEntities ??= GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.GuestBathroomLightsGroup);
        
        if (_entities.Switch.SceneControllerMainLightswitchInGuestBathroom.IsOn())
            ModifyCeilingLightsBrightnessBy(ha, 20);
    }

    public async Task SetGuestBathLightsDimmer(IHaContext ha)
    {
        _entities ??= new Entities(ha);
        _guestBathCeilingLightsEntities ??= GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.GuestBathroomLightsGroup);
        
        if (AreAnyAboveMirrorLightsOn(ha))
        {
            ModifyCeilingLightsBrightnessBy(ha, -20);   
        }
        else
        {
            var newState = CustomColors.WarmWhite(20);
            
            await TurnMainRelayOn(ha, newState);
            
            _entities.Light.GuestBathroomLightsGroup.TurnOn(
                colorTempKelvin:newState.Temperature, 
                brightnessPct:newState.BrightnessPct);
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
        
        _entities.Light.GuestBathroomLightsGroup.TurnOn(
            colorTempKelvin:CustomColors.WarmWhite().Temperature, 
            brightnessPct:CustomColors.WarmWhite().BrightnessPct);
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
        
        _entities.Light.GuestBathroomLightsGroup.TurnOn(
            hsColor:CustomColors.RedDim().HsColor, 
            brightnessPct:CustomColors.RedDim().BrightnessPct);
        
        //_guestBathCeilingLightsEntities.CallService("turn_on", CustomColors.RedDim() );
    }
    
    public void ModifyCeilingLightsBrightnessBy(IHaContext ha, int brightnessModifier)
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
            
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Current brightness: {Bright} and new brightness will be: {NewBright}", currentLightBrightness, newLightBrightness);
          
            ceilingLight.CallService("turn_on", new { brightness_pct = newLightBrightness } );
        }
    }

    private void allGuestBathLightsOnWithBrightness(IHaContext ha, int brightPercent)
    {
        _entities ??= new Entities(ha);
        
        _entities.Light.GuestBathroomLightsGroup.TurnOn(brightnessPct:brightPercent);
    }
    
    private async Task TurnMainRelayOn(IHaContext ha, CustomColorsTemperature turnOnStateData)
    {
        _entities ??= new Entities(ha);
        
        _entities.Switch.SceneControllerMainLightswitchInGuestBathroom.TurnOn();
        
        await Task.Delay(300);
        
        _entities.Light.GuestBathroomLightsGroup.TurnOn(
            colorTempKelvin:turnOnStateData.Temperature,
            brightnessPct:turnOnStateData.BrightnessPct);
        
        // Make sure it got set
        for (var i = 0; i < 2; i++)
        {
            await Task.Delay(500);
            
            _entities.Light.GuestBathroomLightsGroup.TurnOn(
                colorTempKelvin:turnOnStateData.Temperature,
                brightnessPct:turnOnStateData.BrightnessPct);
        }
    }
    
    private async Task TurnMainRelayOn(IHaContext ha, CustomColorsHs turnOnStateData)
    {
        _entities ??= new Entities(ha);
        
        _entities.Switch.SceneControllerMainLightswitchInGuestBathroom.TurnOn();
        
        await Task.Delay(300);
        
        _entities.Light.GuestBathroomLightsGroup.TurnOn(
            hsColor:turnOnStateData.HsColor,
            brightnessPct:turnOnStateData.BrightnessPct);
        
        // Make sure it got set
        for (var i = 0; i < 2; i++)
        {
            await Task.Delay(500);
            
            _entities.Light.GuestBathroomLightsGroup.TurnOn(
                hsColor:turnOnStateData.HsColor,
                brightnessPct:turnOnStateData.BrightnessPct);
        }
    }
}