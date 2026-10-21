using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One obligation for one target in one period, with its due window. Status
// is derived at read time and never stored; only the exception state is.
public class MonitoringOccurrence : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }

    public int CreatedByRevisionId { get; set; }
    public MonitoringPlanRevision? CreatedByRevision { get; set; }
    // Set when a later revision removes this occurrence. Never deleted.
    public int? RetiredByRevisionId { get; set; }
    public MonitoringPlanRevision? RetiredByRevision { get; set; }

    public MonitoringCategory Category { get; set; }
    public MonitoringTargetType TargetType { get; set; }
    // Exactly one of these is set, matching TargetType.
    public int? WaterSamplingPointId { get; set; }
    public int? RoomId { get; set; }
    public int? MachineId { get; set; }

    // Week ("2026-W43", lab week), month ("2026-11") or a manual-entry key.
    public string PeriodKey { get; set; } = string.Empty;
    public int Sequence { get; set; } = 1;

    // The rule in force when generated, e.g. "Weekly, Sunday - own".
    public string FrequencySnapshot { get; set; } = string.Empty;

    public DateOnly? TargetDate { get; set; }
    public DateOnly WindowStart { get; set; }
    public DateOnly WindowEnd { get; set; }
    public DateOnly? OriginalTargetDate { get; set; }
    public DateOnly OriginalWindowEnd { get; set; }

    public OccurrenceExceptionState ExceptionState { get; set; } = OccurrenceExceptionState.None;

    public List<MonitoringOccurrenceItem> Items { get; set; } = new();
}
