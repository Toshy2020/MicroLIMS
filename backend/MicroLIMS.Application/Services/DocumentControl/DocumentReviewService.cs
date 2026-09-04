using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services.DocumentControl;

public class DocumentReviewService : IDocumentReviewService
{
    private readonly MicroLimsDbContext _db;
    private readonly IAuditEventService _audit;
    private readonly IDocumentAuthorizationService _auth;

    public DocumentReviewService(
        MicroLimsDbContext db,
        IAuditEventService audit,
        IDocumentAuthorizationService auth)
    {
        _db = db;
        _audit = audit;
        _auth = auth;
    }

    private async Task<(bool IsActive, RoleType? RoleType)> GetUserRoleAsync(int userId)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null || !user.IsActive)
            return (false, null);

        return (true, user.Role?.Type);
    }

    public async Task<DocumentReviewTaskDto> SubmitForReviewAsync(int revisionId, SubmitForReviewRequest request, int userId)
    {
        var (isActive, role) = await GetUserRoleAsync(userId);
        if (!isActive || role == null)
            throw new UnauthorizedAccessException("User is inactive or not found.");

        var revision = await _db.DocumentRevisions
            .Include(r => r.DocumentMaster)
            .Include(r => r.Files)
            .Include(r => r.ReviewTasks)
            .FirstOrDefaultAsync(r => r.Id == revisionId);

        if (revision == null)
            throw new KeyNotFoundException($"Document revision {revisionId} not found.");

        if (revision.RevisionStatus != DocumentRevisionStatus.Draft)
            throw new InvalidOperationException($"Cannot submit revision for technical review in status {revision.RevisionStatus}. Status must be Draft.");

        // Must have an active controlled PDF attached
        var hasActivePdf = revision.Files.Any(f => f.FileRole == FileRole.ControlledPdf && f.IsActive);
        if (!hasActivePdf)
            throw new InvalidOperationException("Cannot submit revision for review without an active Controlled PDF attached.");

        // WP2 Gate: Multi-category Revision Impact Assessment (DC-URS-062)
        var impact = await _db.RevisionImpactAssessments.FirstOrDefaultAsync(a => a.DocumentRevisionId == revisionId);
        if (revision.RevisionSequence > 1 || impact != null)
        {
            if (impact == null || !impact.IsComplete)
            {
                throw new InvalidOperationException("A multi-category Revision Impact Assessment must be completed before submitting for technical review.");
            }
        }

        // WP2 Gate: Unresolved Originating Findings / Change Items Gate (DC-URS-063, DC-URS-064)
        var unaddressedOriginatingItems = await _db.RevisionChangeItems
            .Where(c => c.DocumentRevisionId == revisionId &&
                        c.OriginatingReviewFindingId.HasValue &&
                        c.Status != "Addressed")
            .ToListAsync();

        if (unaddressedOriginatingItems.Count > 0)
        {
            throw new InvalidOperationException($"Cannot submit revision for review: {unaddressedOriginatingItems.Count} originating review finding(s) remain unaddressed.");
        }

        // Authorization check: User must be Document Owner, assigned Author, or SectionHead/Admin
        var canEdit = await _auth.CanEditDraftMetadataAsync(revision.DocumentMasterId, userId);
        if (!canEdit)
            throw new UnauthorizedAccessException("User is not authorized to submit this document revision for technical review.");

        // HARD SEGREGATION OF DUTIES RULE (DC-URS-078, DC-URS-164, DC-URS-165)
        // Author != Reviewer. SystemAdministrator is NOT exempt.
        if (request.ReviewerUserId == revision.CreatedByUserId)
            throw new UnauthorizedAccessException("Segregation of Duties Violation: The author of a document revision cannot be assigned as its technical reviewer.");

        if (request.ReviewerUserId == userId && userId == revision.CreatedByUserId)
            throw new UnauthorizedAccessException("Segregation of Duties Violation: The author cannot assign themselves as technical reviewer.");

        // Target reviewer must exist and be active
        var (reviewerActive, reviewerRole) = await GetUserRoleAsync(request.ReviewerUserId);
        if (!reviewerActive || reviewerRole == null)
            throw new InvalidOperationException("Assigned reviewer is not an active user in the system.");

        // Check for any existing active review tasks
        var hasActiveTask = revision.ReviewTasks.Any(t => t.Status == ReviewTaskStatus.Pending || t.Status == ReviewTaskStatus.InProgress);
        if (hasActiveTask)
            throw new InvalidOperationException("A technical review task is already active for this revision.");

        var task = new DocumentReviewTask
        {
            DocumentRevisionId = revision.Id,
            AssignedReviewerUserId = request.ReviewerUserId,
            AssignedByUserId = userId,
            AssignedAt = DateTime.UtcNow,
            DueDate = request.DueDate,
            Status = ReviewTaskStatus.Pending,
            SubmissionNotes = request.SubmissionNotes?.Trim()
        };

        revision.RevisionStatus = DocumentRevisionStatus.InReview;
        _db.DocumentReviewTasks.Add(task);

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var changes = new List<AuditFieldChange>
        {
            new("RevisionStatus", DocumentRevisionStatus.Draft.ToString(), DocumentRevisionStatus.InReview.ToString()),
            new("AssignedReviewerUserId", null, request.ReviewerUserId.ToString()),
            new("DueDate", null, request.DueDate?.ToString("O"))
        };

        await _audit.RecordUserEventAsync(
            actionCode: "RevisionSubmittedForReview",
            actionCategory: AuditActionCategory.Review,
            recordType: nameof(DocumentReviewTask),
            documentMasterId: revision.DocumentMasterId,
            documentRevisionId: revision.Id,
            reason: $"Submitted for technical review: {request.SubmissionNotes}",
            changes: changes,
            entityId: task.Id.ToString());

        return await GetReviewTaskByIdAsync(task.Id, userId);
    }

    public async Task<DocumentReviewTaskDto> GetReviewTaskByIdAsync(int reviewTaskId, int userId)
    {
        var task = await _db.DocumentReviewTasks
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.DocumentMaster)
            .Include(t => t.AssignedReviewerUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.DecisionByUser)
            .Include(t => t.Findings)
                .ThenInclude(f => f.CreatedByUser)
            .Include(t => t.Findings)
                .ThenInclude(f => f.AuthorResponseByUser)
            .Include(t => t.Findings)
                .ThenInclude(f => f.ReviewerVerifiedByUser)
            .Include(t => t.Findings)
                .ThenInclude(f => f.ResolvedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == reviewTaskId);

        if (task == null)
            throw new KeyNotFoundException($"Document review task {reviewTaskId} not found.");

        var canView = await _auth.CanViewDocumentAsync(task.DocumentRevision.DocumentMasterId, userId);
        if (!canView)
            throw new UnauthorizedAccessException("User is not authorized to view this document review task.");

        return MapToTaskDto(task);
    }

    public async Task<List<DocumentReviewTaskDto>> GetReviewTasksByRevisionIdAsync(int revisionId, int userId)
    {
        var revision = await _db.DocumentRevisions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == revisionId);
        if (revision == null)
            throw new KeyNotFoundException($"Document revision {revisionId} not found.");

        var canView = await _auth.CanViewDocumentAsync(revision.DocumentMasterId, userId);
        if (!canView)
            throw new UnauthorizedAccessException("User is not authorized to view review tasks for this document.");

        var tasks = await _db.DocumentReviewTasks
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.DocumentMaster)
            .Include(t => t.AssignedReviewerUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.DecisionByUser)
            .Include(t => t.Findings)
                .ThenInclude(f => f.CreatedByUser)
            .Where(t => t.DocumentRevisionId == revisionId)
            .OrderByDescending(t => t.AssignedAt)
            .AsNoTracking()
            .ToListAsync();

        return tasks.Select(MapToTaskDto).ToList();
    }

    public async Task<List<DocumentReviewTaskDto>> GetMyAssignedReviewTasksAsync(int userId)
    {
        var tasks = await _db.DocumentReviewTasks
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.DocumentMaster)
            .Include(t => t.AssignedReviewerUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.DecisionByUser)
            .Include(t => t.Findings)
            .Where(t => t.AssignedReviewerUserId == userId && (t.Status == ReviewTaskStatus.Pending || t.Status == ReviewTaskStatus.InProgress))
            .OrderByDescending(t => t.AssignedAt)
            .AsNoTracking()
            .ToListAsync();

        return tasks.Select(MapToTaskDto).ToList();
    }

    public async Task<DocumentReviewFindingDto> AddReviewFindingAsync(int reviewTaskId, AddReviewFindingRequest request, int userId)
    {
        var task = await _db.DocumentReviewTasks
            .Include(t => t.DocumentRevision)
            .FirstOrDefaultAsync(t => t.Id == reviewTaskId);

        if (task == null)
            throw new KeyNotFoundException($"Document review task {reviewTaskId} not found.");

        if (task.Status != ReviewTaskStatus.Pending && task.Status != ReviewTaskStatus.InProgress)
            throw new InvalidOperationException($"Cannot add review findings to a review task in status {task.Status}.");

        // Hard Segregation of Duties: Author cannot add reviewer findings
        if (userId == task.DocumentRevision.CreatedByUserId)
            throw new UnauthorizedAccessException("Segregation of Duties Violation: The author cannot create reviewer findings.");

        // Must be the assigned reviewer or SectionHead
        var (isActive, role) = await GetUserRoleAsync(userId);
        if (!isActive || role == null)
            throw new UnauthorizedAccessException("User is inactive or not found.");

        if (userId != task.AssignedReviewerUserId && role != RoleType.SectionHead && role != RoleType.SystemAdministrator)
            throw new UnauthorizedAccessException("Only the assigned technical reviewer or document controller may add review findings.");

        var finding = new DocumentReviewFinding
        {
            DocumentReviewTaskId = task.Id,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            PageNumber = request.PageNumber,
            SectionNumber = request.SectionNumber?.Trim(),
            CommentText = request.CommentText.Trim(),
            IsMandatory = request.IsMandatory,
            Status = ReviewFindingStatus.Open
        };

        if (task.Status == ReviewTaskStatus.Pending)
        {
            task.Status = ReviewTaskStatus.InProgress;
        }

        _db.DocumentReviewFindings.Add(finding);
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var changes = new List<AuditFieldChange>
        {
            new("PageNumber", null, finding.PageNumber?.ToString()),
            new("SectionNumber", null, finding.SectionNumber),
            new("IsMandatory", null, finding.IsMandatory.ToString()),
            new("Status", null, finding.Status.ToString())
        };

        await _audit.RecordUserEventAsync(
            actionCode: "ReviewFindingCreated",
            actionCategory: AuditActionCategory.Review,
            recordType: nameof(DocumentReviewFinding),
            documentMasterId: task.DocumentRevision.DocumentMasterId,
            documentRevisionId: task.DocumentRevisionId,
            reason: $"Finding created: {finding.CommentText}",
            changes: changes,
            entityId: finding.Id.ToString());

        return await GetFindingByIdAsync(finding.Id);
    }

    public async Task<DocumentReviewFindingDto> RespondToFindingAsync(int findingId, RespondToFindingRequest request, int userId)
    {
        var finding = await _db.DocumentReviewFindings
            .Include(f => f.DocumentReviewTask)
                .ThenInclude(t => t.DocumentRevision)
            .FirstOrDefaultAsync(f => f.Id == findingId);

        if (finding == null)
            throw new KeyNotFoundException($"Review finding {findingId} not found.");

        if (finding.Status != ReviewFindingStatus.Open)
            throw new InvalidOperationException($"Cannot respond to finding in status {finding.Status}. Status must be Open.");

        // Author, Owner, or assigned Author can respond
        var canEdit = await _auth.CanEditDraftMetadataAsync(finding.DocumentReviewTask.DocumentRevision.DocumentMasterId, userId);
        if (!canEdit)
            throw new UnauthorizedAccessException("User is not authorized to respond to this review finding.");

        var prevStatus = finding.Status.ToString();
        finding.AuthorResponse = request.Response.Trim();
        finding.AuthorResponseAt = DateTime.UtcNow;
        finding.AuthorResponseByUserId = userId;
        finding.Status = ReviewFindingStatus.AuthorResponded;

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var changes = new List<AuditFieldChange>
        {
            new("Status", prevStatus, finding.Status.ToString()),
            new("AuthorResponse", null, finding.AuthorResponse)
        };

        await _audit.RecordUserEventAsync(
            actionCode: "ReviewFindingAuthorResponded",
            actionCategory: AuditActionCategory.Review,
            recordType: nameof(DocumentReviewFinding),
            documentMasterId: finding.DocumentReviewTask.DocumentRevision.DocumentMasterId,
            documentRevisionId: finding.DocumentReviewTask.DocumentRevisionId,
            reason: "Author responded to review finding",
            changes: changes,
            entityId: finding.Id.ToString());

        return await GetFindingByIdAsync(finding.Id);
    }

    public async Task<DocumentReviewFindingDto> VerifyFindingAsync(int findingId, VerifyFindingRequest request, int userId)
    {
        var finding = await _db.DocumentReviewFindings
            .Include(f => f.DocumentReviewTask)
                .ThenInclude(t => t.DocumentRevision)
            .FirstOrDefaultAsync(f => f.Id == findingId);

        if (finding == null)
            throw new KeyNotFoundException($"Review finding {findingId} not found.");

        // Hard Segregation of Duties: Author cannot verify findings
        if (userId == finding.DocumentReviewTask.DocumentRevision.CreatedByUserId)
            throw new UnauthorizedAccessException("Segregation of Duties Violation: The author cannot verify reviewer findings.");

        // Must be the assigned reviewer or SectionHead
        var (isActive, role) = await GetUserRoleAsync(userId);
        if (!isActive || role == null)
            throw new UnauthorizedAccessException("User is inactive or not found.");

        if (userId != finding.DocumentReviewTask.AssignedReviewerUserId && role != RoleType.SectionHead && role != RoleType.SystemAdministrator)
            throw new UnauthorizedAccessException("Only the assigned technical reviewer may verify findings.");

        var prevStatus = finding.Status.ToString();
        finding.ReviewerVerificationNotes = request.VerificationNotes?.Trim();
        finding.ReviewerVerifiedAt = DateTime.UtcNow;
        finding.ReviewerVerifiedByUserId = userId;
        finding.Status = ReviewFindingStatus.ReviewerVerified;

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var changes = new List<AuditFieldChange>
        {
            new("Status", prevStatus, finding.Status.ToString()),
            new("ReviewerVerificationNotes", null, finding.ReviewerVerificationNotes)
        };

        await _audit.RecordUserEventAsync(
            actionCode: "ReviewFindingVerified",
            actionCategory: AuditActionCategory.Review,
            recordType: nameof(DocumentReviewFinding),
            documentMasterId: finding.DocumentReviewTask.DocumentRevision.DocumentMasterId,
            documentRevisionId: finding.DocumentReviewTask.DocumentRevisionId,
            reason: "Reviewer verified finding resolution",
            changes: changes,
            entityId: finding.Id.ToString());

        return await GetFindingByIdAsync(finding.Id);
    }

    public async Task<DocumentReviewFindingDto> ResolveFindingAsync(int findingId, int userId)
    {
        var finding = await _db.DocumentReviewFindings
            .Include(f => f.DocumentReviewTask)
                .ThenInclude(t => t.DocumentRevision)
            .FirstOrDefaultAsync(f => f.Id == findingId);

        if (finding == null)
            throw new KeyNotFoundException($"Review finding {findingId} not found.");

        // Hard Reviewer Concurrence Gate (DC-URS-070):
        // The author shall NOT be able to close a reviewer-required comment.
        if (userId == finding.DocumentReviewTask.DocumentRevision.CreatedByUserId)
            throw new UnauthorizedAccessException("Segregation of Duties Violation: The author cannot close or resolve a reviewer-mandated finding without reviewer verification.");

        // Must be assigned reviewer or SectionHead
        var (isActive, role) = await GetUserRoleAsync(userId);
        if (!isActive || role == null)
            throw new UnauthorizedAccessException("User is inactive or not found.");

        if (userId != finding.DocumentReviewTask.AssignedReviewerUserId && role != RoleType.SectionHead && role != RoleType.SystemAdministrator)
            throw new UnauthorizedAccessException("Only the assigned technical reviewer or document controller may resolve review findings.");

        var prevStatus = finding.Status.ToString();
        finding.ResolvedAt = DateTime.UtcNow;
        finding.ResolvedByUserId = userId;
        finding.Status = ReviewFindingStatus.Resolved;

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var changes = new List<AuditFieldChange>
        {
            new("Status", prevStatus, finding.Status.ToString())
        };

        await _audit.RecordUserEventAsync(
            actionCode: "ReviewFindingResolved",
            actionCategory: AuditActionCategory.Review,
            recordType: nameof(DocumentReviewFinding),
            documentMasterId: finding.DocumentReviewTask.DocumentRevision.DocumentMasterId,
            documentRevisionId: finding.DocumentReviewTask.DocumentRevisionId,
            reason: "Review finding marked as resolved",
            changes: changes,
            entityId: finding.Id.ToString());

        return await GetFindingByIdAsync(finding.Id);
    }

    public async Task<DocumentReviewTaskDto> DecideReviewAsync(int reviewTaskId, ReviewDecisionRequest request, int userId)
    {
        var task = await _db.DocumentReviewTasks
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.DocumentMaster)
            .Include(t => t.Findings)
            .FirstOrDefaultAsync(t => t.Id == reviewTaskId);

        if (task == null)
            throw new KeyNotFoundException($"Document review task {reviewTaskId} not found.");

        if (task.Status != ReviewTaskStatus.Pending && task.Status != ReviewTaskStatus.InProgress)
            throw new InvalidOperationException($"Cannot make a decision on review task in status {task.Status}.");

        // Hard Segregation of Duties: Author cannot decide/complete review of own revision (DC-URS-164, DC-URS-165)
        if (userId == task.DocumentRevision.CreatedByUserId)
            throw new UnauthorizedAccessException("Segregation of Duties Violation: The author cannot perform technical review of their own revision.");

        // Must be assigned reviewer or SectionHead
        var (isActive, role) = await GetUserRoleAsync(userId);
        if (!isActive || role == null)
            throw new UnauthorizedAccessException("User is inactive or not found.");

        if (userId != task.AssignedReviewerUserId && role != RoleType.SectionHead)
            throw new UnauthorizedAccessException("Only the assigned technical reviewer or document controller may complete this technical review.");

        if (request.Decision == ReviewDecision.CompleteReview)
        {
            // MANDATORY FINDINGS GATE (DC-URS-071):
            // Technical Review completion shall be blocked while mandatory review comments remain unresolved.
            var openMandatoryFindings = task.Findings
                .Where(f => f.IsMandatory && f.Status != ReviewFindingStatus.Resolved)
                .ToList();

            if (openMandatoryFindings.Any())
            {
                throw new InvalidOperationException(
                    $"Cannot complete technical review while {openMandatoryFindings.Count} mandatory review finding(s) remain unresolved.");
            }

            var prevStatus = task.Status.ToString();
            var prevRevStatus = task.DocumentRevision.RevisionStatus.ToString();

            task.Status = ReviewTaskStatus.Completed;
            task.Decision = ReviewDecision.CompleteReview;
            task.DecisionAt = DateTime.UtcNow;
            task.DecisionByUserId = userId;
            task.ReviewNotes = request.ReviewNotes?.Trim();

            // Advance revision to AwaitingApproval (DC-URS-073)
            task.DocumentRevision.RevisionStatus = DocumentRevisionStatus.AwaitingApproval;

            _db.CurrentUserId = userId;
            await _db.SaveChangesAsync();

            var changes = new List<AuditFieldChange>
            {
                new("TaskStatus", prevStatus, task.Status.ToString()),
                new("RevisionStatus", prevRevStatus, task.DocumentRevision.RevisionStatus.ToString()),
                new("Decision", null, task.Decision.ToString())
            };

            await _audit.RecordUserEventAsync(
                actionCode: "TechnicalReviewCompleted",
                actionCategory: AuditActionCategory.Review,
                recordType: nameof(DocumentReviewTask),
                documentMasterId: task.DocumentRevision.DocumentMasterId,
                documentRevisionId: task.DocumentRevisionId,
                reason: $"Technical review completed. Notes: {task.ReviewNotes}",
                changes: changes,
                entityId: task.Id.ToString());
        }
        else if (request.Decision == ReviewDecision.ReturnForCorrection)
        {
            var prevStatus = task.Status.ToString();
            var prevRevStatus = task.DocumentRevision.RevisionStatus.ToString();

            task.Status = ReviewTaskStatus.ReturnedForCorrection;
            task.Decision = ReviewDecision.ReturnForCorrection;
            task.DecisionAt = DateTime.UtcNow;
            task.DecisionByUserId = userId;
            task.ReviewNotes = request.ReviewNotes?.Trim();

            // Return revision to Draft status so author can make corrections (DC-URS-072)
            task.DocumentRevision.RevisionStatus = DocumentRevisionStatus.Draft;

            _db.CurrentUserId = userId;
            await _db.SaveChangesAsync();

            var changes = new List<AuditFieldChange>
            {
                new("TaskStatus", prevStatus, task.Status.ToString()),
                new("RevisionStatus", prevRevStatus, task.DocumentRevision.RevisionStatus.ToString()),
                new("Decision", null, task.Decision.ToString())
            };

            await _audit.RecordUserEventAsync(
                actionCode: "TechnicalReviewReturned",
                actionCategory: AuditActionCategory.Review,
                recordType: nameof(DocumentReviewTask),
                documentMasterId: task.DocumentRevision.DocumentMasterId,
                documentRevisionId: task.DocumentRevisionId,
                reason: $"Technical review returned for correction. Notes: {task.ReviewNotes}",
                changes: changes,
                entityId: task.Id.ToString());
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(request.Decision), "Invalid review decision.");
        }

        return await GetReviewTaskByIdAsync(task.Id, userId);
    }

    private async Task<DocumentReviewFindingDto> GetFindingByIdAsync(int findingId)
    {
        var f = await _db.DocumentReviewFindings
            .Include(x => x.CreatedByUser)
            .Include(x => x.AuthorResponseByUser)
            .Include(x => x.ReviewerVerifiedByUser)
            .Include(x => x.ResolvedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == findingId);

        if (f == null)
            throw new KeyNotFoundException($"Finding {findingId} not found.");

        return MapToFindingDto(f);
    }

    private static DocumentReviewTaskDto MapToTaskDto(DocumentReviewTask t)
    {
        return new DocumentReviewTaskDto
        {
            Id = t.Id,
            DocumentMasterId = t.DocumentRevision.DocumentMasterId,
            MicroLimsDocumentId = t.DocumentRevision.DocumentMaster.MicroLimsDocumentId,
            CompanyDocumentCode = t.DocumentRevision.DocumentMaster.CompanyDocumentCode,
            DocumentTitle = t.DocumentRevision.DocumentMaster.Title,
            DocumentRevisionId = t.DocumentRevisionId,
            RevisionNumber = t.DocumentRevision.RevisionNumber,
            RevisionStatus = t.DocumentRevision.RevisionStatus,
            AssignedReviewerUserId = t.AssignedReviewerUserId,
            AssignedReviewerUsername = t.AssignedReviewerUser?.Username ?? string.Empty,
            AssignedReviewerFullName = t.AssignedReviewerUser?.FullName ?? string.Empty,
            AssignedByUserId = t.AssignedByUserId,
            AssignedByUsername = t.AssignedByUser?.Username ?? string.Empty,
            AssignedAt = t.AssignedAt,
            DueDate = t.DueDate,
            Status = t.Status,
            Decision = t.Decision,
            DecisionAt = t.DecisionAt,
            DecisionByUsername = t.DecisionByUser?.Username,
            ReviewNotes = t.ReviewNotes,
            SubmissionNotes = t.SubmissionNotes,
            TotalFindingsCount = t.Findings?.Count ?? 0,
            OpenMandatoryFindingsCount = t.Findings?.Count(f => f.IsMandatory && f.Status != ReviewFindingStatus.Resolved) ?? 0,
            Findings = t.Findings?.Select(MapToFindingDto).ToList() ?? new List<DocumentReviewFindingDto>()
        };
    }

    private static DocumentReviewFindingDto MapToFindingDto(DocumentReviewFinding f)
    {
        return new DocumentReviewFindingDto
        {
            Id = f.Id,
            DocumentReviewTaskId = f.DocumentReviewTaskId,
            CreatedByUserId = f.CreatedByUserId,
            CreatedByUsername = f.CreatedByUser?.Username ?? string.Empty,
            CreatedByFullName = f.CreatedByUser?.FullName ?? string.Empty,
            CreatedAt = f.CreatedAt,
            PageNumber = f.PageNumber,
            SectionNumber = f.SectionNumber,
            CommentText = f.CommentText,
            IsMandatory = f.IsMandatory,
            Status = f.Status,
            AuthorResponse = f.AuthorResponse,
            AuthorResponseAt = f.AuthorResponseAt,
            AuthorResponseUsername = f.AuthorResponseByUser?.Username,
            ReviewerVerificationNotes = f.ReviewerVerificationNotes,
            ReviewerVerifiedAt = f.ReviewerVerifiedAt,
            ReviewerVerifiedUsername = f.ReviewerVerifiedByUser?.Username,
            ResolvedAt = f.ResolvedAt,
            ResolvedByUsername = f.ResolvedByUser?.Username
        };
    }
}
