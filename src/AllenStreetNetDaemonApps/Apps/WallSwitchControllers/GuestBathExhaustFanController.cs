using AllenStreetNetDaemonApps.EntityWrappers;
using AllenStreetNetDaemonApps.EntityWrappers.Interfaces;

namespace AllenStreetNetDaemonApps.Apps.WallSwitchControllers;

[NetDaemonApp]
public class GuestBathExhaustFanController
{
    private readonly IHaContext _ha;
    private readonly IGuestBathLightsWrapper _guestBathLightsWrapper;
    private readonly ILogger _logger;
    
    private readonly Entities _entities;

    private static bool _fanLastState;
    
    public GuestBathExhaustFanController(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger, IGuestBathLightsWrapper guestBathLightsWrapper)
    {
        _ha = ha;
        _guestBathLightsWrapper = guestBathLightsWrapper;
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        scheduler.RunIn(TimeSpan.FromSeconds(10), InitializeFanState);
        scheduler.RunEvery(TimeSpan.FromSeconds(40), CheckExhaustFanTimeout);
    }

    private void InitializeFanState()
    {
        _fanLastState = _entities.Fan.GuestBathExhaustFanAboveToiletSpeedControllerSwitch.IsOn();
    }

    private void CheckExhaustFanTimeout()
    {
        if (!_entities.Fan.GuestBathExhaustFanAboveToiletSpeedControllerSwitch.IsOn())
        {
            resetState();
            return;
        }
        
        // The above if checks if the fan is on, so now all we have to check is "was last state off"
        if (!_fanLastState)
        {
            _fanLastState = true;
            
            SharedState.Timeouts.ExhaustFanInGuestBathTurnedOnAt = DateTimeOffset.Now;
        } 
        
        // Now LastTurnedOnAt will always be when the fan was first turned on
        var fifteenMinutesAgo = DateTimeOffset.Now.AddMinutes(-15);
        var aLittleLonger = fifteenMinutesAgo.AddMinutes(-2);
        
        if (SharedState.Timeouts.ExhaustFanInGuestBathTurnedOnAt > fifteenMinutesAgo) return;
        
        // If it's had a chance to handle the off events, and now it's tried a few times, let's stop trying so needless events don't keep firing 
        if (SharedState.MotionSensors.LastMotionInKitchenAt < aLittleLonger) return;

        // Otherwise
        _entities.Fan.GuestBathExhaustFanAboveToiletSpeedControllerSwitch.TurnOff();
        
        resetState();
    }

    private static void resetState()
    {
        _fanLastState = false;
        SharedState.Timeouts.ExhaustFanInGuestBathTurnedOnAt = DateTimeOffset.MinValue;
    }
}
