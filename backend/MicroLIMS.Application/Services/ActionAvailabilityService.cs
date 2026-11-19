using MicroLIMS.Shared.Messages;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services;

// One gated action on a record: whether the caller could perform it now and, when not, the sentence the server
// would refuse it with.
public sealed record ActionAvailability(
    string Action,
    bool Allowed,
    string? Reason,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? ReasonCode = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string>? ReasonParams = null);

public static class GatedActions
{
    public const string ReviewComplete = "review.complete";
    public const string ReviewReturn = "review.return";
    public const string ApprovalApprove = "approval.approve";
    public const string ApprovalReject = "approval.reject";
    public const string SampleVoid = "sample.void";
    public const string SampleAssign = "sample.assign";
    public const string TestTake = "test.take";
    public const string TestRelease = "test.release";
    public const string MediaRelease = "media.release";
    public const string DocumentApprove = "document.approve";
}

// Why a button is or is not available, from the server's own guards. For each action the checks run in the order a
// request meets them - the endpoint's permission policy, the endpoint's section-scope guard, then the service's
// Check... method, which is the very method the action calls before it signs - and the first refusal is the
// reason. Read-only: nothing is saved, audited, notified or signalled.
//
// Every method answers null when the record does not exist or the caller may not read it (the read rule of the
// record's own GET endpoint), so a reason never describes a record the caller cannot open.
//
// The permission policies read the caller's token; here the role's permissions are read from the database, so
// a role changed since sign-in shows its new permissions at once.
public sealed class ActionAvailabilityService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly SampleReviewService _review;
    private readonly SampleApprovalService _approval;
    private readonly SampleAssignmentService _assignment;
    private readonly TestSelfAssignmentService _selfAssignment;
    private readonly MediaReleaseService _mediaRelease;
    private readonly IDocumentApprovalService _documentApproval;
    private readonly IDocumentAuthorizationService _documents;

    public ActionAvailabilityService(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        SampleReviewService review,
        SampleApprovalService approval,
        SampleAssignmentService assignment,
        TestSelfAssignmentService selfAssignment,
        MediaReleaseService mediaRelease,
        IDocumentApprovalService documentApproval,
        IDocumentAuthorizationService documents)
    {
        _db = db;
        _scope = scope;
        _review = review;
        _approval = approval;
        _assignment = assignment;
        _selfAssignment = selfAssignment;
        _mediaRelease = mediaRelease;
        _documentApproval = documentApproval;
        _documents = documents;
    }

    // review.complete, approval.approve, approval.reject, sample.void, sample.assign.
    // sectionId: the laboratory section a review or decision is for, as the two endpoints take it.
    public async Task<IReadOnlyList<ActionAvailability>?> ForSampleAsync(int sampleId, int? sectionId, int userId, CancellationToken ct = default)
    {
        var sample = await _db.Samples.AsNoTracking().Include(s => s.TestOrders).Include(s => s.SectionSignoffs)
            .FirstOrDefaultAsync(s => s.Id == sampleId, ct);
        if (sample is null) return null;
        if (!await MayAsync(() => _scope.EnsureSampleAccessAsync(userId, sampleId, ct, SectionAccess.Read))) return null;

        var permissions = await PermissionsAsync(userId, ct);
        // The guard SampleController runs before a void or an assignment.
        var memberOfSample = await ActionCheck.FromGuardAsync(() => _scope.EnsureSampleAccessAsync(userId, sampleId, ct));

        return new[]
        {
            await FirstRefusalAsync(GatedActions.ReviewComplete,
                () => Holds(permissions, PermissionConstants.SamplesReview),
                async () => (await _review.CheckCompleteReviewAsync(sample, userId, sectionId, ct)).Check),
            await FirstRefusalAsync(GatedActions.ApprovalApprove,
                () => Holds(permissions, PermissionConstants.SamplesApprove),
                async () => (await _approval.CheckDecideAsync(sample, userId, ApprovalDecision.Approve, sectionId, ct)).Check),
            await FirstRefusalAsync(GatedActions.ApprovalReject,
                () => Holds(permissions, PermissionConstants.SamplesApprove),
                async () => (await _approval.CheckDecideAsync(sample, userId, ApprovalDecision.Reject, sectionId, ct)).Check),
            await FirstRefusalAsync(GatedActions.SampleVoid,
                () => Holds(permissions, PermissionConstants.SamplesCorrect),
                () => Task.FromResult(memberOfSample),
                () => Task.FromResult(SampleCorrectionService.CheckVoid(sample))),
            await FirstRefusalAsync(GatedActions.SampleAssign,
                () => Holds(permissions, PermissionConstants.SamplesAssignAnalyst),
                () => Task.FromResult(memberOfSample),
                () => _assignment.CheckAssignAsync(sample, userId, ct)),
        };
    }

    // review.return, test.take, test.release.
    public async Task<IReadOnlyList<ActionAvailability>?> ForTestOrderAsync(int testOrderId, int userId, CancellationToken ct = default)
    {
        if (!await _db.TestOrders.AsNoTracking().AnyAsync(t => t.Id == testOrderId, ct)) return null;
        if (!await MayAsync(() => _scope.EnsureTestOrderAccessAsync(userId, testOrderId, ct, SectionAccess.Read))) return null;

        var permissions = await PermissionsAsync(userId, ct);

        return new[]
        {
            await FirstRefusalAsync(GatedActions.ReviewReturn,
                () => Holds(permissions, PermissionConstants.SamplesReview),
                // The guard SampleReviewController runs before a return.
                () => ActionCheck.FromGuardAsync(() => _scope.EnsureTestOrderAccessAsync(userId, testOrderId, ct)),
                () => _review.CheckReturnToAnalystAsync(testOrderId, userId, ct)),
            // Take and release name their own permission refusal, so no generic sentence is needed.
            await FirstRefusalAsync(GatedActions.TestTake, () => _selfAssignment.CheckTakeAsync(testOrderId, userId, ct)),
            await FirstRefusalAsync(GatedActions.TestRelease, () => _selfAssignment.CheckReleaseAsync(testOrderId, userId, ct)),
        };
    }

    // media.release (the decision to release the lot; rejecting it is not listed).
    public async Task<IReadOnlyList<ActionAvailability>?> ForMediaAsync(int mediaId, int userId, CancellationToken ct = default)
    {
        if (!await _db.Media.AsNoTracking().AnyAsync(m => m.Id == mediaId, ct)) return null;
        if (!await MayAsync(() => _scope.EnsureMediaAccessAsync(userId, mediaId, ct, SectionAccess.Read))) return null;

        var permissions = await PermissionsAsync(userId, ct);

        return new[]
        {
            await FirstRefusalAsync(GatedActions.MediaRelease,
                // MediaController asks for Media.Prepare on every endpoint and Media.Release on this one.
                () => Holds(permissions, PermissionConstants.MediaPrepare),
                () => Holds(permissions, PermissionConstants.MediaRelease),
                () => ActionCheck.FromGuardAsync(() => _scope.EnsureMediaAccessAsync(userId, mediaId, ct)),
                () => _mediaRelease.CheckDecideAsync(mediaId, userId, approved: true, ct)),
        };
    }

    // document.approve, through the revision's pending approval task (else its latest). A revision that was
    // never sent for approval has no decision to make, and the list is empty.
    public async Task<IReadOnlyList<ActionAvailability>?> ForDocumentRevisionAsync(int revisionId, int userId, CancellationToken ct = default)
    {
        var masterId = await _db.DocumentRevisions.AsNoTracking()
            .Where(r => r.Id == revisionId)
            .Select(r => (int?)r.DocumentMasterId)
            .FirstOrDefaultAsync(ct);
        if (masterId is null) return null;
        if (!await _documents.CanViewDocumentAsync(masterId.Value, userId)) return null;

        var taskId = await _documentApproval.FindDecisionTaskIdAsync(revisionId, ct);
        if (taskId is null) return Array.Empty<ActionAvailability>();

        return new[]
        {
            await FirstRefusalAsync(GatedActions.DocumentApprove, () => _documentApproval.CheckApproveAsync(taskId.Value, userId, ct)),
        };
    }

    private static async Task<ActionAvailability> FirstRefusalAsync(string action, params Func<Task<ActionCheck>>[] checks)
    {
        foreach (var check in checks)
        {
            var result = await check();
            if (!result.Allowed) return new ActionAvailability(action, false, result.Reason, result.Code, result.Params);
        }

        return new ActionAvailability(action, true, null);
    }

    private static async Task<bool> MayAsync(Func<Task> readGuard) => (await ActionCheck.FromGuardAsync(readGuard)).Allowed;

    // The endpoint's permission policy answers 403 with no sentence, so this refusal has none of its own.
    private static Task<ActionCheck> Holds(IReadOnlySet<string> permissions, string code) =>
        Task.FromResult(permissions.Contains(code) ? ActionCheck.Ok : ActionCheck.Denied(MessageCodes.AuthRoleDoesNotIncludeAction, ActionCheck.RoleDoesNotIncludeAction));

    private async Task<IReadOnlySet<string>> PermissionsAsync(int userId, CancellationToken ct) =>
        (await (
            from u in _db.Users.AsNoTracking()
            join rp in _db.RolePermissions.AsNoTracking() on u.RoleId equals rp.RoleId
            join p in _db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
            where u.Id == userId && u.IsActive
            select p.Code).ToListAsync(ct)).ToHashSet();
}
