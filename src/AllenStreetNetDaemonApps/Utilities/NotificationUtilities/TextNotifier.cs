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
    }
    
    public void NotifyDavid(string notifyTitle, string notifyBody, string? notificationId = null)
    {
        _logger.Information("Notifying [DAVID]: {Title} | {Body}", notifyTitle, notifyBody);
        
        _haContext.CallService("notify", "html5", data: new
        {
            target = "html5_2025_02_david_desktop_firefox",
            title = notifyTitle,
            message = notifyBody
        });
        
        _haContext.CallService("notify", "mobile_app_pixelfoldapril24", data: new
        {
            title = notifyTitle,
            message = notifyBody
        });
    }
    
    public void NotifyAlyssa(string notifyTitle, string notifyBody, string? notificationId = null)
    {
        _logger.Information("Notifying [ALYSSA]: {Title} | {Body}", notifyTitle, notifyBody);

        _haContext.CallService("notify", "mobile_app_" + SECRETS.AlyssaIPhoneMobileAppId, data: new
        {
            title = notifyTitle,
            message = notifyBody
        });
        
        _haContext.CallService("notify", "html5", data: new
        {
            target = "html5_alyssa_desktop_chrome_2025_02",
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
        usersToExclude ??= [];

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