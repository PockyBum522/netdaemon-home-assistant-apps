using System;
using System.Linq;
using CoordinateSharp;
using HomeAssistantGenerated;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using Serilog;

namespace NetdaemonApps.apps.Scheduled;

[NetDaemonApp]
public class ScheduledLights
{
    private readonly IHaContext _ha;
    private readonly Serilog.ILogger _logger;
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

            TurnLightsOffAtMorning();

            return;
        }
        
        // Sunset
        if (currentTime > modifiedSunsetTime &&
            !_sunsetStuffActivated)
        {
            _sunsetStuffActivated = true;
            _sunriseStuffActivated = false;
            
            TurnLightsOnAtSunset();
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

    private void TurnLightsOnAtSunset()
    {
        _entities.Light.Bulbfrontporch1.TurnOn();
        _entities.Switch.BackPorchChristmasWarm.TurnOn();

        _logger.Debug("Lights turned on at sunset");
    }
    
    private void TurnLightsOffAtMorning()
    {
        _entities.Light.Backporchbydoor.TurnOff();
        _entities.Light.Bulbbackporchfar.TurnOff();
        
        _entities.Light.Bulbfrontporch1.TurnOff();
        _entities.Switch.BackPorchChristmasWarm.TurnOff();
        
        _logger.Debug("Lights turned off at morning");
    }
}
