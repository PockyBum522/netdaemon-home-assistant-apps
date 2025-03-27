using System.IO;
using System.Linq;

namespace AllenStreetNetDaemonApps.FrontDoorCameraMotion;

public class CameraImageTaker
{
    private readonly ILogger _logger;
    private readonly DateTimeOffset _lastMotionSeenAt;

    private string _cameraSnapshotsDirectory => SECRETS.CameraSnapshotDirectory;
    private string _mediaSnapshotsDirectory => SECRETS.MediaSnapshotDirectory;
    
    public CameraImageTaker(ILogger logger)
    {
        _logger = logger;
        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        // _logger = new LoggerConfiguration()
        //     .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
        //     .MinimumLevel.Information()
        //     .WriteTo.Console()
        //     .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
        //     .CreateLogger();
        
        _lastMotionSeenAt = DateTimeOffset.Now;
    }

    public string CaptureFontDoorImage()
    {
        _logger.Debug("CaptureFontDoorImage last motion at: {LastMotionAt}, current time: {Now}", _lastMotionSeenAt, DateTimeOffset.Now);

        Directory.CreateDirectory(_cameraSnapshotsDirectory);

        var fileSafeTimestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd_HH-mm-ss.ff");

        var newImageFilename = "frontDoorCamClose_" + fileSafeTimestamp + ".jpg";

        var fullPathToMedia = Path.Join(_mediaSnapshotsDirectory, newImageFilename);
        var fullPathToLocal = Path.Join(_cameraSnapshotsDirectory, newImageFilename);
        
        //_entities.Camera.FrontDoor.Snapshot(fullPathToMedia);
        //_entities.Camera.FrontDoor.Snapshot(fullPathToLocal);
        
        _logger.Debug("Saving images to: {MediaPath} and {LocalPath}", fullPathToMedia, fullPathToLocal);
        
        DeleteImagesOlderThan(TimeSpan.FromDays(90));
        
        return newImageFilename;
    }

    private void DeleteImagesOlderThan(TimeSpan howLongAgoToDelete)
    {
        var filesToCheck = Directory.GetFiles(_cameraSnapshotsDirectory);

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

        Directory.CreateDirectory(_cameraSnapshotsDirectory);

        var fileSafeTimestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd_HH-mm-ss.ff");

        var newImageFilename = "frontDoorCamFar_" + fileSafeTimestamp + ".jpg";

        var fullPathToMedia = Path.Join(_mediaSnapshotsDirectory, newImageFilename);
        var fullPathToLocal = Path.Join(_cameraSnapshotsDirectory, newImageFilename);
        
        // _entities.Camera.FrontYardFromSwCorner.Snapshot(fullPathToMedia);
        // _entities.Camera.FrontYardFromSwCorner.Snapshot(fullPathToLocal);
        
        _logger.Debug("Saving images to: {MediaPath} and {LocalPath}", fullPathToMedia, fullPathToLocal);
        
        DeleteImagesOlderThan(TimeSpan.FromDays(90));
        
        return newImageFilename;
    }
}

