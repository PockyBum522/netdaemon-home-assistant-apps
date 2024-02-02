using MQTTnet;
using MQTTnet.Client;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AllenStreetNetDaemonApps.Apps.FrontDoorCameraMotion;

[NetDaemonApp]
public class MqttWatcher 
{
    private readonly Entities _entities;
    
    private readonly ILogger _logger;
    private DateTimeOffset _lastLockWakeAtTime;
    private DateTimeOffset _lastFrontDoorImageNotifyTime;

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
        
        _lastLockWakeAtTime = DateTimeOffset.Now - TimeSpan.FromMinutes(5);
        _lastFrontDoorImageNotifyTime = DateTimeOffset.Now - TimeSpan.FromMinutes(5);

        _cameraImageNotifier = new CameraImageNotifier(_logger, ha);

        _cameraImageTaker = new CameraImageTaker(_logger, ha);
            
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        // For Testing:
        scheduler.RunIn(TimeSpan.FromSeconds(15), async () => await NotifyUsersWithCameraImage());
        
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
        
        if (!LastLockWakeTimeWasMoreThanXMinutesAgo())
        {
            _logger.Information("Not waking lock/sending picture notification since timeout has not elapsed");

            _lastLockWakeAtTime = DateTimeOffset.Now;

            return;
        }
        
        if (e.ApplicationMessage.Topic == SECRETS.FrontDoorMotionZoneATopic && asciiPayload.Equals("triggered"))
        {            
            _logger.Information("Waking front door lock/sending picture notification");

            WakeFrontDoorLock();

            await NotifyUsersWithCameraImage();
            
            _lastFrontDoorImageNotifyTime = DateTimeOffset.Now;
        }
    }

    private async Task NotifyUsersWithCameraImage()
    {
        if (!LastDoorImageNotificationWasMoreThanXMinutesAgo()) return;
            
        var newImageFilename = _cameraImageTaker.CaptureFontDoorImage();
        var newFarImageFilename = _cameraImageTaker.CaptureFontDoorImageFromFarCam();

        _logger.Information("New image path is: {Path}", newImageFilename);

        // Increase this if notifications ever come through without images
        // Offset if this runs twice rapidly to hopefully stop a race condition where _lastRunAtTime isn't updated as fast as it needs to be
        await Task.Delay(new Random().Next(2000, 2200));

        var stackedPath = StackPicturesVertically(newImageFilename, newFarImageFilename);
        _logger.Information("Stacked image path is: {Path}", newImageFilename);
        
        // Needs to be a while for the image to be ready to serve over nabu casa url and media folder
        await Task.Delay(new Random().Next(30000, 31000));
        
        _cameraImageNotifier.NotifyDavid("Front Door Motion", "", stackedPath); 
        _cameraImageNotifier.NotifyAlyssa("Front Door Motion", "", stackedPath);
        _cameraImageNotifier.NotifyGeneral("Front Door Motion", "", stackedPath);    
    
        await Task.Delay(2000);
    }

    private string StackPicturesVertically(string newImageFilename, string newFarImageFilename)
    {
        using var closeImage = Image.Load<Rgba32>(Path.Join(SECRETS.CameraSnapshotDirectory, newImageFilename));
        using var farImage = Image.Load<Rgba32>(Path.Join(SECRETS.CameraSnapshotDirectory, newFarImageFilename));
        
        var newHeight = (closeImage.Height + farImage.Height) / 2;
        
        var newWidth = closeImage.Width;

        if (farImage.Width > closeImage.Width)
            newWidth = farImage.Width;

        using var outputImage = new Image<Rgba32>(newWidth, newHeight);
        
        var closeImageHalfHeight = closeImage.Height / 2;

        // closeImage.Mutate(
        //     i => i.Crop(
        //         new Rectangle(0, 0, closeImage.Width, closeImageHalfHeight + 50)));
        
        // take the 2 source images and draw them onto the image
        outputImage.Mutate(o => o
                .DrawImage(closeImage, new Point(0, 0), 1f) // draw the first one top left
                .DrawImage(farImage, new Point(0, closeImageHalfHeight + 50), 1f) // draw the second next to it
        );

        // foreach (var font in SystemFonts.)
        // {
        //     _logger.Warning("{FontName}", font.Name);
        // }
        
        // outputImage.Mutate(o => o.DrawText(
        //     DateTimeOffset.Now.ToString("F"),
        //     SystemFonts.CreateFont("Arial", 25),
        //     new Color(Rgba32.ParseHex("#FFFFFFFFFFF")),
        //     new PointF(10, outputImage.Height - 200)
        //     ));
        
        var fileSafeTimestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd_HH-mm-ss.ff");

        var stackedImageFilename = "vertStacked_" + fileSafeTimestamp + ".png";

        var fullPathToMedia = Path.Join(SECRETS.CameraSnapshotDirectory, stackedImageFilename);
        
        outputImage.Save(fullPathToMedia);
        
        return stackedImageFilename;
    }

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

    private bool LastDoorImageNotificationWasMoreThanXMinutesAgo()
    {
        var xMinutesAgo = DateTimeOffset.Now - TimeSpan.FromMinutes(3);

        // _logger.Debug("Two minutes ago: {TwoMinutesAgo}", xMinutesAgo);
        // _logger.Debug("_lastLockWakeTime: {LastLockWakeTime}", _lastRunAtTime);
        // _logger.Debug(
        //     "_lastLockWakeTime < twoMinutesAgo: {LastWakeTimeMoreThanTwoMinutesAgo}",
        //     _lastRunAtTime < xMinutesAgo);
        
        return _lastFrontDoorImageNotifyTime < xMinutesAgo;
    }

    private void WakeFrontDoorLock()
    {
        _entities.Button.FrontDoorWake.Press();

        _logger.Information("Front door lock wake up command sent");
    }
}