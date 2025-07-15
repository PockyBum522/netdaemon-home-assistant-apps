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

public class MasterBathLightsWrapper : IMasterBathLightsWrapper
{
    private readonly ILogger _logger;
    
    private readonly Entities _entities;

    private readonly Entity[] _masterBathLightsEntities;

    public MasterBathLightsWrapper(IHaContext ha, INetDaemonScheduler scheduler) //, ILogger logger)
    {
        //_logger = logger;
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.Debug()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        _logger.Debug("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);

        _masterBathLightsEntities = GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.MasterBathLightsGroup);
    }

    public bool AreAnyLightsOn()
    {
        var foundOneOn = false;
        
        foreach (var light in _masterBathLightsEntities)
        {
            _logger.Debug("Walking all master bath lights: {Name}.State: {State}", light.EntityId, light.State);
            
            if (light.State == "on")
                foundOneOn = true;
        }

        return foundOneOn;
    }
    
    public async Task TurnOffMasterBathLights()
    {
        _logger.Debug("Running {NameOfThis}", nameof(TurnOffMasterBathLights));
        
        var anyLightsAreOn = _masterBathLightsEntities.Any(l => l.State == "on");
        
        if (!anyLightsAreOn) return;
        
        _logger.Debug("Turning off MasterBath lights because there was no motion and at least one light state was on");
        
        foreach (var ceilingLight in _masterBathLightsEntities)
            ceilingLight.CallService("light.turn_off");

        // Now turn off the native group
        _entities.Light.MasterBathLightsGroup.TurnOff();

        await Task.Delay(1000);
        
        foreach (var light in _masterBathLightsEntities)
        {
            await Task.Delay(1000);
            
            if (!light.IsOn()) continue;
            
            light.CallService("light.turn_off");
        }
    }
    
    public async Task SetMasterBathLightsBrighter()
    {
        _logger.Debug("Running: {ThisName}", nameof(SetMasterBathLightsBrighter));
        
        await ModifyCeilingLightsBrightnessBy(20);
    }

    public async Task SetMasterBathLightsDimmer()
    {
        _logger.Debug("Running: {ThisName}", nameof(SetMasterBathLightsDimmer));
        
        await ModifyCeilingLightsBrightnessBy(-20);   
    }

    public void SetMasterBathLightsToWarmWhiteScene()
    {
        _logger.Debug("Running: {ThisName}", nameof(SetMasterBathLightsToWarmWhiteScene));
        
        _masterBathLightsEntities.CallService("turn_on", CustomColors.WarmWhite() );
    }

    public void SetMasterBathLightsDimRed()
    {
        _logger.Debug("Running: {ThisName}", nameof(SetMasterBathLightsDimRed));

        _masterBathLightsEntities.CallService("turn_on", CustomColors.RedDim() );
    }
    
    public async Task ModifyCeilingLightsBrightnessBy(int brightnessModifier)
    {
        _logger.Debug("Running: {ThisName} with modifier value: {BrightnessModifier}", nameof(ModifyCeilingLightsBrightnessBy), brightnessModifier);
        
        foreach (var ceilingLight in _masterBathLightsEntities)
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

    private async Task allMasterBathLightsOnWithBrightness(int brightPercent)
    {
        for (var i = 0; i < 5; i++)
        {           
            await Task.Delay(200);

            _masterBathLightsEntities.CallService("turn_on", new { brightness_pct = brightPercent } );
        }
    }
}