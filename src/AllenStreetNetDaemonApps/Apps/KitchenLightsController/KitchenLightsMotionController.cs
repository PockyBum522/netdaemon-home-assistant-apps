using System.Diagnostics;
using Newtonsoft.Json;

namespace AllenStreetNetDaemonApps.Apps.KitchenLightsController;

[NetDaemonApp]
public class KitchenLightsMotionController
{
    private readonly ILogger _logger;
    
    private readonly Entities _entities;

    private readonly Entity[] _kitchenCeilingLightsEntities;

    public KitchenLightsMotionController(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger)
    {
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Verbose()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        _logger.Debug("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        ha.Events.Where(e => e.EventType == "state_changed").Subscribe(HandleKitchenMotion);

        _kitchenCeilingLightsEntities = GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.KitchenCeilingLights);

        scheduler.RunEvery(TimeSpan.FromSeconds(30), checkIfMotionTimerExpired);
    }

    private void HandleKitchenMotion(Event e)
    {
        var sw = new Stopwatch();
        sw.Start();
        
        if (e.DataElement is null) return;

        var commonPrefix = "\"new_state\":{\"entity_id\":\"binary_sensor.motion";

        var stringedEventValue = e.DataElement.Value.ToString();
        
        if (!stringedEventValue.Contains(commonPrefix)) return;
        if (!stringedEventValue.Contains("kitchen")) return;
        // There will only be kitchen motion events now
        
        var nativeEventValue = JsonConvert.DeserializeObject<MotionEventValue>(stringedEventValue);

        if (nativeEventValue is null) return;
        if (nativeEventValue.NewState is null) return;

        if (nativeEventValue.NewState.State != "on") return;
        // Only motion on events for kitchen now

        SharedState.MotionSensors.LastMotionInKitchenAt = DateTimeOffset.Now;
        
        sw.Stop();
        
        _logger.Debug("Full parse of motion event handled in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
        
        turnOnKitchenLightsFromMotion();
    }

    private void checkIfMotionTimerExpired()
    {
        var seventyMinutesAgo = DateTimeOffset.Now.AddMinutes(-70);
        
        // Keep this at like -2 so there's a few chances to retry with scheduler.RunEvery(30 in the constructor
        var longTimeAgo = seventyMinutesAgo.AddMinutes(-2);

        _logger.Debug("Checking if {MinutesAgoVarName}: {MinutesAgo} is greater than lastKitchenMotionSeenAt: {LastKitchenMotionSeenAt}", nameof(seventyMinutesAgo), seventyMinutesAgo, SharedState.MotionSensors.LastMotionInKitchenAt);
        
        // If it's been less than 2 minutes since the last motion event, don't do anything
        if (SharedState.MotionSensors.LastMotionInKitchenAt > seventyMinutesAgo) return;
        
        _logger.Debug("Checking if longTimeAgo: {LongTimeAgo} is less than lastKitchenMotionSeenAt: {LastKitchenMotionSeenAt}", longTimeAgo, SharedState.MotionSensors.LastMotionInKitchenAt);

        // If it's had a chance to handle the off events, and now it's tried a few times, let's stop trying so needless events don't keep firing 
        if (SharedState.MotionSensors.LastMotionInKitchenAt < longTimeAgo) return;

        // Otherwise
        turnOffKitchenLightsFromMotion();
    }
    
    private void turnOnKitchenLightsFromMotion()
    {
        _logger.Debug("Running {NameOfThis}", nameof(turnOnKitchenLightsFromMotion));

        if (IsNightTime())
        {
            // At night
            allKitchenLightsOnWithBrightness(50);
            return;
        }
        
        // Daytime!
        allKitchenLightsOnWithBrightness(100);
    }

    private void allKitchenLightsOnWithBrightness(int brightPercentage)
    {
        _entities.Light.KitchenUndercabinetLights.CallService("turn_on", new { brightness_pct = brightPercentage } );

        brightPercentage = brightPercentage.Map(0, 100, 0, 254);
        _entities.Light.MotionNightlightKitchenBySinkTowardsFrontroomLight.CallService("turn_on", new { brightness = brightPercentage } );
    }

    private bool IsNightTime()
    {
        // These should handle 2300, 0000, 0100 on up to 0600, but not outside of those
        
        if (DateTimeOffset.Now.Hour == 23) return true;
        if (DateTimeOffset.Now.Hour < 7) return true;
        
        return false;
    }

    private void turnOffKitchenLightsFromMotion()
    {
        _logger.Debug("Running {NameOfThis}", nameof(turnOffKitchenLightsFromMotion));
        
        var anyLightsAreOn = _kitchenCeilingLightsEntities.Any(l => l.State == "on") ||
                                  _entities.Light.MotionNightlightKitchenBySinkTowardsFrontroomLight.State == "on" ||
                                  _entities.Light.KitchenUndercabinetLights.State == "on";
        
        if (!anyLightsAreOn) return;
        
        _logger.Debug("Turning off kitchen lights because there was no motion and at least one light state was on");
        
        foreach (var ceilingLight in _kitchenCeilingLightsEntities)
            ceilingLight.CallService("light.turn_off");

        // Now turn off the native group
        _entities.Light.KitchenCeilingLights.TurnOff();
        
        _entities.Light.KitchenUndercabinetLights.TurnOff();
        _entities.Light.MotionNightlightKitchenBySinkTowardsFrontroomLight.TurnOff();
    }
}