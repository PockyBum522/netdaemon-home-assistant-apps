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
        // Mobile app names should be what is in Dev Tools > Actions > Type notify and see what autocompletes 
        attemptToNotifyMobileAppInHa("mobile_app_pixelfoldapril24", notifyTitle, notifyBody);
        
        // GET HTML5 DEVICE NAMES OUT OF html5_push_registrations.conf AND NOT THE HA ENTITY NAME
        attemptToNotifyHtml5InHa("2025-02_DAVID-DESKTOP_Firefox", notifyTitle, notifyBody);
        attemptToNotifyHtml5InHa("DAVID-LAPTOP_2025-02", notifyTitle, notifyBody);
    }
    
    public void NotifyAlyssa(string notifyTitle, string notifyBody, string? notificationId = null)
    {
        // Redo this from scratch because a lot of stuff is extremely confusing.
        
        // Test everything thoroughly for Alyssa notifications one at a time as you go
        
        // Look at working examples in NotifyDavid

        throw new NotImplementedException();
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
        
        //if (!usersToExclude.Contains(WhoToNotify.Alyssa)) NotifyAlyssa(notifyTitle, notifyBody);
    }
    
    public void NotifyUsersInHa(string notifyTitle, string notifyBody, List<WhoToNotify> usersToNotify)
    {
        if (usersToNotify.Contains(WhoToNotify.General)) NotifyGeneral(notifyTitle, notifyBody);
        
        if (usersToNotify.Contains(WhoToNotify.David)) NotifyDavid(notifyTitle, notifyBody);
        
        //if (usersToNotify.Contains(WhoToNotify.Alyssa)) NotifyAlyssa(notifyTitle, notifyBody);
    }
    
    
    private void attemptToNotifyMobileAppInHa(string deviceName, string notifyTitle, string notifyBody)
    {
        _logger.Information("About to send HA notification: {Title} | {Body} | To HA device: {DeviceName}", notifyTitle, notifyBody, deviceName);
        
        try
        {
            var dataPacket = new
            {
                title = notifyTitle,
                message = notifyBody
            };
            
            _haContext.CallService("notify", deviceName, data: dataPacket);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception: {ExMessage} (while notifying HA entity: {NotifyDeviceName}", ex.Message, deviceName);
        }
    }

    private void attemptToNotifyHtml5InHa(string haNotifyEntityName, string notifyTitle, string notifyBody)
    {
        _logger.Information("About to send HA notification: {Title} | {Body} | To HA device: {DeviceName}", notifyTitle, notifyBody, haNotifyEntityName);
        
        try
        {
            var dataPacket = new
            {
                target = haNotifyEntityName,
                title = notifyTitle,
                message = notifyBody
            };
            
            _haContext.CallService("notify", "html5", data: dataPacket);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception: {ExMessage} (while notifying HA entity: {NotifyDeviceName}", ex.Message, haNotifyEntityName);
        }
    }
}