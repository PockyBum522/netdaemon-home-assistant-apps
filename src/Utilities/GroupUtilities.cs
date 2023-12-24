using System.Collections.Generic;
using HomeAssistantGenerated;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using Serilog;

namespace NetdaemonApps.Utilities;

public static class GroupUtilities
{
    public static Entity[] GetEntitiesFromGroup(IHaContext ha, GroupEntity group, ILogger? logger = null)
    {
        var groupEntities = group.EntityState?.Attributes?.EntityId;

        logger?.Information("Group entities strings: {@Entities}", groupEntities);
        
        var returnEntities = new List<Entity>();

        foreach (var entityId in groupEntities ?? new []{ "Error" })
        {
            logger?.Information("Looking up entity with ID: {EntityId}", entityId);
            
            var entity  = new Entity(ha, entityId);

            returnEntities.Add(entity);
        }

        return returnEntities.ToArray();
    }
}