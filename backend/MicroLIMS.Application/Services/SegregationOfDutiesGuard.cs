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

    // The single source of the eight "performed" conditions, set-based: which of the given test orders did the
    // user perform? One query per condition however many orders are asked about; ids that do not exist are
    // simply absent. DidUserPerformTestAsync is this method with one id, so the two cannot drift.
    public async Task<IReadOnlySet<int>> GetTestOrderIdsPerformedByAsync(
        int userId, IReadOnlyCollection<int> testOrderIds, CancellationToken ct = default)
    {
        var performed = new HashSet<int>();
        if (testOrderIds.Count == 0) return performed;
        var ids = testOrderIds.Distinct().ToArray();

        // 1. Assigned as the analyst.
        performed.UnionWith(await _db.TestOrders.AsNoTracking()
            .Where(t => ids.Contains(t.Id) && t.AssignedAnalystId == userId)
            .Select(t => t.Id).ToListAsync(ct));

        // 2. Prepared the sample the test belongs to.
        performed.UnionWith(await _db.TestOrders.AsNoTracking()
            .Where(t => ids.Contains(t.Id)
                && _db.SamplePreparations.Any(p => p.SampleId == t.SampleId && p.PreparedByUserId == userId))
            .Select(t => t.Id).ToListAsync(ct));

        // 3-6. Entered or observed a raw result.
        performed.UnionWith(await _db.Results.AsNoTracking()
            .Where(r => ids.Contains(r.TestOrderId) && r.EnteredByUserId == userId)
            .Select(r => r.TestOrderId).ToListAsync(ct));
        performed.UnionWith(await _db.CountTestReadings.AsNoTracking()
            .Where(r => ids.Contains(r.TestOrderId) && r.EnteredByUserId == userId)
            .Select(r => r.TestOrderId).ToListAsync(ct));
        performed.UnionWith(await _db.PathogenObservations.AsNoTracking()
            .Where(p => ids.Contains(p.TestOrderId) && p.ObservedByUserId == userId)
            .Select(p => p.TestOrderId).ToListAsync(ct));
        performed.UnionWith(await _db.WorkflowStepResults.AsNoTracking()
            .Where(r => ids.Contains(r.TestOrderId) && r.SubmittedByUserId == userId)
            .Select(r => r.TestOrderId).ToListAsync(ct));

        // 7. Physicochemical workspaces (HPLC, dissolution, titration...) record here.
        performed.UnionWith(await _db.TestAnalyses.AsNoTracking()
            .Where(a => ids.Contains(a.TestOrderId) && a.EnteredByUserId == userId)
            .Select(a => a.TestOrderId).ToListAsync(ct));

        // 8. Confirmatory plate readings are APPENDED to a result row that
        // whoever ran the setup submitted, so the WorkflowStepResult
        // check above never sees the second analyst who actually read the
        // plates - without this they could review and approve their own
        // reading. Joined through the parent row, since the observation
        // carries no TestOrderId of its own.
        performed.UnionWith(await _db.ConfirmatoryPlateObservations.AsNoTracking()
            .Where(o => o.RecordedByUserId == userId && ids.Contains(o.WorkflowStepResult!.TestOrderId))
            .Select(o => o.WorkflowStepResult!.TestOrderId).ToListAsync(ct));

        return performed;
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
