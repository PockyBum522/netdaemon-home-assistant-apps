using System.Net.Sockets;

namespace AllenStreetNetDaemonApps.Apps.FrontDoorNfcReader;

[NetDaemonApp]
public class MainController
{
    private readonly IHaContext _ha;
    private readonly ILogger _logger;
    private readonly ILogger _loopLogger;
    
    private Task<UdpReceiveResult>? _udpReceiveTask;
    private DateTimeOffset _lastTagScannedTime;
    private UdpClient? _receivingUdpClient;
    private readonly Entities _entities;

    private int _udpReinitializeCounter = 0;
    
    private readonly TextNotifier _textNotifier;
    
    public MainController(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger)
    {
        _ha = ha;

        _entities = new Entities(_ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        _loopLogger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_SCHEDULED_CHECK.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        _lastTagScannedTime = DateTimeOffset.Now;
        
        _logger.Information("Initialized {NamespaceLastPart} v0.02", namespaceLastPart);
        
        _textNotifier = new TextNotifier(_logger, ha);
        
        // Two seconds works fine. 0.25 seconds does not ever see UDP message
        
        // Disabled in preparation for moving reader to MQTT
        
        // scheduler.RunEvery(TimeSpan.FromSeconds(1), async () => await CheckForUdpMessage());
        // scheduler.RunEvery(TimeSpan.FromSeconds(10), () => LogDebugStatus());
    }

    private void LogDebugStatus()
    {
        var logMessage = $@"

            Debug info:
                _receivingUdpClient: {_receivingUdpClient}

                _udpReceiveTask: {_udpReceiveTask}
                _udpReceiveTask status: {_udpReceiveTask?.Status}

                Last valid tag received: {_lastTagScannedTime}

";
        
        _logger.Debug("{LogMessage}", logMessage);
    }

    private async Task CheckForUdpMessage()
    {
        _loopLogger.Information("Scheduled task starting");
        
        _udpReceiveTask ??= InitializeUdpListener();

        // Reinit every ~10 minutes and let's see if this keeps things behaving
        if (_udpReinitializeCounter++ > 600)
        {
            _udpReceiveTask = InitializeUdpListener();

            _udpReinitializeCounter = 0;
        }

        if (_udpReceiveTask is null)
            throw new ArgumentNullException(nameof(_udpReceiveTask));
        
        _loopLogger.Information("_udpReceiveTask: {UdpReceiveTask}", _udpReceiveTask);
        _loopLogger.Information("_udpReceiveTask: {UdpReceiveTaskStatus}", _udpReceiveTask.Status);
        _loopLogger.Information("_udpReceiveException: {UdpReceiveTaskException}", _udpReceiveTask.Exception);
        
        if (!_udpReceiveTask.IsCompleted) return;
        
        // Otherwise:
        await WorkUdpMessageAsync();
    }

    private Task<UdpReceiveResult>? InitializeUdpListener()
    {
        // If there's an existing one, dispose before reinitializing
        _receivingUdpClient?.Dispose();
        
        // 20000 is external port 45554 in the HA Netdaemon Add-On configuration tab
        var portToUse = 20000;
        
        _receivingUdpClient = new UdpClient(portToUse);
        
        _logger.Information("Starting UDP Listener on port {Port}", portToUse);
    
        _logger.Information("Listening for new UDP message...");
            
        try
        {
            return _receivingUdpClient.ReceiveAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Exception: {ExMessage}", ex.Message);
        }

        return null;
    }

    private async Task WorkUdpMessageAsync()
    {
        if (_udpReceiveTask is null)
            throw new ArgumentNullException(nameof(_udpReceiveTask));
        
        var udpResult = await _udpReceiveTask;
        
        // Reset this so that it will reinitialize next scheduled run
        _udpReceiveTask = null;
        
        _logger.Information(
            "Checking last valid tag scan time: {LastScanTime} and seeing if it's more than ten seconds old", 
            _lastTagScannedTime);
        
        // If ten seconds has not passed since the last tag scan time, just ignore
        if (_lastTagScannedTime > DateTimeOffset.Now - TimeSpan.FromSeconds(10))
        {
            _logger.Warning("Ignoring this tag scan because last valid scan was less than ten seconds ago");
            return;
        }
        
        // Wake the lock because we're probably about to send an unlock command, this should make that a hair faster
        _entities.Button.FrontDoorWake.Press();
        
        // Otherwise:
        var udpDataAsString = ToHexString(udpResult.Buffer);
        
        _logger.Information("Last tag scan more than ten seconds old. " +
                            "Incoming UDP Message (Raw): {ReturnData}", udpDataAsString);
        
        try
        {
            var matchedTag = LookupTagByUid(udpDataAsString);
            
            // Since we didn't throw:
            _logger.Information("Tag found in TagDatabase: {TagFriendlyName}", matchedTag.FriendlyName);
        
            _logger.Information("About to send unlock command");
            
            // Since we have a valid tag, stop checking for 10s
            _lastTagScannedTime = DateTimeOffset.Now;

            OpenFrontDoorLatches();

            _textNotifier.NotifyDavid($"{matchedTag.FriendlyName} authenticated", $"{matchedTag.FriendlyName} has been authenticated and front door has been unlocked" );
            
            // Lock will unlock from LockController seeing that the door is open
        }
        catch (KeyNotFoundException)
        {
            _logger.Warning("Tag not found in TagDatabase with UID: {Uid}", udpDataAsString);
            _logger.Warning("Unlock command NOT sent");
            
            _textNotifier.NotifyDavid($"{udpDataAsString} not in tag DB", $"{udpDataAsString} was scanned at the front door but is not in database. Please add if you wish to authenticate with this tag" );
        }
    }

    private void OpenFrontDoorLatches()
    {
        var deadboltLatch = _entities.Switch.DeadboltDoorframeLatch;
        var doorknobLatch = _entities.Switch.DoorknobDoorframeLatch;
        
        deadboltLatch.TurnOn();
        doorknobLatch.TurnOn();
        
        // We can try unlocking here to get a head start
        _entities.Lock.FrontDoor.Unlock();
    }

    private static NfcTag LookupTagByUid(string uid)
    {
        foreach (var tag in SECRETS.AuthorizedNfcTags)
        {
            if (!tag.Uid.Equals(uid)) continue;
            
            // Otherwise, tag is a match:
            return tag;
        }
    
        // If we got through the whole database of tags
        throw new KeyNotFoundException("No matching Tag UID was found");
    }
    
    private static string ToHexString(byte[] data)
    {
        var hexString = BitConverter.ToString(data);
    
        return hexString.Replace("-", ":");
    }
}