using Microsoft.EntityFrameworkCore;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// GMP segregation of duties: the person who performed a test must not
// also review or approve it. Shared by ReviewService.MarkReviewedAsync
// and ApprovalService.DecideAsync. Deliberately role-agnostic - it only
// ever compares user IDs, so there is no code path for any role
// (including SystemAdministrator) to bypass it.
public class SegregationOfDutiesGuard
{
    private readonly MicroLimsDbContext _db;

    public SegregationOfDutiesGuard(MicroLimsDbContext db)
    {
        _db = db;
    }

    // "Performed" = assigned as the analyst, or entered/observed any raw
    // result for this test order - Result, CountTestReading,
    // PathogenObservation, and WorkflowStepResult are the only result-
    // bearing tables tied to a TestOrder with a *ByUserId column
    // (MediaEvaluationChallenge.ReadByUserId belongs to a Media lot, not
    // a TestOrder, so it doesn't apply here).
    public async Task<bool> DidUserPerformTestAsync(int testOrderId, int userId)
    {
        var assignedAnalystId = await _db.TestOrders
            .Where(t => t.Id == testOrderId)
            .Select(t => t.AssignedAnalystId)
            .FirstOrDefaultAsync();

        if (assignedAnalystId == userId) return true;

        if (await _db.Results.AnyAsync(r => r.TestOrderId == testOrderId && r.EnteredByUserId == userId)) return true;
        if (await _db.CountTestReadings.AnyAsync(r => r.TestOrderId == testOrderId && r.EnteredByUserId == userId)) return true;
        if (await _db.PathogenObservations.AnyAsync(p => p.TestOrderId == testOrderId && p.ObservedByUserId == userId)) return true;
        if (await _db.WorkflowStepResults.AnyAsync(r => r.TestOrderId == testOrderId && r.SubmittedByUserId == userId)) return true;

        return false;
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

    // A Cryovial batch has exactly one actor - whoever prepared it and
    // filled in its identity-confirmation panel (IdentityConfirmationEntry
    // carries no separate user of its own).
    public Task<bool> DidUserPrepareCryovialAsync(int cryovialId, int userId) =>
        _db.Cryovials.AnyAsync(c => c.Id == cryovialId && c.PreparedByUserId == userId);
}
