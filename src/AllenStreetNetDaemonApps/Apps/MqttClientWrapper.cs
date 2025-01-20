using System.Buffers;
using AllenStreetNetDaemonApps.Apps.DoorsLockAfterTimePeriod;
using AllenStreetNetDaemonApps.Apps.FrontDoorCameraMotion;
using AllenStreetNetDaemonApps.EntityWrappers.Interfaces;
using MQTTnet;

namespace AllenStreetNetDaemonApps.Apps;

[NetDaemonApp]
public class MqttClientWrapper 
{
    private readonly IHaContext _ha;
    private readonly Entities _entities;
    
    private readonly ILogger _logger;
    private DateTimeOffset _lastLockWakeAtTime;
    private DateTimeOffset _lastFrontDoorImageNotifyTime;
    private DateTimeOffset _lastScanNotificationTime = DateTimeOffset.Now;
    
    private DateTimeOffset _lastGarageOpenTime = DateTimeOffset.Now;

    private CameraImageTaker _cameraImageTaker;
    private CameraImageNotifier _cameraImageNotifier;
    private TextNotifier? _textNotifier;
    private SwitchEntity? _deadboltLatch;
    private SwitchEntity? _doorknobLatch;
    private LockEntity? _frontDoorLock;
    private readonly DoorLocksController _doorLocksController;
    private readonly string _clientIdString;
    
    public const int MqttSetupDelaySeconds = 30;

    public MqttClientWrapper(IHaContext ha, INetDaemonScheduler scheduler, IKitchenLightsWrapper kitchenLightsWrapper, IFrontRoomLightsWrapper frontRoomLightsWrapper)
    {
        _ha = ha;
        _entities = new Entities(ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        _doorLocksController = new DoorLocksController(ha, scheduler, kitchenLightsWrapper, frontRoomLightsWrapper);
        
        _lastLockWakeAtTime = DateTimeOffset.Now - TimeSpan.FromMinutes(5);
        _lastFrontDoorImageNotifyTime = DateTimeOffset.Now - TimeSpan.FromMinutes(5);

        _cameraImageNotifier = new CameraImageNotifier(ha);

        _cameraImageTaker = new CameraImageTaker();
            
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        // For Testing:
        //scheduler.RunIn(TimeSpan.FromSeconds(15), async () => await NotifyUsersWithCameraImage());

        if (Environment.UserDomainName.ToLower().Contains("david"))
        {
            scheduler.RunIn(TimeSpan.FromSeconds(3), async () => await StartMqttListener());

            _clientIdString = "nd_testing_client_" + DateTime.Now.ToString("ffff");
        }
        else
        {
            // Hold off for a bit to make sure Mosquitto is up and ready
            scheduler.RunIn(TimeSpan.FromSeconds(MqttSetupDelaySeconds), async () => await StartMqttListener());
            
            _clientIdString = "ha_nd_client_" + DateTime.Now.ToString("ffff");
        }
    }
    
    

    private async Task StartMqttListener()
    {
        _textNotifier = new TextNotifier(_logger, _ha);
        
        _deadboltLatch = _entities.Switch.DeadboltDoorframeLatch;
        _doorknobLatch = _entities.Switch.DoorknobDoorframeLatch;

        _frontDoorLock = _entities.Lock.BluetoothFrontDoorLock;
        
        var mqttFactory = new MqttClientFactory();

        using var mqttClient = mqttFactory.CreateMqttClient();
        
        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithClientId(_clientIdString)
            .WithTcpServer("192.168.1.25", 1883)
            .WithCredentials(SECRETS.MqttUsername, SECRETS.MqttPassword)
            .Build();

        mqttClient.DisconnectedAsync += async e =>
        {
            if (e.ClientWasConnected)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                
                // Use the current options as the new options.
                await mqttClient.ConnectAsync(mqttClient.Options);
            }
        };
        
        // Setup message handling before connecting 
        mqttClient.ApplicationMessageReceivedAsync += HandleIncomingMessage;

        await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

        var mqttSubscribeOptions = 
            mqttFactory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => { f.WithTopic(SECRETS.TopicFrontDoorMotionZoneA); })
                .WithTopicFilter(f => { f.WithTopic(SECRETS.TopicDrivewayMotionZoneA); })
                .WithTopicFilter(f => { f.WithTopic(SECRETS.TopicFrontYardFrontDoorMotion); })
                .WithTopicFilter(f => { f.WithTopic(SECRETS.TopicFrontYardDrivewayMotion); })
                .WithTopicFilter(f => { f.WithTopic(SECRETS.TopicFrontDoorNfcReaderTags); })
                .WithTopicFilter(f => { f.WithTopic(SECRETS.TopicDenTouchscreenButtons); })
                .WithTopicFilter(f => { f.WithTopic(SECRETS.TopicBackPorchPeopleAreas); })
                .Build();

        await mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);

        _logger.Information("MQTT client subscribed to topics");
        _logger.Information("MQTT client connected: {StatusMqtt}", mqttClient.IsConnected);

        // Pause forever to wait for incoming messages
        while (true){ await Task.Delay(1000); }
     
        // ReSharper disable once FunctionNeverReturns because it's not supposed to
    }

    private async Task HandleIncomingMessage(MqttApplicationMessageReceivedEventArgs e)
    {
        var asciiPayload = System.Text.Encoding.ASCII.GetString(e.ApplicationMessage.Payload.ToArray());
        
        _logger.Information(
            "On topic: {TopicName}, received {Message}", e.ApplicationMessage.Topic, asciiPayload);
     
        await checkIfDenTouchscreenButtons(e.ApplicationMessage.Topic, asciiPayload);
        
        if (e.ApplicationMessage.Topic == SECRETS.TopicFrontDoorNfcReaderTags)
        {
            _logger.Debug("Tag scanned on topic: {Topic}", SECRETS.TopicFrontDoorNfcReaderTags);

            _logger.Debug("Mqtt payload is: '{Payload}'", asciiPayload);

            var foundAuthorizedTag = false;

            if (_deadboltLatch is null ||
                _doorknobLatch is null ||
                _frontDoorLock is null ||
                _textNotifier is null)
            {
                throw new NullReferenceException();
            }
            
            foreach (var tag in SECRETS.AuthorizedNfcTags)
            {
                if (tag.Uid != asciiPayload) continue;
                
                if (_lastScanNotificationTime >= DateTimeOffset.Now - TimeSpan.FromSeconds(4)) continue;
                
                _deadboltLatch.TurnOn();
                _doorknobLatch.TurnOn();

                _logger.Debug("Front door latches open!");
                    
                _doorLocksController.FrontDoorLock.AddAutoLockTime();

                await _doorLocksController.FrontDoorLock.Unlock();
                
                foundAuthorizedTag = true;

                
                // Send notification about unrecognized UID
                //_textNotifier.NotifyDavid("Tag Scan at Front Door", $"Tag used was for: {tag.FriendlyName}");
                        
                _lastScanNotificationTime = DateTimeOffset.Now;
            }

            if (!foundAuthorizedTag)
            {
                // Send notification about unrecognized UID
//                 _textNotifier.NotifyAll("UNAUTHORIZED TAG", 
//                     $@"Unauthorized tag was scanned at front door NFC reader.
//
// Tag UID is: {asciiPayload}");
            }
        }
        
        if (!LastLockWakeTimeWasMoreThanXMinutesAgo())
        {
            _logger.Information("Not waking lock/sending picture notification since timeout has not elapsed");

            _lastLockWakeAtTime = DateTimeOffset.Now;

            return;
        }
        
        if (e.ApplicationMessage.Topic == SECRETS.TopicFrontDoorMotionZoneA && asciiPayload.Equals("triggered"))
        {            
            _logger.Information("Waking front door lock/sending picture notification");

            // Disabled to see if it's what's causing the delay on firing the front door unlatch
            //await NotifyUsersWithCameraImage();
            
            _lastFrontDoorImageNotifyTime = DateTimeOffset.Now;
        }
    }

    private async Task checkIfDenTouchscreenButtons(string topic, string asciiPayload)
    {
        if (topic != SECRETS.TopicDenTouchscreenButtons) return;
        
        if (asciiPayload.ToLower() == "garage_button_pressed")
        {
            // If this button has already been pressed within the last 15 seconds, then user might want to stop garage midway, make it so another button press immediately activates the button 
            if (_lastGarageOpenTime + TimeSpan.FromSeconds(15) > DateTimeOffset.Now)
            {
                _lastGarageOpenTime = DateTimeOffset.Now;
                
                _entities.Switch.OpenGarageDoor.TurnOn();
                
                return;
            }
            
            _lastGarageOpenTime = DateTimeOffset.Now;
            
            // Delay to give user time to walk to garage
            await Task.Delay(TimeSpan.FromSeconds(8));
            
            _entities.Switch.OpenGarageDoor.TurnOn();    
        }
        
        if (asciiPayload.ToLower() == "den_lights_button_pressed")
        {
            if (_entities.Light.DenLights.IsOn())
            {
                _entities.Light.DenLights.TurnOff();
            }
            else
            {
                _entities.Light.DenLights.TurnOn();   
            }
        }
        
        if (asciiPayload.ToLower() == "tv_power_button_pressed")
        {
            if (_entities.MediaPlayer.DenLgTv.IsOn())
            {
                _entities.MediaPlayer.DenLgTv.TurnOff();    
            }
            else
            {
                _entities.MediaPlayer.DenLgTv.TurnOn();
            }
        }
    }

    // private async Task NotifyUsersWithCameraImage()
    // {
    //     if (!LastDoorImageNotificationWasMoreThanXMinutesAgo()) return;
    //         
    //     var newImageFilename = _cameraImageTaker.CaptureFontDoorImage();
    //     var newFarImageFilename = _cameraImageTaker.CaptureFontDoorImageFromFarCam();
    //
    //     _logger.Information("New image path is: {Path}", newImageFilename);
    //
    //     // Increase this if notifications ever come through without images
    //     // Offset if this runs twice rapidly to hopefully stop a race condition where _lastRunAtTime isn't updated as fast as it needs to be
    //     await Task.Delay(new Random().Next(2000, 2200));
    //
    //     var stackedPath = StackPicturesVertically(newImageFilename, newFarImageFilename);
    //     _logger.Information("Stacked image path is: {Path}", newImageFilename);
    //     
    //     // Needs to be 20? 30s? (a while) for the image to be ready to serve over nabu casa url and media folder
    //     await Task.Delay(new Random().Next(20000, 21000));
    //     
    //     _cameraImageNotifier.NotifyDavid("Front Door Motion", "", stackedPath); 
    //     _cameraImageNotifier.NotifyAlyssa("Front Door Motion", "", stackedPath);
    //     _cameraImageNotifier.NotifyGeneral("Front Door Motion", "", stackedPath);    
    //
    //     await Task.Delay(2000);
    // }

    // private string StackPicturesVertically(string newImageFilename, string newFarImageFilename)
    // {
    //     using var closeImage = Image.Load<Rgba32>(Path.Join(SECRETS.CameraSnapshotDirectory, newImageFilename));
    //     using var farImage = Image.Load<Rgba32>(Path.Join(SECRETS.CameraSnapshotDirectory, newFarImageFilename));
    //     
    //     var newHeight = (closeImage.Height + farImage.Height) / 2;
    //     
    //     var newWidth = closeImage.Width;
    //
    //     if (farImage.Width > closeImage.Width)
    //         newWidth = farImage.Width;
    //
    //     using var outputImage = new Image<Rgba32>(newWidth, newHeight);
    //     
    //     var closeImageHalfHeight = closeImage.Height / 2;
    //
    //     // closeImage.Mutate(
    //     //     i => i.Crop(
    //     //         new Rectangle(0, 0, closeImage.Width, closeImageHalfHeight + 50)));
    //     
    //     // take the 2 source images and draw them onto the image
    //     outputImage.Mutate(o => o
    //             .DrawImage(closeImage, new Point(0, 0), 1f) // draw the first one top left
    //             .DrawImage(farImage, new Point(0, closeImageHalfHeight + 50), 1f) // draw the second next to it
    //     );
    //
    //     // foreach (var font in SystemFonts.)
    //     // {
    //     //     _logger.Warning("{FontName}", font.Name);
    //     // }
    //     
    //     // outputImage.Mutate(o => o.DrawText(
    //     //     DateTimeOffset.Now.ToString("F"),
    //     //     SystemFonts.CreateFont("Arial", 25),
    //     //     new Color(Rgba32.ParseHex("#FFFFFFFFFFF")),
    //     //     new PointF(10, outputImage.Height - 200)
    //     //     ));
    //     
    //     var fileSafeTimestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd_HH-mm-ss.ff");
    //
    //     var stackedImageFilename = "vertStacked_" + fileSafeTimestamp + ".png";
    //
    //     var fullPathToMedia = Path.Join(SECRETS.CameraSnapshotDirectory, stackedImageFilename);
    //     
    //     outputImage.Save(fullPathToMedia);
    //     
    //     return stackedImageFilename;
    // }

    private bool LastLockWakeTimeWasMoreThanXMinutesAgo()
    {
        var xMinutesAgo = DateTimeOffset.Now - TimeSpan.FromMinutes(3);

        // _logger.Debug("Two minutes ago: {TwoMinutesAgo}", xMinutesAgo);
        // _logger.Debug("_lastLockWakeTime: {LastLockWakeTime}", _lastRunAtTime);
        // _logger.Debug(
        //     "_lastLockWakeTime < twoMinutesAgo: {LastWakeTimeMoreThanTwoMinutesAgo}",
        //     _lastRunAtTime < xMinutesAgo);
        
        return _lastLockWakeAtTime < xMinutesAgo;
    }
    //
    // private bool LastDoorImageNotificationWasMoreThanXMinutesAgo()
    // {
    //     var xMinutesAgo = DateTimeOffset.Now - TimeSpan.FromMinutes(3);
    //
    //     // _logger.Debug("Two minutes ago: {TwoMinutesAgo}", xMinutesAgo);
    //     // _logger.Debug("_lastLockWakeTime: {LastLockWakeTime}", _lastRunAtTime);
    //     // _logger.Debug(
    //     //     "_lastLockWakeTime < twoMinutesAgo: {LastWakeTimeMoreThanTwoMinutesAgo}",
    //     //     _lastRunAtTime < xMinutesAgo);
    //     
    //     return _lastFrontDoorImageNotifyTime < xMinutesAgo;
    // }
}