using System.IO;
using NetDaemon.HassModel;
using ILogger = Serilog.ILogger;

namespace NetdaemonApps.Utilities.NotificationUtilities;

public class CameraImageNotifier
{
    private readonly ILogger _logger;
    private readonly IHaContext _haContext;

    
    string MediaPath => SECRETS.CameraImageNotificationCachePath;
    string LocalPath => SECRETS.CameraImageNotificationLocalUrl;
    
    public CameraImageNotifier(ILogger logger, IHaContext haContext)
    {
        _logger = logger;
        _haContext = haContext;
    }
    
    public void NotifyGeneral(string notifyTitle, string notifyBody, string imageFileName)
    {
        _logger.Information("Notifying [GENERAL]: {Title} | {Body}", notifyTitle, notifyBody);
        
        _haContext.CallService("notify", "persistent_notification", data: new
        {
            title = notifyTitle,
            message = $"![front door motion]({Path.Join(LocalPath, imageFileName)})"
        }); 
    }
    
    public void NotifyDavid(string notifyTitle, string notifyBody, string imageFileName)
    {
        var localFullPath = Path.Join(LocalPath, imageFileName);
        var mediaFullPath = Path.Join(MediaPath, imageFileName);
        
        _logger.Information("Notifying [DAVID]: {Title} | {Body} | LOCAL: {LocalPath} | MEDIA: {MediaPath}",
            notifyTitle, notifyBody, localFullPath, mediaFullPath);
        
        _haContext.CallService("notify", "david_desktop", data: new
        {
            title = notifyTitle,
            message = notifyBody,
            data = new
            {
                image = localFullPath,
                duration = 3
            }
        });
        
        // _haContext.CallService("notify", "david_laptop", data: new
        // {
        //     title = notifyTitle,
        //     message = notifyBody
        // });
        
        _haContext.CallService("notify", "mobile_app_pixel_fold", data: new
        {
            title = notifyTitle,
            message = notifyBody,
            data = new
            {
                image = mediaFullPath
            }
        });
        
        // _haContext.CallService("notify", "guest_bedroom", data: new
        // {
        //     title = notifyTitle,
        //     message = notifyBody,
        //     notification_id = notificationId
        // });
        
    }
    
    public void NotifyAlyssa(string notifyTitle, string notifyBody, string imageFileName)
    {
        _logger.Information("Notifying [ALYSSA]: {Title} | {Body}", notifyTitle, notifyBody);
        
        _haContext.CallService("notify", "mobile_app_alyssaphone23", data: new
        {
            title = notifyTitle,
            message = notifyBody,
            data = new
            {
                image = Path.Join(MediaPath, imageFileName)
                
                // This works, you have to hold down on the notification to get to options
                // actions = new[]
                // {
                //     new 
                //     {
                //         action = "OPEN",
                //         title = "View",
                //         uri = "https://google.com"    
                //     }
                // }
            }
        });    
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