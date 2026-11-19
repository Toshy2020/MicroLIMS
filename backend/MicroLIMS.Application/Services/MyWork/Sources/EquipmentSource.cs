using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.MyWork.Sources;

// Open equipment calibration and preventive maintenance tasks due within the look-ahead window
// (measured in the laboratory's local date). Equipment tasks have no owner and equipment links to a
// document section rather than a laboratory section, so they are shown to every holder of
// EquipmentWork.Record with no per-user or lab-section filtering.
public sealed class EquipmentSource : IMyWorkSource
{
    private const string TargetType = "equipment-task";

    private readonly IMicroLimsDbContext _db;
    private readonly ILabClock _clock;

    public EquipmentSource(IMicroLimsDbContext db, ILabClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public string Key => "equipment";

    public bool AppliesTo(MyWorkContext ctx) => ctx.Permissions.Contains(PermissionConstants.EquipmentWorkRecord);

    private sealed class Row
    {
        public int Id { get; init; }
        public int EquipmentId { get; init; }
        public string EquipmentCode { get; init; } = string.Empty;
        public string InstrumentType { get; init; } = string.Empty;
        public EquipmentTaskType TaskType { get; init; }
        public DateOnly DueDate { get; init; }
        public int? SectionId { get; init; }
        public string? SectionName { get; init; }
    }

    private DateOnly LabToday(MyWorkContext ctx) => DateOnly.FromDateTime(_clock.ToLabLocal(ctx.NowUtc));

    private IQueryable<Row> Rows(DateOnly horizon) =>
        _db.EquipmentTasks.AsNoTracking()
            .Where(t => t.CompletedOn == null && t.RetiredByRevisionId == null && t.DueDate <= horizon)
            .Select(t => new Row
            {
                Id = t.Id,
                EquipmentId = t.EquipmentInventoryId,
                EquipmentCode = t.EquipmentInventory!.Code,
                InstrumentType = t.EquipmentInventory.InstrumentType,
                TaskType = t.TaskType,
                DueDate = t.DueDate,
                SectionId = t.EquipmentInventory.SectionId,
                SectionName = t.EquipmentInventory.Section != null ? t.EquipmentInventory.Section.Name : null,
            });

    public async Task<MyWorkSourceResult> GetAsync(MyWorkContext ctx, int take, CancellationToken ct)
    {
        var labToday = LabToday(ctx);
        var horizon = labToday.AddDays(ctx.LookAheadDays);
        var counts = await CountAsync(ctx, ct);
        var total = counts.Values.Sum();
        var rows = total == 0
            ? new List<Row>()
            : await Rows(horizon)
                .OrderBy(r => r.DueDate).ThenBy(r => r.Id)
                .Take(take).ToListAsync(ct);

        var items = rows.Select(r => ToItem(labToday, r)).ToList();
        return new MyWorkSourceResult(items, new Dictionary<string, int> { ["equipment"] = total }, new Dictionary<string, int>());
    }

    public async Task<IReadOnlyDictionary<MyWorkUrgencyLevel, int>> CountAsync(MyWorkContext ctx, CancellationToken ct)
    {
        var labToday = LabToday(ctx);
        var horizon = labToday.AddDays(ctx.LookAheadDays);
        var buckets = await Rows(horizon)
            .GroupBy(r => r.DueDate < labToday ? MyWorkUrgencyLevel.Overdue
                : r.DueDate == labToday ? MyWorkUrgencyLevel.Today
                : MyWorkUrgencyLevel.Later)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return buckets.ToDictionary(b => b.Level, b => b.Count);
    }

    private MyWorkItem ToItem(DateOnly labToday, Row r)
    {
        var dueAtUtc = EndOfLabDateUtc(r.DueDate);
        var urgency = r.DueDate < labToday ? MyWorkUrgencyLevel.Overdue
            : r.DueDate == labToday ? MyWorkUrgencyLevel.Today
            : MyWorkUrgencyLevel.Later;
        var title = r.TaskType == EquipmentTaskType.Calibration
            ? $"Calibration due: {r.EquipmentCode}"
            : $"Preventive maintenance due: {r.EquipmentCode}";
        var context = r.SectionName is not null ? $"{r.InstrumentType} · {r.SectionName}" : r.InstrumentType;

        return new MyWorkItem(
            $"{TargetType}:{r.Id}",
            "equipment",
            r.EquipmentCode,
            title,
            context,
            dueAtUtc,
            urgency,
            dueAtUtc,
            false,
            null,
            new MyWorkTarget(TargetType, TaskId: r.Id, EquipmentId: r.EquipmentId),
            Array.Empty<string>(),
            r.SectionId,
            r.SectionName,
            null);
    }

    private DateTime EndOfLabDateUtc(DateOnly date)
    {
        var zone = _clock.LabTimeZone;
        var local = date.ToDateTime(new TimeOnly(23, 59, 59));
        while (zone.IsInvalidTime(local))
            local = local.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
    }
}
