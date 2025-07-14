using System.Linq;
using AllenStreetNetDaemonApps.EntityWrappers.Lights.Interfaces;
using HomeAssistantGenerated;
using NetDaemon.Extensions.Scheduler;

namespace AllenStreetNetDaemonApps.LightControllers.DoorEvents;

[NetDaemonApp]
public class LaundryRoomLightsController
{
    private readonly ILogger _logger;
    private readonly Entities _entities;
    
    public LaundryRoomLightsController(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger, IFrontRoomLightsWrapper frontRoomLightsWrapper)
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
        
        scheduler.RunEvery(TimeSpan.FromSeconds(1), checkBackDoorState);
        scheduler.RunEvery(TimeSpan.FromSeconds(30), checkIfMotionTimerExpired);
    }

    private void checkBackDoorState()
    {
        if ((_entities.BinarySensor.FrontDoorReedSwitch.State ?? "off").ToLower() == "on")
        {
            // Back door opened
            
            SharedState.MotionSensors.LastMotionAtBackDoorAt = DateTimeOffset.Now;
            
            _entities.Switch.LaundryRoomLights.TurnOn();
        }
    }

    private void checkIfMotionTimerExpired()
    {
        var tenMinutesAgo = DateTimeOffset.Now.AddMinutes(-10);
        
        // Keep this at like -2 so there's a few chances to retry with scheduler.RunEvery(30 in the constructor
        var longTimeAgo = tenMinutesAgo.AddMinutes(-2);

        _logger.Information("Checking if {MinutesAgoVarName}: {MinutesAgo} is greater than LastMotionAtBackDoorAt: {LastMotionAtBackDoor}",
            nameof(tenMinutesAgo), tenMinutesAgo, SharedState.MotionSensors.LastMotionAtBackDoorAt);
        
        // If it's been less than timeout since the last motion event, don't do anything
        if (SharedState.MotionSensors.LastMotionAtBackDoorAt > tenMinutesAgo) return;
        
        _logger.Information("Checking if longTimeAgo: {LongTimeAgo} is less than LastMotionAtBackDoorAt: {LastMotionAtBackDoor}", 
            longTimeAgo, SharedState.MotionSensors.LastMotionAtBackDoorAt);

        // If it's had a chance to handle the off events, and now it's tried a few times, let's stop trying so needless events don't keep firing 
        if (SharedState.MotionSensors.LastMotionAtBackDoorAt < longTimeAgo) return;

        // Otherwise
        _entities.Switch.LaundryRoomLights.TurnOff();
    }
}