using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Oos;

public sealed record CloseExcursionRequest(int CauseCategoryId, string CauseDescription, string ActionTaken, string Password);

public sealed partial class OosInvestigationService
{
    public Task<OosManualOpeningDto> OpenManualExcursionAsync(int testOrderId, string reason, int userId, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            await EnsureInvestigationPermissionAsync(userId, PermissionConstants.OosContribute, PermissionConstants.OosManage, ct);
            if (!ValidText(reason))
                throw new BusinessRuleException(MessageCodes.OosExcursionReasonInvalid, "Enter an excursion reason of 1 to 4000 characters.");

            var order = await db.TestOrders.AsNoTracking()
                .Where(o => o.Id == testOrderId)
                .Select(o => new { o.Id, o.SampleId, o.SectionId, o.IsSuperseded, SampleStatus = o.Sample!.Status })
                .SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException(MessageCodes.TestOrderNotFound, $"Test order {testOrderId} not found.",
                    new Dictionary<string, string> { ["testOrderId"] = testOrderId.ToString(CultureInfo.InvariantCulture) });

            await EnsureScopeAsync(userId, order.SectionId, SectionAccess.Write, ct);
            if (order.IsSuperseded)
                throw new BusinessRuleException(MessageCodes.OosExcursionOrderSuperseded,
                    "A superseded test order cannot open an excursion review.");
            if (order.SampleStatus is SampleStatus.Approved or SampleStatus.Rejected or SampleStatus.Cancelled or SampleStatus.Voided)
                throw new BusinessRuleException(MessageCodes.OosExcursionSampleClosed,
                    "An excursion review cannot be opened for a sample that is already decided or cancelled.");

            var (failing, outcomes) = await rule.ReadReviewOutcomesAsync([order.Id], ct);
            if (failing.ContainsKey(order.Id))
                throw new BusinessRuleException(MessageCodes.OosExcursionFailingOrderRequiresOos,
                    "An out-of-specification or detected result must use an OOS investigation.");

            await numbers.AcquireOosNumberLockAsync(ct);

            var openExcursion = db.OosInvestigations.Where(i => i.Kind == OosInvestigationKind.Excursion &&
                i.SampleId == order.SampleId && i.SectionId == order.SectionId && i.Status != OosInvestigationStatus.Concluded);
            var excursion = await LockOpenAsync(openExcursion, ct);
            var now = time.GetUtcNow().UtcDateTime;
            db.CurrentUserId = userId;
            var trimmedReason = reason.Trim();
            db.CurrentReason = trimmedReason;

            if (excursion is null)
            {
                var sample = await db.Samples.SingleAsync(s => s.Id == order.SampleId, ct);
                excursion = await CreateAsync(sample, order.SectionId, OosInvestigationKind.Excursion, userId, now, ct);
            }

            var alreadyLinked = excursion.Id != 0 && await db.OosInvestigationResults.AsNoTracking()
                .AnyAsync(r => r.InvestigationId == excursion.Id && r.TestOrderId == order.Id, ct);
            if (!alreadyLinked)
            {
                var hasOutcome = outcomes.TryGetValue(order.Id, out var outcome);
                excursion.Results.Add(new OosInvestigationResult
                {
                    TestOrderId = order.Id,
                    OutcomeAtOpening = hasOutcome ? OosOutcomeMapper.Map(outcome!.Outcome) : OosOutcomeClass.Conforms,
                    ResultSummaryAtOpening = hasOutcome
                        ? outcome!.Summary
                        : (trimmedReason.Length > 500 ? trimmedReason[..500] : trimmedReason),
                    AddedAtUtc = now
                });
            }

            if (excursion.Id == 0)
                await db.SaveChangesAsync(ct);

            await AppendAsync(excursion, 1, OosEntryKind.Note, null, null, trimmedReason, userId, ct);
            return new OosManualOpeningDto(excursion.Id, excursion.Number, excursion.Version);
        }, ct);

    public Task CloseExcursionAsync(int investigationId, CloseExcursionRequest request, uint expectedVersion, int userId,
        string? ip, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            var investigation = await OpenForEditAsync(investigationId, userId, PermissionConstants.OosManage, ct, ExcursionInvalidState);
            CheckVersion(investigation.Version, expectedVersion);
            if (investigation.Kind != OosInvestigationKind.Excursion || investigation.Status == OosInvestigationStatus.Concluded)
                throw ExcursionInvalidState();

            if (!ValidText(request.CauseDescription) || !ValidText(request.ActionTaken))
                throw new BusinessRuleException(MessageCodes.OosExcursionTextInvalid,
                    "Enter a cause description and action taken of 1 to 4000 characters.");

            await EnsureIndependentSignerAsync(investigationId, userId, ct);
            await EnsureExcursionChecklistAnsweredAsync(investigation, ct);

            if (db.Database.IsRelational())
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""SELECT 1 FROM "OosCauseCategories" WHERE "Id" = {request.CauseCategoryId} FOR SHARE""", ct);
            var category = await db.OosCauseCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == request.CauseCategoryId, ct)
                ?? throw new BusinessRuleException(MessageCodes.OosCategoryNotFound, "The OOS cause category was not found.");
            if (!category.IsActive)
                throw new BusinessRuleException(MessageCodes.OosConclusionCauseInactive, "The selected cause category is no longer active.");

            var signature = await SignInvestigationAsync(investigationId, userId, request.Password,
                SignatureMeaning.ExcursionReviewClosed, request.ActionTaken.Trim(), ip, ct);

            investigation.Status = OosInvestigationStatus.Concluded;
            investigation.Conclusion = OosConclusion.ExcursionClosed;
            investigation.RootCauseCategoryId = request.CauseCategoryId;
            investigation.RootCauseDescription = request.CauseDescription.Trim();
            investigation.ConclusionSummary = request.ActionTaken.Trim();
            investigation.ConcludedByUserId = userId;
            investigation.ConcludedAtUtc = signature.SignedAt;
            investigation.ConclusionSignatureId = signature.Id;

            await db.SaveChangesAsync(ct);
        }, ct);

    private async Task<(OosInvestigation? Investigation, bool Opened)> ApplyReviewedExcursionsAsync(
        Sample sample, int sectionId, int reviewerUserId, Dictionary<int, OosOpeningResult> excursions, DateTime now,
        CancellationToken ct)
    {
        if (excursions.Count == 0) return default;

        var belongsToMonitoring = await db.SampleLocations.AsNoTracking()
            .AnyAsync(l => l.SampleId == sample.Id && l.MonitoringOccurrenceId != null, ct);
        if (belongsToMonitoring) return default;

        var alertOpens = await db.OosInvestigationTemplates.AsNoTracking()
            .Where(t => t.SectionId == sectionId && t.Kind == OosInvestigationKind.Excursion)
            .Select(t => t.AlertOpensExcursionReview)
            .SingleOrDefaultAsync(ct);

        var eligible = excursions
            .Where(pair => pair.Value.Outcome.Status == ResultStatus.ActionLimitExceeded ||
                (alertOpens && pair.Value.Outcome.Status == ResultStatus.AlertLimitExceeded))
            .ToList();
        if (eligible.Count == 0) return default;

        await numbers.AcquireOosNumberLockAsync(ct);

        var openExcursion = db.OosInvestigations.Where(i => i.Kind == OosInvestigationKind.Excursion &&
            i.SampleId == sample.Id && i.SectionId == sectionId && i.Status != OosInvestigationStatus.Concluded);
        var excursion = await LockOpenAsync(openExcursion, ct);
        var opened = excursion is null;

        var existingOrderIds = new HashSet<int>();
        if (excursion is null)
        {
            excursion = await CreateAsync(sample, sectionId, OosInvestigationKind.Excursion, reviewerUserId, now, ct);
        }
        else
        {
            var orderIds = eligible.Select(pair => pair.Key).ToArray();
            existingOrderIds = (await db.OosInvestigationResults.AsNoTracking()
                .Where(r => r.InvestigationId == excursion.Id && orderIds.Contains(r.TestOrderId))
                .Select(r => r.TestOrderId).ToListAsync(ct)).ToHashSet();
            if (eligible.Any(pair => !existingOrderIds.Contains(pair.Key)))
                db.Entry(excursion).Property(nameof(OosInvestigation.Status)).IsModified = true;
        }

        foreach (var (orderId, outcome) in eligible)
        {
            if (existingOrderIds.Contains(orderId)) continue;
            excursion.Results.Add(new OosInvestigationResult
            {
                TestOrderId = orderId,
                OutcomeAtOpening = OosOutcomeMapper.Map(outcome.Outcome),
                ResultSummaryAtOpening = outcome.Summary,
                AddedAtUtc = now
            });
        }

        return (excursion, opened);
    }

    private async Task EnsureExcursionChecklistAnsweredAsync(OosInvestigation investigation, CancellationToken ct)
    {
        var keys = ReadChecklistKeys(investigation, 1);
        if (keys.Count == 0)
            throw new BusinessRuleException(MessageCodes.OosExcursionTemplateEmpty,
                "No excursion checklist is configured for this laboratory.");

        var entries = db.OosInvestigationEntrys.AsNoTracking()
            .Where(e => e.InvestigationId == investigation.Id && e.Phase == 1 &&
                e.Kind == OosEntryKind.ChecklistAnswer && e.ChecklistItemKey != null);
        var latestIds = entries.GroupBy(e => e.ChecklistItemKey).Select(g => g.Max(e => e.Id));
        var answered = await entries.Where(e => latestIds.Contains(e.Id) && e.Answer != null)
            .Select(e => e.ChecklistItemKey).ToListAsync(ct);
        if (!keys.IsSubsetOf(answered.OfType<string>()))
            throw new BusinessRuleException(MessageCodes.OosExcursionChecklistIncomplete,
                "Answer every active excursion checklist item before closing.");
    }

    private static BusinessRuleException ExcursionInvalidState() =>
        new(MessageCodes.OosExcursionInvalidState, "This action is only available for an open excursion review.");
}
