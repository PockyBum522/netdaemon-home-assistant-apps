namespace AllenStreetNetDaemonApps.Apps.Scheduled;

[NetDaemonApp]
public class HomeAssistantDashboardSwitchResetter
{
    private readonly IHaContext _ha;
    private readonly INetDaemonScheduler _scheduler;
    private readonly ILogger _logger;
    private readonly Entities _entities;

    private List<Entity> _switchesToReset = new();

    private List<KeyValuePair<string, bool>> _lastSwitchStates = new();
    
    public HomeAssistantDashboardSwitchResetter(IHaContext ha, INetDaemonScheduler scheduler)
    {
        _ha = ha;
        _scheduler = scheduler;

        _entities = new Entities(_ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();

        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        _scheduler = scheduler;
        
        _scheduler.RunIn(TimeSpan.FromSeconds(10), GetAllSwitches);
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
    }

    private void GetAllSwitches()
    {
        _switchesToReset.Add(_entities.Switch.TvMediaPcWol);
        _switchesToReset.Add(_entities.Switch.DavidDesktopWol);
        _switchesToReset.Add(_entities.Switch.AlyssaDesktopWol);
        _switchesToReset.Add(_entities.Switch.GuestBedroomMediaPcWol);
        
        _scheduler.RunEvery(TimeSpan.FromSeconds(5), CheckAllScheduledTasks);
    }

    private void CheckAllScheduledTasks()
    {
        _logger.Debug("Starting: {ThisMethodName}", nameof(CheckAllScheduledTasks));
        
        var currentTime = TimeOnly.FromDateTime(DateTimeOffset.Now.DateTime);

        _logger.Debug("Current time gotten: {CurrentTime}", currentTime);
        
        var currentState = GetSwitchStates();

        // If nothing's on, stop
        if (_lastSwitchStates.Any(x => x.Value))
        {
            _logger.Debug("Checking state: {@States}", currentState);
            _logger.Debug("VS last state: {@LastState}", _lastSwitchStates);
            
            // If nothing's changed in the last 5 seconds...
            if (AreListsOfKeyValuesEqual(_lastSwitchStates, currentState))
            {
                _logger.Information("Tuning off all switches!!");

                foreach (var switchEntity in _switchesToReset)
                {
                    var switchConverted = (SwitchEntity)switchEntity;
                    switchConverted.TurnOff();
                }
            }
        }
        
        // Update last state for next run's check:
        _lastSwitchStates = currentState;
    }

    private List<KeyValuePair<string, bool>> GetSwitchStates()
    {
        var switchStates = new List<KeyValuePair<string, bool>>();

        foreach (var switchEntity in _switchesToReset)
        {
            _logger.Verbose("Adding switch state: {Name} is {State}", switchEntity.EntityId, switchEntity.State);
            
            switchStates.Add(
                new KeyValuePair<string, bool>(
                        switchEntity.EntityId,
                        switchEntity.State is not ("off" or "unavailable")));
        }
     
        _logger.Debug("Got all switch states: {@Switches}", switchStates);
        
        return switchStates;
    }

    private bool AreListsOfKeyValuesEqual(
        List<KeyValuePair<string, bool>> list1,
        List<KeyValuePair<string, bool>> list2)
    {
        foreach (var pair in list1)
        {
            // ReSharper disable once UsageOfDefaultStructEquality because this is fine as I care about readability over performance here.
            if (!list2.Contains(pair)) return false;
        }

        return true;
    }
}