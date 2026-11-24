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
        var result = await ApplyReviewedSectionAsync(sample, sectionId, reviewedOrders, reviewerUserId, includeExcursions: true, ct);
        return result.Investigation ?? result.Excursion;
    }

    internal async Task<OosInvestigation?> OpenForReviewedOosOnlyAsync(
        Sample sample, int sectionId, IReadOnlyCollection<TestOrder> reviewedOrders, int reviewerUserId,
        CancellationToken ct = default) =>
        (await ApplyReviewedSectionAsync(sample, sectionId, reviewedOrders, reviewerUserId, includeExcursions: false, ct)).Investigation;

    internal async Task<(OosInvestigation? Investigation, bool Opened, bool PhaseTwoStarted, OosInvestigation? Excursion, bool ExcursionOpened)> ApplyReviewedSectionAsync(
        Sample sample, int sectionId, IReadOnlyCollection<TestOrder> reviewedOrders, int reviewerUserId,
        CancellationToken ct = default) =>
        await ApplyReviewedSectionAsync(sample, sectionId, reviewedOrders, reviewerUserId, includeExcursions: true, ct);

    private async Task<(OosInvestigation? Investigation, bool Opened, bool PhaseTwoStarted, OosInvestigation? Excursion, bool ExcursionOpened)> ApplyReviewedSectionAsync(
        Sample sample, int sectionId, IReadOnlyCollection<TestOrder> reviewedOrders, int reviewerUserId,
        bool includeExcursions, CancellationToken ct)
    {
        var (failing, excursions) = await rule.ReadReviewOutcomesAsync(reviewedOrders.Select(o => o.Id).ToArray(), ct);
        if (failing.Count == 0 && (!includeExcursions || excursions.Count == 0)) return default;

        var now = time.GetUtcNow().UtcDateTime;
        OosInvestigation? oosInvestigation = null;
        var oosOpened = false;
        var phaseTwoStarted = false;

        if (failing.Count > 0)
        {
            await numbers.AcquireOosNumberLockAsync(ct);

            var open = db.OosInvestigations.Where(i => i.Kind == OosInvestigationKind.Oos &&
                i.SectionId == sectionId && i.Status != OosInvestigationStatus.Concluded);
            oosInvestigation = await LockOpenAsync(open.Where(i => i.SampleId == sample.Id), ct);
            if (oosInvestigation is null)
                oosInvestigation = await LockOpenAsync(open.Where(i =>
                    (from link in db.OosRetestPlanSamples
                     join plan in db.OosRetestPlans on link.PlanId equals plan.Id
                     where link.SampleId == sample.Id && plan.InvestigationId == i.Id && plan.SignedAtUtc != null
                     select link.SampleId).Any()), ct);

            oosOpened = oosInvestigation is null;
            var existingOrderIds = new HashSet<int>();
            if (oosInvestigation is null)
            {
                oosInvestigation = await CreateAsync(sample, sectionId, OosInvestigationKind.Oos, reviewerUserId, now, ct);
            }
            else
            {
                var orderIds = failing.Keys.ToArray();
                existingOrderIds = (await db.OosInvestigationResults.AsNoTracking()
                    .Where(r => r.InvestigationId == oosInvestigation.Id && orderIds.Contains(r.TestOrderId))
                    .Select(r => r.TestOrderId).ToListAsync(ct)).ToHashSet();
            }

            if (oosInvestigation.Id != 0 && failing.Keys.Any(id => !existingOrderIds.Contains(id)))
            {
                if (oosInvestigation.SampleId != sample.Id && oosInvestigation.Status == OosInvestigationStatus.AwaitingRetest)
                {
                    oosInvestigation.Status = OosInvestigationStatus.PhaseTwo;
                    phaseTwoStarted = true;
                    db.CurrentUserId = reviewerUserId;
                    db.SetNextAuditReason(oosInvestigation, "A failing retest from a signed plan joined the investigation.");
                }
                db.Entry(oosInvestigation).Property(nameof(OosInvestigation.Status)).IsModified = true;
            }

            foreach (var (orderId, outcome) in failing)
            {
                if (existingOrderIds.Contains(orderId)) continue;
                oosInvestigation.Results.Add(new OosInvestigationResult
                {
                    TestOrderId = orderId,
                    OutcomeAtOpening = OosOutcomeMapper.Map(outcome.Outcome),
                    ResultSummaryAtOpening = outcome.Summary,
                    AddedAtUtc = now
                });
            }
        }

        var (excursion, excursionOpened) = includeExcursions
            ? await ApplyReviewedExcursionsAsync(sample, sectionId, reviewerUserId, excursions, now, ct)
            : default;
        return (oosInvestigation, oosOpened, phaseTwoStarted, excursion, excursionOpened);
    }

    internal Task<OosInvestigation?> LockOpenAsync(IQueryable<OosInvestigation> candidates, CancellationToken ct = default) =>
        LockInvestigationAsync(candidates, closed: null, ct);

    private async Task<OosInvestigation?> LockInvestigationAsync(IQueryable<OosInvestigation> candidates,
        OosInvestigationStatus? closed, CancellationToken ct, bool anyStatus = false)
    {
        if (!db.Database.IsRelational())
            return await candidates.SingleOrDefaultAsync(ct);

        var id = await candidates.Select(i => (int?)i.Id).SingleOrDefaultAsync(ct);
        if (id is null) return null;

        // Check status again after PostgreSQL acquires the lock.
        var locked = db.OosInvestigations.FromSql(
            $"""SELECT i.*, i.xmin FROM "OosInvestigations" AS i WHERE i."Id" = {id.Value} FOR UPDATE""");
        if (!anyStatus)
            return await (closed is null
                ? locked.Where(i => i.Status != OosInvestigationStatus.Concluded)
                : locked.Where(i => i.Status == closed)).SingleOrDefaultAsync(ct);
        return await locked.AsNoTracking().SingleOrDefaultAsync(ct);
    }

    internal Task<int[]> InvestigationOrderIdsAsync(int investigationId, CancellationToken ct = default) =>
        InvestigationOrderIds(db, investigationId).ToArrayAsync(ct);

    internal static IQueryable<int> InvestigationOrderIds(IMicroLimsDbContext db, int investigationId)
    {
        var investigated = db.OosInvestigationResults.Where(r => r.InvestigationId == investigationId).Select(r => r.TestOrderId);
        var retestOrders = from plan in db.OosRetestPlans
                           join link in db.OosRetestPlanSamples on plan.Id equals link.PlanId
                           join order in db.TestOrders on link.SampleId equals order.SampleId
                           where plan.InvestigationId == investigationId && plan.SignedAtUtc != null
                           select order.Id;
        return investigated.Union(retestOrders);
    }

    private async Task<OosInvestigation> CreateAsync(
        Sample sample, int sectionId, OosInvestigationKind kind, int reviewerUserId, DateTime now, CancellationToken ct = default)
    {
        var number = await numbers.GenerateOosCodeAsync(ct);
        var template = await db.OosInvestigationTemplates.AsNoTracking()
            .Where(t => t.SectionId == sectionId && t.Kind == kind)
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
            Kind = kind,
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

        if (kind == OosInvestigationKind.Oos)
            sample.OosGroupCode ??= number;
        db.OosInvestigations.Add(investigation);
        return investigation;
    }
}
