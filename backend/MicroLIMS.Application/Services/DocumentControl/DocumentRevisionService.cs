using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services.DocumentControl;

public class DocumentRevisionService : IDocumentRevisionService
{
    private readonly MicroLimsDbContext _db;
    private readonly IAuditEventService _auditEventService;
    private readonly IDocumentAuthorizationService _authService;

    public DocumentRevisionService(
        MicroLimsDbContext db,
        IAuditEventService auditEventService,
        IDocumentAuthorizationService authService)
    {
        _db = db;
        _auditEventService = auditEventService;
        _authService = authService;
    }

    public async Task<ProposeNextRevisionResponse> ProposeNextRevisionAsync(int documentMasterId, RevisionType revisionType, int userId)
    {
        var master = await _db.DocumentMasters
            .Include(d => d.Revisions)
            .FirstOrDefaultAsync(d => d.Id == documentMasterId)
            ?? throw new KeyNotFoundException($"Document Master {documentMasterId} not found.");

        if (master.RecordStatus == DocumentRecordStatus.Void)
            throw new InvalidOperationException("Cannot propose revision for a voided document.");

        var effectiveRev = master.Revisions.FirstOrDefault(r =>
            r.Id == master.CurrentEffectiveRevisionId || r.RevisionStatus == DocumentRevisionStatus.Effective);

        if (effectiveRev == null)
            throw new InvalidOperationException("Cannot propose a revision: Document Master has no currently effective revision.");

        var proposed = ComputeNextRevisionNumber(effectiveRev.RevisionNumber, revisionType);

        return new ProposeNextRevisionResponse(proposed, revisionType, effectiveRev.RevisionNumber);
    }

    public async Task<DocumentRevisionDto> CreateRevisionFromEffectiveAsync(int documentMasterId, CreateRevisionRequest request, int userId)
    {
        // 1. Authorization: Document Owner, assigned Author, or Document Controller
        var canEdit = await _authService.CanEditDraftMetadataAsync(documentMasterId, userId);
        if (!canEdit)
            throw new UnauthorizedAccessException("You are not authorized to create revisions for this document.");

        var master = await _db.DocumentMasters
            .Include(d => d.Revisions)
            .FirstOrDefaultAsync(d => d.Id == documentMasterId)
            ?? throw new KeyNotFoundException($"Document Master {documentMasterId} not found.");

        if (master.RecordStatus == DocumentRecordStatus.Void)
            throw new InvalidOperationException("Cannot create a revision for a voided document.");

        // 2. Precondition: Master must have an effective revision
        var effectiveRev = master.Revisions.FirstOrDefault(r =>
            r.Id == master.CurrentEffectiveRevisionId || r.RevisionStatus == DocumentRevisionStatus.Effective);

        if (effectiveRev == null)
            throw new InvalidOperationException("Cannot create a new revision from an effective document because this document has no effective revision.");

        // 3. Precondition: No revision currently in Draft, InReview, or AwaitingApproval
        var hasActiveWorkflowRevision = master.Revisions.Any(r =>
            r.RevisionStatus == DocumentRevisionStatus.Draft ||
            r.RevisionStatus == DocumentRevisionStatus.InReview ||
            r.RevisionStatus == DocumentRevisionStatus.AwaitingApproval);

        if (hasActiveWorkflowRevision)
            throw new InvalidOperationException("Cannot create a new revision while another revision is currently in draft or review/approval workflow.");

        // 4. Mandatory Change Reason validation (>= 10 chars)
        if (string.IsNullOrWhiteSpace(request.ReasonForRevision) || request.ReasonForRevision.Trim().Length < 10)
            throw new ArgumentException("Reason for Revision is mandatory and must contain at least 10 characters.", nameof(request.ReasonForRevision));

        if (!string.IsNullOrWhiteSpace(request.ChangeSummary) && request.ChangeSummary.Trim().Length < 10)
            throw new ArgumentException("Change Summary, if provided, must contain at least 10 characters.", nameof(request.ChangeSummary));

        // 5. Revision Number calculation & override verification
        var proposedNumber = ComputeNextRevisionNumber(effectiveRev.RevisionNumber, request.RevisionType);
        string finalRevisionNumber = proposedNumber;
        bool isOverridden = false;

        if (!string.IsNullOrWhiteSpace(request.CustomRevisionNumber) &&
            !string.Equals(request.CustomRevisionNumber.Trim(), proposedNumber, StringComparison.OrdinalIgnoreCase))
        {
            var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new KeyNotFoundException($"User {userId} not found.");

            // Strict Role Check: Override requires Document Controller (SectionHead) or SystemAdministrator
            if (user.Role.Type != RoleType.SectionHead && user.Role.Type != RoleType.SystemAdministrator)
            {
                throw new UnauthorizedAccessException("Only Document Controllers are authorized to override the proposed revision number.");
            }

            var trimmedCustom = request.CustomRevisionNumber.Trim();
            if (!Regex.IsMatch(trimmedCustom, @"^[a-zA-Z0-9\.\-_]{1,20}$"))
            {
                throw new ArgumentException("Custom revision number must be 1-20 alphanumeric characters, dots, hyphens, or underscores.", nameof(request.CustomRevisionNumber));
            }

            finalRevisionNumber = trimmedCustom;
            isOverridden = true;
        }

        // 6. Uniqueness check across all revisions of this master
        var duplicateExists = master.Revisions.Any(r =>
            string.Equals(r.RevisionNumber, finalRevisionNumber, StringComparison.OrdinalIgnoreCase));

        if (duplicateExists)
            throw new InvalidOperationException($"Revision number '{finalRevisionNumber}' already exists for this document.");

        // 7. Sequence Allocation
        var maxSequence = master.Revisions.Any() ? master.Revisions.Max(r => r.RevisionSequence) : 0;
        var nextSequence = maxSequence + 1;

        // 8. Create new DocumentRevision in status Draft
        var newRevision = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = finalRevisionNumber,
            RevisionSequence = nextSequence,
            RevisionStatus = DocumentRevisionStatus.Draft,
            RevisionType = request.RevisionType,
            ReasonForRevision = request.ReasonForRevision.Trim(),
            ChangeSummary = request.ChangeSummary?.Trim(),
            ChangeReference = request.ChangeReference?.Trim(),
            OriginatingPeriodicReviewTaskId = request.OriginatingPeriodicReviewTaskId,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            ReviewCycleMonths = effectiveRev.ReviewCycleMonths,
            RecordOrigin = RecordOrigin.Native
        };

        _db.DocumentRevisions.Add(newRevision);
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        // 9. Semantic Audit Logging
        await _auditEventService.RecordUserEventAsync(
            actionCode: "DocumentRevisionCreated",
            actionCategory: AuditActionCategory.Document,
            recordType: nameof(DocumentRevision),
            documentMasterId: master.Id,
            documentRevisionId: newRevision.Id,
            reason: newRevision.ReasonForRevision,
            entityId: newRevision.Id.ToString(),
            changes: new[]
            {
                new AuditFieldChange("RevisionNumber", null, newRevision.RevisionNumber),
                new AuditFieldChange("RevisionStatus", null, newRevision.RevisionStatus.ToString()),
                new AuditFieldChange("RevisionType", null, newRevision.RevisionType.ToString()),
                new AuditFieldChange("DocumentMasterId", null, master.Id.ToString()),
                new AuditFieldChange("MicroLimsDocumentId", null, master.MicroLimsDocumentId),
                new AuditFieldChange("CompanyDocumentCode", null, master.CompanyDocumentCode)
            }
        );

        if (isOverridden)
        {
            await _auditEventService.RecordUserEventAsync(
                actionCode: "RevisionNumberOverridden",
                actionCategory: AuditActionCategory.Document,
                recordType: nameof(DocumentRevision),
                documentMasterId: master.Id,
                documentRevisionId: newRevision.Id,
                reason: $"Revision number manually overridden from '{proposedNumber}' to '{finalRevisionNumber}'",
                entityId: newRevision.Id.ToString(),
                changes: new[]
                {
                    new AuditFieldChange("ProposedRevisionNumber", null, proposedNumber),
                    new AuditFieldChange("FinalRevisionNumber", null, finalRevisionNumber)
                }
            );
        }

        return await MapRevisionDtoAsync(newRevision.Id);
    }

    public async Task<DocumentRevisionDto> GetRevisionDetailsAsync(int revisionId, int userId)
    {
        return await MapRevisionDtoAsync(revisionId);
    }

    public async Task<RevisionChangeItemDto> AddChangeItemAsync(int revisionId, AddChangeItemRequest request, int userId)
    {
        var revision = await _db.DocumentRevisions
            .Include(r => r.DocumentMaster)
            .FirstOrDefaultAsync(r => r.Id == revisionId)
            ?? throw new KeyNotFoundException($"Document Revision {revisionId} not found.");

        if (revision.RevisionStatus != DocumentRevisionStatus.Draft)
            throw new InvalidOperationException("Change items can only be added while the revision is in Draft status.");

        var canEdit = await _authService.CanEditDraftMetadataAsync(revision.DocumentMasterId, userId);
        if (!canEdit)
            throw new UnauthorizedAccessException("You do not have permission to modify change items for this draft.");

        if (string.IsNullOrWhiteSpace(request.SectionNumber))
            throw new ArgumentException("Section Number is required.", nameof(request.SectionNumber));

        if (string.IsNullOrWhiteSpace(request.SectionTitle))
            throw new ArgumentException("Section Title is required.", nameof(request.SectionTitle));

        if (string.IsNullOrWhiteSpace(request.DescriptionOfChange))
            throw new ArgumentException("Description of Change is required.", nameof(request.DescriptionOfChange));

        if (string.IsNullOrWhiteSpace(request.ChangeRationale))
            throw new ArgumentException("Change Rationale is required.", nameof(request.ChangeRationale));

        if (string.IsNullOrWhiteSpace(request.ChangeCategory))
            throw new ArgumentException("Change Category is required.", nameof(request.ChangeCategory));

        var item = new RevisionChangeItem
        {
            DocumentRevisionId = revisionId,
            SectionNumber = request.SectionNumber.Trim(),
            SectionTitle = request.SectionTitle.Trim(),
            DescriptionOfChange = request.DescriptionOfChange.Trim(),
            ChangeRationale = request.ChangeRationale.Trim(),
            ChangeCategory = request.ChangeCategory.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status.Trim(),
            OriginatingReviewFindingId = request.OriginatingReviewFindingId,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _db.RevisionChangeItems.Add(item);
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(userId);
        await _auditEventService.RecordUserEventAsync(
            actionCode: "RevisionChangeItemAdded",
            actionCategory: AuditActionCategory.Document,
            recordType: nameof(RevisionChangeItem),
            documentMasterId: revision.DocumentMasterId,
            documentRevisionId: revisionId,
            reason: $"Added change item for section {item.SectionNumber}",
            entityId: item.Id.ToString(),
            changes: new[]
            {
                new AuditFieldChange("SectionNumber", null, item.SectionNumber),
                new AuditFieldChange("DescriptionOfChange", null, item.DescriptionOfChange),
                new AuditFieldChange("ChangeCategory", null, item.ChangeCategory)
            }
        );

        return MapChangeItemDto(item, user?.Username ?? "", user?.FullName ?? "");
    }

    public async Task<RevisionChangeItemDto> UpdateChangeItemAsync(int changeItemId, UpdateChangeItemRequest request, int userId)
    {
        var item = await _db.RevisionChangeItems
            .Include(c => c.DocumentRevision)
            .ThenInclude(r => r.DocumentMaster)
            .Include(c => c.CreatedByUser)
            .FirstOrDefaultAsync(c => c.Id == changeItemId)
            ?? throw new KeyNotFoundException($"Revision Change Item {changeItemId} not found.");

        if (item.DocumentRevision.RevisionStatus != DocumentRevisionStatus.Draft)
            throw new InvalidOperationException("Change items can only be modified while the revision is in Draft status.");

        var canEdit = await _authService.CanEditDraftMetadataAsync(item.DocumentRevision.DocumentMasterId, userId);
        if (!canEdit)
            throw new UnauthorizedAccessException("You do not have permission to modify change items for this draft.");

        item.SectionNumber = request.SectionNumber.Trim();
        item.SectionTitle = request.SectionTitle.Trim();
        item.DescriptionOfChange = request.DescriptionOfChange.Trim();
        item.ChangeRationale = request.ChangeRationale.Trim();
        item.ChangeCategory = request.ChangeCategory.Trim();
        item.Status = request.Status.Trim();
        item.ModifiedAt = DateTime.UtcNow;

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        await _auditEventService.RecordUserEventAsync(
            actionCode: "RevisionChangeItemUpdated",
            actionCategory: AuditActionCategory.Document,
            recordType: nameof(RevisionChangeItem),
            documentMasterId: item.DocumentRevision.DocumentMasterId,
            documentRevisionId: item.DocumentRevisionId,
            reason: $"Updated change item {item.Id}",
            entityId: item.Id.ToString(),
            changes: new[]
            {
                new AuditFieldChange("SectionNumber", null, item.SectionNumber),
                new AuditFieldChange("Status", null, item.Status)
            }
        );

        return MapChangeItemDto(item, item.CreatedByUser?.Username ?? "", item.CreatedByUser?.FullName ?? "");
    }

    public async Task DeleteChangeItemAsync(int changeItemId, int userId)
    {
        var item = await _db.RevisionChangeItems
            .Include(c => c.DocumentRevision)
            .FirstOrDefaultAsync(c => c.Id == changeItemId)
            ?? throw new KeyNotFoundException($"Revision Change Item {changeItemId} not found.");

        if (item.DocumentRevision.RevisionStatus != DocumentRevisionStatus.Draft)
            throw new InvalidOperationException("Change items can only be removed while the revision is in Draft status.");

        var canEdit = await _authService.CanEditDraftMetadataAsync(item.DocumentRevision.DocumentMasterId, userId);
        if (!canEdit)
            throw new UnauthorizedAccessException("You do not have permission to delete change items for this draft.");

        _db.RevisionChangeItems.Remove(item);
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        await _auditEventService.RecordUserEventAsync(
            actionCode: "RevisionChangeItemDeleted",
            actionCategory: AuditActionCategory.Document,
            recordType: nameof(RevisionChangeItem),
            documentMasterId: item.DocumentRevision.DocumentMasterId,
            documentRevisionId: item.DocumentRevisionId,
            reason: $"Deleted change item for section {item.SectionNumber}",
            entityId: changeItemId.ToString()
        );
    }

    public async Task<IReadOnlyList<RevisionChangeItemDto>> GetChangeItemsAsync(int revisionId, int userId)
    {
        var items = await _db.RevisionChangeItems
            .Include(c => c.CreatedByUser)
            .Where(c => c.DocumentRevisionId == revisionId)
            .OrderBy(c => c.SectionNumber)
            .ToListAsync();

        return items.Select(i => MapChangeItemDto(i, i.CreatedByUser?.Username ?? "", i.CreatedByUser?.FullName ?? "")).ToList();
    }

    public async Task<RevisionChangeItemDto> ConvertFindingToChangeItemAsync(int revisionId, int findingId, ConvertFindingRequest request, int userId)
    {
        var revision = await _db.DocumentRevisions
            .Include(r => r.DocumentMaster)
            .FirstOrDefaultAsync(r => r.Id == revisionId)
            ?? throw new KeyNotFoundException($"Document Revision {revisionId} not found.");

        if (revision.RevisionStatus != DocumentRevisionStatus.Draft)
            throw new InvalidOperationException("Cannot transfer findings to a revision that is not in Draft status.");

        var canEdit = await _authService.CanEditDraftMetadataAsync(revision.DocumentMasterId, userId);
        if (!canEdit)
            throw new UnauthorizedAccessException("You do not have permission to convert findings for this draft.");

        var finding = await _db.DocumentReviewFindings
            .FirstOrDefaultAsync(f => f.Id == findingId)
            ?? throw new KeyNotFoundException($"Review Finding {findingId} not found.");

        var item = new RevisionChangeItem
        {
            DocumentRevisionId = revisionId,
            SectionNumber = finding.SectionNumber ?? (finding.PageNumber.HasValue ? $"Page {finding.PageNumber}" : "General"),
            SectionTitle = request.SectionTitle.Trim(),
            DescriptionOfChange = finding.CommentText,
            ChangeRationale = request.ChangeRationale.Trim(),
            ChangeCategory = request.ChangeCategory.Trim(),
            Status = "Draft",
            OriginatingReviewFindingId = finding.Id,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _db.RevisionChangeItems.Add(item);
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(userId);
        await _auditEventService.RecordUserEventAsync(
            actionCode: "ReviewFindingConvertedToChangeItem",
            actionCategory: AuditActionCategory.Document,
            recordType: nameof(RevisionChangeItem),
            documentMasterId: revision.DocumentMasterId,
            documentRevisionId: revisionId,
            reason: $"Transferred review finding #{finding.Id} to structured revision change item",
            entityId: item.Id.ToString(),
            changes: new[]
            {
                new AuditFieldChange("OriginatingReviewFindingId", null, finding.Id.ToString()),
                new AuditFieldChange("SectionNumber", null, item.SectionNumber)
            }
        );

        return MapChangeItemDto(item, user?.Username ?? "", user?.FullName ?? "");
    }

    public async Task<RevisionImpactAssessmentDto> SaveImpactAssessmentAsync(int revisionId, SaveImpactAssessmentRequest request, int userId)
    {
        var revision = await _db.DocumentRevisions
            .Include(r => r.DocumentMaster)
            .Include(r => r.ImpactAssessment)
            .FirstOrDefaultAsync(r => r.Id == revisionId)
            ?? throw new KeyNotFoundException($"Document Revision {revisionId} not found.");

        if (revision.RevisionStatus != DocumentRevisionStatus.Draft)
            throw new InvalidOperationException("Impact assessment can only be saved or modified while the revision is in Draft status.");

        var canEdit = await _authService.CanEditDraftMetadataAsync(revision.DocumentMasterId, userId);
        if (!canEdit)
            throw new UnauthorizedAccessException("You do not have permission to modify impact assessment for this draft.");

        // Validation: For every category marked true, details must be provided (>= 5 chars)
        ValidateImpactDetails(request.ProcedureOrMethodImpact, request.ProcedureOrMethodDetails, "Procedure / Method");
        ValidateImpactDetails(request.TrainingImpact, request.TrainingDetails, "Training");
        ValidateImpactDetails(request.FormsOrTemplatesImpact, request.FormsOrTemplatesDetails, "Forms / Templates");
        ValidateImpactDetails(request.SpecificationsImpact, request.SpecificationsDetails, "Specifications");
        ValidateImpactDetails(request.EquipmentImpact, request.EquipmentDetails, "Equipment");
        ValidateImpactDetails(request.MaterialsOrMediaImpact, request.MaterialsOrMediaDetails, "Materials / Media");
        ValidateImpactDetails(request.ValidationImpact, request.ValidationDetails, "Validation");
        ValidateImpactDetails(request.RegulatoryCommitmentImpact, request.RegulatoryCommitmentDetails, "Regulatory Commitment");
        ValidateImpactDetails(request.RelatedDocumentsImpact, request.RelatedDocumentsDetails, "Related Documents");

        var assessment = revision.ImpactAssessment;
        bool isNew = false;
        if (assessment == null)
        {
            assessment = new RevisionImpactAssessment
            {
                DocumentRevisionId = revisionId
            };
            _db.RevisionImpactAssessments.Add(assessment);
            isNew = true;
        }

        assessment.ProcedureOrMethodImpact = request.ProcedureOrMethodImpact;
        assessment.ProcedureOrMethodDetails = request.ProcedureOrMethodDetails?.Trim();

        assessment.TrainingImpact = request.TrainingImpact;
        assessment.TrainingDetails = request.TrainingDetails?.Trim();

        assessment.FormsOrTemplatesImpact = request.FormsOrTemplatesImpact;
        assessment.FormsOrTemplatesDetails = request.FormsOrTemplatesDetails?.Trim();

        assessment.SpecificationsImpact = request.SpecificationsImpact;
        assessment.SpecificationsDetails = request.SpecificationsDetails?.Trim();

        assessment.EquipmentImpact = request.EquipmentImpact;
        assessment.EquipmentDetails = request.EquipmentDetails?.Trim();

        assessment.MaterialsOrMediaImpact = request.MaterialsOrMediaImpact;
        assessment.MaterialsOrMediaDetails = request.MaterialsOrMediaDetails?.Trim();

        assessment.ValidationImpact = request.ValidationImpact;
        assessment.ValidationDetails = request.ValidationDetails?.Trim();

        assessment.RegulatoryCommitmentImpact = request.RegulatoryCommitmentImpact;
        assessment.RegulatoryCommitmentDetails = request.RegulatoryCommitmentDetails?.Trim();

        assessment.RelatedDocumentsImpact = request.RelatedDocumentsImpact;
        assessment.RelatedDocumentsDetails = request.RelatedDocumentsDetails?.Trim();

        assessment.IsComplete = true;
        assessment.CompletedByUserId = userId;
        assessment.CompletedAt = DateTime.UtcNow;

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(userId);
        await _auditEventService.RecordUserEventAsync(
            actionCode: "RevisionImpactAssessmentSaved",
            actionCategory: AuditActionCategory.Document,
            recordType: nameof(RevisionImpactAssessment),
            documentMasterId: revision.DocumentMasterId,
            documentRevisionId: revisionId,
            reason: isNew ? "Completed initial Revision Impact Assessment" : "Updated Revision Impact Assessment",
            entityId: assessment.Id.ToString(),
            changes: new[]
            {
                new AuditFieldChange("IsComplete", null, "true"),
                new AuditFieldChange("CompletedByUserId", null, userId.ToString())
            }
        );

        return MapImpactDto(assessment, user?.Username ?? "", user?.FullName ?? "");
    }

    public async Task<RevisionImpactAssessmentDto?> GetImpactAssessmentAsync(int revisionId, int userId)
    {
        var assessment = await _db.RevisionImpactAssessments
            .Include(a => a.CompletedByUser)
            .FirstOrDefaultAsync(a => a.DocumentRevisionId == revisionId);

        if (assessment == null) return null;

        return MapImpactDto(assessment, assessment.CompletedByUser?.Username ?? "", assessment.CompletedByUser?.FullName ?? "");
    }

    // ---- Internal Helpers ----

    private static void ValidateImpactDetails(bool hasImpact, string? details, string categoryName)
    {
        if (hasImpact && (string.IsNullOrWhiteSpace(details) || details.Trim().Length < 5))
        {
            throw new ArgumentException($"Details are required for '{categoryName}' impact and must contain at least 5 characters.", nameof(details));
        }
    }

    private static string ComputeNextRevisionNumber(string currentRevisionNumber, RevisionType type)
    {
        if (string.IsNullOrWhiteSpace(currentRevisionNumber))
            return type == RevisionType.Minor ? "01.1" : "02";

        var trimmed = currentRevisionNumber.Trim();
        var dotIndex = trimmed.IndexOf('.');

        if (type == RevisionType.Major)
        {
            string majorStr = dotIndex >= 0 ? trimmed.Substring(0, dotIndex) : trimmed;
            if (int.TryParse(majorStr, out int majorInt))
            {
                int nextMajor = majorInt + 1;
                return majorStr.Length >= 2 && nextMajor < 100 ? nextMajor.ToString("D2") : nextMajor.ToString();
            }
            return $"{trimmed}.1";
        }
        else // Minor
        {
            if (dotIndex >= 0)
            {
                string majorStr = trimmed.Substring(0, dotIndex);
                string minorStr = trimmed.Substring(dotIndex + 1);
                if (int.TryParse(minorStr, out int minorInt))
                {
                    int nextMinor = minorInt + 1;
                    return $"{majorStr}.{nextMinor}";
                }
                return $"{trimmed}.1";
            }
            else
            {
                return $"{trimmed}.1";
            }
        }
    }

    private static RevisionChangeItemDto MapChangeItemDto(RevisionChangeItem i, string username, string fullName)
    {
        return new RevisionChangeItemDto(
            i.Id,
            i.DocumentRevisionId,
            i.SectionNumber,
            i.SectionTitle,
            i.DescriptionOfChange,
            i.ChangeRationale,
            i.ChangeCategory,
            i.Status,
            i.OriginatingReviewFindingId,
            i.CreatedByUserId,
            username,
            fullName,
            i.CreatedAt,
            i.ModifiedAt
        );
    }

    private static RevisionImpactAssessmentDto MapImpactDto(RevisionImpactAssessment a, string username, string fullName)
    {
        return new RevisionImpactAssessmentDto(
            a.Id,
            a.DocumentRevisionId,
            a.ProcedureOrMethodImpact,
            a.ProcedureOrMethodDetails,
            a.TrainingImpact,
            a.TrainingDetails,
            a.FormsOrTemplatesImpact,
            a.FormsOrTemplatesDetails,
            a.SpecificationsImpact,
            a.SpecificationsDetails,
            a.EquipmentImpact,
            a.EquipmentDetails,
            a.MaterialsOrMediaImpact,
            a.MaterialsOrMediaDetails,
            a.ValidationImpact,
            a.ValidationDetails,
            a.RegulatoryCommitmentImpact,
            a.RegulatoryCommitmentDetails,
            a.RelatedDocumentsImpact,
            a.RelatedDocumentsDetails,
            a.IsComplete,
            a.CompletedByUserId,
            username,
            fullName,
            a.CompletedAt
        );
    }

    private async Task<DocumentRevisionDto> MapRevisionDtoAsync(int revisionId)
    {
        var r = await _db.DocumentRevisions
            .Include(r => r.CreatedByUser)
            .Include(r => r.CancelledByUser)
            .Include(r => r.Files)
            .ThenInclude(f => f.UploadedByUser)
            .Include(r => r.DocumentMaster)
            .FirstOrDefaultAsync(r => r.Id == revisionId)
            ?? throw new KeyNotFoundException($"Revision {revisionId} not found.");

        var files = r.Files.OrderByDescending(f => f.UploadedAt).Select(f => new RevisionFileDto(
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
            f.UploadedByUser?.FullName ?? ""
        )).ToList();

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
            files
        );
    }
}
