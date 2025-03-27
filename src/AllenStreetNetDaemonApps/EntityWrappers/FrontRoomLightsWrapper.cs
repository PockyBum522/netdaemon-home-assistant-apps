using System.Linq;
using AllenStreetNetDaemonApps.EntityWrappers.Interfaces;
using AllenStreetNetDaemonApps.Utilities;
using HomeAssistantGenerated;
using NetDaemon.Extensions.Scheduler;

namespace AllenStreetNetDaemonApps.EntityWrappers;

public class FrontRoomLightsWrapper : IFrontRoomLightsWrapper
{
    private readonly ILogger _logger;
    
    private readonly Entities _entities;

    public FrontRoomLightsWrapper(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger)
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
    }
    
    public void TurnOnFrontRoomLightsFromMotion()
    {
        _logger.Debug("Running {NameOfThis}", nameof(TurnOnFrontRoomLightsFromMotion));

        SharedState.MotionSensors.LastMotionInFrontRoomAt = DateTimeOffset.Now;
        
        var anyLightsAreOn = _entities.Light.FrontRoomLightsGroup.State == "on" ||
                                  _entities.Light.FoyerLightsGroup.State == "on";
        
        if (TimeRangeHelpers.IsNightTime())
        {
            // At night
            if (!anyLightsAreOn)
                frontRoomLightsOnWithBrightness(20);
            
            return;
        }
        
        // Daytime!
        if (!anyLightsAreOn)
            frontRoomAndFoyerLightsOnWithBrightness(50);
    }
    
    public void TurnOffFrontRoomLightsFromMotion()
    {
        _logger.Debug("Running {NameOfThis}", nameof(TurnOffFrontRoomLightsFromMotion));

        var anyLightsAreOn = _entities.Light.FrontRoomLightsGroup.State == "on" ||
                                  _entities.Light.FoyerLightsGroup.State == "on";
        
        if (!anyLightsAreOn) return;
        
        _logger.Debug("Turning off FrontRoom lights because there was no motion and at least one light state was on");
        
        // Now turn off the native group
        _entities.Light.FrontRoomLightsGroup.TurnOff();
        _entities.Light.FoyerLightsGroup.TurnOff();
        
        SharedState.MotionSensors.LastMotionInFrontRoomAt = DateTimeOffset.MinValue;
    }
    
    private void frontRoomLightsOnWithBrightness(int brightPercent)
    {
        SharedState.MotionSensors.LastMotionInFrontRoomAt = DateTimeOffset.Now;
        
        _entities.Light.FrontRoomLightsGroup.CallService("turn_on", new { brightness_pct = brightPercent } );
    }
    
    private void frontRoomAndFoyerLightsOnWithBrightness(int brightPercent)
    {
        SharedState.MotionSensors.LastMotionInFrontRoomAt = DateTimeOffset.Now;
        
        _entities.Light.FrontRoomLightsGroup.CallService("turn_on", new { brightness_pct = brightPercent } );
        _entities.Light.FoyerLightsGroup.CallService("turn_on", new { brightness_pct = brightPercent } );
    }
}