namespace AllenStreetNetDaemonApps.Apps.ThermostatInitializer;

[NetDaemonApp]
public class ThermostatInitializer : IAsyncInitializable
{
    private readonly IHaContext _ha;
    private Serilog.ILogger _logger;
    private Entities _entities;
    
    private Task<double>? _currentTempTask;
    private readonly IDisposable _scheduledTask;

    public ThermostatInitializer(IHaContext ha, INetDaemonScheduler scheduler)
    {
        _ha = ha;
        
        _entities = new Entities(_ha);
        
        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        _scheduledTask = scheduler.RunEvery(TimeSpan.FromSeconds(5), async () => await SetThermostatOnceTemperatureFetched());
    }

    private async Task SetThermostatOnceTemperatureFetched()
    {
        await new WeatherUtilities(_logger, _entities)
            .SetAirConditioningByOutsideTemperature(71.0);
        
        // Cancel our scheduler
        _scheduledTask.Dispose();
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            _currentTempTask = WeatherUtilities.GetCurrentTemperatureFahrenheit();
        }
        catch (ArgumentNullException)
        {
            _logger.Warning("Null arg exception, this is fine if running off HA");
        }

        return Task.CompletedTask;
    }
}