using AllenStreetNetDaemonApps.Internal.Interfaces;

namespace AllenStreetNetDaemonApps.Internal;

public class KitchenLightsControl : IKitchenLightsControl
{
    private readonly ILogger _logger;
    
    private readonly Entities _entities;

    private readonly Entity[] _kitchenCeilingLightsEntities;

    public KitchenLightsControl(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger)
    {
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        _logger.Debug("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);

        _kitchenCeilingLightsEntities = GroupUtilities.GetEntitiesFromGroup(ha, _entities.Light.KitchenCeilingLights);
    }
    
    public void TurnOnKitchenLightsFromMotion()
    {
        _logger.Debug("Running {NameOfThis}", nameof(TurnOnKitchenLightsFromMotion));

        SharedState.MotionSensors.LastMotionInKitchenAt = DateTimeOffset.Now;
        
        if (TimeRangeHelpers.IsNightTime())
        {
            // At night
            allKitchenLightsOnWithBrightness(50);
            return;
        }
        
        // Daytime!
        allKitchenLightsOnWithBrightness(100);
    }
    
    public void TurnOffKitchenLightsFromMotion()
    {
        _logger.Debug("Running {NameOfThis}", nameof(TurnOffKitchenLightsFromMotion));
        
        var anyLightsAreOn = _kitchenCeilingLightsEntities.Any(l => l.State == "on") ||
                                  _entities.Light.MotionNightlightKitchenBySinkTowardsFrontroomLight.State == "on" ||
                                  _entities.Light.KitchenUndercabinetLights.State == "on";
        
        if (!anyLightsAreOn) return;
        
        _logger.Debug("Turning off kitchen lights because there was no motion and at least one light state was on");
        
        foreach (var ceilingLight in _kitchenCeilingLightsEntities)
            ceilingLight.CallService("light.turn_off");

        // Now turn off the native group
        _entities.Light.KitchenCeilingLights.TurnOff();
        
        _entities.Light.KitchenUndercabinetLights.TurnOff();
        _entities.Light.MotionNightlightKitchenBySinkTowardsFrontroomLight.TurnOff();
    }
    
    public async Task SetKitchenLightsBrighter()
    {
        if (_entities.Switch.KitchenMainLightswitch.IsOn())
            await ModifyCeilingLightsBrightnessBy(20);
        
        if (_entities.Switch.KitchenMainLightswitch.IsOff())
            await TurnMainRelayOn(CustomColors.WarmWhite());
    }

    public async Task SetKitchenLightsDimmer()
    {
        if (_entities.Switch.KitchenMainLightswitch.IsOn())
            await ModifyCeilingLightsBrightnessBy(-20);
        
        if (_entities.Switch.KitchenMainLightswitch.IsOff())
            await TurnMainRelayOn(CustomColors.WarmWhite(20));
    }

    public async Task SetKitchenLightsToPurpleScene()
    {
        _logger.Debug("Setting purple scene");

        // Handle when kitchen main relay was off, turning on and try to not blind people with defaults
        if (_entities.Switch.KitchenMainLightswitch.IsOff())
            await TurnMainRelayOn(CustomColors.AlyssaPurple());

        // Then set the right colors, whether main relay was on or not
        _kitchenCeilingLightsEntities.CallService("turn_on", CustomColors.AlyssaPurple() );
    }

    public Task ModifyCeilingLightsBrightnessBy(int brightnessModifier)
    {
        foreach (var ceilingLight in _kitchenCeilingLightsEntities)
        {
            var lightAttributesDict = (Dictionary<string,object>?)ceilingLight.Attributes;
            
            if (lightAttributesDict is null)
                throw new Exception("lightAttributesDict is null");

            var currentLightBrightness = decimal.Parse(lightAttributesDict["brightness"].ToString() ?? "0");

            var currentLightBrightnessPercent = currentLightBrightness.Map(0, 255, 0, 100); 
            
            var newLightBrightness = (int)currentLightBrightnessPercent + brightnessModifier;

            if (newLightBrightness > 100)
                newLightBrightness = 100;
            
            if (newLightBrightness < 0)
                newLightBrightness = 0;
            
            _logger.Information("Current brightness: {Bright} and new brightness will be: {NewBright}", currentLightBrightness, newLightBrightness);
            
            ceilingLight.CallService("turn_on", new { brightness_pct = newLightBrightness } );

            // await Task.Delay(100);
        }

        return Task.CompletedTask;
    }

    public async Task SetKitchenLightsToEspressoMachineScene()
    {
        // Handle when kitchen main relay was off, turning on and try to not blind people with defaults
        if (_entities.Switch.KitchenMainLightswitch.IsOff())
            await TurnMainRelayOn(CustomColors.WarmWhite(20));
        
        // Then set the right colors, whether main relay was on or not
        await TurnMainRelayOn(CustomColors.WarmWhite(20));
    }
    
    private void allKitchenLightsOnWithBrightness(int brightPercent)
    {
        _entities.Light.KitchenUndercabinetLights.CallService("turn_on", new { brightness_pct = brightPercent } );

        var brightnessMapped = brightPercent.Map(0, 100, 0, 254);
        _entities.Light.MotionNightlightKitchenBySinkTowardsFrontroomLight.CallService("turn_on", new { brightness = brightnessMapped } );
    
        _logger.Debug("Mapped bright for MotionNightlightKitchenBySinkTowardsFrontroomLight: {Bright}", brightnessMapped);
    }
    private async Task TurnMainRelayOn(object turnOnStateData)
    {
        _entities.Switch.KitchenMainLightswitch.TurnOn();

        for (var i = 0; i < 5; i++)
        {
            _kitchenCeilingLightsEntities.CallService("turn_on", turnOnStateData );

            await Task.Delay(500);
        }
    }
}