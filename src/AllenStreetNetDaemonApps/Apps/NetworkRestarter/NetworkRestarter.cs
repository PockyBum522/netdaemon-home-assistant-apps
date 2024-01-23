using Renci.SshNet;
using Renci.SshNet.Common;

namespace AllenStreetNetDaemonApps.Apps.NetworkRestarter;

[NetDaemonApp]
public class NetworkRestarter
{
    private readonly IHaContext _ha;
    private readonly Serilog.ILogger _logger;
    private readonly Entities _entities;
    
    private readonly InputBooleanEntity _networkRestartToggle;
    private readonly TextNotifier _textNotifier;

    private bool _networkIsRestarting;
    
    public NetworkRestarter(IHaContext ha, INetDaemonScheduler scheduler)
    {
        _ha = ha;

        _entities = new Entities(_ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();

        _logger = new LoggerConfiguration()
            .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        _networkRestartToggle = _entities.InputBoolean.RestartAllNetworkDevices;
        
        // ReSharper disable once AsyncVoidLambda because I'm fairly sure it is useless
        scheduler.RunEvery(TimeSpan.FromSeconds(2), async () => await CheckAllToggles());
        
        _textNotifier = new TextNotifier(_logger, ha);
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
    }
    
    private async Task CheckAllToggles()
    {
        await Task.Delay(500);
        
        if (_networkRestartToggle.State != "on")
        {
            _networkIsRestarting = false;
            return;
        }
        
        await Task.Delay(500);

        if (_networkIsRestarting == false)
        {
            _networkIsRestarting = true;
            
            var nl = Environment.NewLine + Environment.NewLine;

            _textNotifier.NotifyUsersInHaExcept(
                "Network Restart in 20s",
                $"Network restarting in 20 seconds.{nl}Please turn off restart network switch if this is not desired.",
                new List<WhoToNotify>(){WhoToNotify.Alyssa});
            
            // TODO: Make this 25s when finished
            await Task.Delay(TimeSpan.FromSeconds(5));
            
            // Second check after 25s to make sure they really meant to
            if (_networkRestartToggle.State != "on") return;
        
            _textNotifier.NotifyUsersInHaExcept(
                "Network Restarting Now",
                $"Network restarting imminently.{nl}Please follow progress in ND logs for NetworkRestarter.",
                new List<WhoToNotify>(){WhoToNotify.Alyssa});
        
            // Leave this for lazy threading block
            await Task.Delay(TimeSpan.FromSeconds(5));
            
            await TurnOffLaundryRoomNetworkEquipment();
            
            // await TurnOffMasterBathroomCabinetNetworkEquipment();
            //
            // await ShutdownMikrotikMainRouterViaSsh();
            
            // After we've finished, reset dashboard controls and lazy thread safety bool:
            _networkRestartToggle.TurnOff();
            _networkIsRestarting = false;

            await Task.Delay(500);
        }
        
    }

    private async Task ShutdownMikrotikMainRouterViaSsh()
    {
        var privateKeyFilename = "tik-routr-priv";

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        
            File.WriteAllText(privateKeyFilename, SECRETS.RouterSshPrivateKey);
        
            // Setup Credentials and Server Information
            var connectionInfo = new ConnectionInfo("192.168.1.1", 22, SECRETS.RouterSshUsername,
                [
                    new PrivateKeyAuthenticationMethod(SECRETS.RouterSshUsername,
                    [
                        new PrivateKeyFile(privateKeyFilename,"")
                    ])
                ]
            );

            // Execute a (SHELL) Command - prepare upload directory
            using var sshclient = new SshClient(connectionInfo);
        
            sshclient.Connect();

            using var cmd = sshclient.CreateCommand("/system shutdown");
            cmd.Execute();
                
            _logger.Information("SSH Command > {Text}", cmd.CommandText);
            _logger.Information("Exit status value = {Value}", cmd.ExitStatus);

            sshclient.Disconnect();

            await Task.Delay(TimeSpan.FromSeconds(1));
        
            File.Delete(privateKeyFilename);
        }
        catch (SshConnectionException)
        {
            _logger.Information("SSH Connection exception raised! Router just likely shut down properly");
        }
    }

    private async Task TurnOffMasterBathroomCabinetNetworkEquipment()
    {
        _logger.Information("_entities.Light.NetworkPowerMasterBathCabinetEquipment State: {State}", _entities.Light.NetworkPowerLaundryRoomEquipment.State);

        _logger.Information("Turning off: _entities.Light.NetworkPowerMasterBathCabinetEquipment");
            
        _entities.Light.NetworkPowerMasterBathCabinetEquipment.TurnOff();      // Accidentally got dimmers so it's a light not a switch, still works.
            
        await Task.Delay(TimeSpan.FromSeconds(10));
            
        _logger.Information("_entities.Light.NetworkPowerMasterBathCabinetEquipment State: {State}", _entities.Light.NetworkPowerLaundryRoomEquipment.State);

    }

    private async Task TurnOffLaundryRoomNetworkEquipment()
    {
        
        _logger.Information("_entities.Light.NetworkPowerLaundryRoomEquipment State: {State}", _entities.Light.NetworkPowerLaundryRoomEquipment.State);

        _logger.Information("Turning off: _entities.Light.NetworkPowerLaundryRoomEquipment");
            
        _entities.Light.NetworkPowerLaundryRoomEquipment.TurnOff();      // Accidentally got dimmers so it's a light not a switch, still works.
            
        await Task.Delay(TimeSpan.FromSeconds(10));
            
        _logger.Information("_entities.Light.NetworkPowerLaundryRoomEquipment State: {State}", _entities.Light.NetworkPowerLaundryRoomEquipment.State);
    }
}
