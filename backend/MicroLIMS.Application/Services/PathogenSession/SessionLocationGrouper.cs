using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Services;

// Collapses a sample's per-test SampleLocation rows into the session's
// physical locations. Pure: no database access.
public static class SessionLocationGrouper
{
    public static List<SessionLocationDto> Group(Sample sample)
    {
        var groupedLocations = new List<SessionLocationDto>();
        // Keyed by the underlying physical location's stable id, not its
        // display name - two distinct Room/MachinePart/WaterSamplingPoint
        // rows can share the same display text (e.g. multiple water points
        // all named "WTU"), and a display-name key would silently merge
        // them, dropping every TestCode mapping but the last one written.
        var locationMap = new Dictionary<(string Kind, int Id), SessionLocationDto>();

        foreach (var loc in sample.Locations)
        {
            var locName = loc.RoomTestConfiguration?.Room?.Name
                ?? loc.MachinePartConfiguration?.MachinePart?.Name
                ?? loc.WaterSamplingPoint?.Location
                ?? $"Location #{loc.Id}";

            var locKey = loc.RoomTestConfigurationId.HasValue ? ("Room", loc.RoomTestConfigurationId.Value)
                : loc.MachinePartConfigurationId.HasValue ? ("MachinePart", loc.MachinePartConfigurationId.Value)
                : loc.WaterSamplingPointId.HasValue ? ("Water", loc.WaterSamplingPointId.Value)
                : ("Loc", loc.Id);

            var grade = loc.RoomTestConfiguration?.Room?.GradeClassification.ToString();
            var locType = loc.LocationType.ToString();

            if (!locationMap.TryGetValue(locKey, out var group))
            {
                group = new SessionLocationDto(
                    groupedLocations.Count + 1,
                    loc.Id,
                    locName,
                    locType,
                    grade,
                    new Dictionary<string, int>());
                locationMap[locKey] = group;
                groupedLocations.Add(group);
            }

            var to = sample.TestOrders.FirstOrDefault(t => t.Id == loc.TestOrderId);
            if (to != null)
            {
                group.TestLocationMap[to.TestCode] = loc.Id;
            }
        }

        // If no locations exist on sample (e.g. single product sample), create 1 default session location
        if (groupedLocations.Count == 0)
        {
            var defaultLoc = new SessionLocationDto(
                1,
                0,
                sample.Item?.Name ?? sample.ReferenceNumber,
                "Product",
                null,
                new Dictionary<string, int>());
            foreach (var to in sample.TestOrders)
            {
                defaultLoc.TestLocationMap[to.TestCode] = 0;
            }
            groupedLocations.Add(defaultLoc);
        }

        return groupedLocations;
    }
}
