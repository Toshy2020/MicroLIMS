using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Monitoring;

// Display names for occurrences: their target, group and expected tests.
public static class OccurrenceNameResolver
{
    public static (MonitoringTargetType, int) TargetKey(MonitoringOccurrence o) =>
        (o.TargetType, (o.WaterSamplingPointId ?? o.RoomId ?? o.MachineId)!.Value);

    public static async Task<Dictionary<(MonitoringTargetType, int), string>> TargetNamesAsync(IMicroLimsDbContext _db, IReadOnlyCollection<MonitoringOccurrence> occurrences)
    {
        var names = new Dictionary<(MonitoringTargetType, int), string>();
        var pointIds = occurrences.Where(o => o.WaterSamplingPointId.HasValue).Select(o => o.WaterSamplingPointId!.Value).Distinct().ToList();
        var roomIds = occurrences.Where(o => o.RoomId.HasValue).Select(o => o.RoomId!.Value).Distinct().ToList();
        var machineIds = occurrences.Where(o => o.MachineId.HasValue).Select(o => o.MachineId!.Value).Distinct().ToList();
        if (pointIds.Count > 0)
            foreach (var p in await _db.WaterSamplingPoints.AsNoTracking().Where(p => pointIds.Contains(p.Id)).Select(p => new { p.Id, p.Code }).ToListAsync())
                names[(MonitoringTargetType.WaterSamplingPoint, p.Id)] = p.Code;
        if (roomIds.Count > 0)
            foreach (var r in await _db.Rooms.AsNoTracking().Where(r => roomIds.Contains(r.Id)).Select(r => new { r.Id, r.Name, r.Kind }).ToListAsync())
                names[(MonitoringTargetType.Room, r.Id)] = r.Kind == RoomKind.LAF ? $"{r.Name} (LAF)" : r.Name;
        if (machineIds.Count > 0)
            foreach (var m in await _db.Machines.AsNoTracking().Where(m => machineIds.Contains(m.Id)).Select(m => new { m.Id, m.Name }).ToListAsync())
                names[(MonitoringTargetType.Machine, m.Id)] = m.Name;
        return names;
    }

    public static async Task<Dictionary<int, string>> ItemLabelsAsync(IMicroLimsDbContext _db, IReadOnlyCollection<MonitoringOccurrence> occurrences)
    {
        var labels = new Dictionary<int, string>();
        var items = occurrences.SelectMany(o => o.Items).ToList();
        var roomConfigIds = items.Where(i => i.RoomTestConfigurationId.HasValue).Select(i => i.RoomTestConfigurationId!.Value).Distinct().ToList();
        var partConfigIds = items.Where(i => i.MachinePartConfigurationId.HasValue).Select(i => i.MachinePartConfigurationId!.Value).Distinct().ToList();

        var roomConfigs = roomConfigIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.RoomTestConfigurations.AsNoTracking().Where(c => roomConfigIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.TestCode);
        var partConfigs = partConfigIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.MachinePartConfigurations.AsNoTracking().Where(c => partConfigIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.MachinePart!.Name + " - " + c.TestCode);

        foreach (var item in items)
            labels[item.Id] = item.RoomTestConfigurationId is int r ? roomConfigs.GetValueOrDefault(r, "?")
                : item.MachinePartConfigurationId is int m ? partConfigs.GetValueOrDefault(m, "?")
                : item.TestCode ?? "?";
        return labels;
    }

    // The department, water department or nothing (machines) a target sits in.
    public static async Task<Dictionary<(MonitoringTargetType, int), string>> GroupNamesAsync(
        IMicroLimsDbContext _db, IReadOnlyCollection<MonitoringOccurrence> occurrences)
    {
        var names = new Dictionary<(MonitoringTargetType, int), string>();
        var pointIds = occurrences.Where(o => o.WaterSamplingPointId.HasValue).Select(o => o.WaterSamplingPointId!.Value).Distinct().ToList();
        var roomIds = occurrences.Where(o => o.RoomId.HasValue).Select(o => o.RoomId!.Value).Distinct().ToList();
        if (pointIds.Count > 0)
            foreach (var p in await _db.WaterSamplingPoints.AsNoTracking().Where(p => pointIds.Contains(p.Id))
                         .Select(p => new { p.Id, Name = p.WaterDepartment != null ? p.WaterDepartment.Name : null }).ToListAsync())
                if (p.Name != null) names[(MonitoringTargetType.WaterSamplingPoint, p.Id)] = p.Name;
        if (roomIds.Count > 0)
            foreach (var r in await _db.Rooms.AsNoTracking().Where(r => roomIds.Contains(r.Id))
                         .Select(r => new { r.Id, Name = r.Department != null ? r.Department.Name : null }).ToListAsync())
                if (r.Name != null) names[(MonitoringTargetType.Room, r.Id)] = r.Name;
        return names;
    }
}
