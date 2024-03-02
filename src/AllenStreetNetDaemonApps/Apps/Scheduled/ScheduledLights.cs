using CoordinateSharp;

namespace AllenStreetNetDaemonApps.Apps.Scheduled;

[NetDaemonApp]
public class ScheduledLights
{
    private readonly IHaContext _ha;
    private readonly ILogger _logger;
    private readonly Entities _entities;

    private bool _sunriseStuffActivated;
    private bool _sunsetStuffActivated;
    
    public ScheduledLights(IHaContext ha, INetDaemonScheduler scheduler)
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
        
        scheduler.RunIn(TimeSpan.FromSeconds(1), CheckAllScheduledTasks);
        scheduler.RunEvery(TimeSpan.FromSeconds(120), CheckAllScheduledTasks);
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
    }

    private void CheckAllScheduledTasks()
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

        var sunriseTime = celestialTimes.SunRise ?? throw new Exception();

        var modifiedSunsetTime = TimeOnly.FromDateTime(sunsetTime - TimeSpan.FromMinutes(30));
        
        var modifiedSunriseTime = TimeOnly.FromDateTime(sunriseTime + TimeSpan.FromMinutes(30));
        
        LogDebugInfo(currentTime, sunsetTime, sunriseTime, modifiedSunsetTime, modifiedSunriseTime);

        // Sunrise
        if (currentTime > modifiedSunriseTime  &&
            currentTime < modifiedSunsetTime &&
            !_sunriseStuffActivated)
        {
            _sunriseStuffActivated = true;
            _sunsetStuffActivated = false;

            SetLightsAtMorning();

            return;
        }
        
        // Sunset
        if (currentTime > modifiedSunsetTime &&
            !_sunsetStuffActivated)
        {
            _sunsetStuffActivated = true;
            _sunriseStuffActivated = false;
            
            SetLightsAtSunset();
        }
    }

    private void LogDebugInfo(TimeOnly currentTime, DateTimeOffset sunsetTime, DateTimeOffset sunriseTime, TimeOnly modifiedSunsetTime, TimeOnly modifiedSunriseTime)
    {
        _logger.Debug("Currently: {CurrentTime}, SunRise: {SunRiseTime}, SunSet: {SunSetTime}",
            currentTime, sunriseTime, sunsetTime);

        _logger.Debug("_sunriseStuffActivated: {SunriseActivated}, _sunsetStuffActivated: {SunsetStuffActivated}",
            _sunriseStuffActivated, _sunsetStuffActivated);
        
        _logger.Debug("currentTime > modifiedSunriseTime: {CurGreaterThanSunriseTime}",
            currentTime > modifiedSunriseTime);

        _logger.Debug("currentTime > modifiedSunsetTime: {CurGreaterThanSunsetTime}",
            currentTime > modifiedSunsetTime);

        _logger.Debug("");
    }

    private void SetLightsAtSunset()
    {
        _entities.Light.FrontPorchLights.TurnOn();
        
        _entities.Switch.BackPorchChristmasLights.TurnOn();
        
        _entities.Light.DenLamp.TurnOn();

        _entities.Light.KitchenUndercabinetLights.TurnOn();

        _logger.Debug("Lights set for sunset");
    }
    
    private void SetLightsAtMorning()
    {
        _entities.Light.BackPorchLights.TurnOff();
        
        _entities.Light.FrontPorchLights.TurnOff();
        
        _entities.Switch.BackPorchChristmasLights.TurnOff();
        
        _entities.Switch.LaundryRoomLights2.TurnOff();
        
        _entities.Switch.FoyerPlantGrowLights.TurnOn();
        
        _logger.Debug("Lights set for morning");
    }
}
