using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Services.MyWork.Sources;

namespace MicroLIMS.Application.Services;

// When may an analyst give a test back? One rule for the release endpoint and for the "release" action My Work
// shows on a row, so the button never offers what the endpoint would refuse. Set-based: one answer for many tests.
//
// A test is releasable by the caller when all of these hold:
//   1. it is assigned to the caller, still open, not superseded and not outsourced;
//   2. the last assignment audit row for it is a TEST_TAKEN written by the caller (a Section Head's assignment,
//      or any later reassignment, ends the right to release);
//   3. the caller has recorded nothing for the sample: no result, count reading, pathogen observation, workflow
//      step, analysis, incubation (started or read) or sample preparation.
internal static class TestReleaseRules
{
    public const string TakenAction = "TEST_TAKEN";
    public const string ReleasedAction = "TEST_RELEASED";
    public const string CannotRelease = "This test can no longer be released. Ask a Section Head to reassign it.";

    // Every action that changes a test's analyst (SampleAssignmentService.ApplyAsync and Take / Release).
    private static readonly string[] AssignmentActions =
        { "AssignedAnalyst", "UnassignedAnalyst", "ReassignedAnalyst", TakenAction, ReleasedAction };

    public static async Task<HashSet<int>> ReleasableAsync(
        IMicroLimsDbContext db, int userId, IReadOnlyCollection<int> testOrderIds, CancellationToken ct)
    {
        var releasable = new HashSet<int>();
        if (testOrderIds.Count == 0) return releasable;
        var ids = testOrderIds.Distinct().ToArray();

        var mine = await db.TestOrders.AsNoTracking()
            .Where(t => ids.Contains(t.Id) && t.AssignedAnalystId == userId && t.OutsourcedToLab == null)
            .Where(AnalystQueries.IsActiveTest)
            .Select(t => new { t.Id, t.SampleId })
            .ToListAsync(ct);
        if (mine.Count == 0) return releasable;

        // Latest assignment row per test (audit ids only grow).
        var idTexts = mine.Select(t => t.Id.ToString()).ToArray();
        var rows = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == "TestOrder" && idTexts.Contains(a.EntityId) && AssignmentActions.Contains(a.Action))
            .OrderBy(a => a.Id)
            .Select(a => new { a.EntityId, a.Action, a.UserId })
            .ToListAsync(ct);
        var lastByTest = new Dictionary<string, (string Action, int? UserId)>();
        foreach (var row in rows) lastByTest[row.EntityId] = (row.Action, row.UserId);

        var takenByMe = mine
            .Where(t => lastByTest.TryGetValue(t.Id.ToString(), out var last) && last.Action == TakenAction && last.UserId == userId)
            .ToList();
        if (takenByMe.Count == 0) return releasable;

        var recorded = await SamplesWithRecordsByAsync(db, userId, takenByMe.Select(t => t.SampleId).Distinct().ToArray(), ct);
        foreach (var t in takenByMe.Where(t => !recorded.Contains(t.SampleId)))
            releasable.Add(t.Id);
        return releasable;
    }

    // Which of these samples has anything recorded by the user? One query per table, however many samples.
    private static async Task<HashSet<int>> SamplesWithRecordsByAsync(
        IMicroLimsDbContext db, int userId, int[] sampleIds, CancellationToken ct)
    {
        var found = new HashSet<int>();
        var orders = db.TestOrders.AsNoTracking().Where(t => sampleIds.Contains(t.SampleId));

        found.UnionWith(await orders.Where(t => db.Results.Any(r => r.TestOrderId == t.Id && r.EnteredByUserId == userId))
            .Select(t => t.SampleId).ToListAsync(ct));
        found.UnionWith(await orders.Where(t => db.CountTestReadings.Any(r => r.TestOrderId == t.Id && r.EnteredByUserId == userId))
            .Select(t => t.SampleId).ToListAsync(ct));
        found.UnionWith(await orders.Where(t => db.PathogenObservations.Any(p => p.TestOrderId == t.Id && p.ObservedByUserId == userId))
            .Select(t => t.SampleId).ToListAsync(ct));
        found.UnionWith(await orders.Where(t => db.WorkflowStepResults.Any(r => r.TestOrderId == t.Id && r.SubmittedByUserId == userId))
            .Select(t => t.SampleId).ToListAsync(ct));
        found.UnionWith(await orders.Where(t => db.TestAnalyses.Any(a => a.TestOrderId == t.Id && a.EnteredByUserId == userId))
            .Select(t => t.SampleId).ToListAsync(ct));
        found.UnionWith(await orders.Where(t => db.Incubations.Any(i => i.TestOrderId == t.Id
                && (i.StartedByUserId == userId || i.CompletedByUserId == userId)))
            .Select(t => t.SampleId).ToListAsync(ct));
        found.UnionWith(await db.SamplePreparations.AsNoTracking()
            .Where(p => sampleIds.Contains(p.SampleId) && p.PreparedByUserId == userId)
            .Select(p => p.SampleId).ToListAsync(ct));
        return found;
    }
}
