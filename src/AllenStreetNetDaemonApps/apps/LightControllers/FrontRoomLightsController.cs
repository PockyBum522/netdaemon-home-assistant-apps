using System.Linq;
using AllenStreetNetDaemonApps.EntityWrappers.Interfaces;
using NetDaemon.Extensions.Scheduler;

namespace AllenStreetNetDaemonApps.LightControllers;

[NetDaemonApp]
public class FrontRoomLightsController
{
    private readonly IFrontRoomLightsWrapper _frontRoomLightsWrapper;
    private readonly ILogger _logger;
    
    public FrontRoomLightsController(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger, IFrontRoomLightsWrapper frontRoomLightsWrapper)
    {
        _logger = logger;
        _frontRoomLightsWrapper = frontRoomLightsWrapper;

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        // _logger = new LoggerConfiguration()
        //     .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
        //     .MinimumLevel.Information()
        //     .WriteTo.Console()
        //     .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
        //     .CreateLogger();
        
        _logger.Debug("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        scheduler.RunEvery(TimeSpan.FromSeconds(30), checkIfMotionTimerExpired);
    }

    private void checkIfMotionTimerExpired()
    {
        var fiveMinutesAgo = DateTimeOffset.Now.AddMinutes(-5);
        
        // Keep this at like -2 so there's a few chances to retry with scheduler.RunEvery(30 in the constructor
        var longTimeAgo = fiveMinutesAgo.AddMinutes(-2);

        _logger.Debug("Checking if {MinutesAgoVarName}: {MinutesAgo} is greater than lastFrontRoomMotionSeenAt: {LastFrontRoomMotionSeenAt}", nameof(fiveMinutesAgo), fiveMinutesAgo, SharedState.MotionSensors.LastMotionInFrontRoomAt);
        
        // If it's been less than timeout since the last motion event, don't do anything
        if (SharedState.MotionSensors.LastMotionInFrontRoomAt > fiveMinutesAgo) return;
        
        _logger.Debug("Checking if longTimeAgo: {LongTimeAgo} is less than lastFrontRoomMotionSeenAt: {LastFrontRoomMotionSeenAt}", longTimeAgo, SharedState.MotionSensors.LastMotionInFrontRoomAt);

        // If it's had a chance to handle the off events, and now it's tried a few times, let's stop trying so needless events don't keep firing 
        if (SharedState.MotionSensors.LastMotionInFrontRoomAt < longTimeAgo) return;

        // Otherwise
        _frontRoomLightsWrapper.TurnOffFrontRoomLightsFromMotion();
    }
}