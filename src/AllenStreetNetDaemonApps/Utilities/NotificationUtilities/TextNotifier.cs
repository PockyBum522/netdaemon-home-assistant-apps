namespace AllenStreetNetDaemonApps.Utilities.NotificationUtilities;

public class TextNotifier
{
    private readonly ILogger _logger;
    private readonly IHaContext _haContext;

    public TextNotifier(ILogger logger, IHaContext haContext)
    {
        _logger = logger;
        _haContext = haContext;
    }
    
    public void NotifyGeneral(string notifyTitle, string notifyBody)
    {
        _logger.Information("Notifying [GENERAL]: {Title} | {Body}", notifyTitle, notifyBody);
        
        _haContext.CallService("notify", "persistent_notification", data: new
        {
            title = notifyTitle,
            message = notifyBody
        }); 
        
        _haContext.CallService("notify", "tv_desktop", data: new
        {
            title = notifyTitle,
            message = notifyBody
        });
    }
    
    public void NotifyDavid(string notifyTitle, string notifyBody, string? notificationId = null)
    {
        _logger.Information("Notifying [DAVID]: {Title} | {Body}", notifyTitle, notifyBody);
        
        _haContext.CallService("notify", "david_desktop", data: new
        {
            title = notifyTitle,
            message = notifyBody
        });
        
        _haContext.CallService("notify", "david_laptop", data: new
        {
            title = notifyTitle,
            message = notifyBody
        });
        
        _haContext.CallService("notify", "mobile_app_pixel_fold", data: new
        {
            title = notifyTitle,
            message = notifyBody
        });
        
        // _haContext.CallService("notify", "guest_bedroom", data: new
        // {
        //     title = notifyTitle,
        //     message = notifyBody
        // });
    }
    
    public void NotifyAlyssa(string notifyTitle, string notifyBody, string? notificationId = null)
    {
        _logger.Information("Notifying [ALYSSA]: {Title} | {Body}", notifyTitle, notifyBody);

        _haContext.CallService("notify", "mobile_app_" + SECRETS.AlyssaiPhoneMobileAppId, data: new
        {
            title = notifyTitle,
            message = notifyBody
        });
    }

    public void NotifyAll(string notifyTitle, string notifyBody)
    {
        NotifyUsersInHaExcept(notifyTitle, notifyBody);    
    }

    public void NotifyUsersInHaExcept(string notifyTitle, string notifyBody, List<WhoToNotify>? usersToExclude = null)
    {
        usersToExclude ??= new List<WhoToNotify>();

        if (!usersToExclude.Contains(WhoToNotify.General)) NotifyGeneral(notifyTitle, notifyBody);
        
        if (!usersToExclude.Contains(WhoToNotify.David)) NotifyDavid(notifyTitle, notifyBody);
        
        if (!usersToExclude.Contains(WhoToNotify.Alyssa)) NotifyAlyssa(notifyTitle, notifyBody);
    }
    
    public void NotifyUsersInHa(string notifyTitle, string notifyBody, List<WhoToNotify> usersToNotify)
    {
        if (usersToNotify.Contains(WhoToNotify.General)) NotifyGeneral(notifyTitle, notifyBody);
        
        if (usersToNotify.Contains(WhoToNotify.David)) NotifyDavid(notifyTitle, notifyBody);
        
        if (usersToNotify.Contains(WhoToNotify.Alyssa)) NotifyAlyssa(notifyTitle, notifyBody);
    }
}