using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AllenStreetNetDaemonApps.EntityWrappers.Lights.Interfaces;
using AllenStreetNetDaemonApps.Models;
using AllenStreetNetDaemonApps.Utilities;
using HomeAssistantGenerated;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel.Entities;
using Newtonsoft.Json;

namespace AllenStreetNetDaemonApps.LightControllers.Motion;

[NetDaemonApp]
public class MasterBathLightsMotionController
{
    private readonly IMasterBathLightsWrapper _masterBathLightsWrapper;
    private readonly ILogger _logger;
    private readonly Entities _entities;
    
    public MasterBathLightsMotionController(IHaContext ha, INetDaemonScheduler scheduler, IMasterBathLightsWrapper masterBathLightsWrapper) //, ILogger logger)
    {
        //_logger = logger;
        _masterBathLightsWrapper = masterBathLightsWrapper;

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        _entities = new Entities(ha);
        
        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.Debug()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        _logger.Debug("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        ha.Events.Where(e => e.EventType == "state_changed").Subscribe(HandleMasterBathMotion);

        scheduler.RunEvery(TimeSpan.FromSeconds(30), async void () => await checkIfMotionTimerExpired());
    }

    private void HandleMasterBathMotion(Event e)
    {
        var sw = new Stopwatch();
        sw.Start();
        
        if (e.DataElement is null) return;

        var stringedEventValue = e.DataElement.Value.ToString();

        if (!stringedEventValue.Contains("\"new_state\":{\"entity_id\":\"binary_sensor.nightlight_in_master_bath"))
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
        
        // Only motion on events for masterBath and den now
        
        sw.Stop();
        
        _logger.Debug("Full parse of motion event handled in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
        
        // Make sure no lights are already on so we don't mess up something someone already has going
        if (_masterBathLightsWrapper.AreAnyLightsOn()) return;
        
        SharedState.MotionSensors.LastMotionInMasterBathAt =  DateTimeOffset.Now;
        
        if (DateTimeOffset.Now.Hour > 9 &&
            DateTimeOffset.Now.Hour < 20)
        {
            _logger.Debug("Setting all master bath lights to warm white");

            _masterBathLightsWrapper.SetMasterBathLightsToWarmWhiteScene();

            return;
        }
        
        if (_entities.Light.BulbInMasterBedroomCeilingFan01.IsOn())
        {
            _logger.Debug("Setting all master bath lights to warm white because master bedroom fan light was on");

            _masterBathLightsWrapper.SetMasterBathLightsToWarmWhiteScene();

            return;
        }
        
        _logger.Debug("Setting all master bath lights to dim red");
        
        _masterBathLightsWrapper.SetMasterBathLightsDimRed();
    }

    private async Task<Task> checkIfMotionTimerExpired()
    {
        var tenMinutesAgo = DateTimeOffset.Now.AddMinutes(-10);
        
        // Keep this at like -2 so there's a few chances to retry with scheduler.RunEvery(30 in the constructor
        var longTimeAgo = tenMinutesAgo.AddMinutes(-2);

        await Task.Delay(1000);
        
        _logger.Debug("Checking if {MinutesAgoVarName}: {MinutesAgo} is greater than lastMasterBathMotionSeenAt: {LastMasterBathMotionSeenAt}", 
            nameof(tenMinutesAgo), tenMinutesAgo, SharedState.MotionSensors.LastMotionInMasterBathAt);
        
        // If it's been less than 2 minutes since the last motion event, don't do anything
        if (SharedState.MotionSensors.LastMotionInMasterBathAt > tenMinutesAgo) return Task.CompletedTask;
        
        _logger.Debug("Checking if longTimeAgo: {LongTimeAgo} is less than lastMasterBathMotionSeenAt: {LastMasterBathMotionSeenAt}", 
            longTimeAgo, SharedState.MotionSensors.LastMotionInMasterBathAt);

        // If it's had a chance to handle the off events, and now it's tried a few times, let's stop trying so needless events don't keep firing 
        if (SharedState.MotionSensors.LastMotionInMasterBathAt < longTimeAgo) return Task.CompletedTask;

        // Otherwise
        await _masterBathLightsWrapper.TurnOffMasterBathLights();
        
        return Task.CompletedTask;
    }
}