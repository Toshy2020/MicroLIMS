using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Oos;

public sealed class OosGate(IMicroLimsDbContext db, OosOpeningRule rule)
{
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
    }

    private IQueryable<OosInvestigation> Covering(int sampleId, int? sectionId)
    {
        var orders = db.TestOrders.Where(o => o.SampleId == sampleId);
        var investigations = db.OosInvestigations.AsNoTracking().Where(i => i.Kind == OosInvestigationKind.Oos);
        if (sectionId is int section)
            orders = orders.Where(o => o.SectionId == section);

        return investigations.Where(i => (i.SampleId == sampleId && (sectionId == null || i.SectionId == sectionId)) ||
            i.Results.Any(r => orders.Any(o => o.Id == r.TestOrderId)));
    }

    private static Task<string?> FirstOpenNumberAsync(IQueryable<OosInvestigation> investigations, CancellationToken ct) =>
        investigations.Where(i => i.Status != OosInvestigationStatus.Concluded)
            .OrderBy(i => i.Id).Select(i => i.Number).FirstOrDefaultAsync(ct);

    private static ActionCheck InvestigationOpen(string number) =>
        ActionCheck.No(MessageCodes.OosInvestigationOpen, $"OOS investigation {number} is open.", NumberParameter(number));

    private static Dictionary<string, string> NumberParameter(string number) => new() { ["number"] = number };
}
