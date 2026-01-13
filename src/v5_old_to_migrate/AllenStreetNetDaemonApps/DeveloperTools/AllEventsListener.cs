using System.Linq;
using HomeAssistantGenerated;
using NetDaemon.Extensions.Scheduler;

namespace AllenStreetNetDaemonApps.DeveloperTools;

[NetDaemonApp]
public class AllEventsListener
{
    private readonly IHaContext _ha;
    private readonly ILogger _logger;
    
    private readonly Entities _entities;
    
    public AllEventsListener(IHaContext ha, INetDaemonScheduler scheduler, ILogger logger)
    {
        _ha = ha;
        _logger = logger;

        _entities = new Entities(_ha);

        var namespaceLastPart = GetType().Namespace?.Split('.').Last();
        
        // _logger = new LoggerConfiguration()
        //     .Enrich.WithProperty("netDaemonLogging", $"Serilog{GetType().Name}Context")
        //     .MinimumLevel.Information()
        //     .WriteTo.Console()
        //     .WriteTo.File($"logs/{namespaceLastPart}/{GetType().Name}_.log", rollingInterval: RollingInterval.Day)
        //     .CreateLogger();
        
        _logger.Information("Initialized {NamespaceLastPart} v0.01", namespaceLastPart);
        
        // Uncomment to subscribe to all events
        // ha.Events.Where(e => e.EventType != "something that will never match").Subscribe(LogEventData);
    }

    private void LogEventData(Event eventData)
    {
        var dataElement = eventData.DataElement;

        if (dataElement is null) return;
        
        _logger.Information("Raw JSON: {EventData}", dataElement.Value.ToString());
    }
}