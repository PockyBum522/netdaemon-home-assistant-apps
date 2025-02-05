using System.ComponentModel;

namespace AllenStreetNetDaemonApps.Apps.WasherDryerNotifier;

[NetDaemonApp]
public class WasherDryerNotifier
{
    private readonly Serilog.Core.Logger _logger;
    private readonly Entities _entities;

    private DateTimeOffset _washerStartedAt = DateTimeOffset.MaxValue;

    private int _washerErrorCount; 
    
    private WasherState _washerState;
    
    // Transition thresholds
    private const int _washerOffWattThreshold = 3;
    private const int _washerDryingWattThreshold = 500;
    private const int _washerStayFreshWattThreshold = 20;

    private const int _historyMaximum = 10;
    private readonly List<double> _washerPowerUsageHistory = [];

    private enum WasherState
    {
        Uninitialized,
        Off,
        Washing,
        Drying,
        StayFresh,
        Problem
    }
    
    public WasherDryerNotifier(IHaContext ha, INetDaemonScheduler scheduler)
    {
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();

        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        scheduler.RunEvery(TimeSpan.FromSeconds(30), checkCurrentWasherPowerUsage); 
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
    }

    private void checkCurrentWasherPowerUsage()
    {
        var washerCurrentWattsRaw = _entities.Sensor.WasherDryerComboMainPowerElectricConsumptionW.State;
        
        if (washerValueInvalid(washerCurrentWattsRaw)) return;
        
        if (washerCurrentWattsRaw is null) return;
        var washerWatts = (double)washerCurrentWattsRaw;
        
        updateHistory(washerWatts);
        
        var averagedValue = _washerPowerUsageHistory.AsQueryable().Average();

        handleWasherStateUpdate(averagedValue);
    }

    private void updateHistory(double washerWatts)
    {
        _washerPowerUsageHistory.Add(washerWatts);

        if (_washerPowerUsageHistory.Count > _historyMaximum)
        {
            _washerPowerUsageHistory.RemoveAt(0);
        }

        _logger.Verbose("Washer power usage history count: {HistoryCount}", _washerPowerUsageHistory.Count);

        _logger.Verbose("Washer power usage history: {@WasherPowerUsageHistory}", _washerPowerUsageHistory);

        var averagedValue = _washerPowerUsageHistory.AsQueryable().Average();
        
        _logger.Debug("Washer power usage history AVERAGE: {Average} out of {Count} values", averagedValue.ToString("F2"), _washerPowerUsageHistory.Count);
    }

    // ReSharper disable once CognitiveComplexity because it's a state machine. It's simple, the linter just is dumb.
    private void handleWasherStateUpdate(double washerWatts)
    {
        switch (_washerState)
        {
            case WasherState.Uninitialized:
                
                // Wait for 20 reads when Uninitialized before handling state so we have an accurate idea of what's going on:
                if (_washerPowerUsageHistory.Count < _historyMaximum) return;
                
                // Triggers:
                
                //      Uninitialized => Off = Washer average <= 3w
                if (washerWatts <= _washerOffWattThreshold) stateChangeTo(WasherState.Off);
                
                //      Uninitialized => StayFresh = Washer average > 3w && <= 20w
                if (washerWatts is > _washerOffWattThreshold and
                                   <= _washerStayFreshWattThreshold)
                {
                    _washerStartedAt = DateTimeOffset.Now;
                    
                    stateChangeTo(WasherState.StayFresh);
                }  
                
                //      Uninitialized => Washing = Washer average > 20w && <= 500w
                if (washerWatts is > _washerStayFreshWattThreshold and 
                                   <= _washerDryingWattThreshold)
                {
                    _washerStartedAt = DateTimeOffset.Now;
                    
                    stateChangeTo(WasherState.Washing);
                }  
                
                //      Uninitialized => Drying = Washer average > 500w
                if (washerWatts > _washerDryingWattThreshold)
                {
                    _washerStartedAt = DateTimeOffset.Now;
                    
                    stateChangeTo(WasherState.Drying);
                }
                
                break;
            
            case WasherState.Off:
                
                //      Off => Washing = Washer average > 3w
                if (washerWatts > _washerOffWattThreshold)
                {
                    _washerStartedAt = DateTimeOffset.Now;
                    
                    stateChangeTo(WasherState.Washing);
                }
                
                break;
            
            case WasherState.Washing:
                
                _logger.Debug("Washer has been running for: {ElapsedTime}", DateTimeOffset.Now - _washerStartedAt);
                
                // Triggers:
                
                //      Washing => Drying = Washer average >= 500w
                if (washerWatts >= _washerDryingWattThreshold) stateChangeTo(WasherState.Drying);
                
                //      Washing => Problem = Washer average < 3w
                if (washerWatts < _washerOffWattThreshold) stateChangeTo(WasherState.Problem);
                
                break;
            
            case WasherState.Drying:

                _logger.Debug("Washer has been running for: {ElapsedTime}", DateTimeOffset.Now - _washerStartedAt);

                // Triggers:
                
                //      Drying => StayFresh = Washer average < 20w
                if (washerWatts < _washerStayFreshWattThreshold) stateChangeTo(WasherState.StayFresh);
                
                break;
            
            case WasherState.StayFresh:
                
                _logger.Debug("LOAD IS FINISHED, PLEASE UNLOAD WASHER!");
                
                // Triggers:
                
                //      StayFresh => Off = Washer average < 3w
                if (washerWatts < _washerOffWattThreshold)
                {
                    _washerStartedAt = DateTimeOffset.MaxValue;
                    
                    stateChangeTo(WasherState.Off);
                }
                
                break;
            
            case WasherState.Problem:
                
                _logger.Debug("WASHER HAS A PROBLEM HALLLLLLLLP");
                
                // Triggers:
                
                //      Problem => Washing = Washer average > 3w
                if (washerWatts > _washerOffWattThreshold) stateChangeTo(WasherState.Washing);
                
                //      Problem => Off = 24h Timeout
                if (_washerStartedAt < DateTimeOffset.Now - TimeSpan.FromHours(24))
                {
                    _logger.Warning("Washer was in WasherState.Problem, but load started more than 24h ago so timing out and moving to WasherState.Off");
                    
                    stateChangeTo(WasherState.Off);
                }
                
                break;
            
            default:
                throw new InvalidEnumArgumentException(
                    $"{nameof(_washerState)} in {nameof(handleWasherStateUpdate)} was not a handled state (Did you add something to the enum recently?)");
        }
    }

    private void stateChangeTo(WasherState newState)
    {
        var oldStateName = Enum.GetName(_washerState.GetType(), _washerState);
        var newStateName = Enum.GetName(newState.GetType(), newState);
        
        _logger.Debug("STATE: {OldStateName} => {NewStateName}", oldStateName, newStateName);
        
        _washerState = newState;
    }

    private bool washerValueInvalid(double? washerCurrentWattsRaw)
    {
        if (_washerErrorCount > 10)
        {
            _washerErrorCount = 0;
            _logger.Fatal("Washer returned bad info more than 10 times in a row");
        }

        if (washerCurrentWattsRaw is null or < -1)
        {
            _logger.Error("Washer power usage is null or less than -1, skipping");
            _washerErrorCount++;
            return true;
        }

        return false;
    }
}
