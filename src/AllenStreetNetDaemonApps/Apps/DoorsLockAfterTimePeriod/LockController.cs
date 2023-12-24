using AllenStreetNetDaemonApps.Apps.DoorsLockAfterTimePeriod.Models;

namespace AllenStreetNetDaemonApps.Apps.DoorsLockAfterTimePeriod;

[NetDaemonApp]
public class LockController
{
    private AutomaticallyLockableLock FrontDoorLock { get; }
    private AutomaticallyLockableLock BackDoorLock { get; }

    private TimeSpan _frontDoorLockTimeout = TimeSpan.FromMinutes(3);
    private TimeSpan _backDoorLockTimeout = TimeSpan.FromMinutes(5);

    private string _frontLockLastDisableText = "";
    private string _backLockLastDisableText = "";
    
    private DateTimeOffset _frontDoorDisableAddingTimeUntil = DateTimeOffset.Now;
    private DateTimeOffset _backDoorDisableAddingTimeUntil = DateTimeOffset.Now;

    private static readonly List<string> DisabledLockNames = new();
    private static List<string> _previousDisabledLockNames = new();

    private readonly ILogger _logger;
    private readonly Entities _entities;
    private readonly TextNotifier _textNotifier;
    
    public LockController(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger)
    {
        _entities = new Entities(ha);
        
        FrontDoorLock = new AutomaticallyLockableLock(TimeSpan.FromMinutes(3), _entities.Lock.FrontDoor);
        BackDoorLock = new AutomaticallyLockableLock(TimeSpan.FromMinutes(5), _entities.Lock.BackDoor);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);

        _textNotifier = new TextNotifier(_logger, ha);
        
        InitializeLockDisableForHours();
        
        // Set schedule for initial runs of things
        // ReSharper disable once AsyncVoidLambda
        scheduler.RunIn(TimeSpan.FromSeconds(3), async () => await WorkLocks());
        
        // Set recurring runs for door lock/door state related stuff
        // ReSharper disable once AsyncVoidLambda
        scheduler.RunEvery(TimeSpan.FromSeconds(10), async () => await WorkLocks());
        
        // ReSharper disable once AsyncVoidLambda
        scheduler.RunEvery(TimeSpan.FromSeconds(0.5), async () => await CheckAllDoors());

        // ReSharper disable once AsyncVoidLambda
        scheduler.RunEvery(TimeSpan.FromSeconds(0.5), async () => await UnlockFrontDoorIfOpen());
    }

    private void DisableFrontDoorLockForHours(double hoursToDisableFor)
    {
        FrontDoorLock.DisabledUntil = DateTimeOffset.Now + TimeSpan.FromHours(hoursToDisableFor);
    }

    private void DisableBackDoorLockForHours(double hoursToDisableFor)
    {
        BackDoorLock.DisabledUntil = DateTimeOffset.Now + TimeSpan.FromHours(hoursToDisableFor);
    }
    
    private async Task WorkLocks()
    {
        await WorkLockDisableTimes();
            
        await WorkLock(FrontDoorLock);

        await Task.Delay(1000);
        
        await WorkLock(BackDoorLock);
    }
    
    private void InitializeLockDisableForHours()
    {
        _entities.InputText.FrontDoorLockDisableHours.SetValue("0");
        _entities.InputText.BackDoorLockDisableHours.SetValue("0");
        
        _frontLockLastDisableText = "0";
        _backLockLastDisableText = "0";
    }

    private async Task WorkLockDisableTimes()
    {
        var frontLockDisableText = _entities.InputText.FrontDoorLockDisableHours.EntityState?.State;
        var backLockDisableText = _entities.InputText.BackDoorLockDisableHours.EntityState?.State;

        if (frontLockDisableText is null || backLockDisableText is null)
            throw new ArgumentNullException(nameof(frontLockDisableText));
        
        if (_frontLockLastDisableText != frontLockDisableText)
        {
            // If we're in here, the user changed the text in the HA dashboard
            
            // Update this
            _frontLockLastDisableText = frontLockDisableText;
            
            await UnlockWithRetriesAsync(FrontDoorLock, 5000);

            DisableFrontDoorLockForHours(double.Parse(frontLockDisableText));

            _logger.Debug("New time input to disable front door lock from HA dashboard: {HoursInput}",
                frontLockDisableText);
        }
        
        if (_backLockLastDisableText != backLockDisableText)
        {
            // If we're in here, the user changed the text in the HA dashboard
            
            // Update this
            _backLockLastDisableText = backLockDisableText;

            await UnlockWithRetriesAsync(BackDoorLock, 5000);
            
            DisableBackDoorLockForHours(double.Parse(backLockDisableText));
            
            _logger.Debug("New time input to disable back door lock from HA dashboard: {HoursInput}",
                frontLockDisableText);
        }

        var currentTime = DateTimeOffset.Now;

        if (FrontDoorLock.DisabledUntil > currentTime)
        {
            var hoursLeft = (FrontDoorLock.DisabledUntil - currentTime).TotalHours;
            
            _entities.InputText.FrontDoorLockDisableHours.SetValue(hoursLeft.ToString("N2"));
            _frontLockLastDisableText = hoursLeft.ToString("N2");
            
            _logger.Debug("(FrontDoorLock.DisabledUntil - currentTime).TotalHours = {HoursLeft}", hoursLeft);
            _logger.Debug("Setting HA input text on dash now");
            _logger.Debug("Exact time left: {TimeLeft}", FrontDoorLock.DisabledUntil - currentTime);
        }
        else
        {
            _entities.InputText.FrontDoorLockDisableHours.SetValue("0");
            _frontLockLastDisableText = "0";
        }
        
        if (BackDoorLock.DisabledUntil > currentTime)
        {
            var hoursLeft = (BackDoorLock.DisabledUntil - currentTime).TotalHours;
            
            _entities.InputText.BackDoorLockDisableHours.SetValue(hoursLeft.ToString("N2"));
            _backLockLastDisableText = hoursLeft.ToString("N2");
            
            _logger.Debug("(BackDoorLock.DisabledUntil - currentTime).TotalHours = {HoursLeft}", hoursLeft);
            _logger.Debug("Setting HA input text on dash now");
            _logger.Debug("Exact time left: {TimeLeft}", BackDoorLock.DisabledUntil - currentTime);
        }
        else
        {
            _entities.InputText.BackDoorLockDisableHours.SetValue("0");
            _backLockLastDisableText = "0";
        }
    }
    
    private async Task CheckAllDoors()
    {
        _logger.Debug("FrontDoor august open state: {FrontDoorOpen}, BackDoor august open state: {BackDoorOpen}, front door reed sw: {FrontDoorReed}, Back door reed sw: {BackDoorReed}", 
            _entities.BinarySensor.FrontDoorOpen.State,
            _entities.BinarySensor.BackDoorOpen.State,
            _entities.BinarySensor.FrontDoorReedSwitch.State,
            _entities.BinarySensor.BackDoorReedSwitch.State);

        await HandleFrontDoorCheck();

        await HandleBackDoorCheck();
    }
    
    private async Task UnlockFrontDoorIfOpen()
    {
        _logger.Debug("FrontDoor august open state: {FrontDoorOpen}, front door reed sw: {FrontDoorReed}", 
            _entities.BinarySensor.FrontDoorOpen.State,
            _entities.BinarySensor.FrontDoorReedSwitch.State);

        if (_entities.BinarySensor.FrontDoorReedSwitch.State != "on") return;  // Only do the following things when the door is open
        
        // If the door is open, we'd better make damn sure the deadbolt retracts so it doesn't slam into the door frame latch when the door shuts
        if (FrontDoorLock.IsLocked) await UnlockWithRetriesAsync(FrontDoorLock, 750);
        
        // If the door is open then we're done with these
        DisableFrontDoorLatches();
    }

    private Task HandleFrontDoorCheck()
    {
        _logger.Debug("Front door timeout: {FrontDoorTimeout}", _frontDoorLockTimeout);
        
        // Slowly reduce the timeout if the door isn't opening, that way it'll get back to normal over a while of the door being closed
        if (_frontDoorLockTimeout > FrontDoorLock.LockAfterDuration)
        {
            _frontDoorLockTimeout -= TimeSpan.FromSeconds(0.5);
        }
        
        if (_entities.BinarySensor.FrontDoorReedSwitch.State != "on") return Task.CompletedTask;
        
        _logger.Debug("Front door open! Adding time");

        _logger.Debug("Checking now: {Now} > {FrontDoorDisableAddingTimeUntil}", DateTimeOffset.Now, _frontDoorDisableAddingTimeUntil);
        
        // Check if we're past the timeout for adding more time. This prevents a bunch of time getting added rapidly to the lock timeout just because the door is staying open
        if (DateTimeOffset.Now > _frontDoorDisableAddingTimeUntil)
        {
            _logger.Debug("Disable adding time until elapsed, adding more time!");
            
            _frontDoorDisableAddingTimeUntil = DateTimeOffset.Now + TimeSpan.FromSeconds(30);
            
            _logger.Debug("Will be disabled to add more time until: {TimeUntil}", _frontDoorDisableAddingTimeUntil);

            _frontDoorLockTimeout += TimeSpan.FromMinutes(3);
            
            _logger.Debug("Added to FrontDoorLockTimeout, is now: {Timeout}", _frontDoorLockTimeout);

            // Don't let it get to be more than the original lock after timeout * 3
            if (_frontDoorLockTimeout > FrontDoorLock.LockAfterDuration * 3)
                _frontDoorLockTimeout = FrontDoorLock.LockAfterDuration * 3;
            
            _logger.Debug("If it was too great, we would have reduced FrontDoorLockTimeout, is now: {Timeout}", _frontDoorLockTimeout);
        }
        
        FrontDoorLock.LockAtTime = DateTimeOffset.Now + _frontDoorLockTimeout;
        
        _logger.Debug("FrontDoorLock.LockAtTime is now: {LockAt}", FrontDoorLock.LockAtTime);

        _logger.Debug("Front door open, adding time to auto-lock for front door");
        _logger.Debug("Front door will lock at: {DoorLockAtTime}", FrontDoorLock.LockAtTime);
        
        return Task.CompletedTask;
    }
    
    private async Task HandleBackDoorCheck()
    {
        // Slowly reduce the timeout if the door isn't opening, that way it'll get back to normal over a while of the door being closed
        if (_backDoorLockTimeout > BackDoorLock.LockAfterDuration)
        {
            _backDoorLockTimeout -= TimeSpan.FromSeconds(0.5);
        }
        
        if (_entities.BinarySensor.BackDoorReedSwitch.State != "on") return;  // Only do the following things when the door is open
        
        // Check if we're past the timeout for adding more time. This prevents a bunch of time getting added rapidly to the lock timeout just because the door is staying open
        if (DateTimeOffset.Now > _backDoorDisableAddingTimeUntil)
        {
            _backDoorDisableAddingTimeUntil = DateTimeOffset.Now + TimeSpan.FromSeconds(30);

            _backDoorLockTimeout += TimeSpan.FromMinutes(5);

            // Don't let it get to be more than the original lock after timeout * 3
            if (_backDoorLockTimeout > BackDoorLock.LockAfterDuration * 4)
                _backDoorLockTimeout = BackDoorLock.LockAfterDuration * 4;
        }
        
        BackDoorLock.LockAtTime = DateTimeOffset.Now + _backDoorLockTimeout;

        // If the door is open, we'd better make damn sure the deadbolt retracts so it doesn't slam into the door frame latch when the door shuts
        if (BackDoorLock.IsLocked) await UnlockWithRetriesAsync(BackDoorLock, 750);

        _logger.Debug("Back door open, adding time to auto-lock for back door");
        _logger.Debug("Back door will lock at: {DoorLockAtTime}", BackDoorLock.LockAtTime);        
    }
    
    private void DisableFrontDoorLatches()
    {
        var deadboltLatch = _entities.Switch.DeadboltDoorframeLatch;
        var doorknobLatch = _entities.Switch.DoorknobDoorframeLatch;
        
        deadboltLatch.TurnOff();
        doorknobLatch.TurnOff();
    }

    private async Task WorkLock(AutomaticallyLockableLock lockToWork)
    {
        var currentTime = DateTimeOffset.Now;
        
        LogDebugLockState(lockToWork);
        
        _logger.Debug("Working {LockName} which is currently {State}", 
            lockToWork.Name,
            lockToWork.HomeAssistantLockEntity.State);

        LogWarningIfLockRemainsUnlocked(lockToWork, currentTime);

        ResetWhenLocked(lockToWork, currentTime);
        
        if (lockToWork is { IsUnlocked: true, AutoLockControllerActive: false })
        {
            // If lock is unlocked and we have not yet caught the unlock event then this IS the unlock event
            lockToWork.LastUnlockedAtTime = currentTime;
            lockToWork.LockAtTime = currentTime + lockToWork.LockAfterDuration;
            lockToWork.AutoLockControllerActive = true;

            _logger.Debug("Caught event where lock was locked, now is unlocked");
            _logger.Debug("Setting lock LockAtTime to the future");
            _logger.Debug("Setting controller active = true for {LockId}",
                lockToWork.HomeAssistantLockEntity.EntityId);
            
            return;
        }
        
        // If unlocking, no point in going further
        if (lockToWork.HomeAssistantLockEntity.State == "unlocking") return;

        // If no unlock event seen, no point in going further
        if (!lockToWork.AutoLockControllerActive) return;
        
        // If it's locked, no point in going further
        if (lockToWork.IsLocked) return;
        _logger.Debug("Lock not locked, proceeding");
    
        _logger.Debug("Lock DisabledUntil is {DisabledUntil}", lockToWork.DisabledUntil);
        if (lockToWork.DisabledUntil > currentTime) return;
        
        _logger.Debug("Lock lockAtTime is {LockAtTime}", lockToWork.LockAtTime);
        if (lockToWork.LockAtTime > currentTime) return;
        
        // If we got through all that
        _logger.Debug("Locking with retries");
        await LockWithRetriesAsync(lockToWork, 15000);

        await Task.Delay(5000);
        
        if (lockToWork.HomeAssistantLockEntity.State is not "locked" &&  
            lockToWork.HomeAssistantLockEntity.State is not "locking")
        {
            _logger.Error("Could not lock {LockName} even with retries", lockToWork.Name);
        }
    }

    private void LogWarningIfLockRemainsUnlocked(AutomaticallyLockableLock lockToWork, DateTimeOffset currentTime)
    {
        if (lockToWork.LastUnlockedAtTime < currentTime - TimeSpan.FromMinutes(10) &&
            lockToWork.IsUnlocked &&
            lockToWork.DisabledUntil < currentTime - TimeSpan.FromMinutes(2))
        {
            _logger.Warning("Lock {Name} hasn't been locked since {UnlockTime}",
                lockToWork.Name,
                lockToWork.LastUnlockedAtTime);
        }
    }

    private void ResetWhenLocked(AutomaticallyLockableLock lockToWork, DateTimeOffset currentTime)
    {
        // Reset this if lock is locked
        if (lockToWork.IsLocked)
        {
            lockToWork.AutoLockControllerActive = false;
            lockToWork.DisabledUntil = currentTime;
        }
    }

    private async Task LockWithRetriesAsync(AutomaticallyLockableLock lockToWork, int timeoutBetweenRetries)
    {
        if (!LockIsEnabled(lockToWork)) return;
        
        var retriesCountdown = 4;
        
        while (lockToWork.HomeAssistantLockEntity.State is not "locked" && 
               lockToWork.HomeAssistantLockEntity.State is not "locking")
        {
            retriesCountdown--;
                
            lockToWork.HomeAssistantLockEntity.Lock();

            await Task.Delay(timeoutBetweenRetries);
            
            if (retriesCountdown <= 1)
            {
                if (!DisabledLockNames.Contains(lockToWork.Name))
                    DisabledLockNames.Add(lockToWork.Name);
                
                _logger.Error("Couldn't lock {LockName} with retries", lockToWork.Name);
            }
        }
    }

    private async Task UnlockWithRetriesAsync(AutomaticallyLockableLock lockToWork, int timeoutBetweenRetries)
    {
        if (!LockIsEnabled(lockToWork)) return;
        
        var retriesCountdown = 4;
        
        while (lockToWork.HomeAssistantLockEntity.State is not "locked" && 
               lockToWork.HomeAssistantLockEntity.State is not "locking")
        {
            retriesCountdown--;
                
            lockToWork.HomeAssistantLockEntity.Unlock();

            await Task.Delay(timeoutBetweenRetries);
            
            if (retriesCountdown <= 1)
            {
                if (!DisabledLockNames.Contains(lockToWork.Name))
                    DisabledLockNames.Add(lockToWork.Name);
                
                _logger.Error("Couldn't unlock {LockName} with retries", lockToWork.Name);
            }
        }
    }

    private bool LockIsEnabled(AutomaticallyLockableLock lockToWork)
    {
        // Make sure lock we're working isn't disabled
        foreach (var disabledLockName in DisabledLockNames)
        {
            _logger.Information("Checking disabled lock strings: {WorkingLockName} against disabled string: {CurrentDisabledLockName}", lockToWork.Name, disabledLockName);
            
            if (disabledLockName != lockToWork.Name) continue;
            
            _logger.Error("Can't attempt to operate on {LockName} as it is disabled", lockToWork.Name);

            NotifyAboutDisabledLocks();
            
            return false;
        }

        return true;
    }

    private void NotifyAboutDisabledLocks()
    {
        // Debug:
        // foreach (var disabledLockName in DisabledLockNames)
        // {
        //     _logger.Information("Iterating DisabledLockNames: {LockName}", DisabledLockNames);   
        // }
        //
        // foreach (var disabledLockName in _previousDisabledLockNames)
        // {
        //     _logger.Information("Iterating PreviousDisabledLockNames: {LockName}", DisabledLockNames);
        // }
        
        foreach (var disabledLockName in DisabledLockNames)
        {
            if (_previousDisabledLockNames.Contains(disabledLockName)) continue;

            var nl = Environment.NewLine + Environment.NewLine;

            _textNotifier.NotifyUsersInHaExcept(
                "Lock Disabled",
                $"Lock disabled: {disabledLockName}{nl}Please check logs and restart netdaemon to re-enable.{nl}This is usually a mechanical problem with the lock or a problem with the lock bridge.",
                new List<WhoToNotify>(){WhoToNotify.Alyssa});
        }

        _previousDisabledLockNames = DisabledLockNames;
    }

    private void LogDebugLockState(AutomaticallyLockableLock lockToWork)
    {
        var message = $@"
Debug for: {lockToWork.HomeAssistantLockEntity.EntityId}

Current state: {lockToWork.HomeAssistantLockEntity.State}
Lock at time: {lockToWork.LockAfterDuration}
Disabled until: {lockToWork.DisabledUntil}
Lock at: {lockToWork.LockAtTime}
";
        
        _logger.Debug("{DebugMessage}", message);
    }
}