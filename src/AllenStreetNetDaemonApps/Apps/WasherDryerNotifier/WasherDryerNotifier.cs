using CoordinateSharp;

namespace AllenStreetNetDaemonApps.Apps.WasherDryerNotifier;

[NetDaemonApp]
public class WasherDryerNotifier
{
    private readonly IHaContext _ha;
    private readonly ILogger _logger;
    private readonly Entities _entities;

    private bool _washerRunning;
    private DateTimeOffset _washerStartedAt = DateTimeOffset.MinValue;

    private int _washerErrorCount = 0; 
    
    
    private int _washerStopWattThreshold = 10; 
    
    private int _washerStopCountdownCount = 10; 
    private int _washerStopCountdown;

    public enum WasherMode
    {
        Uninitialized,
        Off,
        Running,
        Drying,
        StayFresh,
        Problem
    }
    
    public WasherDryerNotifier(IHaContext ha, INetDaemonScheduler scheduler)
    {
        _ha = ha;

        _entities = new Entities(_ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();

        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        //scheduler.RunIn(TimeSpan.FromSeconds(5), async () => await CheckCurrentWasherPowerUsage());
        
        scheduler.RunEvery(TimeSpan.FromSeconds(30), async () => await checkCurrentWasherPowerUsage()); 
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);

        _washerStopCountdown = _washerStopCountdownCount;
    }
    
    private async Task checkCurrentWasherPowerUsage()
    {
        var washerCurrentWattsRaw = _entities.Sensor.WasherDryerComboMainPowerElectricConsumptionW.State;
        
        if (washerValueInvalid(washerCurrentWattsRaw)) return;
        
        if (washerCurrentWattsRaw is null) return;
        var washerWatts = (double)washerCurrentWattsRaw;
        _logger.Debug("Current washer power usage is: {CurrentWatts}w", washerWatts);
        
        checkForWasherStart(washerWatts);

        if (!_washerRunning) return;
        
        _logger.Debug("Washer has been running for: {ElapsedTime}", DateTimeOffset.Now - _washerStartedAt);
        
        checkForWasherStop(washerWatts);
    }

    private void checkForWasherStop(double washerWatts)
    {
        if (washerWatts > _washerStopWattThreshold)
        {
            _washerStopCountdown = _washerStopCountdownCount;
            return;
        }
        
        // Otherwise:
        _washerStopCountdown--;
        _logger.Debug("Washer stop detected, countdown at: {Countdown}", _washerStopCountdown);

        if (_washerStopCountdown >= 1) return;
        
        // Otherwise:
        _washerRunning = false;
        _washerStopCountdown = _washerStopCountdownCount;
        _logger.Debug("Washer stop detected, setting _washerRunning to false");

        if (DateTimeOffset.Now - _washerStartedAt > TimeSpan.FromHours(3))
        {
            _logger.Information("Washer ran for more than 3 hours, should be fine! Time to unload washer!");    
            return;
        }
        
        // Below here washer ran for sub 3 hours
        _logger.Error("Washer ran for less than 3 hours, was either a short load or there is an issue, please check washer");
    }

    private void checkForWasherStart(double washerWatts)
    {
        if (_washerRunning) return;
        if (washerWatts < _washerStopWattThreshold) return;
                
        // Otherwise:
        _washerRunning = true;
        _logger.Debug("Washer start detected");
        
        _washerStartedAt = DateTimeOffset.Now;
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
