using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Urgency is computed here, server-side, and returned as an enum rather
// than a raw due timestamp - GMP dashboards must not let the frontend
// re-derive what's "overdue" from a clock it doesn't control.
public enum TaskUrgency { Overdue, DueSoon, DueToday, DueTomorrow }

public record MyTaskDto(
    string TaskType,
    string Title,
    string Subtitle,
    string ReferenceId,
    DateTime DueAt,
    TaskUrgency Urgency,
    int? SampleId,
    int? TestOrderId,
    int? MediaId,
    bool IsReturned = false,
    string? ReturnReason = null);

// "My Tasks" for the Analyst dashboard. Product/Water/EM/After Cleaning
// all flow through the same Sample -> TestOrder -> Incubation tables (no
// separate entity per category), so TestOrder.AssignedAnalystId already
// covers all four uniformly. Media/GPT tasks are unioned in separately
// from Media.PreparedByUserId - there is no generic "AssignedTo" concept
// anywhere in the schema, so ownership is read from whichever existing
// audit field represents "the analyst responsible for this item" per
// domain, rather than adding a new column.
public class MyTasksService
{
    private static readonly TimeSpan DueSoonWindow = TimeSpan.FromHours(4);
    private static readonly TimeSpan LookaheadWindow = TimeSpan.FromDays(2);

    private readonly MicroLimsDbContext _db;

    public MyTasksService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<MyTaskDto>> GetMyTasksAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var horizon = now.Add(LookaheadWindow);
        var tasks = new List<MyTaskDto>();

        var testOrders = await _db.TestOrders
            .Where(t => t.AssignedAnalystId == userId)
            .Where(t => t.Status != ApprovalStatus.Approved && t.Status != ApprovalStatus.Rejected)
            .Include(t => t.Sample!).ThenInclude(s => s.Item)
            .Include(t => t.Sample!).ThenInclude(s => s.WaterSamplingPoint)
            .Include(t => t.Sample!).ThenInclude(s => s.Department)
            .Include(t => t.Sample!).ThenInclude(s => s.Machine)
            .Include(t => t.Incubations)
            .ToListAsync();

        var pendingReturns = await TestReturnHelper.GetPendingReturnsForOrdersAsync(_db, testOrders.Select(t => t.Id));

        foreach (var t in testOrders)
        {
            var sample = t.Sample!;
            var location = sample.Item?.Name ?? sample.WaterSamplingPoint?.Code ?? sample.Department?.Name ?? sample.Machine?.Name ?? sample.ReferenceNumber;

            if (pendingReturns.TryGetValue(t.Id, out var returnInfo))
            {
                var returnReasonSubtitle = string.IsNullOrWhiteSpace(returnInfo.Reason)
                    ? $"{sample.ReferenceNumber} · {t.TestCode} · Returned for revision"
                    : $"{sample.ReferenceNumber} · {t.TestCode} · Returned: {returnInfo.Reason}";

                tasks.Add(new MyTaskDto(
                    TaskType: "Revise Test",
                    Title: $"Revise {t.TestCode} — {location}",
                    Subtitle: returnReasonSubtitle,
                    ReferenceId: sample.ReferenceNumber,
                    DueAt: returnInfo.ReturnedAt,
                    Urgency: TaskUrgency.Overdue,
                    SampleId: sample.Id,
                    TestOrderId: t.Id,
                    MediaId: null,
                    IsReturned: true,
                    ReturnReason: returnInfo.Reason));
                continue;
            }

            var openIncubation = t.Incubations
                .Where(i => i.CompletedAt == null && i.ExpectedReadingAt != null)
                .OrderBy(i => i.ExpectedReadingAt)
                .FirstOrDefault();
            if (openIncubation?.ExpectedReadingAt is not { } dueAt || dueAt > horizon) continue;

            tasks.Add(new MyTaskDto(
                TaskType: "Read Test",
                Title: $"Read {t.TestCode} — {location}",
                Subtitle: $"{sample.ReferenceNumber} · {t.TestCode} · {openIncubation.StepName}",
                ReferenceId: sample.ReferenceNumber,
                DueAt: dueAt,
                Urgency: ComputeUrgency(dueAt, now),
                SampleId: sample.Id,
                TestOrderId: t.Id,
                MediaId: null));
        }

        var mediaEvaluations = await _db.MediaEvaluations
            .Where(e => e.Status != MediaEvaluationStatus.Completed)
            .Include(e => e.Media!).ThenInclude(m => m.Material)
            .Include(e => e.Challenges).ThenInclude(c => c.Incubation)
            .Where(e => e.Media!.PreparedByUserId == userId)
            .ToListAsync();

        foreach (var e in mediaEvaluations)
        {
            var openIncubation = e.Challenges
                .Select(c => c.Incubation)
                .Where(i => i != null && i.CompletedAt == null && i.ExpectedReadingAt != null)
                .OrderBy(i => i!.ExpectedReadingAt)
                .FirstOrDefault();
            if (openIncubation?.ExpectedReadingAt is not { } dueAt || dueAt > horizon) continue;

            var media = e.Media!;
            tasks.Add(new MyTaskDto(
                TaskType: e.EvaluationType.ToString(),
                Title: $"{FormatEvaluationType(e.EvaluationType)} — Media lot {media.LotNumber}",
                Subtitle: $"{media.Material?.MaterialName} · {media.LotNumber}",
                ReferenceId: media.LotNumber,
                DueAt: dueAt,
                Urgency: ComputeUrgency(dueAt, now),
                SampleId: null,
                TestOrderId: null,
                MediaId: media.Id));
        }

        return tasks.OrderBy(x => x.DueAt).ToList();
    }

    private static string FormatEvaluationType(EvaluationType type) => type switch
    {
        EvaluationType.GrowthPromotion => "Growth promotion test",
        EvaluationType.IndicationInhibition => "Indication/inhibition test",
        EvaluationType.EnrichmentCharacteristics => "Enrichment characteristics test",
        _ => type.ToString()
    };

    private static TaskUrgency ComputeUrgency(DateTime dueAt, DateTime now)
    {
        if (dueAt < now) return TaskUrgency.Overdue;
        if (dueAt - now <= DueSoonWindow) return TaskUrgency.DueSoon;
        if (dueAt.Date == now.Date) return TaskUrgency.DueToday;
        return TaskUrgency.DueTomorrow;
    }
}
