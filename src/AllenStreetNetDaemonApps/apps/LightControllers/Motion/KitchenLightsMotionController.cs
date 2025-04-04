using System.Diagnostics;
using System.Linq;
using AllenStreetNetDaemonApps.EntityWrappers.Lights.Interfaces;
using AllenStreetNetDaemonApps.Models;
using AllenStreetNetDaemonApps.Utilities;
using HomeAssistantGenerated;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel.Entities;
using Newtonsoft.Json;

namespace AllenStreetNetDaemonApps.LightControllers.Motion;

[NetDaemonApp]
public class KitchenLightsMotionController
{
    private readonly IKitchenLightsWrapper _kitchenLightsWrapper;
    private readonly ILogger _logger;
    
    public KitchenLightsMotionController(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger, IKitchenLightsWrapper kitchenLightsWrapper)
    {
        _logger = logger;
        _kitchenLightsWrapper = kitchenLightsWrapper;
        
        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        // _logger = new LoggerConfiguration()
        //     .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
        //     .MinimumLevel.Information()
        //     .WriteTo.Console()
        //     .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
        //     .CreateLogger();
        
        _logger.Debug("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        ha.Events.Where(e => e.EventType == "state_changed").Subscribe(HandleKitchenMotion);

        scheduler.RunEvery(TimeSpan.FromSeconds(30), checkIfMotionTimerExpired);
    }

    private void HandleKitchenMotion(Event e)
    {
        var sw = new Stopwatch();
        sw.Start();
        
        if (e.DataElement is null) return;

        var stringedEventValue = e.DataElement.Value.ToString();
        
        //!stringedEventValue.Contains("\"new_state\":{\"entity_id\":\"binary_sensor.nightlight_in_den")
        if (!stringedEventValue.Contains("\"new_state\":{\"entity_id\":\"binary_sensor.nightlight_in_kitchen"))
        {
            return;
        }
        
        // Debug
        // _logger.Warning("");
        // _logger.Warning("e.DataElement.Value: {@ValueRaw}", e.DataElement.Value.ToString());
        // _logger.Warning("");
        
        var nativeEventValue = JsonConvert.DeserializeObject<MotionEventValue>(stringedEventValue);

        if (nativeEventValue is null) return;
        if (nativeEventValue.NewState is null) return;

        if (nativeEventValue.NewState.State != "on") return;
        
        // Only motion on events for kitchen and den now
        
        sw.Stop();
        
        _logger.Debug("Full parse of motion event handled in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
        
        _kitchenLightsWrapper.TurnOnKitchenLightsFromMotion();
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
        _kitchenLightsWrapper.TurnOffKitchenLightsFromMotion();
    }
}