using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Oos;

public sealed partial class OosInvestigationService(
    IMicroLimsDbContext db, OosOpeningRule rule, ReferenceNumberGenerator numbers, TimeProvider time, ILabClock clock,
    IUserSectionScopeService scope, PermissionService permissions, SegregationOfDutiesGuard duties, IElectronicSignatureService signatures)
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
        if (investigation is null)
            investigation = await LockOpenAsync(open.Where(i =>
                (from link in db.OosRetestPlanSamples
                 join plan in db.OosRetestPlans on link.PlanId equals plan.Id
                 where link.SampleId == sample.Id && plan.InvestigationId == i.Id && plan.SignedAtUtc != null
                 select link.SampleId).Any()), ct);

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

        if (investigation.Id != 0 && failing.Keys.Any(id => !existingOrderIds.Contains(id)))
            db.Entry(investigation).Property(nameof(OosInvestigation.Status)).IsModified = true;

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

    internal Task<OosInvestigation?> LockOpenAsync(IQueryable<OosInvestigation> candidates, CancellationToken ct = default) =>
        LockInvestigationAsync(candidates, closed: null, ct);

    private async Task<OosInvestigation?> LockInvestigationAsync(IQueryable<OosInvestigation> candidates,
        OosInvestigationStatus? closed, CancellationToken ct)
    {
        if (!db.Database.IsRelational())
            return await candidates.SingleOrDefaultAsync(ct);

        var id = await candidates.Select(i => (int?)i.Id).SingleOrDefaultAsync(ct);
        if (id is null) return null;

        // Check status again after PostgreSQL acquires the lock.
        var locked = db.OosInvestigations.FromSql(
            $"""SELECT i.*, i.xmin FROM "OosInvestigations" AS i WHERE i."Id" = {id.Value} FOR UPDATE""");
        return await (closed is null
            ? locked.Where(i => i.Status != OosInvestigationStatus.Concluded)
            : locked.Where(i => i.Status == closed)).SingleOrDefaultAsync(ct);
    }

    internal Task<int[]> InvestigationOrderIdsAsync(int investigationId, CancellationToken ct = default)
    {
        var investigated = db.OosInvestigationResults.Where(r => r.InvestigationId == investigationId).Select(r => r.TestOrderId);
        var retestOrders = from plan in db.OosRetestPlans
                           join link in db.OosRetestPlanSamples on plan.Id equals link.PlanId
                           join order in db.TestOrders on link.SampleId equals order.SampleId
                           where plan.InvestigationId == investigationId && plan.SignedAtUtc != null
                           select order.Id;
        return investigated.Union(retestOrders).ToArrayAsync(ct);
    }

    private async Task<OosInvestigation> CreateAsync(
        Sample sample, int sectionId, int reviewerUserId, DateTime now, CancellationToken ct = default)
    {
        var number = await numbers.GenerateOosCodeAsync(ct);
        var template = await db.OosInvestigationTemplates.AsNoTracking()
            .Where(t => t.SectionId == sectionId && t.Kind == OosInvestigationKind.Oos)
            .Select(t => new
            {
                t.Id,
                t.PhaseOneDueWorkingDays,
                t.TotalDueWorkingDays,
                Items = db.OosChecklistItems.Where(i => i.TemplateId == t.Id && i.IsActive)
                    .OrderBy(i => i.Phase).ThenBy(i => i.Order).ThenBy(i => i.Id)
                    .Select(i => new { phase = i.Phase, key = i.Key, order = i.Order, text = i.Text }).ToList()
            }).AsSingleQuery().SingleOrDefaultAsync(ct);

        var investigation = new OosInvestigation
        {
            Number = number,
            Kind = OosInvestigationKind.Oos,
            Status = OosInvestigationStatus.PhaseOne,
            SampleId = sample.Id,
            SectionId = sectionId,
            OpenedAtUtc = now,
            OpenedByUserId = reviewerUserId,
            TemplateSnapshotJson = JsonSerializer.Serialize(new { templateId = template?.Id, items = template?.Items ?? [] })
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
