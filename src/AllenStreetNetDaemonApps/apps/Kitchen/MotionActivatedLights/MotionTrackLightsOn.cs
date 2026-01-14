using AllenStreetNetDaemonApps.Models;
using HomeAssistantGenerated;
using NetDaemon.Extensions.Scheduler;
using Newtonsoft.Json;

namespace AllenStreetNetDaemonApps.Kitchen.MotionActivatedLights;

[NetDaemonApp]
public class MotionTrackLightsOn
{
    private readonly ILogger<MotionTrackLightsOn> _logger;
    private readonly Entities _entities;

    public MotionTrackLightsOn(IHaContext ha, ILogger<MotionTrackLightsOn> logger)
    {
        _logger = logger;
        _entities = new Entities(ha);

        var namespaceBuiltString = Utilities.TrimmedNamespaceBuilder.GetTrimmedName(this);
        
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Initializing {NamespaceBuildString} v0.02", namespaceBuiltString);
             
        ha.Events.Where(e => e.EventType == "state_changed").Subscribe(HandleKitchenMotion);
    }

    private void HandleKitchenMotion(Event e)
    {
        if (e.DataElement is null) return;
    
        var stringedEventValue = e.DataElement.Value.ToString();
        
        // nightlight_in_kitchen is the prefix of all motion sensor names in kitchen so this should grab all motion sensors we want
        if (!stringedEventValue.Contains("\"new_state\":{\"entity_id\":\"binary_sensor.nightlight_in_kitchen"))
        {
            return;
        }
        
        var nativeEventValue = JsonConvert.DeserializeObject<MotionEventValue>(stringedEventValue);
    
        if (nativeEventValue is null) return;
        if (nativeEventValue.NewState is null) return;
        
        // debugDumpAllStateChangedEvents(e);

        debugDumpStateInformation();
        
        if (nativeEventValue.NewState.State != "on") return;

        turnLightsOnIfShortTimeSinceLastMotion();
    }

    private void debugDumpStateInformation()
    {
        if (!_logger.IsEnabled(LogLevel.Debug)) return;
            
        _logger.LogDebug("Checking if oneMinuteAgo is greater than lastKitchenMotionSeenAt: {LastKitchenMotionSeenAt}", SharedState.MotionSensors.KitchenMotionLastSeenAt);
    }

    private void turnLightsOnIfShortTimeSinceLastMotion()
    {
        var oneMinutesAgo = DateTimeOffset.Now.AddMinutes(-1);
        
        // If it's been less than 2 minutes since the last motion event, don't do anything
        if (SharedState.MotionSensors.KitchenMotionLastSeenAt > oneMinutesAgo) return;
        
        // Set the last time motion was seen and turn the light on
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("About to turn on Track Lights");
            _logger.LogDebug("oneMinuteAgo was greater than SharedState.MotionSensors.KitchenMotionLastSeenAt");
        }
        
        SharedState.MotionSensors.KitchenMotionLastSeenAt = DateTimeOffset.Now;
        
        _entities.Light.KitchenTrackLightsGroup.TurnOn();
    }
    
    private void debugDumpAllStateChangedEvents(Event e)
    {
        if (!_logger.IsEnabled(LogLevel.Warning)) return;
        
        _logger.LogCritical("");
        _logger.LogCritical("DUMP FROM state_changed ALL EVENTS:");
        _logger.LogCritical("e.DataElement.Value: {@ValueRaw}", e.DataElement.Value.ToString());
        _logger.LogCritical("");
    }
}