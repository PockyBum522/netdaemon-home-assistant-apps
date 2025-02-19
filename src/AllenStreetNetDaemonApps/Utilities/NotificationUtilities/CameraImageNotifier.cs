namespace AllenStreetNetDaemonApps.Utilities.NotificationUtilities;

public class CameraImageNotifier
{
    private readonly ILogger _logger;
    private readonly IHaContext _haContext;

    
    string _mediaPath => SECRETS.CameraImageNotificationCachePath;
    string _localPath => SECRETS.CameraImageNotificationLocalUrl;
    
    public CameraImageNotifier(IHaContext haContext)
    {
        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        _haContext = haContext;
    }
    
    public void NotifyGeneral(string notifyTitle, string notifyBody, string imageFileName)
    {
        _logger.Information("Notifying [GENERAL]: {Title} | {Body}", notifyTitle, notifyBody);
        
        _haContext.CallService("notify", "persistent_notification", data: new
        {
            title = notifyTitle,
            message = $"![front door motion]({Path.Join(_localPath, imageFileName)})"
        }); 
    }
    
    public void NotifyDavid(string notifyTitle, string notifyBody, string imageFileName)
    {
        var localFullPath = Path.Join(_localPath, imageFileName);
        var mediaFullPath = Path.Join(_mediaPath, imageFileName);

        mediaFullPath = mediaFullPath.Replace("/media/", "/");

        var mediaFullPathWithUrl = SECRETS.NabuUrl + mediaFullPath; 
        
        _logger.Information("Notifying [DAVID]: {Title} | BODY: {Body}", notifyTitle, notifyBody);
        _logger.Information("Notifying [DAVID] LOCAL: {LocalPath}", localFullPath);
        _logger.Information("Notifying [DAVID] MEDIA: {MediaPath}", mediaFullPathWithUrl);
        
        // _haContext.CallService("notify", "david_desktop", data: new
        // {
        //     title = notifyTitle,
        //     message = notifyBody,
        //     data = new
        //     {
        //         image = localFullPath,
        //         duration = 3
        //     }
        // });
        
        // _haContext.CallService("notify", "mobile_app_pixelfoldapril24", data: new
        // {
        //     title = notifyTitle,
        //     message = notifyBody,
        //     data = new
        //     {
        //         image = mediaFullPathWithUrl
        //     }
        // });
    }
    
    public void NotifyAlyssa(string notifyTitle, string notifyBody, string imageFileName)
    {
        var localFullPath = Path.Join(_localPath, imageFileName);
        var mediaFullPath = Path.Join(_mediaPath, imageFileName);

        mediaFullPath = mediaFullPath.Replace("/media/", "/");

        var mediaFullPathWithUrl = SECRETS.NabuUrl + mediaFullPath;
        
        _logger.Information("Notifying [ALYSSA]: {Title} | BODY: {Body}", notifyTitle, notifyBody);
        _logger.Information("Notifying [ALYSSA] LOCAL: {LocalPath}", localFullPath);
        _logger.Information("Notifying [ALYSSA] MEDIA: {MediaPath}", mediaFullPathWithUrl);
        
        // This is what was running for a while as working:
        // _haContext.CallService("notify", "mobile_app_" + SECRETS.AlyssaiPhoneMobileAppId, data: new
        // {
        //     title = notifyTitle,
        //     message = notifyBody,
        //     data = new
        //     {
        //         image = mediaFullPathWithUrl
        //     }
        // });
        
        // Reference, don't know why I wasn't using the extra bit in the middle
        // _haContext.CallService("notify", "mobile_app_" + SECRETS.AlyssaiPhoneMobileAppId, data: new
        // {
        //     title = notifyTitle,
        //     message = notifyBody,
        //     data = new
        //     {
        //         image = mediaFullPathWithUrl
        //         
        //         // This works, you have to hold down on the notification to get to options
        //         // actions = new[]
        //         // {
        //         //     new 
        //         //     {
        //         //         action = "OPEN",
        //         //         title = "View",
        //         //         uri = "https://google.com"    
        //         //     }
        //         // }
        //     }
        // });
    }
    
    // public void NotifyAll(string notifyTitle, string notifyBody)
    // {
    //     NotifyUsersInHaExcept(notifyTitle, notifyBody);    
    // }
    //
    // public void NotifyUsersInHaExcept(string notifyTitle, string notifyBody, string? notificationId = null, List<WhoToNotify>? usersToExclude = null)
    // {
    //     usersToExclude ??= new List<WhoToNotify>();
    //
    //     if (!usersToExclude.Contains(WhoToNotify.General)) NotifyGeneral(notifyTitle, notifyBody, notificationId);
    //     
    //     if (!usersToExclude.Contains(WhoToNotify.David)) NotifyDavid(notifyTitle, notifyBody, notificationId);
    //     
    //     if (!usersToExclude.Contains(WhoToNotify.Alyssa)) NotifyAlyssa(notifyTitle, notifyBody, notificationId);
    // }
    //
    // public void NotifyUsersInHa(string notifyTitle, string notifyBody, List<WhoToNotify> usersToNotify, string? notificationId = null)
    // {
    //     if (usersToNotify.Contains(WhoToNotify.General)) NotifyGeneral(notifyTitle, notifyBody, notificationId);
    //     
    //     if (usersToNotify.Contains(WhoToNotify.David)) NotifyDavid(notifyTitle, notifyBody, notificationId);
    //     
    //     if (usersToNotify.Contains(WhoToNotify.Alyssa)) NotifyAlyssa(notifyTitle, notifyBody, notificationId);
    // }
}