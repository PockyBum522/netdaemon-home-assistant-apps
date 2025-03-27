//namespace AllenStreetNetDaemonApps.Apps.FrontDoorNfcReader;

// TODO: Eventually move everything reader related back to here once I have figured out how best to handle MQTT messages going to different apps

// [NetDaemonApp]
// public class MainController
// {
//     private readonly IHaContext _ha;
//     private readonly ILogger _logger;
//     private readonly ILogger _loopLogger;
//     
//     private Task<UdpReceiveResult>? _udpReceiveTask;
//     private DateTimeOffset _lastTagScannedTime;
//     private UdpClient? _receivingUdpClient;
//     private readonly Entities _entities;
//
//     private int _udpReinitializeCounter;
//     
//     private readonly TextNotifier _textNotifier;
//     
//     public MainController(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger)
//     {
//         _ha = ha;
//
//         _entities = new Entities(_ha);
//
//         var namespaceLastPart = GetType().Namespace?.Split('.').Last();
//         
//         _logger = new LoggerConfiguration()
//             .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
//             .MinimumLevel.Information()
//             .WriteTo.Console()
//             .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
//             .CreateLogger();
//         
//         _loopLogger = new LoggerConfiguration()
//             .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
//             .MinimumLevel.Information()
//             .WriteTo.Console()
//             .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_SCHEDULED_CHECK.log", rollingInterval: RollingInterval.Day)
//             .CreateLogger();
//         
//         _lastTagScannedTime = DateTimeOffset.Now;
//         
//         _logger.Information("Initialized {NamespaceLastPart} v0.02", namespaceLastPart);
//         
//         _textNotifier = new TextNotifier(_logger, ha);
//         
//         // Two seconds works fine. 0.25 seconds does not ever see UDP message
//         
//         // Disabled in preparation for moving reader to MQTT
//         
//         // scheduler.RunEvery(TimeSpan.FromSeconds(1), async () => await CheckForUdpMessage());
//         // scheduler.RunEvery(TimeSpan.FromSeconds(10), () => LogDebugStatus());
//     }
// }