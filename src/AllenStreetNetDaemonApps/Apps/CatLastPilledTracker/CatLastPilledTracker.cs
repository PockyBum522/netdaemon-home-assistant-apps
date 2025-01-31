using AllenStreetNetDaemonApps.EntityWrappers.Interfaces;
using Newtonsoft.Json;

namespace AllenStreetNetDaemonApps.Apps.CatLastPilledTracker;

[NetDaemonApp]
public class CatLastPilledTracker
{
    private readonly CatPilledAtState _catPilledState = new();

    private readonly ILogger _logger;
    private readonly Entities _entities;

    public CatLastPilledTracker(IHaContext ha, INetDaemonScheduler scheduler, IKitchenLightsWrapper kitchenLightsWrapper, IFrontRoomLightsWrapper frontRoomLightsWrapper)
    {
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();

        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);

        
        ha.Events.Where(e => e.EventType == "zha_event").Subscribe(async e => await HandleCatPilledButtonPress(e));
        
        scheduler.RunEvery(TimeSpan.FromSeconds(29), UpdateCatLastPilledAtTextbox);
    }

    private async Task HandleCatPilledButtonPress(Event e)
    {
        if (e.DataElement is null) return;
        
        var stringedEventValue = e.DataElement.ToString();
        
        if (stringedEventValue is null) return;
        
        // Make sure it's from the cat pilled button
        if (!stringedEventValue.Contains("\"device_id\":\"bb7fdad3cfb9bdad3e9e519df2ae0c20\"")) return;
        
        // Debug
        // _logger.Debug("");
        // _logger.Debug("e.DataElement.Value.ToString() {ValueRaw}", e.DataElement.Value.ToString());
        // _logger.Debug("");

        // Handle single tap (quick press)
        if (stringedEventValue.Contains("\"command\":\"single\""))
        {
            // Check when cat was last pilled and show on the kitchen nightlight

            await blinkKitchenNightlightBasedOnLastPilledTime();
            
            _logger.Debug("Single click event fired");
        }
        
        // Handle long hold
        if (stringedEventValue.Contains("\"command\":\"hold\""))
        {
            // Reset when cat was last pilled to now
            
            _catPilledState.LastPilledAt = DateTimeOffset.Now;

            savePersistentCatPilledAtState(_catPilledState);
            
            _entities.Light.MotionNightlightKitchenBySinkTowardsFrontroomLight.CallService("turn_on", new { color_temp_kelvin = 2000 } );
            await blinkLightWithCurrentColor();
            
            _logger.Debug("Long hold event fired");
        }
        
        // // Handle double tap
        // if (stringedEventValue.Contains("\"command\":\"double\""))
        // {
        //     _logger.Debug("Double click event fired");
        // }
        
        UpdateCatLastPilledAtTextbox();
    }

    private async Task blinkKitchenNightlightBasedOnLastPilledTime()
    {
        var eightHoursAgo = DateTimeOffset.Now.AddHours(-8);

        if (_catPilledState.LastPilledAt > eightHoursAgo)
        {
            // More than 8 hours ago = no pill, Maxx

            // Set red
            _entities.Light.MotionNightlightKitchenBySinkTowardsFrontroomLight.CallService("turn_on", new { hs_color = new[] { 2, 100 } } );
        }
        else
        {
            // Less than 8 hours ago = You get a pill!
            
            // Set green
            _entities.Light.MotionNightlightKitchenBySinkTowardsFrontroomLight.CallService("turn_on", new { hs_color = new[] { 120, 100 } } );
        }

        await blinkLightWithCurrentColor();

        await Task.Delay(TimeSpan.FromSeconds(2));
        
        // Set back to warm white
        _entities.Light.MotionNightlightKitchenBySinkTowardsFrontroomLight.CallService("turn_on", new { color_temp_kelvin = 2000 } );
    }

    private async Task blinkLightWithCurrentColor()
    {
        // Blink
        for (var i = 0; i < 4; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(0.5));
        
            _entities.Light.MotionNightlightKitchenBySinkTowardsFrontroomLight.CallService("turn_on", new { brightness = 1 } );
        
            await Task.Delay(TimeSpan.FromSeconds(0.5));
        
            _entities.Light.MotionNightlightKitchenBySinkTowardsFrontroomLight.CallService("turn_on", new { brightness = 254 } );
        }
    }

    private void UpdateCatLastPilledAtTextbox()
    {
        restoreSavedState();
        
        // For demoing, save the state with this then uncomment the above
        //savePersistentCatPilledAtState(new CatPilledAtState() { LastPilledAt = DateTimeOffset.Now - TimeSpan.FromHours(9) });
        
        var message = _catPilledState.LastPilledAt.ToString("dddd");
        message += ", at ";
        message += _catPilledState.LastPilledAt.ToString("hh:mm tt");
        
        _entities.InputText.CatLastPilledAt.SetValue(message);
        
        
        // Handle default value
        if (_catPilledState.LastPilledAt == DateTimeOffset.MinValue) 
            _entities.InputText.CatLastPilledAt.SetValue("Unknown");
    }
    
    private void restoreSavedState()
    {
        var stateFilePath = Path.Combine(SECRETS.CatPilledAtSavedStateDirectory, "saved-state.json");
        
        if (!File.Exists(stateFilePath)) return;
        
        var jsonString = File.ReadAllText(stateFilePath);
        
        var fetchedState = JsonConvert.DeserializeObject<CatPilledAtState>(jsonString) ?? new CatPilledAtState();
        
        _logger.Debug("Restoring CatPilledAt saved state: {LastPilledAt}", fetchedState.LastPilledAt);
        
        _catPilledState.LastPilledAt = fetchedState.LastPilledAt;
    }
    
    private void savePersistentCatPilledAtState(CatPilledAtState state)
    {
        Directory.CreateDirectory(SECRETS.CatPilledAtSavedStateDirectory);
        
        var stateFilePath = Path.Combine(SECRETS.CatPilledAtSavedStateDirectory, "saved-state.json");
        
        File.Create(stateFilePath).Close();
        
        var jsonString = JsonConvert.SerializeObject(state);
        
        File.WriteAllText(stateFilePath, jsonString);
        
        _logger.Debug("Saving CatPilledAt state: {LastPilledAt}", state.LastPilledAt);
    }
}

public class CatPilledAtState
{
    public DateTimeOffset LastPilledAt { get; set; } = DateTimeOffset.MinValue;
}