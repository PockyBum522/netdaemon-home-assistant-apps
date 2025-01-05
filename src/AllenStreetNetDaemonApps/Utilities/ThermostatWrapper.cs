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
        
        _logger.Information("Finished initializing ThermostatWrapper, current state: {@CurrentState}", CurrentThermostatState);
    }

    public void RestoreSavedModeToThermostat()
    {
        restoreSavedState();
        
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

    public double GetCurrentSetpointInHa()
    {
        // This is all so dumb. I don't care, this doesn't need to be the least bit performant
        
        var numericEntity = new NumericEntity(_ha, "climate.house_hvac");

        dynamic dynamicAttributes = numericEntity.Attributes ?? throw new Exception("Attributes is null");
        
        return double.Parse(dynamicAttributes["temperature"].ToString());
    }
    
    public void CheckThermostatStateInHa()
    {
        var currentSetpoint = GetCurrentSetpointInHa();
        var currentMode = _entities.Climate.HouseHvac.State ?? "unknown";

        restoreSavedState(); // Make sure our CurrentThermostatState is up to date
        
        // Make linter happy:
        var setpointDifference = Math.Abs(CurrentThermostatState.SetPoint - currentSetpoint);
        
        if (setpointDifference > 0.2)
        {
            _logger.Information("Thermostat persistent file is out of sync. Setpoint was {LastSetpoint} and now settings to {NewSetpoint} and saving to persistent", CurrentThermostatState.SetPoint, currentSetpoint);
            
            CurrentThermostatState.SetPoint = currentSetpoint;
            savePersistentThermostatState(CurrentThermostatState);
        }

        if (!CurrentThermostatState.Mode.Equals(currentMode, StringComparison.InvariantCultureIgnoreCase))
        {
            _logger.Information("Thermostat persistent file is out of sync. Mode was {LastMode} and now settings to {NewMode} and saving to persistent", CurrentThermostatState.Mode, currentMode);

            CurrentThermostatState.Mode = currentMode;
            savePersistentThermostatState(CurrentThermostatState);
        }
    }

    private void restoreSavedState()
    {
        var stateFilePath = Path.Combine(SECRETS.ThermostatSavedStateDirectory, "saved-state.json");
        
        if (!File.Exists(stateFilePath)) return;
        
        var jsonString = File.ReadAllText(stateFilePath);
        
        var fetchedState = JsonConvert.DeserializeObject<ThermostatState>(jsonString) ?? new ThermostatState();
        
        _logger.Information("Restoring thermostat saved state: {SavedMode} at {SavedTemperature}", fetchedState.Mode, fetchedState.SetPoint);
        
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
