using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Oos;

public sealed record ConcludeRequest(OosConclusion Conclusion, int? RootCauseCategoryId,
    string? RootCauseDescription, string Summary, string Password);

public sealed record OosManualOpeningDto(int Id, string Number, uint Version);

public sealed partial class OosInvestigationService
{
    public Task AddEntryAsync(int investigationId, OosEntryKind kind, string text, int? parentEntryId, int userId,
        CancellationToken ct = default) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            var investigation = await OpenForEditAsync(investigationId, userId, PermissionConstants.OosContribute, ct,
                EntryClosed, PermissionConstants.OosManage);
            if (investigation.Kind != OosInvestigationKind.Oos || kind is not (OosEntryKind.OutsideReview or OosEntryKind.Hypothesis or OosEntryKind.HypothesisOutcome or OosEntryKind.Note) ||
                !ValidText(text) || (kind != OosEntryKind.Note && investigation.Status != OosInvestigationStatus.PhaseTwo))
                throw new BusinessRuleException(MessageCodes.OosEntryInvalid, "Choose an entry allowed in this phase and enter 1 to 4000 characters.");

            if (kind == OosEntryKind.HypothesisOutcome
                ? !await db.OosInvestigationEntrys.AsNoTracking().AnyAsync(e => e.Id == parentEntryId &&
                    e.InvestigationId == investigationId && e.Phase == 2 && e.Kind == OosEntryKind.Hypothesis, ct)
                : parentEntryId is not null)
                throw new BusinessRuleException(MessageCodes.OosEntryParentInvalid, "Only a hypothesis outcome can reference a hypothesis from this investigation.");

            var phase = investigation.Status == OosInvestigationStatus.PhaseTwo ? 2 : 1;
            await AppendAsync(investigation, phase, kind, null, null, text.Trim(), userId, ct, parentEntryId);
        }, ct);

    public Task ConcludeAsync(int investigationId, ConcludeRequest request, uint expectedVersion, int userId,
        string? ip, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            var investigation = await OpenForEditAsync(investigationId, userId, PermissionConstants.OosManage, ct, ConclusionPhaseInvalid);
            CheckVersion(investigation.Version, expectedVersion);
            var awaitingRetest = investigation.Status == OosInvestigationStatus.AwaitingRetest;
            if (investigation.Kind != OosInvestigationKind.Oos ||
                investigation.Status is not (OosInvestigationStatus.AwaitingRetest or OosInvestigationStatus.PhaseTwo) ||
                request.Conclusion is not (OosConclusion.Confirmed or OosConclusion.Invalidated) ||
                (awaitingRetest && request.Conclusion != OosConclusion.Invalidated))
                throw ConclusionPhaseInvalid();

            await EnsureIndependentSignerAsync(investigationId, userId, ct);
            if (!ValidText(request.Summary))
                throw new BusinessRuleException(MessageCodes.OosConclusionTextInvalid, "Enter a conclusion summary and any cause description within 4000 characters.");

            if (!awaitingRetest)
                await EnsurePhaseTwoAnsweredAsync(investigation, ct);

            var categoryId = awaitingRetest ? investigation.PhaseOneCauseCategoryId : request.RootCauseCategoryId;
            var description = awaitingRetest ? investigation.PhaseOneCauseDescription : request.RootCauseDescription?.Trim();
            var invalidated = request.Conclusion == OosConclusion.Invalidated;
            await EnsureConclusionCauseAsync(categoryId, description, invalidated, requireActive: !awaitingRetest, ct);
            await EnsureRetestsReviewedAsync(investigation, invalidated, ct);

            var signature = await SignInvestigationAsync(investigationId, userId, request.Password,
                SignatureMeaning.OosInvestigationConcluded, request.Summary.Trim(), ip, ct);

            investigation.Status = OosInvestigationStatus.Concluded;
            investigation.Conclusion = request.Conclusion;
            investigation.RootCauseCategoryId = categoryId;
            investigation.RootCauseDescription = description;
            investigation.ConclusionSummary = request.Summary.Trim();
            investigation.ConcludedByUserId = userId;
            investigation.ConcludedAtUtc = signature.SignedAt;
            investigation.ConclusionSignatureId = signature.Id;
            if (invalidated)
            {
                await SetOriginInvalidationAsync(investigation, signature.SignedAt, ct);
            }
            else
            {
                // Persist the signed Confirmed decision before the trigger checks its evidence.
                await SaveSignedEvidenceAsync(ct);
                await SetOriginInvalidationAsync(investigation, invalidatedAtUtc: null, ct);
            }
            await db.SaveChangesAsync(ct);
        }, ct);

    public Task ReopenAsync(int investigationId, string reason, string password, int userId, string? ip,
        CancellationToken ct = default) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            await EnsureInvestigationPermissionAsync(userId, PermissionConstants.OosManage, null, ct);
            var target = await db.OosInvestigations.AsNoTracking().Where(i => i.Id == investigationId)
                .Select(i => new { i.SampleId, i.SectionId }).SingleOrDefaultAsync(ct) ?? throw InvestigationNotFound();
            await EnsureScopeAsync(userId, target.SectionId, SectionAccess.Write, ct);

            // Section decisions take this lock before consulting the investigation gate.
            await OosGate.LockSampleDecisionAsync(db, target.SampleId, ct);
            var candidates = db.OosInvestigations.Where(i => i.Id == investigationId && i.Status == OosInvestigationStatus.Concluded);
            var investigation = await LockInvestigationAsync(candidates, OosInvestigationStatus.Concluded, ct);
            if (investigation is null || investigation.Kind != OosInvestigationKind.Oos ||
                investigation.Conclusion is not OosConclusion previous || investigation.ConclusionSignatureId is not int previousSignature)
                throw new BusinessRuleException(MessageCodes.OosReopenNotConcluded, "Only a signed OOS conclusion can be reopened.");
            CheckVersion(investigation.Version, db.ExpectedVersion);

            var origin = await db.Samples.AsNoTracking().Include(s => s.SectionSignoffs).SingleAsync(s => s.Id == target.SampleId, ct);
            if (SampleSectionRollup.StatusOf(origin, target.SectionId) is SectionSignoffStatus.Approved or SectionSignoffStatus.Rejected)
                throw new BusinessRuleException(MessageCodes.OosReopenSectionDecided, "An investigation cannot be reopened after its laboratory section is approved or rejected.");

            if (!ValidText(reason))
                throw new BusinessRuleException(MessageCodes.OosReopenReasonInvalid, "Enter a reopening reason of 1 to 4000 characters.");
            await EnsureIndependentSignerAsync(investigationId, userId, ct);

            var signature = await SignInvestigationAsync(investigationId, userId, password,
                SignatureMeaning.OosInvestigationReopened, reason.Trim(), ip, ct);

            db.CurrentReason = reason.Trim();
            db.OosInvestigationReopenings.Add(new OosInvestigationReopening
            {
                InvestigationId = investigationId,
                PreviousConclusion = previous,
                PreviousConclusionSignatureId = previousSignature,
                PreviousRootCauseCategoryId = investigation.RootCauseCategoryId,
                PreviousRootCauseDescription = investigation.RootCauseDescription,
                PreviousConclusionSummary = investigation.ConclusionSummary,
                PreviousConcludedByUserId = investigation.ConcludedByUserId,
                PreviousConcludedAtUtc = investigation.ConcludedAtUtc,
                Reason = reason.Trim(),
                ReopenedByUserId = userId,
                ReopenedAtUtc = signature.SignedAt,
                SignatureId = signature.Id
            });
            if (previous == OosConclusion.Invalidated)
            {
                await SaveSignedEvidenceAsync(ct);
                await SetOriginInvalidationAsync(investigation, invalidatedAtUtc: null, ct);
                // The trigger verifies the previous signed conclusion while it is still on the root.
                await db.SaveChangesAsync(ct);
            }
            investigation.Status = OosInvestigationStatus.PhaseTwo;
            investigation.Conclusion = null;
            investigation.RootCauseCategoryId = null;
            investigation.RootCauseDescription = null;
            investigation.ConclusionSummary = null;
            investigation.ConcludedByUserId = null;
            investigation.ConcludedAtUtc = null;
            investigation.ConclusionSignatureId = null;
            await db.SaveChangesAsync(ct);
        }, ct);

    public async Task<OosManualOpeningDto> OpenManuallyAsync(int sampleId, int sectionId, uint expectedVersion, int userId,
        CancellationToken ct = default)
    {
        var opened = await OpenManuallyCoreAsync(sampleId, sectionId, expectedVersion, userId, ct);
        return new(opened.Id, opened.Number, opened.Version);
    }

    public Task<OosInvestigation> OpenManuallyAsync(int sampleId, int sectionId, int userId, CancellationToken ct = default) =>
        OpenManuallyCoreAsync(sampleId, sectionId, db.ExpectedVersion, userId, ct);

    private Task<OosInvestigation> OpenManuallyCoreAsync(int sampleId, int sectionId, uint? expectedVersion, int userId, CancellationToken ct) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            await EnsureInvestigationPermissionAsync(userId, PermissionConstants.OosManage, null, ct);
            await EnsureScopeAsync(userId, sectionId, SectionAccess.Write, ct);
            await numbers.AcquireOosNumberLockAsync(ct);
            await OosGate.LockSampleDecisionAsync(db, sampleId, ct);
            var sample = await db.Samples.Include(s => s.SectionSignoffs).Include(s => s.TestOrders)
                .SingleOrDefaultAsync(s => s.Id == sampleId && s.TestOrders.Any(o => o.SectionId == sectionId), ct) ??
                throw new NotFoundException(MessageCodes.SampleNotFound, $"Sample {sampleId} not found.",
                    new Dictionary<string, string> { ["sampleId"] = sampleId.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            CheckVersion(sample.Version, expectedVersion);
            var orders = SampleSectionRollup.CurrentOrders(sample, sectionId);
            if (orders.Count == 0 || SampleSectionRollup.StatusOf(sample, sectionId) != SectionSignoffStatus.UnderApproval ||
                await db.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == sampleId && i.SectionId == sectionId &&
                    i.Kind == OosInvestigationKind.Oos, ct))
                throw new BusinessRuleException(MessageCodes.OosManualOpenUnavailable, "Manual opening requires a reviewed failing section without an investigation.");

            var orderIds = orders.Select(o => o.Id).ToArray();
            if (await db.OosInvestigationResults.AsNoTracking().AnyAsync(r => orderIds.Contains(r.TestOrderId), ct) ||
                await (from link in db.OosRetestPlanSamples
                       join plan in db.OosRetestPlans on link.PlanId equals plan.Id
                       where link.SampleId == sampleId && plan.SignedAtUtc != null
                       select link.SampleId).AnyAsync(ct))
                throw new BusinessRuleException(MessageCodes.OosManualOpenUnavailable, "Manual opening requires a reviewed failing section without an investigation.");

            db.CurrentUserId = userId;
            var investigation = await OpenForReviewedOosOnlyAsync(sample, sectionId, orders, userId, ct)
                ?? throw new BusinessRuleException(MessageCodes.OosManualOpenUnavailable, "Manual opening requires a reviewed failing section without an investigation.");
            db.Entry(sample).Property(nameof(Sample.OosGroupCode)).IsModified = true;
            await db.SaveChangesAsync(ct);
            return investigation;
        }, ct);

    private async Task EnsurePhaseTwoAnsweredAsync(OosInvestigation investigation, CancellationToken ct)
    {
        var keys = ReadChecklistKeys(investigation, 2);
        var answered = await db.OosInvestigationEntrys.AsNoTracking()
            .Where(e => e.InvestigationId == investigation.Id && e.Phase == 2 && e.Kind == OosEntryKind.ChecklistAnswer && e.Answer != null)
            .Select(e => e.ChecklistItemKey).ToListAsync(ct);
        if (!keys.IsSubsetOf(answered.OfType<string>()))
            throw new BusinessRuleException(MessageCodes.OosPhaseTwoChecklistIncomplete, "Answer every active Phase II checklist item before concluding.");
    }

    private async Task EnsureConclusionCauseAsync(int? categoryId, string? description, bool invalidated, bool requireActive, CancellationToken ct)
    {
        if (description?.Length > 4000)
            throw new BusinessRuleException(MessageCodes.OosConclusionTextInvalid, "Enter a conclusion summary and any cause description within 4000 characters.");

        if (invalidated && (categoryId is null || !ValidText(description)))
            throw new BusinessRuleException(MessageCodes.OosConclusionCauseRequired, "Invalidation requires an assignable cause and a description.");
        if (categoryId is not int id || !ValidText(description))
            throw new BusinessRuleException(MessageCodes.OosConclusionCauseDetailsRequired, "Select a cause category and enter its description before concluding.");

        if (requireActive && db.Database.IsRelational())
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""SELECT 1 FROM "OosCauseCategories" WHERE "Id" = {id} FOR SHARE""", ct);
        var category = await db.OosCauseCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new BusinessRuleException(MessageCodes.OosCategoryNotFound, "The OOS cause category was not found.");
        if (requireActive && !category.IsActive)
            throw new BusinessRuleException(MessageCodes.OosConclusionCauseInactive, "The selected cause category is no longer active.");
        if (invalidated && category.IsNoCauseFound)
            throw new BusinessRuleException(MessageCodes.OosConclusionCauseRequired, "Invalidation requires an assignable cause and a description.");
    }

    private async Task EnsureRetestsReviewedAsync(OosInvestigation investigation, bool invalidated, CancellationToken ct)
    {
        var plans = await db.OosRetestPlans.AsNoTracking()
            .Where(p => p.InvestigationId == investigation.Id && p.SignedAtUtc != null)
            .Select(p => new
            {
                p.RetestCount, p.SamplesCreatedAtUtc,
                Samples = db.OosRetestPlanSamples.Where(l => l.PlanId == p.Id).Select(l => l.SampleId).ToList()
            }).ToListAsync(ct);
        if (invalidated && !plans.Any(p => p.SamplesCreatedAtUtc != null && p.Samples.Count > 0))
            throw new BusinessRuleException(MessageCodes.OosConclusionPlanRequired, "Invalidation requires a signed retest plan with created samples.");

        var ids = plans.SelectMany(p => p.Samples).Distinct().ToArray();
        var samples = await db.Samples.AsNoTracking().Include(s => s.SectionSignoffs)
            .Where(s => ids.Contains(s.Id) && s.TestOrders.Any(o => o.SectionId == investigation.SectionId))
            .ToListAsync(ct);
        if (plans.Any(p => p.SamplesCreatedAtUtc is null || p.Samples.Count != p.RetestCount) || samples.Count != ids.Length ||
            samples.Any(s => SampleSectionRollup.StatusOf(s, investigation.SectionId) is not
                (SectionSignoffStatus.UnderApproval or SectionSignoffStatus.Approved or SectionSignoffStatus.Rejected)))
            throw new BusinessRuleException(MessageCodes.OosConclusionRetestsUnreviewed, "Complete the laboratory section review for every planned retest sample before concluding.");
    }

    private async Task SetOriginInvalidationAsync(OosInvestigation investigation, DateTime? invalidatedAtUtc, CancellationToken ct)
    {
        var links = await (from link in db.OosInvestigationResults
                           join order in db.TestOrders on link.TestOrderId equals order.Id
                           where link.InvestigationId == investigation.Id && order.SampleId == investigation.SampleId
                           select new { Link = link, Order = order }).ToListAsync(ct);

        var invalidated = invalidatedAtUtc.HasValue;
        foreach (var row in links)
        {
            if (row.Link.IsInvalidated != invalidated)
            {
                row.Link.IsInvalidated = invalidated;
                row.Link.InvalidatedAtUtc = invalidatedAtUtc;
            }
            row.Order.IsSuperseded = invalidated;
        }
    }

    private async Task EnsureIndependentSignerAsync(int investigationId, int actor, CancellationToken ct)
    {
        var orderIds = await InvestigationOrderIdsAsync(investigationId, ct);
        if ((await duties.GetTestOrderIdsPerformedByAsync(actor, orderIds, ct)).Count != 0)
            throw new BusinessRuleException(MessageCodes.OosSignerPerformedTest, "You performed a test in this investigation, so you cannot sign it.");
    }

    private async Task<ElectronicSignature> SignInvestigationAsync(int id, int actor, string password, SignatureMeaning meaning,
        string comment, string? ip, CancellationToken ct)
    {
        ElectronicSignature signature;
        try
        {
            signature = await signatures.SignAsync(actor, password ?? "", meaning, nameof(OosInvestigation), id, comment, ip, ct);
        }
        catch (SignatureVerificationException error)
        {
            if (meaning is SignatureMeaning.OosInvestigationConcluded or SignatureMeaning.ExcursionReviewClosed)
                throw new BusinessRuleException(MessageCodes.OosConclusionPasswordFailed, error.Message);
            throw new BusinessRuleException(MessageCodes.OosReopenPasswordFailed, error.Message);
        }

        db.CurrentUserId = actor;
        signature.SignedAt = time.GetUtcNow().UtcDateTime;
        await SaveSignedEvidenceAsync(ct);
        return signature;
    }

    private async Task SaveSignedEvidenceAsync(CancellationToken ct)
    {
        // The verification trigger matches the evidence rows to the top-level transaction ID.
        var autoSavepoints = db.Database.AutoSavepointsEnabled;
        db.Database.AutoSavepointsEnabled = false;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.Database.AutoSavepointsEnabled = autoSavepoints;
        }
    }

    private static void CheckVersion(uint current, uint? expected)
    {
        if (expected is uint version && current != version)
            throw OosTemplateService.VersionConflict();
    }

    private static bool ValidText(string? text) => !string.IsNullOrWhiteSpace(text) && text.Length <= 4000;

    private static BusinessRuleException EntryClosed() =>
        new(MessageCodes.OosEntryClosed, "Entries cannot be added to a concluded investigation.");

    private static BusinessRuleException ConclusionPhaseInvalid() =>
        new(MessageCodes.OosConclusionPhaseInvalid, "This conclusion is unavailable in the current investigation phase.");
}
