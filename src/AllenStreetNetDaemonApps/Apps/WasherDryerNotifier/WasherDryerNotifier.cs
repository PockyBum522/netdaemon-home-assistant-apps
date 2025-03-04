using System.ComponentModel;
using System.Diagnostics;

namespace AllenStreetNetDaemonApps.Apps.WasherDryerNotifier;

[NetDaemonApp]
public class WasherDryerNotifier
{
    private readonly Serilog.Core.Logger _logger;
    private readonly Entities _entities;
    private readonly TextNotifier _textNotifier;
    
    private DateTimeOffset _washerStartedAt = DateTimeOffset.MaxValue;

    private int _washerErrorCount; 
    
    private WasherState _washerState;

    private Stopwatch _loadStopwatch = new();
    private bool _hadProblem;
    
    // Transition thresholds
    private const int _washerOffWattThreshold = 3;
    private const int _washerDryingWattThreshold = 1200;
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
        
        _textNotifier = new TextNotifier(_logger, ha);
        
        scheduler.RunEvery(TimeSpan.FromSeconds(30), checkCurrentWasherPowerUsage); 
        
        scheduler.RunEvery(TimeSpan.FromSeconds(1), updateWasherCountdown); 
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
    }

    private void updateWasherCountdown()
    {
        var unknownMessage = "unknown";
        var problemMessage = "PROBLEM - Check Laundry!!";
        var finishedMessage = "Finished!";
        
        var timeUntilOff = TimeSpan.FromMinutes(-1201);
        
        if (_washerState == WasherState.Washing &&
            _washerStartedAt != DateTimeOffset.MaxValue)
        {
            timeUntilOff = (_washerStartedAt + TimeSpan.FromHours(5) + TimeSpan.FromMinutes(30)) - DateTimeOffset.Now;    
        }
        
        var countdownMessage = $"{timeUntilOff.Hours:d1}h {timeUntilOff.Minutes:d2}m {timeUntilOff.Seconds:d2}s";
        
        // _logger.Debug("Time until off: {TimeUntilOff}, Minutes only: {TimeUntilMinutes} | countdownMessage: {CountdownMessage}", 
        //     timeUntilOff, timeUntilOff.TotalMinutes, countdownMessage);
        
        if (timeUntilOff.TotalMinutes is < -1200 or > 1200 ||
            _washerState == WasherState.Uninitialized)
        {
            //_logger.Information("_entities.InputText.LaundryCountdown.State {WasherString}", _entities.InputText.LaundryCountdown.State);
            
            if (_entities.InputText.LaundryCountdown.State != unknownMessage)
                _entities.InputText.LaundryCountdown.SetValue(unknownMessage);
            
            return;
        }

        switch (_washerState)
        {
            case WasherState.Problem:
                
                _hadProblem = true;
                
                if (_entities.InputText.LaundryCountdown.State != problemMessage)
                    _entities.InputText.LaundryCountdown.SetValue(problemMessage);
                
                break;
            
            case WasherState.StayFresh:
                if (_entities.InputText.LaundryCountdown.State != finishedMessage)
                    _entities.InputText.LaundryCountdown.SetValue(finishedMessage);
                
                break;
            
            case WasherState.Off:
                if (timeUntilOff.TotalMinutes < -1200)
                {
                    if (_entities.InputText.LaundryCountdown.State != unknownMessage)
                        _entities.InputText.LaundryCountdown.SetValue(unknownMessage);
                    
                    break;
                }
                
                if (_entities.InputText.LaundryCountdown.State != finishedMessage)
                    _entities.InputText.LaundryCountdown.SetValue(finishedMessage);
                
                break;
            
            case WasherState.Washing:
                _entities.InputText.LaundryCountdown.SetValue($"Washing: {countdownMessage}");
                break;
            
            case WasherState.Drying:
                _entities.InputText.LaundryCountdown.SetValue($"Drying: {countdownMessage}");
                break;
            
            default:
                _entities.InputText.LaundryCountdown.SetValue("Switch Error");
                break;
        }
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
                
                // Make sure first value isn't 0, otherwise we might have not enough data to be accurate on uninitialized
                if (_washerPowerUsageHistory.FirstOrDefault() > -1 &&
                    _washerPowerUsageHistory.FirstOrDefault() < 1) return;
                
                // Triggers:
                
                //      Uninitialized => Off = Washer average <= 3w
                if (washerWatts <= _washerOffWattThreshold) changeStateTo(WasherState.Off);
                
                //      Uninitialized => StayFresh = Washer average > 3w && <= 20w
                if (washerWatts is > _washerOffWattThreshold and
                                   <= _washerStayFreshWattThreshold)
                {
                    _washerStartedAt = DateTimeOffset.Now;
                    
                    changeStateTo(WasherState.StayFresh);
                }  
                
                //      Uninitialized => Washing = Washer average > 20w && <= 500w
                if (washerWatts is > _washerStayFreshWattThreshold and 
                                   <= _washerDryingWattThreshold)
                {
                    startLoadWork();
                    
                    changeStateTo(WasherState.Washing);
                }  
                
                //      Uninitialized => Drying = Washer average > 500w
                if (washerWatts > _washerDryingWattThreshold)
                {
                    _washerStartedAt = DateTimeOffset.Now;
                    
                    changeStateTo(WasherState.Drying);
                }
                
                break;
            
            case WasherState.Off:
                
                //      Off => Washing = Washer average > 3w
                if (washerWatts > _washerOffWattThreshold)
                {
                    _washerStartedAt = DateTimeOffset.Now;
                    
                    changeStateTo(WasherState.Washing);
                }
                
                break;
            
            case WasherState.Washing:
                
                _logger.Debug("Washer has been running for: {ElapsedTime}", DateTimeOffset.Now - _washerStartedAt);
                
                // Filter. Wait for 4 minutes after start before allowing transitions to other states
                // This is so it can't switch to washing then immediately to off just in case there is a low power usage briefly
                if (_washerStartedAt + TimeSpan.FromMinutes(4) < DateTimeOffset.Now)
                {
                    _logger.Information("Washer started less than 4 minutes ago, preventing transition to other states until then");
                    break;
                }
                
                // Triggers:
                
                //      Washing => Drying = Washer average >= 500w
                if (washerWatts >= _washerDryingWattThreshold) changeStateTo(WasherState.Drying);
                
                //      Washing => Problem = Washer average < 3w
                if (washerWatts < _washerOffWattThreshold) changeStateTo(WasherState.Problem);
                
                break;
            
            case WasherState.Drying:

                _logger.Debug("Washer has been running for: {ElapsedTime}", DateTimeOffset.Now - _washerStartedAt);

                // Triggers:
                
                //      Drying => StayFresh = Washer average < 20w
                if (washerWatts < _washerStayFreshWattThreshold) changeStateTo(WasherState.StayFresh);
                
                break;
            
            case WasherState.StayFresh:
                
                _logger.Debug("LOAD IS FINISHED, PLEASE UNLOAD WASHER!");
                
                // Triggers:
                
                //      StayFresh => Off = Washer average < 3w
                if (washerWatts < _washerOffWattThreshold)
                {
                    _washerStartedAt = DateTimeOffset.MaxValue;

                    changeStateTo(WasherState.Off);
                }
                
                break;
            
            case WasherState.Problem:
                
                _logger.Debug("WASHER HAS A PROBLEM HALLLLLLLLP");

                // Triggers:
                
                //      Problem => Washing = Washer average > 3w
                if (washerWatts > _washerOffWattThreshold) changeStateTo(WasherState.Washing);
                
                //      Problem => Off = 24h Timeout
                if (_washerStartedAt < DateTimeOffset.Now - TimeSpan.FromHours(24))
                {
                    _logger.Warning("Washer was in WasherState.Problem, but load started more than 24h ago so timing out and moving to WasherState.Off");
                 
                    changeStateTo(WasherState.Off);
                }
                
                break;
            
            default:
                throw new InvalidEnumArgumentException(
                    $"{nameof(_washerState)} in {nameof(handleWasherStateUpdate)} was not a handled state (Did you add something to the enum recently?)");
        }
    }

    private void startLoadWork()
    {
        _hadProblem = false;
        
        _loadStopwatch.Restart();
                        
        _washerStartedAt = DateTimeOffset.Now;
    }

    private void changeStateTo(WasherState newState)
    {
        var oldStateName = Enum.GetName(_washerState.GetType(), _washerState);
        var newStateName = Enum.GetName(newState.GetType(), newState);
        
        _logger.Debug("STATE: {OldStateName} => {NewStateName}", oldStateName, newStateName);

        var lastState = _washerState;
        
        _washerState = newState;

        // Handle notification actions on transition to new state
        switch (_washerState)
        {
            case WasherState.Off:
                
                // Notification if last state was drying
                if (lastState == WasherState.Drying)
                {
                    //_textNotifier.NotifyAll("Laundry Finished", "Laundry load is now dry. Please unload the washer!");
                    _textNotifier.NotifyDavid("Laundry Finished", "Laundry load is now dry. Please unload the washer!");    
                }
                
                // Notification if last state was problem
                if (lastState == WasherState.Problem)
                {
                    //_textNotifier.NotifyAll("PROBLEM - Laundry Issue", "Laundry load seems like it didn't drain properly. Please check and fix.");
                    _textNotifier.NotifyDavid("PROBLEM - Laundry Issue", "Laundry load turned off after having a problem. Please check and fix.");
                }

                break;
            
            case WasherState.StayFresh:
                
                // Filter so we only send notification if last state was drying
                if (lastState != WasherState.Drying) break;
                
                //_textNotifier.NotifyAll("Laundry Finished", "Laundry load is now dry. Please unload the washer!");
                _textNotifier.NotifyDavid("Laundry Finished", "Laundry load is now dry. Please unload the washer!");  
                break;
            
            case WasherState.Problem:
                
                //_textNotifier.NotifyAll("PROBLEM - Laundry Issue", "Laundry load seems like it didn't drain properly. Please check and fix.");
                _textNotifier.NotifyDavid("PROBLEM - Laundry Issue", "Laundry load seems like it didn't drain properly. Please check and fix.");

                break;
            
            default:
                throw new ArgumentOutOfRangeException();
        }
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
