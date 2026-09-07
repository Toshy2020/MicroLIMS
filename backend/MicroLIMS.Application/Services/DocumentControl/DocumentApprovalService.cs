using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services.DocumentControl;

public class DocumentApprovalService : IDocumentApprovalService
{
    private readonly MicroLimsDbContext _db;
    private readonly IAuditEventService _audit;
    private readonly IDocumentAuthorizationService _auth;
    private readonly IElectronicSignatureService _signatureService;

    public DocumentApprovalService(
        MicroLimsDbContext db,
        IAuditEventService audit,
        IDocumentAuthorizationService auth,
        IElectronicSignatureService signatureService)
    {
        _db = db;
        _audit = audit;
        _auth = auth;
        _signatureService = signatureService;
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

    public async Task<DocumentApprovalTaskDto> CreateApprovalTaskAsync(int revisionId, CreateApprovalTaskRequest request, int userId)
    {
        var (isActorActive, actorRole) = await GetUserRoleAsync(userId);
        if (!isActorActive || actorRole == null)
            throw new UnauthorizedAccessException("User is inactive or not found.");

        var revision = await _db.DocumentRevisions
            .Include(r => r.DocumentMaster)
            .Include(r => r.Files)
            .Include(r => r.ReviewTasks)
                .ThenInclude(t => t.Findings)
            .Include(r => r.ChangeItems)
            .Include(r => r.ImpactAssessment)
            .Include(r => r.ApprovalTasks)
            .FirstOrDefaultAsync(r => r.Id == revisionId);

        if (revision == null)
            throw new KeyNotFoundException($"Document revision {revisionId} not found.");

        // Precondition 1: Revision must be in AwaitingApproval status
        if (revision.RevisionStatus != DocumentRevisionStatus.AwaitingApproval)
        {
            throw new InvalidOperationException(
                $"Cannot create an approval task for revision in status '{revision.RevisionStatus}'. Status must be '{DocumentRevisionStatus.AwaitingApproval}'.");
        }

        // Precondition 2: Technical review must be completed
        var completedReview = revision.ReviewTasks.FirstOrDefault(t => t.Status == ReviewTaskStatus.Completed);
        if (completedReview == null)
        {
            throw new InvalidOperationException(
                "Cannot create an approval task: Revision has not completed technical review.");
        }

        // Precondition 3: No pending approval task already exists
        var existingPending = revision.ApprovalTasks.FirstOrDefault(t => t.Status == DocumentApprovalTaskStatus.Pending);
        if (existingPending != null)
        {
            throw new InvalidOperationException(
                $"An active approval task (ID: {existingPending.Id}) is already pending for this revision.");
        }

        // Authorization: Assigning user must be Owner, assigned Author, SectionHead, or Admin
        var canEdit = await _auth.CanEditDraftMetadataAsync(revision.DocumentMasterId, userId);
        if (!canEdit)
            throw new UnauthorizedAccessException("User is not authorized to create approval tasks for this document.");

        // Approver verification
        var approver = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == request.ApproverUserId);

        if (approver == null || !approver.IsActive)
            throw new InvalidOperationException($"Target approver user {request.ApproverUserId} is invalid or inactive.");

        // Approver role eligibility: Reviewer, SectionHead, or SystemAdministrator
        if (approver.Role == null ||
            (approver.Role.Type != RoleType.Reviewer &&
             approver.Role.Type != RoleType.SectionHead &&
             approver.Role.Type != RoleType.SystemAdministrator))
        {
            throw new InvalidOperationException($"User '{approver.FullName}' with role '{approver.Role?.Name ?? "None"}' is not eligible to be assigned as an approver.");
        }

        // HARD SEGREGATION OF DUTIES RULE (DC-URS-078, DC-URS-164..169)
        // Author != Approver, Reviewer != Approver. SystemAdministrator is NOT exempt!
        ValidateSegregationOfDuties(revision, request.ApproverUserId);

        var task = new DocumentApprovalTask
        {
            DocumentRevisionId = revisionId,
            AssignedApproverUserId = request.ApproverUserId,
            AssignedByUserId = userId,
            AssignedAt = DateTime.UtcNow,
            DueDate = request.DueDate,
            Status = DocumentApprovalTaskStatus.Pending,
            SubmissionNotes = request.SubmissionNotes?.Trim(),
            TargetEffectiveDate = request.TargetEffectiveDate
        };

        _db.DocumentApprovalTasks.Add(task);
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var assigner = await _db.Users.FindAsync(userId);

        await _audit.RecordUserEventAsync(
            actionCode: "ApprovalTaskCreated",
            actionCategory: AuditActionCategory.Approval,
            recordType: nameof(DocumentApprovalTask),
            documentMasterId: revision.DocumentMasterId,
            documentRevisionId: revision.Id,
            reason: $"Assigned approver '{approver.FullName}' to Document Revision {revision.RevisionNumber}",
            changes: new[]
            {
                new AuditFieldChange("AssignedApproverUserId", null, approver.Id.ToString()),
                new AuditFieldChange("Status", null, task.Status.ToString()),
                new AuditFieldChange("DueDate", null, task.DueDate?.ToString("O"))
            },
            entityId: task.Id.ToString()
        );

        return MapApprovalTaskDto(task, revision.DocumentMaster, revision, approver, assigner!, null);
    }

    public async Task<DocumentApprovalTaskDto> GetApprovalTaskByIdAsync(int approvalTaskId, int userId)
    {
        var task = await _db.DocumentApprovalTasks
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.DocumentMaster)
            .Include(t => t.AssignedApproverUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.DecisionByUser)
            .FirstOrDefaultAsync(t => t.Id == approvalTaskId);

        if (task == null)
            throw new KeyNotFoundException($"Document approval task {approvalTaskId} not found.");

        return MapApprovalTaskDto(task, task.DocumentRevision.DocumentMaster, task.DocumentRevision,
            task.AssignedApproverUser, task.AssignedByUser, task.DecisionByUser);
    }

    public async Task<DocumentApprovalTaskDto?> GetActiveApprovalTaskForRevisionAsync(int revisionId, int userId)
    {
        var task = await _db.DocumentApprovalTasks
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.DocumentMaster)
            .Include(t => t.AssignedApproverUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.DecisionByUser)
            .Where(t => t.DocumentRevisionId == revisionId && t.Status == DocumentApprovalTaskStatus.Pending)
            .OrderByDescending(t => t.AssignedAt)
            .FirstOrDefaultAsync();

        if (task == null) return null;

        return MapApprovalTaskDto(task, task.DocumentRevision.DocumentMaster, task.DocumentRevision,
            task.AssignedApproverUser, task.AssignedByUser, task.DecisionByUser);
    }

    public async Task<IReadOnlyList<DocumentApprovalTaskDto>> GetApprovalTasksAsync(int? revisionId, int? approverUserId, DocumentApprovalTaskStatus? status, int userId)
    {
        var query = _db.DocumentApprovalTasks
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.DocumentMaster)
            .Include(t => t.AssignedApproverUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.DecisionByUser)
            .AsQueryable();

        if (revisionId.HasValue)
            query = query.Where(t => t.DocumentRevisionId == revisionId.Value);

        if (approverUserId.HasValue)
            query = query.Where(t => t.AssignedApproverUserId == approverUserId.Value);

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        var list = await query
            .OrderByDescending(t => t.AssignedAt)
            .ToListAsync();

        return list.Select(t => MapApprovalTaskDto(t, t.DocumentRevision.DocumentMaster, t.DocumentRevision,
            t.AssignedApproverUser, t.AssignedByUser, t.DecisionByUser)).ToList();
    }

    public async Task<ApprovalReadinessDto> ValidateApprovalReadinessAsync(int approvalTaskId, int userId)
    {
        var task = await _db.DocumentApprovalTasks
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.DocumentMaster)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.Files)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.ReviewTasks)
                    .ThenInclude(rt => rt.Findings)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.ChangeItems)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.ImpactAssessment)
            .FirstOrDefaultAsync(t => t.Id == approvalTaskId);

        if (task == null)
            throw new KeyNotFoundException($"Document approval task {approvalTaskId} not found.");

        return EvaluateReadiness(task, userId);
    }

    public async Task<ApprovalDossierDto> GetApprovalDossierAsync(int approvalTaskId, int userId)
    {
        var task = await _db.DocumentApprovalTasks
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.DocumentMaster)
                    .ThenInclude(m => m.DocumentType)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.DocumentMaster)
                    .ThenInclude(m => m.Department)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.DocumentMaster)
                    .ThenInclude(m => m.Section)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.DocumentMaster)
                    .ThenInclude(m => m.DocumentOwnerUser)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.DocumentMaster)
                    .ThenInclude(m => m.Assignments)
                        .ThenInclude(a => a.User)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.DocumentMaster)
                    .ThenInclude(m => m.Keywords)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.Files)
                    .ThenInclude(f => f.UploadedByUser)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.ReviewTasks)
                    .ThenInclude(rt => rt.AssignedReviewerUser)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.ReviewTasks)
                    .ThenInclude(rt => rt.AssignedByUser)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.ReviewTasks)
                    .ThenInclude(rt => rt.DecisionByUser)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.ReviewTasks)
                    .ThenInclude(rt => rt.Findings)
                        .ThenInclude(f => f.CreatedByUser)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.ChangeItems)
                    .ThenInclude(ci => ci.CreatedByUser)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.ImpactAssessment)
                    .ThenInclude(ia => ia!.CompletedByUser)
            .Include(t => t.AssignedApproverUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.DecisionByUser)
            .FirstOrDefaultAsync(t => t.Id == approvalTaskId);

        if (task == null)
            throw new KeyNotFoundException($"Document approval task {approvalTaskId} not found.");

        var revision = task.DocumentRevision;
        var master = revision.DocumentMaster;

        var taskDto = MapApprovalTaskDto(task, master, revision, task.AssignedApproverUser, task.AssignedByUser, task.DecisionByUser);
        var revisionDto = MapRevisionDto(revision);
        var masterDto = MapMasterDto(master);

        var activePdf = revision.Files.FirstOrDefault(f => f.FileRole == FileRole.ControlledPdf && f.IsActive);
        var pdfDto = activePdf != null ? MapFileDto(activePdf) : null;

        var changeItems = revision.ChangeItems
            .OrderBy(c => c.SectionNumber)
            .Select(c => new RevisionChangeItemDto(
                c.Id,
                c.DocumentRevisionId,
                c.SectionNumber,
                c.SectionTitle,
                c.DescriptionOfChange,
                c.ChangeRationale,
                c.ChangeCategory,
                c.Status,
                c.OriginatingReviewFindingId,
                c.CreatedByUserId,
                c.CreatedByUser?.Username ?? "",
                c.CreatedByUser?.FullName ?? "",
                c.CreatedAt,
                c.ModifiedAt
            )).ToList();

        var impactDto = revision.ImpactAssessment != null ? new RevisionImpactAssessmentDto(
            revision.ImpactAssessment.Id,
            revision.ImpactAssessment.DocumentRevisionId,
            revision.ImpactAssessment.ProcedureOrMethodImpact,
            revision.ImpactAssessment.ProcedureOrMethodDetails,
            revision.ImpactAssessment.TrainingImpact,
            revision.ImpactAssessment.TrainingDetails,
            revision.ImpactAssessment.FormsOrTemplatesImpact,
            revision.ImpactAssessment.FormsOrTemplatesDetails,
            revision.ImpactAssessment.SpecificationsImpact,
            revision.ImpactAssessment.SpecificationsDetails,
            revision.ImpactAssessment.EquipmentImpact,
            revision.ImpactAssessment.EquipmentDetails,
            revision.ImpactAssessment.MaterialsOrMediaImpact,
            revision.ImpactAssessment.MaterialsOrMediaDetails,
            revision.ImpactAssessment.ValidationImpact,
            revision.ImpactAssessment.ValidationDetails,
            revision.ImpactAssessment.RegulatoryCommitmentImpact,
            revision.ImpactAssessment.RegulatoryCommitmentDetails,
            revision.ImpactAssessment.RelatedDocumentsImpact,
            revision.ImpactAssessment.RelatedDocumentsDetails,
            revision.ImpactAssessment.IsComplete,
            revision.ImpactAssessment.CompletedByUserId,
            revision.ImpactAssessment.CompletedByUser?.Username ?? "",
            revision.ImpactAssessment.CompletedByUser?.FullName ?? "",
            revision.ImpactAssessment.CompletedAt
        ) : null;

        var reviewHistory = revision.ReviewTasks
            .OrderByDescending(rt => rt.AssignedAt)
            .Select(MapReviewTaskDto)
            .ToList();

        var readiness = EvaluateReadiness(task, userId);

        var activeSignature = await _db.ElectronicSignatures
            .Where(s => s.EntityType == "DocumentRevision" && s.EntityId == revision.Id && s.MeaningOfSignature == SignatureMeaning.Approved)
            .OrderByDescending(s => s.SignedAt)
            .Select(s => new ElectronicSignatureDto(
                s.Id,
                s.UserId,
                s.UserFullNameSnapshot,
                s.UsernameSnapshot,
                s.RoleSnapshot,
                s.MeaningOfSignature,
                s.EntityType,
                s.EntityId,
                s.SignedAt,
                s.Comment,
                s.IpAddress
            ))
            .FirstOrDefaultAsync();

        // Record Dossier Viewed Audit Event (DC-URS-074, FS-1b-074)
        await _audit.RecordUserEventAsync(
            actionCode: "ApprovalDossierViewed",
            actionCategory: AuditActionCategory.Approval,
            recordType: nameof(DocumentApprovalTask),
            documentMasterId: master.Id,
            documentRevisionId: revision.Id,
            reason: $"Approver inspection dossier viewed for Revision {revision.RevisionNumber}",
            entityId: task.Id.ToString()
        );

        return new ApprovalDossierDto(
            taskDto,
            revisionDto,
            masterDto,
            pdfDto,
            changeItems,
            impactDto,
            reviewHistory,
            readiness,
            activeSignature
        );
    }

    public async Task<ElectronicSignatureDto?> GetApprovalSignatureAsync(int approvalTaskId, int userId)
    {
        var task = await _db.DocumentApprovalTasks
            .Include(t => t.DocumentRevision)
            .FirstOrDefaultAsync(t => t.Id == approvalTaskId);

        if (task == null)
            throw new KeyNotFoundException($"Document approval task {approvalTaskId} not found.");

        return await _db.ElectronicSignatures
            .Where(s => s.EntityType == "DocumentRevision" && s.EntityId == task.DocumentRevisionId && s.MeaningOfSignature == SignatureMeaning.Approved)
            .OrderByDescending(s => s.SignedAt)
            .Select(s => new ElectronicSignatureDto(
                s.Id,
                s.UserId,
                s.UserFullNameSnapshot,
                s.UsernameSnapshot,
                s.RoleSnapshot,
                s.MeaningOfSignature,
                s.EntityType,
                s.EntityId,
                s.SignedAt,
                s.Comment,
                s.IpAddress
            ))
            .FirstOrDefaultAsync();
    }

    public async Task<DocumentApprovalTaskDto> ExecuteApprovalDecisionAsync(int approvalTaskId, ExecuteApprovalDecisionRequest request, int userId, string? ipAddress = null)
    {
        var (isActorActive, actorRole) = await GetUserRoleAsync(userId);
        if (!isActorActive || actorRole == null)
            throw new UnauthorizedAccessException("User is inactive or not found.");

        var task = await _db.DocumentApprovalTasks
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.DocumentMaster)
                    .ThenInclude(m => m.Revisions)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.Files)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.ReviewTasks)
                    .ThenInclude(rt => rt.Findings)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.ChangeItems)
            .Include(t => t.DocumentRevision)
                .ThenInclude(r => r.ImpactAssessment)
            .Include(t => t.AssignedApproverUser)
            .Include(t => t.AssignedByUser)
            .FirstOrDefaultAsync(t => t.Id == approvalTaskId);

        if (task == null)
            throw new KeyNotFoundException($"Document approval task {approvalTaskId} not found.");

        if (task.Status != DocumentApprovalTaskStatus.Pending)
            throw new InvalidOperationException($"Cannot execute decision on approval task in status '{task.Status}'.");

        var revision = task.DocumentRevision;
        var master = revision.DocumentMaster;

        // HARD SEGREGATION OF DUTIES RULE (DC-URS-078, DC-URS-164..169)
        // Actor cannot be Author, Actor cannot be Reviewer! SystemAdministrator is NOT exempt!
        ValidateDecisionSegregationOfDuties(revision, userId);

        // Verify assignment: Must be the assigned approver or Document Controller (SectionHead)
        if (userId != task.AssignedApproverUserId && actorRole != RoleType.SectionHead && actorRole != RoleType.SystemAdministrator)
        {
            throw new UnauthorizedAccessException("Only the assigned approver or Document Controller may execute an approval decision.");
        }

        var actorUser = await _db.Users.FindAsync(userId);
        var now = DateTime.UtcNow;

        if (request.Decision == DocumentApprovalDecision.ReturnForCorrection)
        {
            if (string.IsNullOrWhiteSpace(request.DecisionNotes) || request.DecisionNotes.Trim().Length < 10)
            {
                throw new ArgumentException("A mandatory justification of at least 10 characters is required when returning a revision for correction.", nameof(request.DecisionNotes));
            }

            task.Status = DocumentApprovalTaskStatus.ReturnedForCorrection;
            task.Decision = DocumentApprovalDecision.ReturnForCorrection;
            task.DecisionAt = now;
            task.DecisionByUserId = userId;
            task.DecisionNotes = request.DecisionNotes.Trim();

            // Revert revision status to Draft (DC-URS-075, FS-1b-075)
            revision.RevisionStatus = DocumentRevisionStatus.Draft;

            _db.CurrentUserId = userId;
            await _db.SaveChangesAsync();

            await _audit.RecordUserEventAsync(
                actionCode: "RevisionReturnedByApprover",
                actionCategory: AuditActionCategory.Approval,
                recordType: nameof(DocumentApprovalTask),
                documentMasterId: master.Id,
                documentRevisionId: revision.Id,
                reason: task.DecisionNotes,
                changes: new[]
                {
                    new AuditFieldChange("Status", DocumentApprovalTaskStatus.Pending.ToString(), task.Status.ToString()),
                    new AuditFieldChange("RevisionStatus", DocumentRevisionStatus.AwaitingApproval.ToString(), revision.RevisionStatus.ToString()),
                    new AuditFieldChange("DecisionNotes", null, task.DecisionNotes)
                },
                entityId: task.Id.ToString()
            );

            return MapApprovalTaskDto(task, master, revision, task.AssignedApproverUser, task.AssignedByUser, actorUser);
        }

        if (request.Decision == DocumentApprovalDecision.Decline)
        {
            if (string.IsNullOrWhiteSpace(request.DecisionNotes) || request.DecisionNotes.Trim().Length < 10)
            {
                throw new ArgumentException("A mandatory justification of at least 10 characters is required when declining a revision.", nameof(request.DecisionNotes));
            }

            task.Status = DocumentApprovalTaskStatus.Declined;
            task.Decision = DocumentApprovalDecision.Decline;
            task.DecisionAt = now;
            task.DecisionByUserId = userId;
            task.DecisionNotes = request.DecisionNotes.Trim();

            // Mark revision as Cancelled (DC-URS-075, FS-1b-075)
            revision.RevisionStatus = DocumentRevisionStatus.Cancelled;
            revision.CancelledAt = now;
            revision.CancelledByUserId = userId;
            revision.CancelReason = task.DecisionNotes;

            _db.CurrentUserId = userId;
            await _db.SaveChangesAsync();

            await _audit.RecordUserEventAsync(
                actionCode: "RevisionDeclinedByApprover",
                actionCategory: AuditActionCategory.Approval,
                recordType: nameof(DocumentApprovalTask),
                documentMasterId: master.Id,
                documentRevisionId: revision.Id,
                reason: task.DecisionNotes,
                changes: new[]
                {
                    new AuditFieldChange("Status", DocumentApprovalTaskStatus.Pending.ToString(), task.Status.ToString()),
                    new AuditFieldChange("RevisionStatus", DocumentRevisionStatus.AwaitingApproval.ToString(), revision.RevisionStatus.ToString()),
                    new AuditFieldChange("DecisionNotes", null, task.DecisionNotes)
                },
                entityId: task.Id.ToString()
            );

            return MapApprovalTaskDto(task, master, revision, task.AssignedApproverUser, task.AssignedByUser, actorUser);
        }

        if (request.Decision == DocumentApprovalDecision.Approve)
        {
            // 21 CFR Part 11 Electronic Signature Requirement (DC-URS-076, FS-1b-076)
            // An approval decision strictly requires authenticated password re-entry.
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                throw new InvalidOperationException("Password re-authentication is required to apply an electronic signature for approval (21 CFR Part 11).");
            }

            // Verify readiness strictly server-side
            var readiness = EvaluateReadiness(task, userId);
            if (!readiness.IsReady)
            {
                throw new InvalidOperationException(
                    "Cannot approve document revision: Prerequisites not met. Errors: " + string.Join("; ", readiness.ValidationErrors));
            }

            // Check duplicate signature prevention
            var existingSig = await _db.ElectronicSignatures
                .AnyAsync(s => s.EntityType == "DocumentRevision" && s.EntityId == revision.Id && s.MeaningOfSignature == SignatureMeaning.Approved);
            if (existingSig)
            {
                throw new InvalidOperationException($"An electronic signature for approval has already been applied to revision {revision.RevisionNumber}.");
            }

            // Execute 21 CFR Part 11 Electronic Signature ceremony via qualified IElectronicSignatureService
            // This verifies the user's password hash via BCrypt. If it fails, it throws and writes a failed signature audit event.
            // When successful, it adds the ElectronicSignature entity to the change tracker without saving, ensuring
            // that the electronic signature and the approval state mutations are committed atomically in the single SaveChangesAsync.
            var signatureComment = string.IsNullOrWhiteSpace(request.DecisionNotes)
                ? $"Approved document revision {revision.RevisionNumber}"
                : request.DecisionNotes.Trim();

            var signature = await _signatureService.SignAsync(
                userId: userId,
                password: request.Password,
                meaning: SignatureMeaning.Approved,
                entityType: "DocumentRevision",
                entityId: revision.Id,
                comment: signatureComment,
                ipAddress: ipAddress
            );

            // Determine effective date and target status (DC-URS-075, FS-1b-076)
            var effectiveDate = request.EffectiveDate ?? task.TargetEffectiveDate ?? revision.EffectiveDate ?? now;
            bool isFutureEffective = effectiveDate.Date > now.Date;

            task.Status = DocumentApprovalTaskStatus.Approved;
            task.Decision = DocumentApprovalDecision.Approve;
            task.DecisionAt = now;
            task.DecisionByUserId = userId;
            task.DecisionNotes = request.DecisionNotes?.Trim();

            if (isFutureEffective)
            {
                revision.RevisionStatus = DocumentRevisionStatus.FutureEffective;
                revision.EffectiveDate = effectiveDate;
            }
            else
            {
                revision.RevisionStatus = DocumentRevisionStatus.Effective;
                revision.EffectiveDate = effectiveDate;
                revision.NextReviewDate = effectiveDate.AddMonths(revision.ReviewCycleMonths ?? 24);

                // Supersede prior effective revision in the same transaction
                var priorEffective = master.Revisions
                    .Where(r => r.Id != revision.Id && r.RevisionStatus == DocumentRevisionStatus.Effective)
                    .ToList();

                foreach (var prior in priorEffective)
                {
                    prior.RevisionStatus = DocumentRevisionStatus.Superseded;
                }

                master.CurrentEffectiveRevisionId = revision.Id;
            }

            _db.CurrentUserId = userId;
            await _db.SaveChangesAsync();

            // Semantic Audit Trail for Part 11 Signature & Approval (DC-URS-077, DC-URS-079, FS-1b-077)
            await _audit.RecordUserEventAsync(
                actionCode: "ElectronicSignatureApplied",
                actionCategory: AuditActionCategory.Approval,
                recordType: nameof(ElectronicSignature),
                documentMasterId: master.Id,
                documentRevisionId: revision.Id,
                reason: $"21 CFR Part 11 Electronic Signature ({SignatureMeaning.Approved}) applied by {signature.UserFullNameSnapshot} ({signature.RoleSnapshot})",
                changes: new[]
                {
                    new AuditFieldChange("SignatureId", null, signature.Id.ToString()),
                    new AuditFieldChange("SignerUsername", null, signature.UsernameSnapshot),
                    new AuditFieldChange("SignerFullName", null, signature.UserFullNameSnapshot),
                    new AuditFieldChange("SignerRole", null, signature.RoleSnapshot),
                    new AuditFieldChange("Meaning", null, signature.MeaningOfSignature.ToString()),
                    new AuditFieldChange("SignedAt", null, signature.SignedAt.ToString("O"))
                },
                entityId: signature.Id.ToString()
            );

            await _audit.RecordUserEventAsync(
                actionCode: "DocumentRevisionApproved",
                actionCategory: AuditActionCategory.Approval,
                recordType: nameof(DocumentApprovalTask),
                documentMasterId: master.Id,
                documentRevisionId: revision.Id,
                reason: task.DecisionNotes ?? $"Approved revision {revision.RevisionNumber} (Status: {revision.RevisionStatus})",
                changes: new[]
                {
                    new AuditFieldChange("Status", DocumentApprovalTaskStatus.Pending.ToString(), task.Status.ToString()),
                    new AuditFieldChange("RevisionStatus", DocumentRevisionStatus.AwaitingApproval.ToString(), revision.RevisionStatus.ToString()),
                    new AuditFieldChange("EffectiveDate", null, revision.EffectiveDate?.ToString("O")),
                    new AuditFieldChange("NextReviewDate", null, revision.NextReviewDate?.ToString("O"))
                },
                entityId: task.Id.ToString()
            );

            return MapApprovalTaskDto(task, master, revision, task.AssignedApproverUser, task.AssignedByUser, actorUser);
        }

        throw new ArgumentException($"Unsupported approval decision: {request.Decision}", nameof(request.Decision));
    }

    // ---- Helper Methods ----

    private static void ValidateSegregationOfDuties(DocumentRevision revision, int approverUserId)
    {
        if (approverUserId == revision.CreatedByUserId)
        {
            throw new UnauthorizedAccessException(
                "Segregation of Duties Violation: The author of a document revision cannot be assigned as its approver.");
        }

        var reviewerUserIds = revision.ReviewTasks
            .Select(t => t.AssignedReviewerUserId)
            .Union(revision.ReviewTasks.Where(t => t.DecisionByUserId.HasValue).Select(t => t.DecisionByUserId!.Value))
            .Distinct()
            .ToList();

        if (reviewerUserIds.Contains(approverUserId))
        {
            throw new UnauthorizedAccessException(
                "Segregation of Duties Violation: A technical reviewer for this revision cannot be assigned as its approver.");
        }
    }

    private static void ValidateDecisionSegregationOfDuties(DocumentRevision revision, int actorUserId)
    {
        if (actorUserId == revision.CreatedByUserId)
        {
            throw new UnauthorizedAccessException(
                "Segregation of Duties Violation: The author of a document revision cannot approve or decide upon their own revision.");
        }

        var reviewerUserIds = revision.ReviewTasks
            .Select(t => t.AssignedReviewerUserId)
            .Union(revision.ReviewTasks.Where(t => t.DecisionByUserId.HasValue).Select(t => t.DecisionByUserId!.Value))
            .Distinct()
            .ToList();

        if (reviewerUserIds.Contains(actorUserId))
        {
            throw new UnauthorizedAccessException(
                "Segregation of Duties Violation: A technical reviewer for this revision cannot approve or decide upon this revision.");
        }
    }

    private static ApprovalReadinessDto EvaluateReadiness(DocumentApprovalTask task, int userId)
    {
        var revision = task.DocumentRevision;
        var errors = new List<string>();

        // 1. Revision State
        if (revision.RevisionStatus != DocumentRevisionStatus.AwaitingApproval)
        {
            errors.Add($"Revision is in status '{revision.RevisionStatus}'. Status must be 'AwaitingApproval'.");
        }

        // 2. Active Controlled PDF
        bool hasActivePdf = revision.Files.Any(f => f.FileRole == FileRole.ControlledPdf && f.IsActive);
        if (!hasActivePdf)
        {
            errors.Add("An active Controlled PDF file is required.");
        }

        // 3. Technical Review Completed
        bool isReviewCompleted = revision.ReviewTasks.Any(t => t.Status == ReviewTaskStatus.Completed);
        if (!isReviewCompleted)
        {
            errors.Add("Technical review has not been completed.");
        }

        // 4. Mandatory Findings Resolved
        bool areMandatoryFindingsResolved = !revision.ReviewTasks.Any(t =>
            t.Findings.Any(f => f.IsMandatory && f.Status != ReviewFindingStatus.Resolved));
        if (!areMandatoryFindingsResolved)
        {
            errors.Add("Mandatory technical review findings remain unresolved.");
        }

        // 5. Impact Assessment Completed
        bool isImpactComplete = revision.RevisionSequence == 1
            ? (revision.ImpactAssessment == null || revision.ImpactAssessment.IsComplete)
            : (revision.ImpactAssessment != null && revision.ImpactAssessment.IsComplete);
        if (!isImpactComplete)
        {
            errors.Add("Multi-category revision impact assessment is incomplete.");
        }

        // 6. Change Items Addressed
        bool areChangeItemsAddressed = !revision.ChangeItems.Any(c =>
            c.OriginatingReviewFindingId.HasValue && c.Status != "Addressed");
        if (!areChangeItemsAddressed)
        {
            errors.Add("One or more originating review findings in change items remain unaddressed.");
        }

        // 7. Segregation of Duties Check
        bool isSodSatisfied = true;
        if (userId == revision.CreatedByUserId)
        {
            isSodSatisfied = false;
            errors.Add("Segregation of Duties Violation: You are the author of this revision and cannot approve it.");
        }

        var reviewerUserIds = revision.ReviewTasks
            .Select(t => t.AssignedReviewerUserId)
            .Union(revision.ReviewTasks.Where(t => t.DecisionByUserId.HasValue).Select(t => t.DecisionByUserId!.Value))
            .Distinct()
            .ToList();

        if (reviewerUserIds.Contains(userId))
        {
            isSodSatisfied = false;
            errors.Add("Segregation of Duties Violation: You participated in technical review and cannot approve this revision.");
        }

        var targetEffective = task.TargetEffectiveDate ?? revision.EffectiveDate;
        var targetStatus = (targetEffective.HasValue && targetEffective.Value.Date > DateTime.UtcNow.Date)
            ? DocumentRevisionStatus.FutureEffective
            : DocumentRevisionStatus.Effective;

        return new ApprovalReadinessDto(
            IsReady: errors.Count == 0,
            ValidationErrors: errors,
            RequiresElectronicSignature: true, // Clean boundary for WP4
            TargetStatusOnApproval: targetStatus,
            HasActiveControlledPdf: hasActivePdf,
            IsTechnicalReviewCompleted: isReviewCompleted,
            AreMandatoryFindingsResolved: areMandatoryFindingsResolved,
            IsImpactAssessmentCompleted: isImpactComplete,
            AreChangeItemsAddressed: areChangeItemsAddressed,
            IsSegregationOfDutiesSatisfied: isSodSatisfied
        );
    }

    private static DocumentApprovalTaskDto MapApprovalTaskDto(
        DocumentApprovalTask t,
        DocumentMaster m,
        DocumentRevision r,
        User approver,
        User assigner,
        User? decider)
    {
        return new DocumentApprovalTaskDto(
            t.Id,
            t.DocumentRevisionId,
            m.Id,
            m.CompanyDocumentCode,
            m.Title,
            r.RevisionNumber,
            r.RevisionSequence,
            t.AssignedApproverUserId,
            approver.Username,
            approver.FullName,
            t.AssignedByUserId,
            assigner.Username,
            assigner.FullName,
            t.AssignedAt,
            t.DueDate,
            t.Status,
            t.Decision,
            t.DecisionAt,
            t.DecisionByUserId,
            decider?.Username,
            decider?.FullName,
            t.DecisionNotes,
            t.SubmissionNotes,
            t.TargetEffectiveDate
        );
    }

    private static DocumentRevisionDto MapRevisionDto(DocumentRevision r)
    {
        return new DocumentRevisionDto(
            r.Id,
            r.DocumentMasterId,
            r.RevisionNumber,
            r.RevisionSequence,
            r.RevisionStatus,
            r.EffectiveDate,
            r.NextReviewDate,
            r.ReviewCycleMonths,
            r.RevisionType,
            r.ReasonForRevision,
            r.ChangeReference,
            r.RecordOrigin,
            r.CreatedAt,
            r.CreatedByUserId,
            r.CreatedByUser?.FullName ?? "",
            r.CancelledAt,
            r.CancelledByUserId,
            r.CancelledByUser?.FullName,
            r.CancelReason,
            r.Files.OrderByDescending(f => f.UploadedAt).Select(MapFileDto).ToList()
        );
    }

    private static RevisionFileDto MapFileDto(RevisionFile f)
    {
        return new RevisionFileDto(
            f.Id,
            f.DocumentRevisionId,
            f.FileRole,
            f.FileName,
            f.ContentType,
            f.SizeBytes,
            f.ContentSha256,
            f.IsActive,
            f.SupersededByFileId,
            f.UploadedAt,
            f.UploadedByUserId,
            f.UploadedByUser?.FullName ?? "",
            f.FileVersion,
            f.IsApprovedFinalSource,
            f.GeneratedFromSourceFileId
        );
    }

    private static DocumentMasterDto MapMasterDto(DocumentMaster m)
    {
        var curRev = m.CurrentEffectiveRevisionId.HasValue
            ? m.Revisions.FirstOrDefault(r => r.Id == m.CurrentEffectiveRevisionId.Value)
            : m.Revisions.OrderByDescending(r => r.RevisionSequence).FirstOrDefault();

        return new DocumentMasterDto(
            m.Id,
            m.MicroLimsDocumentId,
            m.CompanyDocumentCode,
            m.Title,
            m.DocumentTypeId,
            m.DocumentType?.Name ?? "",
            m.DocumentType?.Code ?? "",
            m.DepartmentId,
            m.Department?.Name ?? "",
            m.SectionId,
            m.Section?.Name ?? "",
            m.DocumentOwnerUserId,
            m.DocumentOwnerUser?.FullName ?? "",
            m.Confidentiality,
            m.Category,
            m.RecordOrigin,
            m.RecordStatus,
            m.CurrentEffectiveRevisionId,
            curRev?.RevisionNumber,
            m.CreatedAt,
            m.CreatedByUserId,
            m.CreatedByUser?.FullName ?? "",
            m.ModifiedAt,
            m.ModifiedByUserId,
            m.ModifiedByUser?.FullName,
            m.VoidedAt,
            m.VoidedByUserId,
            m.VoidedByUser?.FullName,
            m.VoidReason,
            m.Keywords.Select(k => k.Keyword).ToList(),
            m.Assignments.Where(a => a.IsActive).Select(a => new DocumentMasterAssignmentDto(
                a.Id,
                a.DocumentMasterId,
                a.UserId,
                a.User?.Username ?? "",
                a.User?.FullName ?? "",
                a.AssignmentRole,
                a.IsActive,
                a.AssignedAt,
                a.AssignedByUserId,
                a.AssignedByUser?.FullName ?? ""
            )).ToList(),
            m.Revisions.OrderByDescending(r => r.RevisionSequence).Select(MapRevisionDto).ToList()
        );
    }

    private static DocumentReviewTaskDto MapReviewTaskDto(DocumentReviewTask t)
    {
        return new DocumentReviewTaskDto
        {
            Id = t.Id,
            DocumentRevisionId = t.DocumentRevisionId,
            DocumentMasterId = t.DocumentRevision.DocumentMasterId,
            MicroLimsDocumentId = t.DocumentRevision.DocumentMaster.MicroLimsDocumentId,
            CompanyDocumentCode = t.DocumentRevision.DocumentMaster.CompanyDocumentCode,
            DocumentTitle = t.DocumentRevision.DocumentMaster.Title,
            RevisionNumber = t.DocumentRevision.RevisionNumber,
            RevisionStatus = t.DocumentRevision.RevisionStatus,
            AssignedReviewerUserId = t.AssignedReviewerUserId,
            AssignedReviewerUsername = t.AssignedReviewerUser?.Username ?? "",
            AssignedReviewerFullName = t.AssignedReviewerUser?.FullName ?? "",
            AssignedByUserId = t.AssignedByUserId,
            AssignedByUsername = t.AssignedByUser?.Username ?? "",
            AssignedAt = t.AssignedAt,
            DueDate = t.DueDate,
            Status = t.Status,
            Decision = t.Decision,
            DecisionAt = t.DecisionAt,
            DecisionByUsername = t.DecisionByUser?.Username,
            ReviewNotes = t.ReviewNotes,
            SubmissionNotes = t.SubmissionNotes,
            ReviewCycleNumber = t.ReviewCycleNumber,
            ReviewedSourceFileId = t.ReviewedSourceFileId,
            TotalFindingsCount = t.Findings.Count,
            OpenMandatoryFindingsCount = t.Findings.Count(f => f.IsMandatory && f.Status != ReviewFindingStatus.Resolved),
            Findings = t.Findings.OrderBy(f => f.PageNumber).ThenBy(f => f.SectionNumber).Select(f => new DocumentReviewFindingDto
            {
                Id = f.Id,
                DocumentReviewTaskId = f.DocumentReviewTaskId,
                CreatedByUserId = f.CreatedByUserId,
                CreatedByUsername = f.CreatedByUser?.Username ?? "",
                CreatedByFullName = f.CreatedByUser?.FullName ?? "",
                CreatedAt = f.CreatedAt,
                PageNumber = f.PageNumber,
                SectionNumber = f.SectionNumber,
                CommentText = f.CommentText,
                IsMandatory = f.IsMandatory,
                RevisionFileId = f.RevisionFileId,
                SourceFileVersion = f.SourceFileVersion,
                Status = f.Status,
                AuthorResponse = f.AuthorResponse,
                AuthorResponseAt = f.AuthorResponseAt,
                AuthorResponseUsername = null,
                ReviewerVerificationNotes = f.ReviewerVerificationNotes,
                ReviewerVerifiedAt = f.ReviewerVerifiedAt,
                ReviewerVerifiedUsername = null,
                ResolvedAt = f.ResolvedAt,
                ResolvedByUsername = null
            }).ToList()
        };
    }
}
