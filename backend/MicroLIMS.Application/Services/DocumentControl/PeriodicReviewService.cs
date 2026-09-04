using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services.DocumentControl;

public class PeriodicReviewService : IPeriodicReviewService
{
    private readonly MicroLimsDbContext _db;
    private readonly IAuditEventService _audit;
    private readonly IDocumentAuthorizationService _auth;
    private readonly IDocumentApprovalService _approvalService;
    private readonly ILogger<PeriodicReviewService> _logger;

    public PeriodicReviewService(
        MicroLimsDbContext db,
        IAuditEventService audit,
        IDocumentAuthorizationService auth,
        IDocumentApprovalService approvalService,
        ILogger<PeriodicReviewService> logger)
    {
        _db = db;
        _audit = audit;
        _auth = auth;
        _approvalService = approvalService;
        _logger = logger;
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

    public async Task<PeriodicReviewGenerationResultDto> GenerateDueReviewTasksAsync(DateTime? utcNowOverride = null, CancellationToken cancellationToken = default)
    {
        var now = utcNowOverride ?? DateTime.UtcNow;
        var createdTaskIds = new List<int>();
        var errors = new List<string>();
        int evaluatedCount = 0;
        int skippedCount = 0;

        _logger.LogInformation("[PeriodicReview] Starting review task evaluation at {TimestampUtc} UTC.", now);

        // Fetch active document masters with current effective revision
        var masters = await _db.DocumentMasters
            .Include(m => m.CurrentEffectiveRevision)
            .Include(m => m.Assignments)
            .Where(m => m.RecordStatus == DocumentRecordStatus.Active &&
                        m.CurrentEffectiveRevisionId != null &&
                        m.CurrentEffectiveRevision != null &&
                        m.CurrentEffectiveRevision.RevisionStatus == DocumentRevisionStatus.Effective)
            .ToListAsync(cancellationToken);

        evaluatedCount = masters.Count;

        foreach (var master in masters)
        {
            var rev = master.CurrentEffectiveRevision!;

            // Check if NextReviewDate is reached or due (NextReviewDate <= now)
            // If NextReviewDate is null, calculate from EffectiveDate + ReviewCycleMonths
            var dueDate = rev.NextReviewDate ?? (rev.EffectiveDate.HasValue
                ? rev.EffectiveDate.Value.AddMonths(rev.ReviewCycleMonths ?? 24)
                : now);

            if (dueDate > now)
            {
                skippedCount++;
                continue;
            }

            // IDEMPOTENCY / UNIQUE ACTIVE TASK PER REVISION CHECK:
            // Check if an active review task already exists for this revision (Pending or InProgress)
            var hasActiveTask = await _db.PeriodicReviewTasks
                .AnyAsync(t => t.DocumentRevisionId == rev.Id &&
                               (t.Status == PeriodicReviewTaskStatus.Pending || t.Status == PeriodicReviewTaskStatus.InProgress),
                               cancellationToken);

            if (hasActiveTask)
            {
                skippedCount++;
                continue;
            }

            // Per master isolation: wrap in atomic transaction
            var isRelational = _db.Database.IsRelational();
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
            if (isRelational)
            {
                tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            }

            try
            {
                // Determine default assigned reviewer: DocumentOwner or designated TechnicalReviewer assignment
                int? assignedUserId = master.DocumentOwnerUserId;
                var reviewerAssignment = master.Assignments.FirstOrDefault(a => a.AssignmentRole == AssignmentRole.TechnicalReviewer && a.IsActive);
                if (reviewerAssignment != null)
                {
                    assignedUserId = reviewerAssignment.UserId;
                }

                // If assigned reviewer is the author of this revision, clear to avoid SoD violation
                if (assignedUserId == rev.CreatedByUserId)
                {
                    assignedUserId = null;
                }

                var task = new PeriodicReviewTask
                {
                    DocumentMasterId = master.Id,
                    DocumentRevisionId = rev.Id,
                    ReviewCycleMonths = rev.ReviewCycleMonths ?? 24,
                    ScheduledDueDate = dueDate,
                    AssignedReviewerUserId = assignedUserId,
                    Status = PeriodicReviewTaskStatus.Pending,
                    CreatedAt = now
                };

                _db.PeriodicReviewTasks.Add(task);
                await _db.SaveChangesAsync(cancellationToken);

                // System-attributed audit trail logging
                await _audit.RecordSystemEventAsync(
                    systemProcessName: "DocumentEffectiveDateWorker",
                    actionCode: "PeriodicReviewTaskCreated",
                    actionCategory: AuditActionCategory.Review,
                    recordType: nameof(PeriodicReviewTask),
                    documentMasterId: master.Id,
                    documentRevisionId: rev.Id,
                    reason: $"Automated Periodic Review task created for revision {rev.RevisionNumber} due on {dueDate:yyyy-MM-dd} UTC",
                    entityId: task.Id.ToString(),
                    changes: new[]
                    {
                        new AuditFieldChange("Status", null, PeriodicReviewTaskStatus.Pending.ToString()),
                        new AuditFieldChange("ScheduledDueDate", null, dueDate.ToString("O")),
                        new AuditFieldChange("AssignedReviewerUserId", null, assignedUserId?.ToString())
                    }
                );

                if (tx != null)
                {
                    await tx.CommitAsync(cancellationToken);
                }

                createdTaskIds.Add(task.Id);
                _logger.LogInformation("[PeriodicReview] Created task #{TaskId} for master '{Code}' (Rev {Rev})",
                    task.Id, master.CompanyDocumentCode, rev.RevisionNumber);
            }
            catch (Exception ex)
            {
                if (tx != null)
                {
                    await tx.RollbackAsync(cancellationToken);
                }

                var msg = $"Error generating review task for Master #{master.Id} ({master.CompanyDocumentCode}): {ex.Message}";
                _logger.LogError(ex, "[PeriodicReview] {ErrorMessage}", msg);
                errors.Add(msg);

                await _audit.RecordSystemEventAsync(
                    systemProcessName: "DocumentEffectiveDateWorker",
                    actionCode: "SystemProcessError",
                    actionCategory: AuditActionCategory.Other,
                    recordType: nameof(DocumentMaster),
                    documentMasterId: master.Id,
                    documentRevisionId: rev.Id,
                    reason: $"Failure during periodic review task creation: {ex.Message}",
                    entityId: master.Id.ToString()
                );
            }
            finally
            {
                if (tx != null)
                {
                    await tx.DisposeAsync();
                }
            }
        }

        return new PeriodicReviewGenerationResultDto(
            evaluatedCount,
            createdTaskIds.Count,
            skippedCount,
            createdTaskIds,
            errors
        );
    }

    public async Task<List<PeriodicReviewTaskDto>> GetReviewTasksAsync(int? masterId = null, int? revisionId = null, PeriodicReviewTaskStatus? status = null, int? reviewerUserId = null)
    {
        var query = _db.PeriodicReviewTasks
            .Include(t => t.DocumentMaster)
            .Include(t => t.DocumentRevision)
            .Include(t => t.AssignedReviewerUser)
            .Include(t => t.CompletedByUser)
            .Include(t => t.Findings).ThenInclude(f => f.CreatedByUser)
            .AsNoTracking()
            .AsQueryable();

        if (masterId.HasValue)
            query = query.Where(t => t.DocumentMasterId == masterId.Value);

        if (revisionId.HasValue)
            query = query.Where(t => t.DocumentRevisionId == revisionId.Value);

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (reviewerUserId.HasValue)
            query = query.Where(t => t.AssignedReviewerUserId == reviewerUserId.Value);

        var list = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return list.Select(MapToTaskDto).ToList();
    }

    public async Task<PeriodicReviewTaskDto> GetReviewTaskByIdAsync(int taskId, int userId)
    {
        var task = await _db.PeriodicReviewTasks
            .Include(t => t.DocumentMaster)
            .Include(t => t.DocumentRevision)
            .Include(t => t.AssignedReviewerUser)
            .Include(t => t.CompletedByUser)
            .Include(t => t.Findings).ThenInclude(f => f.CreatedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null)
            throw new KeyNotFoundException($"Periodic review task {taskId} not found.");

        return MapToTaskDto(task);
    }

    public async Task<PeriodicReviewWorkspaceDto> GetReviewWorkspaceAsync(int taskId, int userId)
    {
        var task = await _db.PeriodicReviewTasks
            .Include(t => t.DocumentMaster).ThenInclude(m => m.DocumentType)
            .Include(t => t.DocumentMaster).ThenInclude(m => m.Department)
            .Include(t => t.DocumentMaster).ThenInclude(m => m.Section)
            .Include(t => t.DocumentMaster).ThenInclude(m => m.DocumentOwnerUser)
            .Include(t => t.DocumentRevision).ThenInclude(r => r.CreatedByUser)
            .Include(t => t.DocumentRevision).ThenInclude(r => r.Files).ThenInclude(f => f.UploadedByUser)
            .Include(t => t.AssignedReviewerUser)
            .Include(t => t.CompletedByUser)
            .Include(t => t.Findings).ThenInclude(f => f.CreatedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null)
            throw new KeyNotFoundException($"Periodic review task {taskId} not found.");

        var rev = task.DocumentRevision;
        var master = task.DocumentMaster;

        var pdfFile = rev.Files.FirstOrDefault(f => f.FileRole == FileRole.ControlledPdf && f.IsActive);
        RevisionFileDto? pdfDto = null;
        if (pdfFile != null)
        {
            pdfDto = new RevisionFileDto(
                pdfFile.Id,
                pdfFile.DocumentRevisionId,
                pdfFile.FileRole,
                pdfFile.FileName,
                pdfFile.ContentType,
                pdfFile.SizeBytes,
                pdfFile.ContentSha256,
                pdfFile.IsActive,
                pdfFile.SupersededByFileId,
                pdfFile.UploadedAt,
                pdfFile.UploadedByUserId,
                pdfFile.UploadedByUser?.FullName ?? ""
            );
        }

        var masterSummary = new DocumentMasterSummaryDto(
            master.Id,
            master.MicroLimsDocumentId,
            master.CompanyDocumentCode,
            master.Title,
            master.DocumentTypeId,
            master.DocumentType?.Name ?? "",
            master.DocumentType?.Code ?? "",
            master.DepartmentId,
            master.Department?.Name ?? "",
            master.SectionId,
            master.Section?.Name ?? "",
            master.DocumentOwnerUserId,
            master.DocumentOwnerUser?.FullName ?? "",
            master.Confidentiality,
            master.Category,
            master.RecordOrigin,
            master.RecordStatus,
            master.CurrentEffectiveRevisionId,
            rev.RevisionNumber,
            rev.EffectiveDate,
            rev.NextReviewDate,
            pdfFile != null,
            rev.Files.Any(f => f.FileRole == FileRole.SourceFile && f.IsActive),
            master.CreatedAt
        );

        var revDto = new DocumentRevisionDto(
            rev.Id,
            rev.DocumentMasterId,
            rev.RevisionNumber,
            rev.RevisionSequence,
            rev.RevisionStatus,
            rev.EffectiveDate,
            rev.NextReviewDate,
            rev.ReviewCycleMonths,
            rev.RevisionType,
            rev.ReasonForRevision,
            rev.ChangeReference,
            rev.RecordOrigin,
            rev.CreatedAt,
            rev.CreatedByUserId,
            rev.CreatedByUser?.FullName ?? "",
            rev.CancelledAt,
            rev.CancelledByUserId,
            null,
            rev.CancelReason,
            rev.Files.Select(f => new RevisionFileDto(
                f.Id, f.DocumentRevisionId, f.FileRole, f.FileName, f.ContentType, f.SizeBytes, f.ContentSha256,
                f.IsActive, f.SupersededByFileId, f.UploadedAt, f.UploadedByUserId, f.UploadedByUser?.FullName ?? ""
            )).ToList()
        );

        // Fetch independent review history across all tasks for this master
        var historicalTasks = await _db.PeriodicReviewTasks
            .Include(t => t.DocumentMaster)
            .Include(t => t.DocumentRevision)
            .Include(t => t.AssignedReviewerUser)
            .Include(t => t.CompletedByUser)
            .Include(t => t.Findings).ThenInclude(f => f.CreatedByUser)
            .Where(t => t.DocumentMasterId == master.Id && t.Id != task.Id)
            .OrderByDescending(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return new PeriodicReviewWorkspaceDto(
            MapToTaskDto(task),
            masterSummary,
            revDto,
            pdfDto,
            task.Findings.Select(MapToFindingDto).ToList(),
            historicalTasks.Select(MapToTaskDto).ToList()
        );
    }

    public async Task<PeriodicReviewFindingDto> AddFindingAsync(int taskId, CreatePeriodicReviewFindingRequest request, int userId)
    {
        var task = await _db.PeriodicReviewTasks
            .Include(t => t.DocumentRevision)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null)
            throw new KeyNotFoundException($"Periodic review task {taskId} not found.");

        if (task.Status == PeriodicReviewTaskStatus.Completed || task.Status == PeriodicReviewTaskStatus.Cancelled)
            throw new InvalidOperationException("Cannot add findings to a completed or cancelled review task.");

        // Hard Segregation of Duties check: Author cannot be Reviewer!
        if (task.DocumentRevision.CreatedByUserId == userId)
            throw new UnauthorizedAccessException("Segregation of Duties Violation: The author of the revision under review cannot add review notes or perform the review.");

        if (string.IsNullOrWhiteSpace(request.NoteText) || request.NoteText.Trim().Length < 5)
            throw new ArgumentException("Review note text must be at least 5 characters.", nameof(request.NoteText));

        var finding = new PeriodicReviewFinding
        {
            PeriodicReviewTaskId = taskId,
            PageNumber = request.PageNumber,
            SectionNumber = request.SectionNumber?.Trim(),
            NoteText = request.NoteText.Trim(),
            Status = PeriodicReviewFindingStatus.Open,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        // Transition task status to InProgress if currently Pending
        if (task.Status == PeriodicReviewTaskStatus.Pending)
        {
            task.Status = PeriodicReviewTaskStatus.InProgress;
        }

        _db.PeriodicReviewFindings.Add(finding);
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(userId);

        await _audit.RecordUserEventAsync(
            actionCode: "PeriodicReviewNoteCreated",
            actionCategory: AuditActionCategory.Review,
            recordType: nameof(PeriodicReviewFinding),
            documentMasterId: task.DocumentMasterId,
            documentRevisionId: task.DocumentRevisionId,
            reason: $"Added periodic review note (Page: {request.PageNumber}, Section: {request.SectionNumber})",
            entityId: finding.Id.ToString()
        );

        return new PeriodicReviewFindingDto(
            finding.Id,
            finding.PeriodicReviewTaskId,
            finding.PageNumber,
            finding.SectionNumber,
            finding.NoteText,
            finding.Status,
            finding.CreatedByUserId,
            user?.Username ?? "",
            user?.FullName ?? "",
            finding.CreatedAt
        );
    }

    public async Task<PeriodicReviewFindingDto> ResolveFindingAsync(int taskId, int findingId, int userId)
    {
        var finding = await _db.PeriodicReviewFindings
            .Include(f => f.PeriodicReviewTask).ThenInclude(t => t.DocumentRevision)
            .Include(f => f.CreatedByUser)
            .FirstOrDefaultAsync(f => f.Id == findingId && f.PeriodicReviewTaskId == taskId);

        if (finding == null)
            throw new KeyNotFoundException($"Finding {findingId} not found on task {taskId}.");

        if (finding.PeriodicReviewTask.Status == PeriodicReviewTaskStatus.Completed)
            throw new InvalidOperationException("Cannot modify findings on a completed review task.");

        // Author cannot resolve/alter reviewer findings directly
        if (finding.PeriodicReviewTask.DocumentRevision.CreatedByUserId == userId)
            throw new UnauthorizedAccessException("The author cannot alter reviewer findings.");

        finding.Status = PeriodicReviewFindingStatus.Resolved;
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        await _audit.RecordUserEventAsync(
            actionCode: "PeriodicReviewFindingResolved",
            actionCategory: AuditActionCategory.Review,
            recordType: nameof(PeriodicReviewFinding),
            documentMasterId: finding.PeriodicReviewTask.DocumentMasterId,
            documentRevisionId: finding.PeriodicReviewTask.DocumentRevisionId,
            reason: $"Resolved periodic review finding #{finding.Id}",
            entityId: finding.Id.ToString()
        );

        return MapToFindingDto(finding);
    }

    public async Task<PeriodicReviewTaskDto> AssignReviewerAsync(int taskId, AssignPeriodicReviewerRequest request, int userId)
    {
        var task = await _db.PeriodicReviewTasks
            .Include(t => t.DocumentRevision)
            .Include(t => t.DocumentMaster)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null)
            throw new KeyNotFoundException($"Periodic review task {taskId} not found.");

        if (task.Status == PeriodicReviewTaskStatus.Completed || task.Status == PeriodicReviewTaskStatus.Cancelled)
            throw new InvalidOperationException("Cannot reassign a completed or cancelled review task.");

        // Check target reviewer exists and is active
        var (targetActive, targetRole) = await GetUserRoleAsync(request.ReviewerUserId);
        if (!targetActive || targetRole == null)
            throw new InvalidOperationException("Target reviewer is not an active user in the system.");

        // HARD SEGREGATION OF DUTIES RULE (DC-URS-078, DC-URS-164..169)
        // Author != Reviewer. SystemAdministrator is NOT exempt!
        if (request.ReviewerUserId == task.DocumentRevision.CreatedByUserId)
            throw new UnauthorizedAccessException("Segregation of Duties Violation: The author of the document revision cannot be assigned as its periodic reviewer.");

        var previousAssignee = task.AssignedReviewerUserId;
        task.AssignedReviewerUserId = request.ReviewerUserId;
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var targetUser = await _db.Users.FindAsync(request.ReviewerUserId);

        await _audit.RecordUserEventAsync(
            actionCode: "PeriodicReviewAssigned",
            actionCategory: AuditActionCategory.Review,
            recordType: nameof(PeriodicReviewTask),
            documentMasterId: task.DocumentMasterId,
            documentRevisionId: task.DocumentRevisionId,
            reason: $"Assigned periodic reviewer to {targetUser?.FullName}",
            entityId: task.Id.ToString(),
            changes: new[]
            {
                new AuditFieldChange("AssignedReviewerUserId", previousAssignee?.ToString(), request.ReviewerUserId.ToString())
            }
        );

        return await GetReviewTaskByIdAsync(taskId, userId);
    }

    public async Task<PeriodicReviewTaskDto> CompleteReviewAsync(int taskId, CompletePeriodicReviewRequest request, int userId)
    {
        var task = await _db.PeriodicReviewTasks
            .Include(t => t.DocumentMaster)
            .Include(t => t.DocumentRevision)
            .Include(t => t.Findings)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null)
            throw new KeyNotFoundException($"Periodic review task {taskId} not found.");

        if (task.Status == PeriodicReviewTaskStatus.Completed)
            throw new InvalidOperationException("This periodic review task is already completed and cannot be altered.");

        if (task.Status == PeriodicReviewTaskStatus.Cancelled)
            throw new InvalidOperationException("Cannot complete a cancelled periodic review task.");

        // Validate user role
        var (isActorActive, actorRole) = await GetUserRoleAsync(userId);
        if (!isActorActive || actorRole == null)
            throw new UnauthorizedAccessException("User is inactive or not found.");

        // HARD SEGREGATION OF DUTIES: Author != Reviewer (System Administrator is NOT exempt)
        if (task.DocumentRevision.CreatedByUserId == userId)
            throw new UnauthorizedAccessException("Segregation of Duties Violation: The author cannot perform or complete the periodic review.");

        // Verify assignment: Must be assigned reviewer or SectionHead (Document Controller)
        if (task.AssignedReviewerUserId.HasValue && task.AssignedReviewerUserId.Value != userId)
        {
            if (actorRole != RoleType.SectionHead && actorRole != RoleType.SystemAdministrator)
            {
                throw new UnauthorizedAccessException("Only the assigned reviewer or Document Controller can complete this review.");
            }
        }

        if (string.IsNullOrWhiteSpace(request.ReviewSummary) || request.ReviewSummary.Trim().Length < 10)
            throw new ArgumentException("A comprehensive review summary of at least 10 characters is mandatory.", nameof(request.ReviewSummary));

        var now = DateTime.UtcNow;
        var rev = task.DocumentRevision;
        var master = task.DocumentMaster;
        var cycleMonths = task.ReviewCycleMonths > 0 ? task.ReviewCycleMonths : (rev.ReviewCycleMonths ?? 24);

        task.Outcome = request.Outcome;
        task.ReviewSummary = request.ReviewSummary.Trim();
        task.CompletedAt = now;
        task.CompletedByUserId = userId;
        task.Status = PeriodicReviewTaskStatus.Completed;

        var auditAction = "PeriodicReviewCompleted";

        if (request.Outcome == PeriodicReviewOutcome.RemainsValid)
        {
            // DC-URS-048 & DC-URS-049:
            // 1. Closes task
            // 2. Retains review evidence
            // 3. Creates NO new revision
            // 4. Preserves current effective revision
            // 5. Advances NextReviewDate according to review cycle
            auditAction = "PeriodicReviewRemainsValid";

            var previousNextReview = rev.NextReviewDate;
            var newNextReviewDate = now.AddMonths(cycleMonths);
            rev.NextReviewDate = newNextReviewDate;

            _db.CurrentUserId = userId;
            await _db.SaveChangesAsync();

            await _audit.RecordUserEventAsync(
                actionCode: auditAction,
                actionCategory: AuditActionCategory.Review,
                recordType: nameof(PeriodicReviewTask),
                documentMasterId: master.Id,
                documentRevisionId: rev.Id,
                reason: $"Periodic review completed: Document remains valid. Next review date advanced to {newNextReviewDate:yyyy-MM-dd} UTC. Summary: {task.ReviewSummary}",
                entityId: task.Id.ToString(),
                changes: new[]
                {
                    new AuditFieldChange("Outcome", null, PeriodicReviewOutcome.RemainsValid.ToString()),
                    new AuditFieldChange("NextReviewDate", previousNextReview?.ToString("O"), newNextReviewDate.ToString("O"))
                }
            );
        }
        else if (request.Outcome == PeriodicReviewOutcome.RevisionRequired)
        {
            // DC-URS-050 & DC-URS-051:
            // 1. Closes task
            // 2. Preserves outcome & findings
            // 3. Enables revision creation referencing OriginatingPeriodicReviewTaskId
            auditAction = "PeriodicReviewRevisionRequired";

            _db.CurrentUserId = userId;
            await _db.SaveChangesAsync();

            await _audit.RecordUserEventAsync(
                actionCode: auditAction,
                actionCategory: AuditActionCategory.Review,
                recordType: nameof(PeriodicReviewTask),
                documentMasterId: master.Id,
                documentRevisionId: rev.Id,
                reason: $"Periodic review completed: Revision required. Findings preserved for transfer to next revision. Summary: {task.ReviewSummary}",
                entityId: task.Id.ToString(),
                changes: new[]
                {
                    new AuditFieldChange("Outcome", null, PeriodicReviewOutcome.RevisionRequired.ToString())
                }
            );
        }
        else if (request.Outcome == PeriodicReviewOutcome.ObsolescenceRecommended)
        {
            // DC-URS-052:
            // 1. Records recommendation
            // 2. Retains review evidence
            // 3. Does NOT directly mark document obsolete
            // 4. Routes recommendation to formal approval workflow
            auditAction = "PeriodicReviewObsolescenceRecommended";

            _db.CurrentUserId = userId;
            await _db.SaveChangesAsync();

            // Create obsolescence approval task if not already in flight
            var approverAssignment = await _db.DocumentMasterAssignments
                .FirstOrDefaultAsync(a => a.DocumentMasterId == master.Id && a.AssignmentRole == AssignmentRole.Approver && a.IsActive);

            int approverUserId = approverAssignment?.UserId ?? master.DocumentOwnerUserId;
            // Ensure approver is not the reviewer or author
            if (approverUserId == userId || approverUserId == rev.CreatedByUserId)
            {
                var adminOrController = await _db.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.IsActive && u.Role != null && u.Role.Type == RoleType.SectionHead && u.Id != userId && u.Id != rev.CreatedByUserId);
                if (adminOrController != null)
                {
                    approverUserId = adminOrController.Id;
                }
            }

            var approvalTask = new DocumentApprovalTask
            {
                DocumentRevisionId = rev.Id,
                AssignedApproverUserId = approverUserId,
                AssignedByUserId = userId,
                AssignedAt = now,
                DueDate = now.AddDays(14),
                Status = DocumentApprovalTaskStatus.Pending,
                SubmissionNotes = $"Obsolescence recommended from Periodic Review #{task.Id}: {request.ReviewSummary}"
            };

            _db.DocumentApprovalTasks.Add(approvalTask);
            await _db.SaveChangesAsync();

            await _audit.RecordUserEventAsync(
                actionCode: auditAction,
                actionCategory: AuditActionCategory.Review,
                recordType: nameof(PeriodicReviewTask),
                documentMasterId: master.Id,
                documentRevisionId: rev.Id,
                reason: $"Periodic review completed: Obsolescence recommended. Routed to quality approval task #{approvalTask.Id}. Summary: {task.ReviewSummary}",
                entityId: task.Id.ToString(),
                changes: new[]
                {
                    new AuditFieldChange("Outcome", null, PeriodicReviewOutcome.ObsolescenceRecommended.ToString()),
                    new AuditFieldChange("ObsolescenceApprovalTaskId", null, approvalTask.Id.ToString())
                }
            );
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(request.Outcome), "Invalid periodic review outcome.");
        }

        return await GetReviewTaskByIdAsync(taskId, userId);
    }

    public async Task<List<PeriodicReviewTaskDto>> GetMasterReviewHistoryAsync(int masterId, int userId)
    {
        var tasks = await _db.PeriodicReviewTasks
            .Include(t => t.DocumentMaster)
            .Include(t => t.DocumentRevision)
            .Include(t => t.AssignedReviewerUser)
            .Include(t => t.CompletedByUser)
            .Include(t => t.Findings).ThenInclude(f => f.CreatedByUser)
            .Where(t => t.DocumentMasterId == masterId)
            .OrderByDescending(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return tasks.Select(MapToTaskDto).ToList();
    }

    private static PeriodicReviewTaskDto MapToTaskDto(PeriodicReviewTask t)
    {
        var now = DateTime.UtcNow;
        var isOverdue = t.Status != PeriodicReviewTaskStatus.Completed &&
                        t.Status != PeriodicReviewTaskStatus.Cancelled &&
                        t.ScheduledDueDate < now;

        return new PeriodicReviewTaskDto(
            t.Id,
            t.DocumentMasterId,
            t.DocumentMaster?.MicroLimsDocumentId ?? "",
            t.DocumentMaster?.CompanyDocumentCode ?? "",
            t.DocumentMaster?.Title ?? "",
            t.DocumentRevisionId,
            t.DocumentRevision?.RevisionNumber ?? "",
            t.ReviewCycleMonths,
            t.ScheduledDueDate,
            isOverdue,
            t.AssignedReviewerUserId,
            t.AssignedReviewerUser?.Username,
            t.AssignedReviewerUser?.FullName,
            t.CreatedAt,
            t.Status,
            t.Outcome,
            t.CompletedAt,
            t.CompletedByUserId,
            t.CompletedByUser?.Username,
            t.CompletedByUser?.FullName,
            t.ReviewSummary,
            t.Findings?.Count ?? 0,
            t.Findings?.OrderBy(f => f.PageNumber).ThenBy(f => f.SectionNumber).Select(MapToFindingDto).ToList() ?? new List<PeriodicReviewFindingDto>()
        );
    }

    private static PeriodicReviewFindingDto MapToFindingDto(PeriodicReviewFinding f)
    {
        return new PeriodicReviewFindingDto(
            f.Id,
            f.PeriodicReviewTaskId,
            f.PageNumber,
            f.SectionNumber,
            f.NoteText,
            f.Status,
            f.CreatedByUserId,
            f.CreatedByUser?.Username ?? "",
            f.CreatedByUser?.FullName ?? "",
            f.CreatedAt
        );
    }
}
