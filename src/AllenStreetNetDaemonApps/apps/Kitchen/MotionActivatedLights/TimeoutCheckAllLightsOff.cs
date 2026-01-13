using HomeAssistantGenerated;
using NetDaemon.Extensions.Scheduler;

namespace AllenStreetNetDaemonApps.Kitchen.MotionActivatedLights;

[NetDaemonApp]
public class TimeoutCheckAllLightsOff
{
    private readonly ILogger<TimeoutCheckAllLightsOff> _logger;
    private readonly IHaContext _ha;
    private readonly Entities _entities;

    public TimeoutCheckAllLightsOff(ILogger<TimeoutCheckAllLightsOff> logger, IHaContext ha, INetDaemonScheduler scheduler)
    {
        _logger = logger;
        _ha = ha;
        _entities = new Entities(ha);
        
        scheduler.RunEvery(TimeSpan.FromSeconds(34), checkIfMotionTimerExpired);
        
        var namespaceBuiltString = Utilities.TrimmedNamespaceBuilder.GetTrimmedName(this);
        
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Initializing {NamespaceBuildString} v0.02", namespaceBuiltString);
    }
    
    private void checkIfMotionTimerExpired()
    {
        var turnOffMinutesAgo = DateTimeOffset.Now.AddMinutes(SharedState.MotionTimeouts.KitchenLightsTimeoutMinutes * -1);
        
        // Keep this at like -2 so there's a few chances to retry with scheduler.RunEvery(30 in the constructor
        var longTimeAgo = turnOffMinutesAgo.AddMinutes(-2);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Checking if {MinutesAgoVarName}: {MinutesAgo} is greater than lastKitchenMotionSeenAt: {LastKitchenMotionSeenAt}", nameof(turnOffMinutesAgo), turnOffMinutesAgo, SharedState.MotionSensors.KitchenMotionLastSeenAt);
        
        // If it's been less than 2 minutes since the last motion event, don't do anything
        if (SharedState.MotionSensors.KitchenMotionLastSeenAt > turnOffMinutesAgo) return;
        
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Checking if longTimeAgo: {LongTimeAgo} is less than lastKitchenMotionSeenAt: {LastKitchenMotionSeenAt}", longTimeAgo, SharedState.MotionSensors.KitchenMotionLastSeenAt);

        // If it's had a chance to handle the off events, and now it's tried a few times, let's stop trying so needless events don't keep firing 
        if (SharedState.MotionSensors.KitchenMotionLastSeenAt < longTimeAgo) return;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Turning off Kitchen Track Lights due to no motion delay of {KitchenLightsTimeoutMinutes} minutes", SharedState.MotionTimeouts.KitchenLightsTimeoutMinutes);

        // Otherwise
        _entities.Light.KitchenTrackLightsGroup.TurnOff();
        _entities.Light.KitchenCeilingLightsGroup.TurnOff();
    }
}
