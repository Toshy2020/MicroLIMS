using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services;

// GMP segregation of duties: the person who performed a test must not
// also review or approve it. Shared by sample review and approval, media
// and cryovial release, stock-lot release and pathogen confirmation. Deliberately role-agnostic - it only
// ever compares user IDs, so there is no code path for any role
// (including SystemAdministrator) to bypass it.
public class SegregationOfDutiesGuard
{
    private readonly IMicroLimsDbContext _db;

    public SegregationOfDutiesGuard(IMicroLimsDbContext db)
    {
        _db = db;
    }

    // "Performed" = assigned as the analyst, prepared the sample, or
    // entered/observed any raw result for this test order - Result,
    // CountTestReading, PathogenObservation, WorkflowStepResult,
    // TestAnalysis and ConfirmatoryPlateObservation are the only
    // result-bearing tables tied to a TestOrder
    public async Task<bool> DidUserPerformTestAsync(int testOrderId, int userId, CancellationToken ct = default) =>
        (await GetTestOrderIdsPerformedByAsync(userId, new[] { testOrderId }, ct)).Contains(testOrderId);

    public async Task<IReadOnlySet<int>> GetTestOrderIdsPerformedByAsync(
        int userId, IReadOnlyCollection<int> testOrderIds, CancellationToken ct = default)
    {
        if (testOrderIds.Count == 0) return new HashSet<int>();

        var performed = await PerformanceEvidence(testOrderIds.Distinct().ToArray())
            .Where(e => e.UserId == userId)
            .Select(e => e.OrderId)
            .Distinct()
            .ToListAsync(ct);
        return performed.ToHashSet();
    }

    internal IQueryable<int> PerformerIds(int[] orderIds) => PerformanceEvidence(orderIds).Select(e => e.UserId);

    private sealed class Performance
    {
        public int OrderId { get; init; }
        public int UserId { get; init; }
    }

    // One set of evidence sources for both the signer guard and the recipient lookup.
    private IQueryable<Performance> PerformanceEvidence(int[] orderIds)
    {
        var orders = _db.TestOrders.AsNoTracking().Where(o => orderIds.Contains(o.Id));
        return orders.Where(o => o.AssignedAnalystId.HasValue).Select(o => new Performance { OrderId = o.Id, UserId = o.AssignedAnalystId!.Value })
            .Concat(from order in orders
                    join prep in _db.SamplePreparations on order.SampleId equals prep.SampleId
                    select new Performance { OrderId = order.Id, UserId = prep.PreparedByUserId })
            .Concat(_db.Results.Where(r => orderIds.Contains(r.TestOrderId)).Select(r => new Performance { OrderId = r.TestOrderId, UserId = r.EnteredByUserId }))
            .Concat(_db.CountTestReadings.Where(r => orderIds.Contains(r.TestOrderId)).Select(r => new Performance { OrderId = r.TestOrderId, UserId = r.EnteredByUserId }))
            .Concat(_db.PathogenObservations.Where(o => orderIds.Contains(o.TestOrderId)).Select(o => new Performance { OrderId = o.TestOrderId, UserId = o.ObservedByUserId }))
            .Concat(_db.WorkflowStepResults.Where(r => orderIds.Contains(r.TestOrderId)).Select(r => new Performance { OrderId = r.TestOrderId, UserId = r.SubmittedByUserId }))
            .Concat(_db.TestAnalyses.Where(a => orderIds.Contains(a.TestOrderId)).Select(a => new Performance { OrderId = a.TestOrderId, UserId = a.EnteredByUserId }))
            // Confirmatory plate readings can have a different performer from the parent result.
            .Concat(_db.ConfirmatoryPlateObservations.Where(o => orderIds.Contains(o.WorkflowStepResult!.TestOrderId))
                .Select(o => new Performance { OrderId = o.WorkflowStepResult!.TestOrderId, UserId = o.RecordedByUserId }));
    }

    // "Performed" for a Media lot = prepared it, or read any challenge
    // result on its evaluation. Both count: the release decision is a
    // judgment on the preparation AND the evaluation that qualified it,
    // so neither hand may also sign it off.
    public async Task<bool> DidUserPrepareOrEvaluateMediaAsync(int mediaId, int userId)
    {
        if (await _db.Media.AnyAsync(m => m.Id == mediaId && m.PreparedByUserId == userId)) return true;

        return await _db.MediaEvaluationChallenges
            .AnyAsync(c => c.MediaEvaluation!.MediaId == mediaId && c.ReadByUserId == userId);
    }

    // A received stock lot has one actor: whoever received (recorded) it.
    public Task<bool> DidUserReceiveMaterialAsync(int materialId, int userId) =>
        _db.Materials.AnyAsync(m => m.Id == materialId && m.CreatedByUserId == userId);

    // A prepared solution has one actor: whoever signed its completion.
    public Task<bool> DidUserPrepareSolutionAsync(int preparationId, int userId) =>
        _db.SolutionPreparations.AnyAsync(p => p.Id == preparationId && p.PreparedByUserId == userId);

    // A Cryovial batch has exactly one actor - whoever prepared it and
    // filled in its identity-confirmation panel (IdentityConfirmationEntry
    // carries no separate user of its own).
    public Task<bool> DidUserPrepareCryovialAsync(int cryovialId, int userId) =>
        _db.Cryovials.AnyAsync(c => c.Id == cryovialId && c.PreparedByUserId == userId);
}
