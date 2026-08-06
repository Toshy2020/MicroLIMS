using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// The human gate on media release. A lot only becomes usable in routine
// testing when a Section Head signs for it here - the evaluation proves
// the lot performs, this proves someone accountable agreed to put it
// into service. Single-step by design (media is prepared frequently
// enough that a two-person review+approval would not be workable), but
// segregation of duties still applies: the preparer and anyone who read
// its evaluation are excluded.
public class MediaReleaseService
{
    private readonly MicroLimsDbContext _db;
    private readonly SegregationOfDutiesGuard _segregationOfDuties;
    private readonly ReviewGateService _reviewGate;
    private readonly MediaSummaryService _summary;
    private readonly RecordArchiveService _archive;

    public MediaReleaseService(MicroLimsDbContext db, SegregationOfDutiesGuard segregationOfDuties,
        ReviewGateService reviewGate, MediaSummaryService summary, RecordArchiveService archive)
    {
        _db = db;
        _segregationOfDuties = segregationOfDuties;
        _reviewGate = reviewGate;
        _summary = summary;
        _archive = archive;
    }

    // Lots whose evaluation has completed Conform and which are still
    // awaiting a release decision - the Section Head's queue.
    public async Task<List<Media>> GetAwaitingApprovalAsync()
    {
        var qualifiedMediaIds = await _db.MediaEvaluations
            .Where(e => e.Status == MediaEvaluationStatus.Completed && e.Outcome == EvaluationOutcome.Conform)
            .Select(e => e.MediaId)
            .ToListAsync();

        return await _db.Media
            .Include(m => m.MediaType)
            .Where(m => qualifiedMediaIds.Contains(m.Id) && m.ApprovalStatus == ApprovalGateStatus.PendingReview)
            .OrderByDescending(m => m.Id)
            .ToListAsync();
    }

    public async Task DecideAsync(int mediaId, int sectionHeadUserId, string password, bool approved, string? comment, string? ipAddress)
    {
        var media = await _db.Media.FirstOrDefaultAsync(m => m.Id == mediaId)
            ?? throw new InvalidOperationException($"Media lot {mediaId} not found.");

        if (media.ApprovalStatus != ApprovalGateStatus.PendingReview)
            throw new InvalidOperationException($"Media lot {media.LotNumber} has already been decided ({media.ApprovalStatus}).");

        var evaluation = await _db.MediaEvaluations.FirstOrDefaultAsync(e => e.MediaId == mediaId)
            ?? throw new InvalidOperationException($"Media lot {media.LotNumber} has no evaluation record.");

        // Approving a lot whose evaluation hasn't finished (or failed)
        // would put unqualified media into routine testing - the whole
        // point of the gate. Rejecting one is always allowed.
        if (approved)
        {
            if (evaluation.Status != MediaEvaluationStatus.Completed)
                throw new InvalidOperationException($"Media lot {media.LotNumber}'s evaluation is not complete yet.");
            if (evaluation.Outcome != EvaluationOutcome.Conform)
                throw new InvalidOperationException($"Media lot {media.LotNumber}'s evaluation did not conform - it cannot be released.");
        }

        if (await _segregationOfDuties.DidUserPrepareOrEvaluateMediaAsync(mediaId, sectionHeadUserId))
            throw new InvalidOperationException("You cannot approve a media lot you prepared or evaluated.");

        // Signs first - if password verification fails, nothing below is
        // written (the signature, the event, and the state change below
        // commit together in the single SaveChangesAsync at the end).
        await _reviewGate.SignAndLogAsync(
            ReviewEntityTypes.Media, mediaId, sectionHeadUserId, password,
            approved ? SignatureMeaning.Approved : SignatureMeaning.Rejected,
            ReviewWorkflowEventType.ApprovalDecisionMade, comment, ipAddress,
            approved ? ApprovalDecision.Approve : ApprovalDecision.Reject);

        media.ApprovalStatus = approved ? ApprovalGateStatus.Approved : ApprovalGateStatus.Rejected;
        media.ApprovedByUserId = sectionHeadUserId;
        media.ApprovedAt = DateTime.UtcNow;

        if (approved)
        {
            media.IsReleasedForUse = true;
            media.Status = MediaStatus.Active;
        }
        else
        {
            media.Status = MediaStatus.QuarantineFailed;
        }

        await _db.SaveChangesAsync();

        // Freeze an immutable PDF of the lot record as decided.
        var document = await _summary.BuildReportDocumentAsync(mediaId);
        if (document is not null)
            await _archive.ArchiveAsync(ReviewEntityTypes.Media, mediaId, document,
                approved ? "Media lot released" : "Media lot rejected", sectionHeadUserId);
    }
}
