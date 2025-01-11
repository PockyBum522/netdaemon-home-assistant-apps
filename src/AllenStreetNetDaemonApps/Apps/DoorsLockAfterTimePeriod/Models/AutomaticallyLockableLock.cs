namespace AllenStreetNetDaemonApps.Apps.DoorsLockAfterTimePeriod.Models;

public class AutomaticallyLockableLock(ILogger logger, TimeSpan autoLockDuration, LockEntity homeAssistantLockEntity)
{
    // Injected
    private ILogger _logger { get; } = logger;

    // Properties
    public LockEntity HomeAssistantLockEntity { get; } = homeAssistantLockEntity;
    public string Name => HomeAssistantLockEntity.EntityId;
    public bool IsLocked => GetLockedState();
    
    public DateTimeOffset LockAtTime { get; private set; } = DateTimeOffset.Now + autoLockDuration;
    public DateTimeOffset LastUnlockedAtTime { get; private set; } = DateTimeOffset.Now;

    public bool Failed { get; private set; }

    public bool AutoLockActive { get; private set; } = true;
    public TimeSpan AutoLockDuration { get; } = autoLockDuration;
    
    /// <summary>
    /// Put this on a scheduler to be called frequently
    /// </summary>
    public async Task CheckIfShouldAutoLock()
    {
        _logger.Verbose("Checking if {Name} should auto-lock: (Now:{Now} > LockAt:{LockAtTime}): {LogicalResult}",
            Name, DateTimeOffset.Now.GetTimeOnly(), LockAtTime.GetTimeOnly(), DateTimeOffset.Now > LockAtTime);
        
        if (IsLocked) return;

        if (IsManuallyDisabled("Could not attempt to operate, lock manually disabled")) return;
        
        _logger.Debug("Checking if {Name} should auto-lock: (Now:{Now} > LockAt:{LockAtTime}): {LogicalResult}",
            Name, DateTimeOffset.Now.GetTimeOnly(), LockAtTime.GetTimeOnly(), DateTimeOffset.Now > LockAtTime);
        
        if (LockAtTime < DateTimeOffset.Now)
        {
            await Lock();
        }
    }
    
    /// <summary>
    /// Locks the lock
    /// </summary>
    public async Task Lock()
    {
        HomeAssistantLockEntity.Lock();

        if (IsLocked) return;
        
        await Task.Delay(5);

        if (IsLocked)
        {
            _logger.Debug("Lock {Name} was locked successfully", Name);
        }
        
        // SetLockAsFailed($"Could not lock {Name} even after retries");
    }

    /// <summary>
    /// Unlocks the lock
    /// </summary>
    public async Task Unlock()
    {
        HomeAssistantLockEntity.Unlock();

        if (!IsLocked) return;
            
        await Task.Delay(5);
        
        if (!IsLocked)
        {
            _logger.Debug("Lock {Name} was unlocked successfully", Name);
            
            LastUnlockedAtTime = DateTimeOffset.Now;
        }
        
        //SetLockAsFailed($"Could not unlock {Name} even after retries");
    }

    /// <summary>
    /// Adds the lock's AutoLockDuration to the current LockAtTime
    ///
    /// If LockAtTime is in the past when this is called, LockAtTime will be set to now, then AutoLockDuration will be added
    /// </summary>
    public void AddAutoLockTime()
    {
        if (LockAtTime < DateTimeOffset.Now)
            LockAtTime = DateTimeOffset.Now;

        // Was adding too much, too fast, let's try less of the time we would otherwise add
        LockAtTime += (AutoLockDuration / 3);

        // Check if we're over the max time until locking, which is AutoLockDuration * 5 in the future
        var timeSpanUntilLocking = LockAtTime - DateTimeOffset.Now;

        var maxTimeUntilLocking = AutoLockDuration * 3;

        if (timeSpanUntilLocking > maxTimeUntilLocking)
        {
            LockAtTime = DateTimeOffset.Now + maxTimeUntilLocking;
        }
        
        _logger.Debug("Adding time to auto-lock for {Name}, will now auto-lock at: {NewTime}", Name, LockAtTime.GetTimeOnly());
    }

    /// <summary>
    /// For disabling auto-lock when we want the lock to stay unlocked until we re-enable this 
    /// </summary>
    public void DisableAutoLock()
    {
        AutoLockActive = false;
        
        _logger.Debug("Disabled auto-lock for {Name}", Name);
    }

    /// <summary>
    /// For enabling auto-lock when we want the lock to be able to lock automatically after a timeout again 
    /// </summary>
    public void EnableAutoLock()
    {
        AutoLockActive = true;

        _logger.Verbose("Enabled auto-lock for {Name}", Name);
    }

    private bool IsManuallyDisabled(string message)
    {
        if (!AutoLockActive)
        {
            _logger.Debug("{Message} {Name}, is manually disabled", message, Name);
            return true;
        }

        return false;
    }

    private void SetLockAsFailed(string message)
    {
        _logger.Error("{Message}", message);

        Failed = true;

        _logger.Debug("{Name} now set as failed", Name);
    }

    private bool GetLockedState()
    {
        switch (HomeAssistantLockEntity.State)
        {
            case "locked" or "locking":
                return true;
            
            case "unlocked" or "unlocking":
                return false;
        }

        SetLockAsFailed($"Lock in unknown state when checking if locked/unlocked. Lock: {Name} was: {HomeAssistantLockEntity.State}");

        return false;
    }
}
