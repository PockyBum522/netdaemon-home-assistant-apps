using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AllenStreetNetDaemonApps.EntityWrappers.Lights.Interfaces;
using HomeAssistantGenerated;
using NetDaemon.Extensions.Scheduler;
using Newtonsoft.Json;

namespace AllenStreetNetDaemonApps.CatLastPilledTrackers;

[NetDaemonApp]
public class MaxxLastPilledTracker
{
    private readonly MaxxPilledAtState _catPilledState = new();

    private readonly ILogger _logger;
    private readonly Entities _entities;

    public MaxxLastPilledTracker(ILogger logger, IHaContext ha, INetDaemonScheduler scheduler, IKitchenLightsWrapper kitchenLightsWrapper, IFrontRoomLightsWrapper frontRoomLightsWrapper)
    {
        _logger = logger;
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();

        // _logger = new LoggerConfiguration()
        //     .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
        //     .MinimumLevel.Information()
        //     .WriteTo.Console()
        //     .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
        //     .CreateLogger();

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
        if (!stringedEventValue.Contains("\"device_id\":\"abc2c8873488ee4b4a61d3fe8da83566\""))
        {
            // Debug
            _logger.Warning("");
            _logger.Warning("Unknown ID in e.DataElement.Value.ToString(): {ValueRaw}", e.DataElement.Value.ToString());
            _logger.Warning("");
            
            return;
        }

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
            
            _entities.Light.NightlightInKitchenBySink.CallService("turn_on", new { color_temp_kelvin = 2000 } );
            _entities.Light.NightlightInKitchenByStove.CallService("turn_on", new { color_temp_kelvin = 2000 } );
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
            _entities.Light.NightlightInKitchenBySink.CallService("turn_on", new { hs_color = new[] { 2, 100 } } );
            _entities.Light.NightlightInKitchenByStove.CallService("turn_on", new { hs_color = new[] { 2, 100 } } );
        }
        else
        {
            // Less than 8 hours ago = You get a pill!
            
            // Set green
            _entities.Light.NightlightInKitchenBySink.CallService("turn_on", new { hs_color = new[] { 120, 100 } } );
            _entities.Light.NightlightInKitchenByStove.CallService("turn_on", new { hs_color = new[] { 120, 100 } } );
        }

        await blinkLightWithCurrentColor();

        await Task.Delay(TimeSpan.FromSeconds(2));
        
        // Set back to warm white
        _entities.Light.NightlightInKitchenBySink.CallService("turn_on", new { color_temp_kelvin = 2000 } );
        _entities.Light.NightlightInKitchenByStove.CallService("turn_on", new { color_temp_kelvin = 2000 } );
    }

    private async Task blinkLightWithCurrentColor()
    {
        // Blink
        for (var i = 0; i < 4; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(0.5));
        
            _entities.Light.NightlightInKitchenBySink.CallService("turn_on", new { brightness = 1 } );
            _entities.Light.NightlightInKitchenByStove.CallService("turn_on", new { brightness = 1 } );
        
            await Task.Delay(TimeSpan.FromSeconds(0.5));
        
            _entities.Light.NightlightInKitchenBySink.CallService("turn_on", new { brightness = 254 } );
            _entities.Light.NightlightInKitchenByStove.CallService("turn_on", new { brightness = 254 } );
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
        
        _entities.InputText.MaxxLastPilledAt.SetValue(message);
        
        
        // Handle default value
        if (_catPilledState.LastPilledAt == DateTimeOffset.MinValue) 
            _entities.InputText.MaxxLastPilledAt.SetValue("Unknown");
    }
    
    private void restoreSavedState()
    {
        var stateFilePath = Path.Combine(SECRETS.CatPilledAtSavedStateDirectory, "maxx-saved-state.json");
        
        if (!File.Exists(stateFilePath)) return;
        
        var jsonString = File.ReadAllText(stateFilePath);
        
        var fetchedState = JsonConvert.DeserializeObject<MaxxPilledAtState>(jsonString) ?? new MaxxPilledAtState();
        
        _logger.Debug("Restoring CatPilledAt saved state: {LastPilledAt}", fetchedState.LastPilledAt);
        
        _catPilledState.LastPilledAt = fetchedState.LastPilledAt;
    }
    
    private void savePersistentCatPilledAtState(MaxxPilledAtState state)
    {
        Directory.CreateDirectory(SECRETS.CatPilledAtSavedStateDirectory);
        
        var stateFilePath = Path.Combine(SECRETS.CatPilledAtSavedStateDirectory, "maxx-saved-state.json");
        
        File.Create(stateFilePath).Close();
        
        var jsonString = JsonConvert.SerializeObject(state);
        
        File.WriteAllText(stateFilePath, jsonString);
        
        _logger.Debug("Saving CatPilledAt state: {LastPilledAt}", state.LastPilledAt);
    }
}

public class MaxxPilledAtState
{
    public DateTimeOffset LastPilledAt { get; set; } = DateTimeOffset.MinValue;
}