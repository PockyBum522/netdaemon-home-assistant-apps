using MQTTnet;
using MQTTnet.Client;

namespace AllenStreetNetDaemonApps.Apps.FrontDoorCameraMotion;

[NetDaemonApp]
public class MqttWatcher 
{
    private readonly Entities _entities;
    
    private readonly ILogger _logger;
    private DateTimeOffset _lastRunAtTime;

    private CameraImageTaker _cameraImageTaker;
    private CameraImageNotifier _cameraImageNotifier;
    
    public MqttWatcher(IHaContext ha, INetDaemonScheduler scheduler)
    {
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        _lastRunAtTime = DateTimeOffset.Now - TimeSpan.FromMinutes(6);

        _cameraImageNotifier = new CameraImageNotifier(_logger, ha);

        _cameraImageTaker = new CameraImageTaker(_logger, ha);
            
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        // For Testing:
        //scheduler.RunIn(TimeSpan.FromSeconds(1), async () => await NotifyUsersWithCameraImage());
        
        scheduler.RunIn(TimeSpan.FromSeconds(10), async () => await StartMqttListener());
    }

    private async Task StartMqttListener()
    {
        var mqttFactory = new MqttFactory();

        using var mqttClient = mqttFactory.CreateMqttClient();
        
        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithClientId("cs4ha_client")
            .WithTcpServer("192.168.1.25", 1883)
            .WithCredentials(SECRETS.MqttUsername, SECRETS.MqttPassword)
            .Build();

        mqttClient.DisconnectedAsync += async e =>
        {
            if (e.ClientWasConnected)
            {
                // Use the current options as the new options.
                await mqttClient.ConnectAsync(mqttClient.Options);
            }
        };
        
        // Setup message handling before connecting 
        mqttClient.ApplicationMessageReceivedAsync += HandleIncomingMessage;

        await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

        var mqttSubscribeOptions = 
            mqttFactory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => { f.WithTopic(SECRETS.FrontDoorMotionZoneATopic); })
                .WithTopicFilter(f => { f.WithTopic(SECRETS.DrivewayMotionZoneATopic); })
                .WithTopicFilter(f => { f.WithTopic(SECRETS.FrontYardFrontDoorMotionTopic); })
                .WithTopicFilter(f => { f.WithTopic(SECRETS.FrontYardDrivewayMotionTopic); })
                .Build();

        await mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);

        _logger.Information("MQTT client subscribed to topics");

        // Pause forever to wait for incoming messages
        while (true){ await Task.Delay(9999); }
     
        // ReSharper disable once FunctionNeverReturns because it's not supposed to
    }

    private async Task HandleIncomingMessage(MqttApplicationMessageReceivedEventArgs e)
    {
        var rawPayload = e.ApplicationMessage.PayloadSegment;

        var asciiPayload = System.Text.Encoding.ASCII.GetString(rawPayload);
        
        _logger.Information(
            "On topic: {TopicName}, received {Message}", e.ApplicationMessage.Topic, asciiPayload);
        
        if (!LastRunTimeIsMoreThanXMinutesAgo())
        {
            _logger.Information("Not waking lock/sending picture notification since timeout has not elapsed");

            _lastRunAtTime = DateTimeOffset.Now;

            return;
        }
        
        if (e.ApplicationMessage.Topic == SECRETS.FrontDoorMotionZoneATopic && asciiPayload.Equals("triggered"))
        {            
            _logger.Information("Waking front door lock/sending picture notification");

            WakeFrontDoorLock();

            await NotifyUsersWithCameraImage();
            
            _lastRunAtTime = DateTimeOffset.Now;
        }
    }

    private async Task NotifyUsersWithCameraImage()
    {
        var newImageFilename = _cameraImageTaker.CaptureFontDoorImage();

        _logger.Information("New image path is: {Path}", newImageFilename);

        // Increase this if notifications ever come through without images
        // Offset if this runs twice rapidly to hopefully stop a race condition where _lastRunAtTime isn't updated as fast as it needs to be
        await Task.Delay(new Random().Next(500, 700));

        if (LastRunTimeIsMoreThanXMinutesAgo())
        {
            _cameraImageNotifier.NotifyDavid("Front Door Motion", "", newImageFilename); 
            _cameraImageNotifier.NotifyAlyssa("Front Door Motion", "", newImageFilename);
            _cameraImageNotifier.NotifyGeneral("Front Door Motion", "", newImageFilename);    
        }
        
        await Task.Delay(2000);
    }

    private bool LastRunTimeIsMoreThanXMinutesAgo()
    {
        var xMinutesAgo = DateTimeOffset.Now - TimeSpan.FromMinutes(3);

        // _logger.Debug("Two minutes ago: {TwoMinutesAgo}", xMinutesAgo);
        // _logger.Debug("_lastLockWakeTime: {LastLockWakeTime}", _lastRunAtTime);
        // _logger.Debug(
        //     "_lastLockWakeTime < twoMinutesAgo: {LastWakeTimeMoreThanTwoMinutesAgo}",
        //     _lastRunAtTime < xMinutesAgo);
        
        return _lastRunAtTime < xMinutesAgo;
    }

    private void WakeFrontDoorLock()
    {
        _entities.Button.FrontDoorWake.Press();

        _logger.Information("Front door lock wake up command sent");
    }
}