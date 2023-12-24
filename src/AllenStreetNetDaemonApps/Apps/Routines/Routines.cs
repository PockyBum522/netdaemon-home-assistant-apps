namespace AllenStreetNetDaemonApps.Apps.Routines;

[NetDaemonApp]
public class Routines
{
    private readonly IHaContext _ha;
    private readonly Serilog.ILogger _logger;
    private readonly Entities _entities;
    
    private readonly InputBooleanEntity _houseAwayToggle;
    private readonly InputBooleanEntity _houseOccupiedToggle;
    private readonly InputBooleanEntity _houseBedtimeToggle;
    private readonly InputBooleanEntity _houseMorningToggle;
    private readonly InputBooleanEntity _houseExerciseToggle;

    private bool _houseAwayActivated;
    private bool _houseOccupiedActivated;
    private bool _houseBedtimeActivated;
    private bool _houseMorningActivated;
    private bool _houseExerciseActivated;

    public Routines(IHaContext ha, INetDaemonScheduler scheduler)
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

        _houseAwayToggle = _entities.InputBoolean.HouseAway;
        _houseOccupiedToggle = _entities.InputBoolean.HouseOccupied;
        _houseBedtimeToggle = _entities.InputBoolean.HouseBedtime;
        _houseMorningToggle = _entities.InputBoolean.HouseMorning;
        _houseExerciseToggle = _entities.InputBoolean.HouseExercise;
        
        _houseMorningToggle.TurnOn();
        _houseOccupiedToggle.TurnOn();
        _houseExerciseToggle.TurnOff();
        
        // ReSharper disable once AsyncVoidLambda because I'm fairly sure it is useless
        scheduler.RunEvery(TimeSpan.FromSeconds(1), async () => await CheckAllToggles());
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
    }

    private async Task StartExerciseRoutine()
    {
        // TODO: Make composite switches for guest bedroom PC and TV-DESKTOP that show when they're on and either
        // sleep/shutdown respectively when you turn them off. 
        
        // 1. WOL Guest Bedroom PC 
        _entities.Switch.GuestBedroomMediaPcWol.TurnOn();
        
        // 2. Guest bed TV on
        _entities.MediaPlayer._43TclRokuTv.TurnOn();
        
        // 3. Set AC to 68 for 1.25 hours if it's on cool mode already
        await new WeatherUtilities(_logger, _entities)
            .SetAirConditioningByOutsideTemperature(68.0);

        // 4. When guest bedroom lightswitch gets wired in, turn it on

        // 5. If guest bedroom lights are on, turn them off
        _entities.Group.GroupBackBedroomLights.CallService("turn_off");

        // Reset switch state
        _entities.Switch.GuestBedroomMediaPcWol.TurnOff();
    }
    
    private async Task StopExerciseRoutine()
    {
        // After 1.75 hours OR when user turns off exercise mode:
        // Do the opposite of everything in turn on sequence

        // 1. Sleep guest bedroom PC 
        _entities.Button.GuestbedroomSleep.CallService("turn_on");
        
        // Reset switch
        _entities.Switch.GuestBedroomMediaPcWol.TurnOff();
        
        // 2. Guest bed TV off
        _entities.MediaPlayer._43TclRokuTv.TurnOff();
        
        // 3. Set AC to 71
        await new WeatherUtilities(_logger, _entities)
            .SetAirConditioningByOutsideTemperature(71.0);

        // 4. When guest bedroom lightswitch gets wired in, turn it on

        // 5. If guest bedroom lights are on, turn them off
        _entities.Group.GroupBackBedroomLights.CallService("turn_off");

        // Reset switch state
        _entities.Switch.GuestBedroomMediaPcWol.TurnOff();
    }
    
    private void HouseAwayRoutine()
    {
        if (_entities.Climate.HouseHvac.State == "cool")
            _entities.Climate.HouseHvac.SetTemperature(74.0);
        
        if (_entities.Climate.HouseHvac.State == "heat")
            _entities.Climate.HouseHvac.SetTemperature(66.0);

        TurnOffEverything();

        _logger.Debug("House set to away mode");
    }

    private void HouseOccupiedRoutine()
    {
        if (_entities.Climate.HouseHvac.State == "cool")
            _entities.Climate.HouseHvac.SetTemperature(70.0);
        
        if (_entities.Climate.HouseHvac.State == "heat")
            _entities.Climate.HouseHvac.SetTemperature(69.0);

        //_entities.Switch.DenLamp.TurnOn();
        
        _logger.Debug("House set to occupied mode");
    }

    private void GoingToBedRoutine()
    {
        if (_entities.Climate.HouseHvac.State == "cool")
            _entities.Climate.HouseHvac.SetTemperature(69.0);
        
        if (_entities.Climate.HouseHvac.State == "heat")
            _entities.Climate.HouseHvac.SetTemperature(68.0);

        TurnOffEverything();

        _logger.Debug("House set to bedtime mode");
    }

    private void WakingUpRoutine()
    {
        if (_entities.Climate.HouseHvac.State == "cool")
            _entities.Climate.HouseHvac.SetTemperature(70.0);
        
        if (_entities.Climate.HouseHvac.State == "heat")
            _entities.Climate.HouseHvac.SetTemperature(68.0);

        _logger.Debug("House set to morning mode");
    }
    
    private void TurnOffEverything()
    {
        // Lights
        _entities.Light.Backporchbydoor.TurnOff();
        _entities.Light.Bulbbackhall1.TurnOff();
        _entities.Light.Bulbbackporchfar.TurnOff();
        _entities.Light.Bulbden1.TurnOff();
        _entities.Light.Bulbden2.TurnOff();
        _entities.Light.Bulbden3.TurnOff();
        _entities.Light.Bulbden4.TurnOff();
        _entities.Light.Bulbfoyer1.TurnOff();
        _entities.Light.Bulbfrontroom1.TurnOff();
        _entities.Light.Bulbfrontroom2.TurnOff();
        _entities.Light.Bulbfrontroom3.TurnOff();
        _entities.Light.Bulbfrontroom4.TurnOff();
        _entities.Light.Bulbguestbathsink1.TurnOff();
        _entities.Light.Bulbguestbathsink2.TurnOff();
        _entities.Light.Bulbguestbathsink3.TurnOff();
        _entities.Light.Bulbmasterbath1.TurnOff();
        _entities.Light.Bulbmasterbath2.TurnOff();
        _entities.Light.Bulbmasterbathshower1.TurnOff();
        _entities.Light.Bulbmasterbathvanity.TurnOff();
        _entities.Light.Bulbmasterbedroom1.TurnOff();
        _entities.Light.Bulbmasterbedroom2.TurnOff();
        _entities.Light.Bulbmasterbedroom3.TurnOff();
        _entities.Light.Bulbmasterbedroom4.TurnOff();
        _entities.Light.Bulbmasterbedroombybath.TurnOff();
        
        // TVs
        _entities.Switch.DenTv.TurnOff();
        _entities.MediaPlayer.LgWebosSmartTv2.TurnOff();
    }
    
    private async Task CheckAllToggles()
    {
        if (_houseAwayToggle.State == "on" &&
            !_houseAwayActivated)
        {
            _houseOccupiedToggle.TurnOff();
            _houseOccupiedActivated = false;
            
            _houseAwayActivated = true;
            
            HouseAwayRoutine();

            return;
        }
        
        if (_houseOccupiedToggle.State == "on" &&
            !_houseOccupiedActivated)
        {
            _houseAwayToggle.TurnOff();
            _houseAwayActivated = false;
            
            _houseOccupiedActivated = true;
            
            HouseOccupiedRoutine();
            
            return;
        }
        
        if (_houseBedtimeToggle.State == "on" &&
            !_houseBedtimeActivated)
        {
            _houseMorningToggle.TurnOff();
            _houseMorningActivated = false;
            
            _houseBedtimeActivated = true;
            
            GoingToBedRoutine();
            
            return;
        }
        
        if (_houseMorningToggle.State == "on" &&
            !_houseMorningActivated)
        {
            _houseBedtimeToggle.TurnOff();
            _houseBedtimeActivated = false;
            
            _houseMorningActivated = true;
            
            WakingUpRoutine();
            
            return;
        }
        
        if (_houseExerciseToggle.State == "on" &&
            !_houseExerciseActivated)
        {
            _houseExerciseActivated = true;
            
            await StartExerciseRoutine();
            
            return;
        }
        
        if (_houseExerciseToggle.State == "off" &&
            _houseExerciseActivated)
        {
            _houseExerciseActivated = false;
            
            await StopExerciseRoutine();
            
            return;
        }
    }
}
