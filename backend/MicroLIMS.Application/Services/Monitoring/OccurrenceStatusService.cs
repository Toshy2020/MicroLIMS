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
                    l.Status, l.Sample!.ReceivedAt, l.Sample.ReferenceNumber))
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
                var best = ItemProgress.None;
                string? reference = null;
                foreach (var row in rows)
                {
                    var facts = new LocationFacts(row.RoomTestConfigurationId, row.MachinePartConfigurationId, row.WaterSamplingPointId, row.TestCode);
                    if (!OccurrenceItemMatcher.Matches(occurrence, item, facts)) continue;

                    var progress = OccurrenceProgressRules.For(row.Status, row.Step, row.Superseded, row.Result);
                    if (progress == ItemProgress.None || progress <= best) continue;
                    best = progress;
                    reference = row.Reference;

                    var on = DateOnly.FromDateTime(_clock.ToLabLocal(row.ReceivedAt));
                    if (latest is null || on > latest) latest = on;
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
