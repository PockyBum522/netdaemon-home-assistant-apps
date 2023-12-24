namespace AllenStreetNetDaemonApps.Utilities;

public class WeatherUtilities
{
    private readonly ILogger _logger;
    private readonly Entities _haEntities;

    public WeatherUtilities(ILogger logger, Entities haEntities)
    {
        _logger = logger;
        _haEntities = haEntities;
    }
    
    public static async Task<double> GetCurrentTemperatureFahrenheit()
    {
        const string accessKey = SECRETS.WeatherApiAccessKey;

        var weatherClient = new OpenWeatherMap.Standard.Current(accessKey);

        var currentData = await weatherClient.GetWeatherDataByCityName("Orlando");

        if (currentData is null)
        {
            return 85.0;
        }

        var currentFeelsLike = currentData.WeatherDayInfo.FeelsLike;

        var currentFeelsLikeFahrenheit = currentFeelsLike * (9d / 5d) + 32;

        return currentFeelsLikeFahrenheit switch
        {
            > 140 => throw new Exception($"Current temp was: {currentFeelsLikeFahrenheit} which is more than is valid"),
            < 2 => throw new Exception($"Current temp was: {currentFeelsLikeFahrenheit} which is less than is valid"),
            _ => currentFeelsLikeFahrenheit
        };
    }

    public async Task SetAirConditioningByOutsideTemperature(double coolSetPoint)
    {
        var currentOutsideTemperature = await GetCurrentTemperatureFahrenheit();

        var modeString = "off";
        var setPoint = coolSetPoint;
        
        switch (currentOutsideTemperature)
        {
            case > 65:
                modeString = "cool";
                setPoint = 71.0;
                break;
            
            case < 60:
                modeString = "heat";
                setPoint = coolSetPoint - 8;
                break;
        }
        
        _haEntities.Climate.HouseHvac.SetHvacMode(modeString);
        _haEntities.Climate.HouseHvac.SetTemperature(setPoint);
        
        _logger.Information(
            "Current outside temp is: {CurrentTemp} now setting thermostat to {SetPoint} and HVAC mode: {HvacMode}", 
            currentOutsideTemperature, setPoint, modeString);
        
        // Give a sec to log in case we shut down the scheduled task right after this 
        await Task.Delay(1000);
    }
}

