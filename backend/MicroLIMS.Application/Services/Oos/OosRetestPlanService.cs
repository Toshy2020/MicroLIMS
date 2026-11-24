using MicroLIMS.Application.Services.Competency;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Oos;

public sealed record SaveRetestPlanRequest(OosRetestSource Source, int RetestCount, IReadOnlyList<int> TestOrderIds,
    IReadOnlyList<int> AnalystUserIds, string JudgingRule, string? AmendmentReason);

public sealed record OosRetestPlanDto(int Id, int Revision, uint InvestigationVersion, OosRetestSource Source, int RetestCount,
    IReadOnlyList<int> TestOrderIds, IReadOnlyList<int> AnalystUserIds, string JudgingRule, string? AmendmentReason);

public sealed class OosRetestPlanService(IMicroLimsDbContext db, OosInvestigationService investigations,
    IUserSectionScopeService scope, SegregationOfDutiesGuard duties, IElectronicSignatureService signatures,
    RetestSampleFactory samples, TimeProvider time, ICompetencyGuard? competency = null)
{
    public Task<OosRetestPlanDto> DraftAsync(int investigationId, SaveRetestPlanRequest r, uint expectedVersion, int userId,
        CancellationToken ct = default) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            var investigation = await investigations.OpenForEditAsync(investigationId, userId, PermissionConstants.OosManage, ct, Closed);
            EnsurePlanningAllowed(investigation);
            EnsureVersion(investigation, expectedVersion);
            var plan = await db.OosRetestPlans.SingleOrDefaultAsync(p => p.InvestigationId == investigationId && p.SignedAtUtc == null, ct);
            var revision = plan?.Revision ?? 1 + (await db.OosRetestPlans.Where(p => p.InvestigationId == investigationId)
                .MaxAsync(p => (int?)p.Revision, ct) ?? 0);
            await ValidateAsync(investigation, r, revision, userId, time.GetUtcNow().UtcDateTime, ct);

            db.CurrentUserId = userId;
            if (plan is null)
            {
                plan = new OosRetestPlan { InvestigationId = investigationId, Revision = revision };
                db.OosRetestPlans.Add(plan);
            }

            plan.Source = r.Source;
            plan.RetestCount = r.RetestCount;
            plan.TestOrderIdsJson = JsonSerializer.Serialize(r.TestOrderIds);
            plan.AnalystUserIdsJson = JsonSerializer.Serialize(r.AnalystUserIds);
            plan.JudgingRule = r.JudgingRule.Trim();
            plan.AmendmentReason = string.IsNullOrWhiteSpace(r.AmendmentReason) ? null : r.AmendmentReason.Trim();
            BumpVersion(investigation);
            await db.SaveChangesAsync(ct);
            return new OosRetestPlanDto(plan.Id, plan.Revision, investigation.Version, plan.Source, plan.RetestCount,
                r.TestOrderIds, r.AnalystUserIds, plan.JudgingRule, plan.AmendmentReason);
        }, ct);

    public Task SignAndCreateAsync(int planId, string password, uint expectedVersion, int userId, string? ip,
        CancellationToken ct = default) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            // Only resolve the foreign key before taking the investigation lock.
            var investigationId = await db.OosRetestPlans.AsNoTracking().Where(p => p.Id == planId)
                .Select(p => (int?)p.InvestigationId).SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException(MessageCodes.OosRetestPlanNotFound, "The OOS retest plan was not found.");
            var investigation = await investigations.OpenForEditAsync(investigationId, userId, PermissionConstants.OosManage, ct, Closed);
            var plan = await db.OosRetestPlans.SingleAsync(p => p.Id == planId, ct);
            if (plan.SignedAtUtc is not null)
                throw new BusinessRuleException(MessageCodes.OosRetestPlanAlreadySigned, "This retest plan has already been signed.");
            EnsurePlanningAllowed(investigation);
            EnsureVersion(investigation, expectedVersion);

            var request = new SaveRetestPlanRequest(plan.Source, plan.RetestCount,
                JsonSerializer.Deserialize<int[]>(plan.TestOrderIdsJson) ?? [],
                JsonSerializer.Deserialize<int[]>(plan.AnalystUserIdsJson) ?? [], plan.JudgingRule, plan.AmendmentReason);
            if (request.AnalystUserIds.Contains(userId))
                throw new BusinessRuleException(MessageCodes.OosRetestPlanSignerIsAnalyst, "You are assigned to a retest in this plan, so you cannot sign it.");
            var investigationOrderIds = await investigations.InvestigationOrderIdsAsync(investigationId, ct);
            if ((await duties.GetTestOrderIdsPerformedByAsync(userId, investigationOrderIds, ct)).Count > 0)
                throw new BusinessRuleException(MessageCodes.OosSignerPerformedTest, "You performed a test in this investigation, so you cannot sign it.");

            var now = time.GetUtcNow().UtcDateTime;
            var orders = await ValidateAsync(investigation, request, plan.Revision, userId, now, ct);
            var origin = await db.Samples.AsNoTracking().SingleAsync(s => s.Id == investigation.SampleId, ct);
            var causeId = await samples.GetRetestCauseIdAsync(ct);

            ElectronicSignature signature;
            try
            {
                signature = await signatures.SignAsync(userId, password, SignatureMeaning.OosRetestPlanApproved,
                    nameof(OosRetestPlan), plan.Id, plan.AmendmentReason, ip, ct);
            }
            catch (SignatureVerificationException error)
            {
                throw new BusinessRuleException(MessageCodes.OosRetestPlanPasswordFailed, error.Message);
            }
            db.CurrentUserId = userId;
            now = time.GetUtcNow().UtcDateTime;
            signature.SignedAt = now;
            await db.SaveChangesAsync(ct);

            foreach (var analyst in request.AnalystUserIds)
            {
                var sample = await samples.AddRetestSampleAsync(origin, orders, _ => analyst, investigation.Number, causeId, userId, ct);
                // ReferenceNumberGenerator counts persisted rows before allocating the next number.
                await db.SaveChangesAsync(ct);
                db.OosRetestPlanSamples.Add(new OosRetestPlanSample { PlanId = plan.Id, SampleId = sample.Id });
            }

            plan.SignedAtUtc = now;
            plan.SignedByUserId = userId;
            plan.SignatureId = signature.Id;
            plan.SamplesCreatedAtUtc = now;
            BumpVersion(investigation);
            await db.SaveChangesAsync(ct);
        }, ct);

    private async Task<List<TestOrder>> ValidateAsync(OosInvestigation investigation, SaveRetestPlanRequest r, int revision,
        int actor, DateTime now, CancellationToken ct)
    {
        if (!Enum.IsDefined(r.Source) || r.RetestCount is < 1 or > 10 || r.AnalystUserIds?.Count != r.RetestCount)
            throw new BusinessRuleException(MessageCodes.OosRetestPlanAssignmentInvalid,
                "Choose a retest source and a count from 1 to 10, with one analyst per sample.");
        if (string.IsNullOrWhiteSpace(r.JudgingRule) || r.JudgingRule.Length > 4000)
            throw new BusinessRuleException(MessageCodes.OosRetestPlanJudgingRuleInvalid, "Enter a judging rule of 1 to 4000 characters.");
        if ((revision > 1 && string.IsNullOrWhiteSpace(r.AmendmentReason)) || r.AmendmentReason?.Length > 4000)
            throw new BusinessRuleException(MessageCodes.OosRetestPlanAmendmentReasonRequired, "Enter an amendment reason of 1 to 4000 characters.");
        if (r.TestOrderIds is null || r.TestOrderIds.Count == 0 || r.TestOrderIds.Distinct().Count() != r.TestOrderIds.Count)
            throw InvalidOrders();

        var orders = await db.TestOrders.AsNoTracking().Where(o => r.TestOrderIds.Contains(o.Id) && !o.IsSuperseded &&
            o.SectionId == investigation.SectionId &&
            db.OosInvestigationResults.Any(result => result.InvestigationId == investigation.Id && result.TestOrderId == o.Id))
            .OrderBy(o => o.Id).ToListAsync(ct);
        if (orders.Count != r.TestOrderIds.Count)
            throw InvalidOrders();

        var investigatedIds = r.Source == OosRetestSource.NewSample
            ? await db.OosInvestigationResults.AsNoTracking().Where(r => r.InvestigationId == investigation.Id).Select(r => r.TestOrderId).ToArrayAsync(ct)
            : [];
        foreach (var analyst in r.AnalystUserIds.Distinct())
        {
            await AnalystAssignmentRules.EnsureAssignableAsync(db, scope, analyst, actor, [investigation.SectionId], now,
                testOrderIds: r.TestOrderIds, competency: competency, ct: ct);
            if (investigatedIds.Length > 0 && (await duties.GetTestOrderIdsPerformedByAsync(analyst, investigatedIds, ct)).Count > 0)
                throw new BusinessRuleException(MessageCodes.OosRetestPlanOriginalAnalyst,
                    "An analyst who performed a test in this investigation cannot be assigned a new sample.");
        }
        return orders;
    }

    private void BumpVersion(OosInvestigation investigation) =>
        db.Entry(investigation).Property(nameof(OosInvestigation.Status)).IsModified = true;

    private static void EnsureVersion(OosInvestigation investigation, uint expectedVersion)
    {
        if (investigation.Version != expectedVersion)
            throw OosTemplateService.VersionConflict();
    }

    private static void EnsurePlanningAllowed(OosInvestigation investigation)
    {
        if (investigation.Kind != OosInvestigationKind.Oos || investigation.PhaseOneSignatureId is null ||
            investigation.Status is not (OosInvestigationStatus.AwaitingRetest or OosInvestigationStatus.PhaseTwo))
            throw new BusinessRuleException(MessageCodes.OosRetestPlanPhaseOneRequired, "Phase I must be concluded before a retest is planned.");
    }

    private static BusinessRuleException InvalidOrders() =>
        new(MessageCodes.OosRetestPlanOrdersInvalid, "Select distinct current test orders linked to this investigation.");

    private static BusinessRuleException Closed() =>
        new(MessageCodes.OosRetestPlanInvestigationConcluded, "A retest plan cannot be changed after the investigation is concluded.");
}
