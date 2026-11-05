using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

public record ReleaseSolutionPreparationRequest(bool Approved, string? Comment, string Password);

// The release gate on a prepared solution whose recipe needs release before use.
// SolutionPreparationService.CompleteAsync leaves such a preparation PendingReview;
// a Reviewer or Section Head signs for it here. Single step, same shape and the
// same segregation of duties as MaterialReleaseService: whoever prepared it cannot
// release it. The status stays Prepared; GetAvailableAsync and the prepared-solution
// picker refuse a PendingReview or Rejected preparation.
public class SolutionPreparationReleaseService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly SegregationOfDutiesGuard _segregationOfDuties;
    private readonly ReviewGateService _reviewGate;
    private readonly ILabClock _clock;

    public SolutionPreparationReleaseService(
        IMicroLimsDbContext db, IUserSectionScopeService scope,
        SegregationOfDutiesGuard segregationOfDuties, ReviewGateService reviewGate, ILabClock? clock = null)
    {
        _db = db;
        _scope = scope;
        _segregationOfDuties = segregationOfDuties;
        _reviewGate = reviewGate;
        _clock = clock ?? LabClock.Default;
    }

    public Task DecideAsync(int id, ReleaseSolutionPreparationRequest r, int userId, string? ipAddress) =>
        UnitOfWork.RunAsync(_db, () => DecideCoreAsync(id, r, userId, ipAddress));

    private async Task DecideCoreAsync(int id, ReleaseSolutionPreparationRequest r, int userId, string? ipAddress)
    {
        var prep = await _db.SolutionPreparations.Include(p => p.StatusHistory).FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException($"Solution preparation {id} not found.");

        var scope = await _scope.GetSectionIdsAsync(userId, SectionAccess.Write);
        if (scope != null && !scope.Contains(prep.SectionId))
            throw new NotFoundException($"Solution preparation {id} not found.");

        RecordVersion.EnsureCurrent(_db, prep);

        var now = _clock.UtcNow.UtcDateTime;
        if (SolutionPreparationResponse.EffectiveStatusOf(prep, now) != SolutionPreparationStatus.Prepared)
            throw new InvalidOperationException("This preparation is closed.");

        var label = prep.Code ?? $"#{prep.Id}";
        if (prep.ApprovalStatus is null)
            throw new InvalidOperationException($"Preparation {label} does not need a release.");
        if (prep.ApprovalStatus != ApprovalGateStatus.PendingReview)
            throw new InvalidOperationException($"Preparation {label} has already been decided ({prep.ApprovalStatus}).");

        var comment = string.IsNullOrWhiteSpace(r.Comment) ? null : r.Comment.Trim();
        if (!r.Approved && comment is null)
            throw new InvalidOperationException("A comment is required when rejecting a preparation.");
        if (comment is { Length: > 500 })
            throw new InvalidOperationException("Comment cannot exceed 500 characters.");

        if (await _segregationOfDuties.DidUserPrepareSolutionAsync(id, userId))
            throw new InvalidOperationException("You cannot release a preparation you prepared.");

        // Signs first - a wrong password throws and nothing below is written (the
        // signature, the event and the state change commit in one SaveChangesAsync).
        var signature = await _reviewGate.SignAndLogAsync(
            ReviewEntityTypes.SolutionPreparation, id, userId, r.Password,
            r.Approved ? SignatureMeaning.Approved : SignatureMeaning.Rejected,
            ReviewWorkflowEventType.ApprovalDecisionMade, comment, ipAddress,
            r.Approved ? ApprovalDecision.Approve : ApprovalDecision.Reject, prep.SectionId);

        prep.ApprovalStatus = r.Approved ? ApprovalGateStatus.Approved : ApprovalGateStatus.Rejected;
        prep.ReleasedByUserId = userId;
        prep.ReleasedAt = now;
        prep.ReleaseComment = comment;
        prep.ReleaseSignature = signature;

        // Status does not change on release; the history row records the decision.
        prep.StatusHistory.Add(new SolutionPreparationStatusHistory
        {
            FromStatus = SolutionPreparationStatus.Prepared,
            ToStatus = SolutionPreparationStatus.Prepared,
            ChangedByUserId = userId,
            ChangedAt = now,
            Reason = (r.Approved ? "Released" : "Release rejected") + (comment is null ? string.Empty : ": " + comment),
        });

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();
    }
}
