using CoordinateSharp;

namespace AllenStreetNetDaemonApps.Apps.RecurringScheduled;

[NetDaemonApp]
public class ScheduledAirConditioning
{
    private readonly IHaContext _ha;
    private readonly ILogger _logger;
    private readonly Entities _entities;

    private bool _middleOfTheNightStuffActivated;
    
    private TextNotifier _textNotifier;
    
    public ScheduledAirConditioning(IHaContext ha, INetDaemonScheduler scheduler)
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

        _textNotifier = new TextNotifier(_logger, _ha);
        
        var runFirstAfter = MqttClientWrapper.MqttSetupDelaySeconds + 10;
        
        // Just testing notifications
        scheduler.RunIn(TimeSpan.FromSeconds(2), TestNotifications);
        
        // Make sure this runs before the next line after
        scheduler.RunIn(TimeSpan.FromSeconds(runFirstAfter - 5), InitializeThermostatOnceMqttUp);
        scheduler.RunIn(TimeSpan.FromSeconds(runFirstAfter), async () => await CheckAllScheduledTasks());
        
        // Keep in mind this will mess with the thermostat init if you set this to shorter than runFirstAfter
        scheduler.RunEvery(TimeSpan.FromSeconds(runFirstAfter * 2), async () => await CheckAllScheduledTasks()); 
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
    }

    private void TestNotifications()
    {
        // _textNotifier.NotifyDavid("Test Title 01", "The washer has finished, please go unload it! This is your test notification!");
        // _textNotifier.NotifyAlyssa("Test Title 01", "This is your test notification! Actionable item! Possibilities!");
    }

    private void InitializeThermostatOnceMqttUp()
    {
        var thermostatWrapper = new ThermostatWrapper(_logger, _ha);
            
        _logger.Information("Setting thermostat to saved mode from {ThisName}", nameof(InitializeThermostatOnceMqttUp));
        thermostatWrapper.RestoreSavedModeToThermostat();
    }
    
    private async Task CheckAllScheduledTasks()
    {
        _logger.Debug("Starting: {ThisMethodName}", nameof(CheckAllScheduledTasks));
        
        var currentTime = TimeOnly.FromDateTime(DateTimeOffset.Now.DateTime);

        _logger.Debug("Current time gotten: {CurrentTime}", currentTime);
        
        var el = new EagerLoad(EagerLoadType.Celestial);
        el.Extensions = new EagerLoad_Extensions(EagerLoad_ExtensionsType.Solar_Cycle);

        var currentUtcOffset = TimeZoneInfo.Local.BaseUtcOffset;

        var offsetAsInt = int.Parse(currentUtcOffset.ToString()[..3]);
        
        _logger.Debug("Current UTC Offset used for calculations: {TimespanInfo}", currentUtcOffset);
        
        var celestialTimes = Celestial.CalculateCelestialTimes(SECRETS.MyLatitude, SECRETS.MyLongitude, DateTime.Now, el, offsetAsInt);

        var sunsetTime = celestialTimes.SunSet ?? throw new Exception();
        
        _logger.Debug("Sunset time gotten: {SunsetTime}", sunsetTime);

        //var sunriseTime = celestialTimes.SunRise ?? throw new Exception();

        var middleOfTheNightDateTime = new DateTime(2000, 1, 1, 0, 30, 0);
        
        var middleOfTheNightTime = TimeOnly.FromDateTime(middleOfTheNightDateTime);
        var middleOfTheNightTimeStop = TimeOnly.FromDateTime(middleOfTheNightDateTime + TimeSpan.FromMinutes(10));
        
        //LogDebugInfo(currentTime, sunsetTime, sunriseTime, middleOfTheNightTime);

        // Check if thermostat was changed manually, if so, save it to the persistent
        var thermostatWrapper = new ThermostatWrapper(_logger, _ha);
        thermostatWrapper.CheckThermostatStateInHa();        
        
        // Middle of the night
        if (currentTime > middleOfTheNightTime  &&
            currentTime < middleOfTheNightTimeStop &&
            !_middleOfTheNightStuffActivated)
        {
            setThermostatToEnergySavings();
            
            // Give a sec to log in case we shut down the scheduled task right after this 
            await Task.Delay(1000);
            
            _middleOfTheNightStuffActivated = true;
        }
    }
    
    private void setThermostatToEnergySavings()
    {
        var thermostatWrapper = new ThermostatWrapper(_logger, _ha);
        
        var thermostatNewState = new ThermostatWrapper.ThermostatState();

        if (_entities.Climate.HouseHvac.State == "cool" &&
            thermostatWrapper.GetCurrentSetpointInHa() >= 73.0)
        {
            return;
        }
        
        thermostatNewState.SetPoint = 73.0;
            
        if (_entities.Climate.HouseHvac.State == "heat")
            thermostatNewState.SetPoint = 65.5;

        thermostatNewState.Mode = _entities.Climate.HouseHvac.State ?? "unknown";
        
        _logger.Information("Setting thermostat to saved mode from {ThisName}", nameof(setThermostatToEnergySavings));
        thermostatWrapper.SetThermostatTo(thermostatNewState);
    }

    private void LogDebugInfo(TimeOnly currentTime, DateTimeOffset sunsetTime, DateTimeOffset sunriseTime, TimeOnly modifiedSunsetTime, TimeOnly modifiedSunriseTime)
    {
        _logger.Debug("Currently: {CurrentTime}, SunRise: {SunRiseTime}, SunSet: {SunSetTime}",
            currentTime, sunriseTime, sunsetTime);
        
        _logger.Debug("currentTime > modifiedSunriseTime: {CurGreaterThanSunriseTime}",
            currentTime > modifiedSunriseTime);

        _logger.Debug("currentTime > modifiedSunsetTime: {CurGreaterThanSunsetTime}",
            currentTime > modifiedSunsetTime);

        _logger.Debug("");
    }
}
