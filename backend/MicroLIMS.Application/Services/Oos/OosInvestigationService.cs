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
        var investigation = await open.SingleOrDefaultAsync(i => i.SampleId == sample.Id, ct);
        if (investigation is null && sample.OriginSampleId is not null && sample.OosGroupCode is not null)
            investigation = await open.SingleOrDefaultAsync(i => i.Number == sample.OosGroupCode, ct);

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
                OutcomeAtOpening = OosOutcomeMapper.Map(outcome),
                ResultSummaryAtOpening = outcome.Status.ToString() ?? string.Empty,
                AddedAtUtc = now
            });
        }

        return investigation;
    }

    private async Task<OosInvestigation> CreateAsync(
        Sample sample, int sectionId, int reviewerUserId, DateTime now, CancellationToken ct = default)
    {
        var number = await numbers.GenerateOosCodeAsync(skipReservedLegacyCodes: true, ct);
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
