using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Oos;

public sealed record ResolvedPlanRetest(
    int PlanId,
    int PlanRevision,
    int ReportableSampleId,
    int? ReportableRetestNumber,
    int? SignatureId,
    DateTime? SignedAtUtc);

public sealed record OosReportableRetestResolution(
    IReadOnlyDictionary<int, ResolvedPlanRetest> ByInvestigationId,
    bool HasUnclear,
    IReadOnlyDictionary<int, int> SignedPlanSampleToInvestigationId);

public static class OosReportableRetest
{
    public static async Task<OosReportableRetestResolution> ResolveAsync(
        IMicroLimsDbContext db, int originSampleId, CancellationToken ct = default)
    {
        var investigationIds = await db.OosInvestigations.AsNoTracking()
            .Where(i => i.SampleId == originSampleId && i.Kind == OosInvestigationKind.Oos &&
                        i.Status == OosInvestigationStatus.Concluded && i.Conclusion == OosConclusion.Invalidated)
            .Select(i => i.Id)
            .ToListAsync(ct);

        if (investigationIds.Count == 0)
        {
            return new OosReportableRetestResolution(
                new Dictionary<int, ResolvedPlanRetest>(),
                HasUnclear: false,
                new Dictionary<int, int>());
        }

        var signedPlans = await db.OosRetestPlans.AsNoTracking()
            .Where(p => investigationIds.Contains(p.InvestigationId) && p.SignedAtUtc != null)
            .OrderByDescending(p => p.Revision)
            .Select(p => new
            {
                p.Id,
                p.InvestigationId,
                p.Revision,
                p.ReportableRetestNumber,
                p.ReportableSampleId,
                p.SignatureId,
                p.SignedAtUtc
            })
            .ToListAsync(ct);

        var planIds = signedPlans.Select(p => p.Id).ToArray();
        var links = planIds.Length == 0
            ? []
            : await db.OosRetestPlanSamples.AsNoTracking()
                .Where(l => planIds.Contains(l.PlanId))
                .Select(l => new { l.PlanId, l.SampleId })
                .ToListAsync(ct);

        var samplesByPlan = links.GroupBy(l => l.PlanId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SampleId).Distinct().ToList());

        var sampleToInvestigation = new Dictionary<int, int>();
        foreach (var plan in signedPlans)
        {
            if (!samplesByPlan.TryGetValue(plan.Id, out var planSamples)) continue;
            foreach (var sampleId in planSamples)
                sampleToInvestigation.TryAdd(sampleId, plan.InvestigationId);
        }

        var byInvestigation = new Dictionary<int, ResolvedPlanRetest>();
        var hasUnclear = false;

        foreach (var invId in investigationIds)
        {
            var governing = signedPlans.FirstOrDefault(p => p.InvestigationId == invId);
            if (governing is null) continue;

            if (governing.ReportableSampleId is int reportableId)
            {
                byInvestigation[invId] = new ResolvedPlanRetest(
                    governing.Id, governing.Revision, reportableId,
                    governing.ReportableRetestNumber, governing.SignatureId, governing.SignedAtUtc);
                continue;
            }

            var linked = samplesByPlan.GetValueOrDefault(governing.Id) ?? [];
            if (linked.Count == 1)
            {
                byInvestigation[invId] = new ResolvedPlanRetest(
                    governing.Id, governing.Revision, linked[0],
                    governing.ReportableRetestNumber ?? 1, governing.SignatureId, governing.SignedAtUtc);
            }
            else if (linked.Count > 1)
            {
                hasUnclear = true;
            }
        }

        return new OosReportableRetestResolution(byInvestigation, hasUnclear, sampleToInvestigation);
    }
}
