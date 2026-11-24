using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Oos;

public sealed class OosGate(IMicroLimsDbContext db, OosOpeningRule rule)
{
    internal static async Task LockSampleDecisionAsync(IMicroLimsDbContext context, int sampleId, CancellationToken ct)
    {
        if (context.Database.IsRelational())
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""SELECT 1 FROM "Samples" WHERE "Id" = {sampleId} FOR UPDATE""", ct);
    }

    public async Task<ActionCheck> CheckSectionMayBeDecidedAsync(
        Sample sample, int sectionId, ApprovalDecision decision, CancellationToken ct = default)
    {
        var isRetest = decision is ApprovalDecision.RetestRetainedSample or ApprovalDecision.NewSampleRequest;
        if (!isRetest && decision is not (ApprovalDecision.Approve or ApprovalDecision.Reject))
            return ActionCheck.Ok;

        var covering = Covering(sample.Id, sectionId);
        if (isRetest)
        {
            if (await covering.AnyAsync(ct))
                return ActionCheck.No(MessageCodes.OosRetestNeedsApprovedPlan, "A retest needs an approved retest plan.");
        }
        else
        {
            var openNumber = await FirstOpenNumberAsync(covering, ct);
            if (openNumber is not null) return InvestigationOpen(openNumber);
        }

        var orderIds = SampleSectionRollup.CurrentOrders(sample, sectionId).Select(o => o.Id).ToArray();
        var failing = await rule.ReadFailingOrdersAsync(orderIds, ct);
        if (failing.Count > 0)
        {
            var failingIds = failing.Keys.ToArray();
            var coveredOrderCount = await db.OosInvestigationResults.AsNoTracking()
                .Where(r => failingIds.Contains(r.TestOrderId) &&
                    db.OosInvestigations.Any(i => i.Id == r.InvestigationId && i.Kind == OosInvestigationKind.Oos))
                .Select(r => r.TestOrderId).Distinct().CountAsync(ct);
            if (coveredOrderCount < failingIds.Length)
                return ActionCheck.No(MessageCodes.OosFailingResultWithoutInvestigation,
                    "This sample has an out-of-specification result without an investigation.");
        }

        if (decision != ApprovalDecision.Approve) return ActionCheck.Ok;

        if (await covering.AnyAsync(i => i.Status == OosInvestigationStatus.Concluded && i.Conclusion == OosConclusion.Confirmed, ct))
            return ActionCheck.No(MessageCodes.OosConfirmedCannotBeApproved,
                "A confirmed out-of-specification result cannot be approved.");

        if (failing.Count > 0 && await (from link in db.OosRetestPlanSamples
                                       join plan in db.OosRetestPlans on link.PlanId equals plan.Id
                                       join investigation in db.OosInvestigations on plan.InvestigationId equals investigation.Id
                                       where link.SampleId == sample.Id && plan.SignedAtUtc != null &&
                                           investigation.SectionId == sectionId
                                       select link.SampleId).AnyAsync(ct))
            return ActionCheck.No(MessageCodes.OosFailingRetestCannotBeApproved,
                "A retest with an out-of-specification result cannot be approved.");

        // The origin waits for signed-plan samples to be approved. A joined retest
        // must be able to reach Approved before the origin's prerequisite can pass.
        var invalidatedNumber = await covering
            .Where(i => i.SampleId == sample.Id && i.SectionId == sectionId &&
                i.Status == OosInvestigationStatus.Concluded && i.Conclusion == OosConclusion.Invalidated)
            .Where(i => (from plan in db.OosRetestPlans
                         join link in db.OosRetestPlanSamples on plan.Id equals link.PlanId
                         join retest in db.Samples on link.SampleId equals retest.Id
                         where plan.InvestigationId == i.Id && plan.SignedAtUtc != null && retest.Status != SampleStatus.Approved
                         select link.SampleId).Any())
            .OrderBy(i => i.Id).Select(i => i.Number).FirstOrDefaultAsync(ct);

        return invalidatedNumber is null ? ActionCheck.Ok : ActionCheck.No(MessageCodes.OosRetestSamplesNotApproved,
            $"Retest samples of {invalidatedNumber} are not all approved.", NumberParameter(invalidatedNumber));
    }

    public async Task EnsureCertificateAllowedAsync(int sampleId, CancellationToken ct = default)
    {
        var number = await FirstOpenNumberAsync(Covering(sampleId, sectionId: null), ct);
        if (number is not null) InvestigationOpen(number).ThrowIfRefused();

        // No approved reporting rule selects a reportable result from several retests.
        if (await db.OosInvestigations.AsNoTracking()
            .Where(i => i.SampleId == sampleId && i.Kind == OosInvestigationKind.Oos &&
                i.Status == OosInvestigationStatus.Concluded && i.Conclusion == OosConclusion.Invalidated)
            .AnyAsync(i => (from plan in db.OosRetestPlans
                            join link in db.OosRetestPlanSamples on plan.Id equals link.PlanId
                            where plan.InvestigationId == i.Id && plan.SignedAtUtc != null
                            select link.SampleId).Distinct().Count() > 1, ct))
            ActionCheck.No(MessageCodes.OosCertificateMultipleRetests,
                "A certificate cannot be issued until the reporting rule for multiple retests is resolved.").ThrowIfRefused();
    }

    private IQueryable<OosInvestigation> Covering(int sampleId, int? sectionId)
    {
        var orders = db.TestOrders.Where(o => o.SampleId == sampleId);
        var investigations = db.OosInvestigations.AsNoTracking().Where(i => i.Kind == OosInvestigationKind.Oos);
        if (sectionId is int section)
            orders = orders.Where(o => o.SectionId == section);

        return investigations.Where(i => (i.SampleId == sampleId && (sectionId == null || i.SectionId == sectionId)) ||
            i.Results.Any(r => orders.Any(o => o.Id == r.TestOrderId)) ||
            ((sectionId == null || i.SectionId == sectionId) &&
                (from link in db.OosRetestPlanSamples
                 join plan in db.OosRetestPlans on link.PlanId equals plan.Id
                 where link.SampleId == sampleId && plan.InvestigationId == i.Id && plan.SignedAtUtc != null
                 select link.SampleId).Any()));
    }

    private static Task<string?> FirstOpenNumberAsync(IQueryable<OosInvestigation> investigations, CancellationToken ct) =>
        investigations.Where(i => i.Status != OosInvestigationStatus.Concluded)
            .OrderBy(i => i.Id).Select(i => i.Number).FirstOrDefaultAsync(ct);

    private static ActionCheck InvestigationOpen(string number) =>
        ActionCheck.No(MessageCodes.OosInvestigationOpen, $"OOS investigation {number} is open.", NumberParameter(number));

    private static Dictionary<string, string> NumberParameter(string number) => new() { ["number"] = number };
}
