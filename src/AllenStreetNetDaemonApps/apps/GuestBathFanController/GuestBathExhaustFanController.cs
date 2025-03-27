using System.Linq;
using AllenStreetNetDaemonApps.EntityWrappers.Interfaces;
using HomeAssistantGenerated;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel.Entities;

namespace AllenStreetNetDaemonApps.GuestBathFanController;

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
        _logger = logger;
        _guestBathLightsWrapper = guestBathLightsWrapper;
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        // _logger = new LoggerConfiguration()
        //     .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
        //     .MinimumLevel.Information()
        //     .WriteTo.Console()
        //     .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
        //     .CreateLogger();
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        scheduler.RunIn(TimeSpan.FromSeconds(10), InitializeFanState);
        scheduler.RunEvery(TimeSpan.FromSeconds(1), CheckExhaustFanTimeout);
    }

    private void InitializeFanState()
    {
        _fanLastState = _entities.Fan.ExhaustFanInGuestBathroom.IsOn();
    }

    private void CheckExhaustFanTimeout()
    {
        var fifteenMinutesAgo = DateTimeOffset.Now.AddMinutes(-15);
        
        if (_entities.Fan.ExhaustFanInGuestBathroom.IsOff())
        {
            resetState();
            return;
        }

        updateCountdownUntilOffText(fifteenMinutesAgo);
        
        // The above if checks if the fan is on, so now all we have to check is "was last state off"
        if (!_fanLastState)
        {
            _fanLastState = true;
            
            SharedState.Timeouts.ExhaustFanInGuestBathTurnedOnAt = DateTimeOffset.Now;
        } 
        
        // Now LastTurnedOnAt will always be when the fan was first turned on
        var aLittleLonger = fifteenMinutesAgo.AddMinutes(-2);
        
        if (SharedState.Timeouts.ExhaustFanInGuestBathTurnedOnAt > fifteenMinutesAgo) return;
        
        // If it's had a chance to handle the off events, and now it's tried a few times, let's stop trying so needless events don't keep firing 
        if (SharedState.MotionSensors.LastMotionInKitchenAt < aLittleLonger) return;

        // Otherwise
        _entities.Fan.ExhaustFanInGuestBathroom.TurnOff();
        
        resetState();
    }

    private void updateCountdownUntilOffText(DateTimeOffset fifteenMinutesAgo)
    {
        if (_entities.Fan.ExhaustFanInGuestBathroom.IsOff())
        {
            resetState();
            return;
        }
        
        var timeUntilOff = SharedState.Timeouts.ExhaustFanInGuestBathTurnedOnAt - fifteenMinutesAgo;

        if (timeUntilOff.Minutes is < 0 or > 15)
        {
            _entities.InputText.GuestBathFanCountdown.SetValue("Unknown");
            return;
        }
        
        // _entities.InputBoolean.IsVisibleGuestBathFanCountdownBadge.TurnOn();
        
        var countdownMessage = $"{timeUntilOff.Minutes:d2}:{timeUntilOff.Seconds:d2}";
            
        _entities.InputText.GuestBathFanCountdown.SetValue(countdownMessage);
    }

    private void resetState()
    {
        _fanLastState = false;
        SharedState.Timeouts.ExhaustFanInGuestBathTurnedOnAt = DateTimeOffset.MinValue;
        
        _entities.InputText.GuestBathFanCountdown.SetValue("Off");
        // _entities.InputBoolean.IsVisibleGuestBathFanCountdownBadge.TurnOff();
    }
}
