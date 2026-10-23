using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Services.Monitoring;

// One expected item and how far its test has got.
public record OccurrenceItemState(int ItemId, ItemProgress Progress, string? SampleReference);

public record OccurrenceStatusDetail(OccurrenceStatusResult Result, List<OccurrenceItemState> Items);

// Works out the status of occurrences from the tests linked to them. The
// only place that reads links, so the calendar, the checklist and the
// reports agree.
public class OccurrenceStatusService
{
    private readonly IMicroLimsDbContext _db;
    private readonly ILabClock _clock;

    public OccurrenceStatusService(IMicroLimsDbContext db, ILabClock clock)
    {
        _db = db;
        _clock = clock;
    }

    private record LinkRow(
        int OccurrenceId, int? RoomTestConfigurationId, int? MachinePartConfigurationId, int? WaterSamplingPointId,
        string TestCode, Domain.Enums.ApprovalStatus Status, Domain.Enums.WorkflowStep Step, bool Superseded,
        Domain.Enums.ResultStatus? Result, DateTime ReceivedAt, string Reference);

    // Occurrences must be loaded with their Items.
    public async Task<Dictionary<int, OccurrenceStatusDetail>> ComputeAsync(IReadOnlyCollection<MonitoringOccurrence> occurrences)
    {
        var details = new Dictionary<int, OccurrenceStatusDetail>();
        if (occurrences.Count == 0) return details;

        var ids = occurrences.Select(o => o.Id).ToList();
        var graceDays = (await _db.MonitoringSettings.AsNoTracking().FirstOrDefaultAsync())?.GraceDays
                        ?? new MonitoringSettings().GraceDays;

        var links = (await _db.SampleLocations.AsNoTracking()
                .Where(l => l.MonitoringOccurrenceId != null && ids.Contains(l.MonitoringOccurrenceId.Value))
                .Select(l => new LinkRow(
                    l.MonitoringOccurrenceId!.Value, l.RoomTestConfigurationId, l.MachinePartConfigurationId, l.WaterSamplingPointId,
                    l.TestOrder!.TestCode, l.TestOrder.Status, l.TestOrder.CurrentStep, l.TestOrder.IsSuperseded,
                    l.Status, l.Sample!.SampledAt ?? l.Sample.ReceivedAt, l.Sample.ReferenceNumber))
                .ToListAsync())
            .ToLookup(l => l.OccurrenceId);

        var today = _clock.LabToday;
        foreach (var occurrence in occurrences)
        {
            var rows = links[occurrence.Id].ToList();
            var items = new List<OccurrenceItemState>();
            DateOnly? latest = null;

            foreach (var item in occurrence.Items)
            {
                // "The machine itself": every part sampled counts, and the slowest one decides how far it has got.
                var machineLevel = occurrence.TargetType == Domain.Enums.MonitoringTargetType.Machine && item.MachinePartConfigurationId is null;
                var matched = new List<(ItemProgress Progress, string Reference)>();
                foreach (var row in rows)
                {
                    var facts = new LocationFacts(row.RoomTestConfigurationId, row.MachinePartConfigurationId, row.WaterSamplingPointId, row.TestCode);
                    if (!OccurrenceItemMatcher.Matches(occurrence, item, facts)) continue;

                    var progress = OccurrenceProgressRules.For(row.Status, row.Step, row.Superseded, row.Result);
                    if (progress == ItemProgress.None) continue;
                    matched.Add((progress, row.Reference));

                    var on = DateOnly.FromDateTime(_clock.ToLabLocal(row.ReceivedAt));
                    if (latest is null || on > latest) latest = on;
                }

                var best = ItemProgress.None;
                string? reference = null;
                if (matched.Count > 0)
                {
                    if (machineLevel)
                    {
                        var slowest = matched.Min(m => m.Progress);
                        best = slowest >= ItemProgress.Performed && matched.Any(m => m.Progress == ItemProgress.PerformedExcursion)
                            ? ItemProgress.PerformedExcursion
                            : slowest;
                        reference = string.Join(", ", matched.Select(m => m.Reference).Distinct());
                    }
                    else
                    {
                        var top = matched.OrderByDescending(m => m.Progress).First();
                        best = top.Progress;
                        reference = top.Reference;
                    }
                }
                items.Add(new OccurrenceItemState(item.Id, best, reference));
            }

            var result = OccurrenceStatusCalculator.Calculate(new OccurrenceStatusInput(
                occurrence.WindowStart, occurrence.WindowEnd, occurrence.TargetDate, occurrence.ExceptionState,
                graceDays, today, items.Select(i => i.Progress).ToList(), latest));
            details[occurrence.Id] = new OccurrenceStatusDetail(result, items);
        }
        return details;
    }
}
