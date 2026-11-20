using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Oos;

public sealed class OosInvestigationService(
    IMicroLimsDbContext db, OosOpeningRule rule, ReferenceNumberGenerator numbers, TimeProvider time, ILabClock clock)
{
    public async Task<OosInvestigation?> OpenForReviewedSectionAsync(
        Sample sample, int sectionId, IReadOnlyCollection<TestOrder> reviewedOrders, int reviewerUserId,
        CancellationToken ct = default)
    {
        var failing = await rule.ReadFailingOrdersAsync(reviewedOrders.Select(o => o.Id).ToArray(), ct);
        if (failing.Count == 0) return null;

        await numbers.AcquireOosNumberLockAsync(ct);

        var open = db.OosInvestigations.Where(i => i.Kind == OosInvestigationKind.Oos &&
            i.SectionId == sectionId && i.Status != OosInvestigationStatus.Concluded);
        var investigation = await LockOpenAsync(open.Where(i => i.SampleId == sample.Id), ct);
        if (investigation is null && sample.OriginSampleId is not null && sample.OosGroupCode is not null)
            investigation = await LockOpenAsync(open.Where(i => i.Number == sample.OosGroupCode), ct);

        var now = time.GetUtcNow().UtcDateTime;
        var existingOrderIds = new HashSet<int>();
        if (investigation is null)
        {
            investigation = await CreateAsync(sample, sectionId, reviewerUserId, now, ct);
        }
        else
        {
            var orderIds = failing.Keys.ToArray();
            existingOrderIds = (await db.OosInvestigationResults.AsNoTracking()
                .Where(r => r.InvestigationId == investigation.Id && orderIds.Contains(r.TestOrderId))
                .Select(r => r.TestOrderId).ToListAsync(ct)).ToHashSet();
        }

        foreach (var (orderId, outcome) in failing)
        {
            if (existingOrderIds.Contains(orderId)) continue;
            investigation.Results.Add(new OosInvestigationResult
            {
                TestOrderId = orderId,
                OutcomeAtOpening = OosOutcomeMapper.Map(outcome.Outcome),
                ResultSummaryAtOpening = outcome.Summary,
                AddedAtUtc = now
            });
        }

        return investigation;
    }

    private async Task<OosInvestigation?> LockOpenAsync(IQueryable<OosInvestigation> candidates, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational())
            return await candidates.SingleOrDefaultAsync(ct);

        var id = await candidates.Select(i => (int?)i.Id).SingleOrDefaultAsync(ct);
        if (id is null) return null;

        // Task 7 conclusion must take the same row lock before reading results or signing.
        // Recheck status under the lock in case a conclusion committed after the lookup.
        return await db.OosInvestigations.FromSql(
            $"""SELECT i.*, i.xmin FROM "OosInvestigations" AS i WHERE i."Id" = {id.Value} AND i."Status" <> {(int)OosInvestigationStatus.Concluded} FOR UPDATE""")
            .SingleOrDefaultAsync(ct);
    }

    private async Task<OosInvestigation> CreateAsync(
        Sample sample, int sectionId, int reviewerUserId, DateTime now, CancellationToken ct = default)
    {
        var number = await numbers.GenerateOosCodeAsync(ct);
        var template = await db.OosInvestigationTemplates.AsNoTracking()
            .SingleOrDefaultAsync(t => t.SectionId == sectionId && t.Kind == OosInvestigationKind.Oos, ct);
        var items = template is null ? [] : await db.OosChecklistItems.AsNoTracking()
            .Where(i => i.TemplateId == template.Id && i.IsActive)
            .OrderBy(i => i.Phase).ThenBy(i => i.Order).ThenBy(i => i.Id)
            .Select(i => new { phase = i.Phase, key = i.Key, order = i.Order, text = i.Text })
            .ToListAsync(ct);

        var investigation = new OosInvestigation
        {
            Number = number,
            Kind = OosInvestigationKind.Oos,
            Status = OosInvestigationStatus.PhaseOne,
            SampleId = sample.Id,
            SectionId = sectionId,
            OpenedAtUtc = now,
            OpenedByUserId = reviewerUserId,
            TemplateSnapshotJson = JsonSerializer.Serialize(new { templateId = template?.Id, items })
        };
        if (template is not null && (template.PhaseOneDueWorkingDays.HasValue || template.TotalDueWorkingDays.HasValue))
        {
            var settings = await db.MonitoringSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new MonitoringSettings();
            var today = clock.LabToday;
            var holidays = (await db.LabCalendarDates.AsNoTracking()
                .Where(d => d.Date >= today).Select(d => d.Date).ToListAsync(ct)).ToHashSet();
            if (template.PhaseOneDueWorkingDays is int phaseOneDays)
                investigation.PhaseOneDueDate = PlanWindowCalculator.AddWorkingDays(today, phaseOneDays, settings.WorkingWeekdays, holidays);
            if (template.TotalDueWorkingDays is int totalDays)
                investigation.DueDate = PlanWindowCalculator.AddWorkingDays(today, totalDays, settings.WorkingWeekdays, holidays);
        }

        sample.OosGroupCode ??= number;
        db.OosInvestigations.Add(investigation);
        return investigation;
    }
}
