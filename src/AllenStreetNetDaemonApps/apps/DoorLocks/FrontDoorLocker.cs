using AllenStreetNetDaemonApps.Kitchen.MotionActivatedLights;
using HomeAssistantGenerated;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel.Entities;

namespace AllenStreetNetDaemonApps.DoorLocks;

[NetDaemonApp]
public class FrontDoorLocker
{
    private readonly ILogger<FrontDoorLocker> _logger;
    private readonly IHaContext _ha;
    private readonly Entities _entities;
    
    public FrontDoorLocker(ILogger<FrontDoorLocker> logger, IHaContext ha, INetDaemonScheduler scheduler)
    {
        _logger = logger;
        _ha = ha;
        _entities = new Entities(ha);
            
        scheduler.RunEvery(TimeSpan.FromSeconds(10), checkIfDoorShouldLock);
            
        var namespaceBuiltString = Utilities.TrimmedNamespaceBuilder.GetTrimmedName(this);
            
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Initializing {NamespaceBuildString} v0.02", namespaceBuiltString);
    }

    private void checkIfDoorShouldLock()
    {
        // Check that state is valid, notify if not
        if (!IsKnownState(_entities.Lock.FrontDoorDeadbolt))
        {
            debugLogForUnknownState();
        
            // If not locking or locked, though, show error:
            var threeHoursAgo =  DateTimeOffset.Now.AddHours(-3);
            
            // Rate-limit to a notification once every three hours
            if (SharedState.Locks.DoorLastNotifiedOfProblemAt > threeHoursAgo) return;
            
            SharedState.Locks.DoorLastNotifiedOfProblemAt = DateTimeOffset.Now;
            
            _ha.CallService("notify", "persistent_notification",
                data: new {message = "Front door lock reporting error!", title = "Front Lock Error"});
            
            return;
        }
        
        var nMinutesFromNow = DateTimeOffset.Now.AddMinutes(6);
        
        // If door is currently locked, update last locked at time, return
        if (IsLocked(_entities.Lock.FrontDoorDeadbolt))
        {
            SharedState.Locks.FrontDoorToLockAt = nMinutesFromNow;

            debugLogForLocked();

            return;
        }
        
        // If door not currently locked, lock if more than X minutes ago
        if (!IsUnlocked(_entities.Lock.FrontDoorDeadbolt)) return;
        
        debugLogForUnlocked(nMinutesFromNow);
            
        if (SharedState.Locks.FrontDoorToLockAt > DateTime.Now) return;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Evaluated to where we should be locking! Locking now");

        // Since we're locking, update when to lock next
        SharedState.Locks.FrontDoorToLockAt = nMinutesFromNow;
        _entities.Lock.FrontDoorDeadbolt.Lock();
    }

    private void debugLogForUnknownState()
    {
        if (!_logger.IsEnabled(LogLevel.Debug)) return;
        
        _logger.LogDebug("Was looking for valid state but current state is: {LockState}", _entities.Lock.FrontDoorDeadbolt.State);
        _logger.LogDebug("Showing error notification");
    }

    private void debugLogForUnlocked(DateTimeOffset nMinutesAgo)
    {
        if (!_logger.IsEnabled(LogLevel.Debug)) return;
        
        _logger.LogDebug("Deadbolt state is: {LockState}", _entities.Lock.FrontDoorDeadbolt.State);
        _logger.LogDebug("DateTimeOffset.Now is: {DateTimeNow}", DateTimeOffset.Now);
        _logger.LogDebug("SharedState.Locks.FrontDoorToLockAt is: {LastLockedAt}", SharedState.Locks.FrontDoorToLockAt);
        _logger.LogDebug("SharedState.Locks.FrontDoorToLockAt < DateTime.Now: {BoolEval}", (SharedState.Locks.FrontDoorToLockAt < DateTime.Now));
    }

    private void debugLogForLocked()
    {
        if (!_logger.IsEnabled(LogLevel.Debug)) return;
            
        _logger.LogDebug("Deadbolt state is: {LockState}", _entities.Lock.FrontDoorDeadbolt.State);
        _logger.LogDebug("Deadbolt to lock time being set to n minutes in the future");
        _logger.LogDebug("Not progressing further");
    }

    private bool IsLocked(LockEntity lockToCheck)
    {
        return (lockToCheck.State ?? "null").Equals("locked", StringComparison.InvariantCultureIgnoreCase);
    }
    
    private bool IsUnlocked(LockEntity lockToCheck)
    {
        return (lockToCheck.State ?? "null").Equals("unlocked", StringComparison.InvariantCultureIgnoreCase);
    }
    
    private bool IsLocking(LockEntity lockToCheck)
    {
        return (lockToCheck.State ?? "null").Equals("locking", StringComparison.InvariantCultureIgnoreCase);
    }
    
    private bool IsKnownState(LockEntity lockToCheck)
    {
        var lockState = lockToCheck.State ?? "null";
        
        if (lockState.Equals("locking", StringComparison.InvariantCultureIgnoreCase)) return true;
        if (lockState.Equals("unlocking", StringComparison.InvariantCultureIgnoreCase)) return true;
        if (lockState.Equals("locked", StringComparison.InvariantCultureIgnoreCase)) return true;
        if (lockState.Equals("unlocked", StringComparison.InvariantCultureIgnoreCase)) return true;

        return false;
    }
}