using AllenStreetNetDaemonApps.Apps.DoorsLockAfterTimePeriod.Models;

namespace AllenStreetNetDaemonApps.Apps.DoorsLockAfterTimePeriod;

/// <summary>
/// This gets set up in MqttWatcher.cs
/// </summary>
public class LockController
{
    internal AutomaticallyLockableLock FrontDoorLock { get; }
    internal AutomaticallyLockableLock BackDoorLock { get; }
    
    private string _frontLockLastDisableText = "";
    private string _backLockLastDisableText = "";

    private readonly ILogger _logger;
    private readonly Entities _entities;

    public LockController(IHaContext ha, INetDaemonScheduler scheduler)
    {
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();

        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);

        var textNotifier = new TextNotifier(_logger, ha);

        FrontDoorLock = new AutomaticallyLockableLock(_logger, textNotifier, TimeSpan.FromMinutes(3), _entities.Lock.BluetoothFrontDoorLock2);
        BackDoorLock = new AutomaticallyLockableLock(_logger, textNotifier, TimeSpan.FromMinutes(6), _entities.Lock.BluetoothBackDoorLock2);
        
        scheduler.RunIn(TimeSpan.FromSeconds(1), InitializeLockDisableForHours);

        // Set schedule for initial runs of things
        scheduler.RunIn(TimeSpan.FromSeconds(3), async () => await WorkLocks());

        // Set recurring runs for door lock/door state related stuff
        scheduler.RunEvery(TimeSpan.FromSeconds(10), async () => await WorkLocks());

        scheduler.RunEvery(TimeSpan.FromSeconds(1), async () => await WorkLockDisableTimes());
        
        scheduler.RunEvery(TimeSpan.FromSeconds(1), async () => await CheckFrontDoor());
        scheduler.RunEvery(TimeSpan.FromSeconds(1), async () => await CheckBackDoor());

        scheduler.RunEvery(TimeSpan.FromSeconds(36), DecrementLockTimeoutTextboxes);
    }

    private async Task CheckFrontDoor()
    {
        // if ((_entities.BinarySensor.FrontDoorOpen.State ?? "closed").ToLower() == "open" ||
        if ((_entities.BinarySensor.FrontDoorReedSwitch.State ?? "off").ToLower() == "on")
        {
            // If the door is open then we're done with these
            RelatchFrontDoorLatches();
            
            // If the door is open, we'd better make damn sure the deadbolt retracts so it doesn't slam into the door frame latch when the door shuts
            if (FrontDoorLock.IsLocked) await FrontDoorLock.Unlock();
            
            FrontDoorLock.AddAutoLockTime();

            _logger.Information("Front door opened as detected in {ThisName}, adding time, door will now lock at: {NewTime}", nameof(CheckFrontDoor), FrontDoorLock.LockAtTime.GetTimeOnly());
            
            // Not sure if this will keep the scheduled task from relaunching before this is done, but that's the intent
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    private async Task CheckBackDoor()
    {
        //if ((_entities.BinarySensor.BackDoorOpen.State ?? "closed").ToLower() == "open" ||
        if ((_entities.BinarySensor.BackDoorReedSwitch.State ?? "off").ToLower() == "on")
        {
            BackDoorLock.AddAutoLockTime();
            
            _logger.Information("Back door opened as detected in {ThisName}, adding time, door will now lock at: {NewTime}", nameof(CheckBackDoor), BackDoorLock.LockAtTime.GetTimeOnly());

            // Not sure if this will keep the scheduled task from relaunching before this is done, but that's the intent
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    private async Task WorkLocks()
    {
        await FrontDoorLock.CheckIfShouldAutoLock();
        await BackDoorLock.CheckIfShouldAutoLock();
    }

    private void InitializeLockDisableForHours()
    {
        _entities.InputText.FrontDoorLockDisableHours.SetValue("0.00");
        _entities.InputText.BackDoorLockDisableHours.SetValue("0.00");

        _frontLockLastDisableText = "0.00";
        _backLockLastDisableText = "0.00";
        
        _logger.Information("Set lock disable textboxes to 0.00");
    }

    private async Task WorkLockDisableTimes()
    {
        var frontLockDisableText = _entities.InputText.FrontDoorLockDisableHours.EntityState?.State ?? "0.00";
        var backLockDisableText = _entities.InputText.BackDoorLockDisableHours.EntityState?.State ?? "0.00";

        if (decimal.Parse(frontLockDisableText) < 0.0001m) FrontDoorLock.EnableAutoLock();
        if (decimal.Parse(backLockDisableText) < 0.0001m) BackDoorLock.EnableAutoLock();
        
        if (frontLockDisableText == "0.00" &&
            backLockDisableText == "0.00") return;
        
        var frontLockDisableValue = decimal.Parse(frontLockDisableText ?? "0.00");
        var backLockDisableValue = decimal.Parse(backLockDisableText ?? "0.00");
        
        if (_frontLockLastDisableText != frontLockDisableText)
        {
            _logger.Debug("User changed front door lock disable textbox in HA dashboard from {OldValue} to {NewValue}", 
                _frontLockLastDisableText, frontLockDisableText);

            // Format whatever the user put in to 2 decimal places
            var newText = frontLockDisableValue.ToString("F2");
            
            _entities.InputText.FrontDoorLockDisableHours.SetValue(newText);
            
            // Update this since we know a user changed the text
            _frontLockLastDisableText = newText;

            FrontDoorLock.AddAutoLockTime();
            
            await Task.Delay(1000);
            
            FrontDoorLock.DisableAutoLock();
            
            await Task.Delay(1000);
            
            await FrontDoorLock.Unlock();
            
            await Task.Delay(1000);
        }
        
        if (_backLockLastDisableText != backLockDisableText)
        {
            _logger.Debug("User changed back door lock disable textbox in HA dashboard from {OldValue} to {NewValue}", 
                _backLockLastDisableText, backLockDisableText);

            // Format whatever the user put in to 2 decimal places
            var newText = backLockDisableValue.ToString("F2");
            
            _entities.InputText.BackDoorLockDisableHours.SetValue(newText);
            
            // Update this since we know a user changed the text
            _backLockLastDisableText = newText;
            
            BackDoorLock.AddAutoLockTime();
            
            await Task.Delay(1000);
            
            BackDoorLock.DisableAutoLock();
            
            await Task.Delay(1000);
            
            await BackDoorLock.Unlock();
            
            await Task.Delay(1000);
        }
    }
    
    // 16:32 set @ 0.1 = 6 minutes 
    private void RelatchFrontDoorLatches()
    {
        var deadboltLatch = _entities.Switch.DeadboltDoorframeLatch;
        var doorknobLatch = _entities.Switch.DoorknobDoorframeLatch;

        deadboltLatch.TurnOff();
        doorknobLatch.TurnOff();

        _logger.Debug("Front door latches relatched");
    }
    
    private void DecrementLockTimeoutTextboxes()
    {
        var frontLockDisableText = _entities.InputText.FrontDoorLockDisableHours.EntityState?.State;
        var backLockDisableText = _entities.InputText.BackDoorLockDisableHours.EntityState?.State;

        _logger.Verbose("frontLockDisableText: {FrontLockText} | backLockDisableText: {BackLockText}", frontLockDisableText, backLockDisableText);
        
        if (frontLockDisableText == "0.00" &&
            backLockDisableText == "0.00") return;
        
        var frontLockDisableValue = decimal.Parse(frontLockDisableText ?? "0.00");
        var backLockDisableValue = decimal.Parse(backLockDisableText ?? "0.00");
        
        _logger.Debug("frontLockDisableValue: {FrontLockValue} | backLockDisableValue: {BackLockValue}", frontLockDisableValue, backLockDisableValue);
        
        if (frontLockDisableValue > 0.0001m)
        {
            frontLockDisableValue -= 0.01m;

            if (frontLockDisableValue < 0m)
                frontLockDisableValue = 0m;

            var newText = frontLockDisableValue.ToString("F2");

            // Update this so our code knows this change wasn't due to a user
            _frontLockLastDisableText = newText;
            
            _entities.InputText.FrontDoorLockDisableHours.SetValue(newText);
            
            _logger.Debug("Decremented front door lock textbox to: {NewValue}", newText);
        }

        if (backLockDisableValue > 0.0001m)
        {
            backLockDisableValue -= 0.01m;

            if (backLockDisableValue < 0m)
                backLockDisableValue = 0m;

            var newText = backLockDisableValue.ToString("F2");

            // Update this so our code knows this change wasn't due to a user
            _backLockLastDisableText = newText;
            
            _entities.InputText.BackDoorLockDisableHours.SetValue(newText);
            
            _logger.Debug("Decremented back door lock textbox to: {NewValue}", newText);
        }
    }
}