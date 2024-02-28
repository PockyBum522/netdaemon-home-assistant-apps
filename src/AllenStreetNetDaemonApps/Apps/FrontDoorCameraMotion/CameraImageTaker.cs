namespace AllenStreetNetDaemonApps.Apps.FrontDoorCameraMotion;

public class CameraImageTaker
{
    private readonly Entities _entities;
    
    private readonly ILogger _logger;
    private readonly DateTimeOffset _lastMotionSeenAt;

    private string CameraSnapshotsDirectory => SECRETS.CameraSnapshotDirectory;
    private string MediaSnapshotsDirectory => SECRETS.MediaSnapshotDirectory;
    
    public CameraImageTaker(IHaContext ha)
    {
        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Verbose()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        _entities = new Entities(ha);
        
        _lastMotionSeenAt = DateTimeOffset.Now;
    }

    public string CaptureFontDoorImage()
    {
        _logger.Debug("CaptureFontDoorImage last motion at: {LastMotionAt}, current time: {Now}", _lastMotionSeenAt, DateTimeOffset.Now);

        Directory.CreateDirectory(CameraSnapshotsDirectory);

        var fileSafeTimestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd_HH-mm-ss.ff");

        var newImageFilename = "frontDoorCamClose_" + fileSafeTimestamp + ".jpg";

        var fullPathToMedia = Path.Join(MediaSnapshotsDirectory, newImageFilename);
        var fullPathToLocal = Path.Join(CameraSnapshotsDirectory, newImageFilename);
        
        _entities.Camera.FrontDoor.Snapshot(fullPathToMedia);
        _entities.Camera.FrontDoor.Snapshot(fullPathToLocal);
        
        _logger.Debug("Saving images to: {MediaPath} and {LocalPath}", fullPathToMedia, fullPathToLocal);
        
        DeleteImagesOlderThan(TimeSpan.FromDays(90));
        
        return newImageFilename;
    }

    private void DeleteImagesOlderThan(TimeSpan howLongAgoToDelete)
    {
        var filesToCheck = Directory.GetFiles(CameraSnapshotsDirectory);

        foreach (var filePath in filesToCheck)
        {
            var currentFileInfo = new FileInfo(filePath);

            var now = DateTimeOffset.Now;

            var whenToDeleteOlderThan = now - howLongAgoToDelete;

            if (currentFileInfo.CreationTime >= whenToDeleteOlderThan) continue;
            
            _logger.Information("Deleting old snapshot: {Path}", filePath);
            File.Delete(filePath);
        }
    }

    public string CaptureFontDoorImageFromFarCam()
    {
        _logger.Debug("CaptureFontDoorImageFromFarCam last motion at: {LastMotionAt}, current time: {Now}", _lastMotionSeenAt, DateTimeOffset.Now);

        Directory.CreateDirectory(CameraSnapshotsDirectory);

        var fileSafeTimestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd_HH-mm-ss.ff");

        var newImageFilename = "frontDoorCamFar_" + fileSafeTimestamp + ".jpg";

        var fullPathToMedia = Path.Join(MediaSnapshotsDirectory, newImageFilename);
        var fullPathToLocal = Path.Join(CameraSnapshotsDirectory, newImageFilename);
        
        _entities.Camera.FrontYardFromSwCorner.Snapshot(fullPathToMedia);
        _entities.Camera.FrontYardFromSwCorner.Snapshot(fullPathToLocal);
        
        _logger.Debug("Saving images to: {MediaPath} and {LocalPath}", fullPathToMedia, fullPathToLocal);
        
        DeleteImagesOlderThan(TimeSpan.FromDays(90));
        
        return newImageFilename;
    }
}

