using AllenStreetNetDaemonApps.Internal.Interfaces;

namespace AllenStreetNetDaemonApps.Internal;

public class FrontRoomLightsControl : IFrontRoomLightsControl
{
    private readonly ILogger _logger;
    
    private readonly Entities _entities;

    public FrontRoomLightsControl(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger)
    {
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        _logger.Debug("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
    }
    
    public void TurnOnFrontRoomLightsFromMotion()
    {
        _logger.Debug("Running {NameOfThis}", nameof(TurnOnFrontRoomLightsFromMotion));

        SharedState.MotionSensors.LastMotionInFrontRoomAt = DateTimeOffset.Now;
        
        var anyLightsAreOn = _entities.Light.FrontRoomLights.State == "on" ||
                                  _entities.Light.FoyerLights.State == "on";
        
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

        var anyLightsAreOn = _entities.Light.FrontRoomLights.State == "on" ||
                                  _entities.Light.FoyerLights.State == "on";
        
        if (!anyLightsAreOn) return;
        
        _logger.Debug("Turning off FrontRoom lights because there was no motion and at least one light state was on");
        
        // Now turn off the native group
        _entities.Light.FrontRoomLights.TurnOff();
        _entities.Light.FoyerLights.TurnOff();
        
        SharedState.MotionSensors.LastMotionInFrontRoomAt = DateTimeOffset.MinValue;
    }
    
    private void frontRoomLightsOnWithBrightness(int brightPercent)
    {
        SharedState.MotionSensors.LastMotionInFrontRoomAt = DateTimeOffset.Now;
        
        _entities.Light.FrontRoomLights.CallService("turn_on", new { brightness_pct = brightPercent } );
    }
    
    private void frontRoomAndFoyerLightsOnWithBrightness(int brightPercent)
    {
        SharedState.MotionSensors.LastMotionInFrontRoomAt = DateTimeOffset.Now;
        
        _entities.Light.FrontRoomLights.CallService("turn_on", new { brightness_pct = brightPercent } );
        _entities.Light.FoyerLights.CallService("turn_on", new { brightness_pct = brightPercent } );
    }
}