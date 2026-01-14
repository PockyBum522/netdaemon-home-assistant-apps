using System.Collections.Generic;
using HomeAssistantGenerated;
using NetDaemon.HassModel.Entities;

namespace AllenStreetNetDaemonApps.Utilities;

public static class GroupUtilities
{
    public static Entity[] GetEntitiesFromGroup(IHaContext ha, LightEntity group, ILogger? logger = null)
    {
        var groupEntities = group.EntityState?.Attributes?.EntityId;

        logger?.LogInformation("Group entities strings: {@Entities}", groupEntities);
        
        var returnEntities = new List<Entity>();

        foreach (var entityId in groupEntities ?? new []{ "Error" })
        {
            logger?.LogInformation("Looking up entity with ID: {EntityId}", entityId);
            
            var entity  = new Entity(ha, entityId);

            returnEntities.Add(entity);
        }

        return returnEntities.ToArray();
    }
}