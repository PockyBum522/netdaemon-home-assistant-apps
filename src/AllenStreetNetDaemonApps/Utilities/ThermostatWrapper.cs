using Newtonsoft.Json;

namespace AllenStreetNetDaemonApps.Utilities;

public class ThermostatWrapper
{ 
    private readonly IHaContext _ha;
    private readonly ILogger _logger;
    private readonly Entities _entities;

    public static readonly ThermostatState CurrentThermostatState = new ();
    
    public ThermostatWrapper(ILogger logger, IHaContext ha)
    {
        _logger = logger;
        _ha = ha;

        _entities = new Entities(_ha);
        
        restoreSavedMode();
    }

    public void RestoreSavedModeToThermostat()
    {
        restoreSavedMode();
        
        SetThermostatTo(CurrentThermostatState);
    }
    
    public void SetThermostatTo(ThermostatState state)
    {
        var loweredMode = state.Mode.ToLower();
        
        if (loweredMode.Contains("unknown")) return;
        
        _logger.Information("Setting thermostat to {NewMode} at {NewTemperature}", state.Mode, state.SetPoint);
        
        savePersistentThermostatState(state);
        
        _entities.Climate.HouseHvac.SetHvacMode(loweredMode);
        _entities.Climate.HouseHvac.SetTemperature(state.SetPoint);
    }
    
    public void CheckThermostatStateInHa()
    {
        var thermostatWrapper = new ThermostatWrapper(_logger, _ha);

        _logger.Information("{@StateDeconstructed}", _entities.Climate.HouseHvac);
        
        // var thermostatStateInHa = _entities.Climate.HouseHvac.EntityState;
        // if (thermostatStateInHa is null ||
        //     thermostatStateInHa.Attributes is null)
        // {
        //     throw new Exception("Thermostat state in HA is null");
        // }
        //
        //
        //
        // var thermostatModeInHa = thermostatStateInHa.Attributes.CurrentTemperature;
        // var thermostatSetpointInHa = thermostatStateInHa.Attributes.;
        //
        // if ( != ThermostatWrapper.CurrentThermostatState.Mode)
        // ThermostatWrapper.CurrentThermostatState.Mode = 
    }

    private void restoreSavedMode()
    {
        var stateFilePath = Path.Combine(SECRETS.ThermostatSavedStateDirectory, "saved-state.json");
        
        if (!File.Exists(stateFilePath)) return;
        
        var jsonString = File.ReadAllText(stateFilePath);
        
        var fetchedState = JsonConvert.DeserializeObject<ThermostatState>(jsonString) ?? new ThermostatState();
        
        _logger.Information("Restoring thermostat saved state: {SavedMode} at {SavedTemperature} and sending to ESP32", fetchedState.Mode, fetchedState.SetPoint);
        
        //SetThermostatTo(fetchedState);
        
        CurrentThermostatState.Mode = fetchedState.Mode;
        CurrentThermostatState.SetPoint = fetchedState.SetPoint;
    }

    private void savePersistentThermostatState(ThermostatState state)
    {
        Directory.CreateDirectory(SECRETS.ThermostatSavedStateDirectory);
        
        var stateFilePath = Path.Combine(SECRETS.ThermostatSavedStateDirectory, "saved-state.json");
        
        File.Create(stateFilePath).Close();
        
        var jsonString = JsonConvert.SerializeObject(state);
        
        File.WriteAllText(stateFilePath, jsonString);
    }
    
    public class ThermostatState
    {
        public string Mode { get; set; } = "unknown";
        public double SetPoint { get; set; } = 0.0;
    }
}
