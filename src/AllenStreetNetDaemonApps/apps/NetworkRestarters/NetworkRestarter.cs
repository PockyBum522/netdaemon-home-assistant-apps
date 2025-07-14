using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using AllenStreetNetDaemonApps.Utilities.NotificationUtilities;
using HomeAssistantGenerated;
using NetDaemon.Extensions.Scheduler;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace AllenStreetNetDaemonApps.NetworkRestarters;

[NetDaemonApp]
public class NetworkRestarter
{
    private readonly IHaContext _ha;
    private readonly ILogger _logger;
    private readonly Entities _entities;
    
    private readonly InputBooleanEntity _networkRestartToggle;
    private readonly TextNotifier _textNotifier;

    private bool _networkIsRestarting;
    
    public NetworkRestarter(ILogger logger, IHaContext ha, INetDaemonScheduler scheduler)
    {
        _logger = logger;
        _ha = ha;

        _entities = new Entities(_ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();

        // _logger = new LoggerConfiguration()
        //     .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
        //     .MinimumLevel.Information()
        //     .WriteTo.Console()
        //     .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
        //     .CreateLogger();

        _networkRestartToggle = _entities.InputBoolean.RestartAllNetworkDevices;
        
        // ReSharper disable once AsyncVoidLambda because I'm fairly sure it is useless
        scheduler.RunEvery(TimeSpan.FromSeconds(2), async () => await CheckAllToggles());
        
        // For testing, uncomment this
        // scheduler.RunIn(TimeSpan.FromSeconds(2), async () => await RestartAllNetworkEquipment());
        
        _textNotifier = new TextNotifier(_logger, ha);
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
    }
    
    private async Task CheckAllToggles()
    {
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        
        if (_networkRestartToggle.State != "on")
        {
            _networkIsRestarting = false;
            return;
        }
        
        await Task.Delay(TimeSpan.FromSeconds(0.5));

        if (_networkIsRestarting == false)
        {
            _networkIsRestarting = true;

            // Lazy thread 'safety'
            await Task.Delay(TimeSpan.FromSeconds(0.5));
            
            var nl = Environment.NewLine + Environment.NewLine;

            _textNotifier.NotifyAll(
                "Network Restart in 20s",
                $"Network restarting in 20 seconds.{nl}Please turn off restart network switch if this is not desired.");
            
            // TODO: Make this 25s when finished
            await Task.Delay(TimeSpan.FromSeconds(25));
            
            // Second check after 25s to make sure they really meant to
            if (_networkRestartToggle.State != "on") return;
        
            _textNotifier.NotifyAll(
                "Network Restarting Now",
                $"Network restarting imminently.{nl}Please follow progress in ND logs for NetworkRestarter.");
        
            // Leave this for lazy threading block
            await Task.Delay(TimeSpan.FromSeconds(5));
            
            await RestartAllNetworkEquipment();
            
            // After we've finished, reset dashboard controls and lazy thread safety bool:
            _networkRestartToggle.TurnOff();
            _networkIsRestarting = false;

            var countdownTimeout = 600;
            
            while (countdownTimeout-- > 0)
            {
                await Task.Delay(1000);

                if (NotifyWhenPingToInternetSucceeds()) break;
            }

            if (NotifyWhenPingToInternetSucceeds())
            {
                _textNotifier.NotifyAll(
                    "Network finished restart!",
                    $"Network restarted, internet now restored, you can use everything normally!");
            }
            else
            {
                _textNotifier.NotifyAll(
                    "Network finished restart (Error)",
                    $"Network restarted, internet was not restored in 10 minutes. There may be a problem with your internet service");
            }
        }
    }

    private async Task RestartAllNetworkEquipment()
    {
        await TurnOffShopNetworkEquipment();
        
        await TurnOffNetworkClosetCamsPoeSwitch();
        
        await TurnOffLaundryRoomNetworkEquipment();
            
        await TurnOffMasterBathroomCabinetNetworkEquipment();
            
        await ShutdownMikrotikMainRouterViaSsh();
            
        // Lazy wait for router shutdown
        await Task.Delay(TimeSpan.FromSeconds(30));
            
        await TurnOffNetworkClosetEquipment();
            
        // Wait a bit with everything off. This is overkill but the modem takes like 5 minutes to come back up anyways sooo
        await Task.Delay(TimeSpan.FromSeconds(60));
            
        await TurnOnNetworkClosetEquipment();
        
        // Wait for main router to come back up. Timed at 60 seconds, so let's wait just a hair longer to start auxiliary switches 
        await Task.Delay(TimeSpan.FromSeconds(70));
        
        await TurnOnMasterBathroomCabinetNetworkEquipment();
            
        await TurnOnLaundryRoomNetworkEquipment();
        
        await TurnOnNetworkClosetCamsPoeSwitch();
        
        // Wait for laundry switch to come back up, this can be removed when garage router is directly connected to main router
        await Task.Delay(TimeSpan.FromSeconds(20));
        
        await TurnOnShopNetworkEquipment();
    }

    private async Task TurnOffShopNetworkEquipment()
    {
        var entityToWork = _entities.Switch.NetworkPowerForShopNetwork;
            
        _logger.Information("_entities.Switch.NetworkPowerShop State: {State}", entityToWork.State);

        _logger.Information("Turning off: _entities.Switch.NetworkPowerShop");
            
        entityToWork.TurnOff();   
            
        await Task.Delay(TimeSpan.FromSeconds(10));
            
        _logger.Information("_entities.Switch.NetworkPowerShop State: {State}", entityToWork.State);
    }
    
    private async Task TurnOnShopNetworkEquipment()
    {
        var entityToWork = _entities.Switch.NetworkPowerForShopNetwork;
            
        _logger.Information("_entities.Switch.NetworkPowerShop State: {State}", entityToWork.State);

        _logger.Information("Turning on: _entities.Switch.NetworkPowerShop");
            
        entityToWork.TurnOn();   
            
        await Task.Delay(TimeSpan.FromSeconds(10));
            
        _logger.Information("_entities.Switch.NetworkPowerShop State: {State}", entityToWork.State);
    }
    
    private async Task TurnOffMasterBathroomCabinetNetworkEquipment()
    {
        var entityToWork = _entities.Switch.NetworkPowerInMasterBedroomClosetAndMasterBathCabinet;
            
        _logger.Information("_entities.Light.NetworkPowerMasterBathCabinetEquipment State: {State}", entityToWork.State);

        _logger.Information("Turning off: _entities.Light.NetworkPowerMasterBathCabinetEquipment");
            
        entityToWork.TurnOff();      // Accidentally got dimmers so it's a light not a switch, still works.
            
        await Task.Delay(TimeSpan.FromSeconds(10));
            
        _logger.Information("_entities.Light.NetworkPowerMasterBathCabinetEquipment State: {State}", entityToWork.State);
    }    
    
    private async Task TurnOnMasterBathroomCabinetNetworkEquipment()
    {
        var entityToWork = _entities.Switch.NetworkPowerInMasterBedroomClosetAndMasterBathCabinet;
            
        _logger.Information("_entities.Light.NetworkPowerMasterBathCabinetEquipment State: {State}", entityToWork.State);

        _logger.Information("Turning on: _entities.Light.NetworkPowerMasterBathCabinetEquipment");
            
        entityToWork.TurnOn();      // Accidentally got dimmers so it's a light not a switch, still works.
            
        await Task.Delay(TimeSpan.FromSeconds(10));
            
        _logger.Information("_entities.Light.NetworkPowerMasterBathCabinetEquipment State: {State}", entityToWork.State);
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

    private async Task TurnOffLaundryRoomNetworkEquipment()
    {
        var entityToWork = _entities.Switch.NetworkPowerForUtilityCloset;
            
        _logger.Information("_entities.Light.NetworkPowerLaundryRoomEquipment State: {State}", entityToWork.State);

        _logger.Information("Turning off: _entities.Light.NetworkPowerLaundryRoomEquipment");
            
        entityToWork.TurnOff();      // Accidentally got dimmers so it's a light not a switch, still works.
            
        await Task.Delay(TimeSpan.FromSeconds(10));
            
        _logger.Information("_entities.Light.NetworkPowerLaundryRoomEquipment State: {State}", entityToWork.State);
    }
    
    private async Task TurnOffNetworkClosetEquipment()
    {
        var entityToWork = _entities.Switch.MasterPowerForAllNetworkInNetworkCloset;
        
        _logger.Information("_entities.Switch.NetworkPowerMainAllNetworkClosetEquipment State: {State}", entityToWork.State);

        _logger.Information("Turning off: _entities.Switch.NetworkPowerMainAllNetworkClosetEquipment");
            
        entityToWork.TurnOff();
            
        await Task.Delay(TimeSpan.FromSeconds(10));
            
        _logger.Information("_entities.Switch.NetworkPowerMainAllNetworkClosetEquipment State: {State}", entityToWork.State);
    }
    
    private async Task TurnOnNetworkClosetEquipment()
    {
        var entityToWork = _entities.Switch.MasterPowerForAllNetworkInNetworkCloset;
        
        _logger.Information("{EntityName} State: {State}", nameof(_entities.Switch.MasterPowerForAllNetworkInNetworkCloset),entityToWork.State);

        _logger.Information("Turning on: {EntityName}", nameof(_entities.Switch.MasterPowerForAllNetworkInNetworkCloset));
            
        entityToWork.TurnOn();
            
        await Task.Delay(TimeSpan.FromSeconds(10));

        _logger.Information("{EntityName} State: {State}", nameof(_entities.Switch.MasterPowerForAllNetworkInNetworkCloset),entityToWork.State);
    }

    private async Task TurnOnLaundryRoomNetworkEquipment()
    {
        var entityToWork = _entities.Switch.NetworkPowerForUtilityCloset;
            
        _logger.Information("_entities.Light.NetworkPowerLaundryRoomEquipment State: {State}", entityToWork.State);

        _logger.Information("Turning on: _entities.Light.NetworkPowerLaundryRoomEquipment");
            
        entityToWork.TurnOn();      // Accidentally got dimmers so it's a light not a switch, still works.
            
        await Task.Delay(TimeSpan.FromSeconds(10));
            
        _logger.Information("_entities.Light.NetworkPowerLaundryRoomEquipment State: {State}", entityToWork.State);
    }
    
    private async Task TurnOffNetworkClosetCamsPoeSwitch()
    {
        var entityToWork = _entities.Switch.NetworkPowerForSmallPoeSwitchInNetworkCloset;
        
        _logger.Information("_entities.Switch.NetworkPowerNetworkClosetPoeCameras4PortSwitch2 State: {State}", entityToWork.State);

        _logger.Information("Turning off: _entities.Switch.NetworkPowerNetworkClosetPoeCameras4PortSwitch2");
            
        entityToWork.TurnOff();

        await Task.Delay(TimeSpan.FromSeconds(2));

        _logger.Information("_entities.Switch.NetworkPowerNetworkClosetPoeCameras4PortSwitch2 State: {State}", entityToWork.State);
    }
    
    private async Task TurnOnNetworkClosetCamsPoeSwitch()
    {
        var entityToWork = _entities.Switch.NetworkPowerForSmallPoeSwitchInNetworkCloset;
        
        _logger.Information("_entities.Switch.NetworkPowerNetworkClosetPoeCameras4PortSwitch2 State: {State}", entityToWork.State);

        _logger.Information("Turning on: _entities.Switch.NetworkPowerNetworkClosetPoeCameras4PortSwitch2");
            
        entityToWork.TurnOn();

        await Task.Delay(TimeSpan.FromSeconds(10));

        _logger.Information("_entities.Switch.NetworkPowerNetworkClosetPoeCameras4PortSwitch2 State: {State}", entityToWork.State);
    }

    private bool NotifyWhenPingToInternetSucceeds()
    {
        var pingSender = new Ping ();
        var options = new PingOptions ();

        // Use the default Ttl value which is 128, but change the fragmentation behavior.
        options.DontFragment = true;

        // Create a buffer of 32 bytes of data to be transmitted.
        const string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const int timeout = 999;
        var buffer = Encoding.ASCII.GetBytes (data);
        var reply = pingSender.Send ("1.1.1.1", timeout, buffer, options);
        
        return reply is { Status: IPStatus.Success, RoundtripTime: < 200 };
    }
}
