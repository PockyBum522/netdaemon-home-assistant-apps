using System;
using HomeAssistantGenerated;

namespace NetdaemonApps.apps.DoorsLockAfterTimePeriod.Models;

public class AutomaticallyLockableLock
{
    public AutomaticallyLockableLock(TimeSpan lockAfterDuration, LockEntity homeAssistantLockEntity)
    {
        LockAfterDuration = lockAfterDuration;
        HomeAssistantLockEntity = homeAssistantLockEntity;

        DisabledUntil = DateTimeOffset.Now;
        LastUnlockedAtTime = DateTimeOffset.Now;
        LockAtTime = DateTimeOffset.Now + TimeSpan.FromSeconds(10);

        AutoLockControllerActive = true;
    }
    
    public LockEntity HomeAssistantLockEntity { get; }
    public string Name => HomeAssistantLockEntity.EntityId;
    public TimeSpan LockAfterDuration { get; }
    public bool IsLocked => HomeAssistantLockEntity.State == "locked";
    public bool IsUnlocked => !IsLocked;
    public DateTimeOffset LockAtTime { get; set; } 
    public DateTimeOffset LastUnlockedAtTime { get; set; } 
    public DateTimeOffset DisabledUntil { get; set; } 
    public bool AutoLockControllerActive { get; set; }
}
