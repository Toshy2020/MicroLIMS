using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services.DocumentControl;

public class DocumentAcknowledgementService : IDocumentAcknowledgementService
{
    private readonly MicroLimsDbContext _db;
    private readonly IAuditEventService _audit;
    private readonly ILogger<DocumentAcknowledgementService> _logger;

    public const string DefaultAcknowledgementStatement =
        "I confirm that I have read, understood, and agree to adhere to the contents of this controlled document revision.";

    public DocumentAcknowledgementService(
        MicroLimsDbContext db,
        IAuditEventService audit,
        ILogger<DocumentAcknowledgementService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<AcknowledgementPresentationDto> PresentAcknowledgementContextAsync(
        int assignmentId,
        int actingUserId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _db.DocumentTrainingAssignments
            .Include(a => a.DocumentMaster)
            .Include(a => a.DocumentRevision)
                .ThenInclude(r => r.Files)
            .Include(a => a.AssignedUser)
            .FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);

        if (assignment == null)
            throw new KeyNotFoundException($"Training assignment {assignmentId} not found.");

        var master = assignment.DocumentMaster;
        var revision = assignment.DocumentRevision;
        var user = assignment.AssignedUser;

        // Controlled PDF resolution
        var controlledFile = revision.Files
            .FirstOrDefault(f => f.FileRole == FileRole.ControlledPdf && f.IsActive);

        // Statement resolution hierarchy (Master-specific > DocumentType-specific > Default)
        var statementText = await ResolveStatementTextAsync(master.Id, master.DocumentTypeId, cancellationToken);

        // Server-side eligibility validation
        bool canAcknowledge = true;
        string? validationMessage = null;

        if (actingUserId != assignment.AssignedUserId)
        {
            canAcknowledge = false;
            validationMessage = "Only the assigned user may acknowledge this document revision.";
        }
        else if (assignment.Status == TrainingAssignmentStatus.Acknowledged)
        {
            canAcknowledge = false;
            validationMessage = "This assignment has already been legally acknowledged.";
        }
        else if (assignment.Status == TrainingAssignmentStatus.SupersededIncomplete)
        {
            canAcknowledge = false;
            validationMessage = "This assignment was superseded by a newer revision and cannot be acknowledged.";
        }
        else if (assignment.Status == TrainingAssignmentStatus.Cancelled)
        {
            canAcknowledge = false;
            validationMessage = "This document assignment was cancelled due to document obsolescence.";
        }
        else if (master.RecordStatus != DocumentRecordStatus.Active)
        {
            canAcknowledge = false;
            validationMessage = $"The document is currently in {master.RecordStatus} status.";
        }
        else if (revision.RevisionStatus != DocumentRevisionStatus.Effective)
        {
            canAcknowledge = false;
            validationMessage = $"Document revision is in {revision.RevisionStatus} state; acknowledgement is only permitted for Effective revisions.";
        }

        return new AcknowledgementPresentationDto(
            AssignmentId: assignment.Id,
            DocumentMasterId: master.Id,
            CompanyDocumentCode: master.CompanyDocumentCode,
            DocumentTitle: master.Title,
            DocumentRevisionId: revision.Id,
            RevisionNumber: revision.RevisionNumber,
            AssignedUserId: user.Id,
            AssignedUserName: user.FullName,
            AssignmentStatus: assignment.Status,
            DueDateUtc: assignment.DueDateUtc,
            ControlledFileId: controlledFile?.Id,
            ControlledFileName: controlledFile?.FileName,
            ControlledFileSha256: controlledFile?.ContentSha256,
            LegalStatementText: statementText,
            CanAcknowledge: canAcknowledge,
            ValidationMessage: validationMessage
        );
    }

    public async Task<AcknowledgementResultDto> AcknowledgeAssignmentAsync(
        AcknowledgementSubmissionRequest request,
        int actingUserId,
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Explicit conscious acknowledgement requirement (DC-URS-189, DC-URS-190)
        if (!request.ConfirmedLegalStatement)
        {
            throw new InvalidOperationException(
                "Explicit confirmation of the legal acknowledgement statement is required. Reading or viewing alone does not constitute legal acknowledgement.");
        }

        var nowUtc = utcNowOverride ?? DateTime.UtcNow;

        // 2. Re-read fresh assignment within transaction boundary
        var isRelational = _db.Database.IsRelational();
        AcknowledgementResultDto? result = null;

        async Task ExecuteAcknowledgeCoreAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx)
        {
            var assignment = await _db.DocumentTrainingAssignments
                .Include(a => a.DocumentMaster)
                .Include(a => a.DocumentRevision)
                    .ThenInclude(r => r.Files)
                .FirstOrDefaultAsync(a => a.Id == request.AssignmentId, cancellationToken);

            if (assignment == null)
                throw new KeyNotFoundException($"Training assignment {request.AssignmentId} not found.");

            var master = assignment.DocumentMaster;
            var revision = assignment.DocumentRevision;

            // 3. User attribution and ownership validation (DC-URS-086, DC-URS-189)
            if (assignment.AssignedUserId != actingUserId)
            {
                _db.CurrentUserId = actingUserId;
                await _audit.RecordUserEventAsync(
                    actionCode: "AcknowledgementRejected",
                    actionCategory: AuditActionCategory.Security,
                    recordType: nameof(DocumentAcknowledgementRecord),
                    documentMasterId: master.Id,
                    documentRevisionId: revision.Id,
                    reason: $"SECURITY VIOLATION: User {actingUserId} attempted to acknowledge assignment {assignment.Id} belonging to User {assignment.AssignedUserId}.",
                    entityId: assignment.Id.ToString(),
                    cancellationToken: cancellationToken);

                throw new UnauthorizedAccessException("You are not authorized to acknowledge an assignment assigned to another user.");
            }

            // 4. Revision-specific invariant: user must acknowledge the exact assigned revision (DC-URS-192)
            if (assignment.DocumentRevisionId != request.DocumentRevisionId)
            {
                _db.CurrentUserId = actingUserId;
                await _audit.RecordUserEventAsync(
                    actionCode: "AcknowledgementRejected",
                    actionCategory: AuditActionCategory.Document,
                    recordType: nameof(DocumentAcknowledgementRecord),
                    documentMasterId: master.Id,
                    documentRevisionId: revision.Id,
                    reason: $"REVISION MISMATCH: Submitted revision ID {request.DocumentRevisionId} does not match assigned revision ID {assignment.DocumentRevisionId}.",
                    entityId: assignment.Id.ToString(),
                    cancellationToken: cancellationToken);

                throw new InvalidOperationException(
                    $"Revision mismatch: Assignment is for Revision ID {assignment.DocumentRevisionId}, but Revision ID {request.DocumentRevisionId} was submitted.");
            }

            // 5. Document Master and Revision state validation
            if (master.RecordStatus != DocumentRecordStatus.Active)
            {
                throw new InvalidOperationException(
                    $"Cannot acknowledge assignment because Document {master.CompanyDocumentCode} is in {master.RecordStatus} status.");
            }

            if (revision.RevisionStatus != DocumentRevisionStatus.Effective)
            {
                throw new InvalidOperationException(
                    $"Cannot acknowledge assignment because Revision {revision.RevisionNumber} is in {revision.RevisionStatus} state (must be Effective).");
            }

            // 6. State machine and idempotency validation
            if (assignment.Status == TrainingAssignmentStatus.Acknowledged)
            {
                // Check if evidentiary record already exists (Idempotent response)
                var existingRecord = await _db.DocumentAcknowledgementRecords
                    .FirstOrDefaultAsync(r => r.DocumentTrainingAssignmentId == assignment.Id, cancellationToken);

                if (existingRecord != null)
                {
                    _logger.LogInformation(
                        "Assignment {AssignmentId} already has recorded acknowledgement {RecordId}. Returning existing evidence idempotently.",
                        assignment.Id, existingRecord.Id);

                    await _audit.RecordUserEventAsync(
                        actionCode: "AcknowledgementAlreadyRecorded",
                        actionCategory: AuditActionCategory.Document,
                        recordType: nameof(DocumentAcknowledgementRecord),
                        documentMasterId: master.Id,
                        documentRevisionId: revision.Id,
                        reason: $"Repeat acknowledgement submission for Assignment {assignment.Id} handled idempotently.",
                        entityId: existingRecord.Id.ToString(),
                        cancellationToken: cancellationToken);

                    result = new AcknowledgementResultDto(
                        AcknowledgementRecordId: existingRecord.Id,
                        AssignmentId: assignment.Id,
                        DocumentMasterId: master.Id,
                        DocumentRevisionId: revision.Id,
                        RevisionNumber: revision.RevisionNumber,
                        UserId: actingUserId,
                        StatementText: existingRecord.StatementText,
                        AcknowledgedAtUtc: existingRecord.AcknowledgedAtUtc,
                        Status: TrainingAssignmentStatus.Acknowledged,
                        ControlledFileHash: existingRecord.ControlledFileHash
                    );
                    return;
                }
            }

            if (assignment.Status == TrainingAssignmentStatus.SupersededIncomplete)
            {
                throw new InvalidOperationException(
                    "Cannot acknowledge an assignment that has been terminally closed as SupersededIncomplete.");
            }

            if (assignment.Status == TrainingAssignmentStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    "Cannot acknowledge an assignment that has been terminally closed as Cancelled due to document obsolescence.");
            }

            // Allowed initial states: Assigned, Reading, Overdue
            if (assignment.Status != TrainingAssignmentStatus.Assigned
                && assignment.Status != TrainingAssignmentStatus.Reading
                && assignment.Status != TrainingAssignmentStatus.Overdue)
            {
                throw new InvalidOperationException(
                    $"Assignment is in an invalid state for acknowledgement: {assignment.Status}.");
            }

            // 7. Controlled PDF verification
            var controlledFile = revision.Files
                .FirstOrDefault(f => f.FileRole == FileRole.ControlledPdf && f.IsActive);

            // 8. Capture exact configured statement text at acknowledgement time (DC-URS-191)
            var statementText = await ResolveStatementTextAsync(master.Id, master.DocumentTypeId, cancellationToken);

            // 9. Create immutable DocumentAcknowledgementRecord
            var ackRecord = new DocumentAcknowledgementRecord
            {
                DocumentTrainingAssignmentId = assignment.Id,
                DocumentMasterId = master.Id,
                DocumentRevisionId = revision.Id,
                AcknowledgedByUserId = actingUserId,
                StatementText = statementText,
                AcknowledgedAtUtc = nowUtc,
                ControlledFileId = controlledFile?.Id,
                ControlledFileHash = controlledFile?.ContentSha256,
                Comments = request.Comments,
                ClientIpAddress = request.ClientIpAddress,
                UserAgent = request.UserAgent,
                CreatedAtUtc = nowUtc
            };

            _db.DocumentAcknowledgementRecords.Add(ackRecord);

            // 10. Transition assignment state to Acknowledged
            var priorStatus = assignment.Status;
            assignment.Status = TrainingAssignmentStatus.Acknowledged;
            assignment.AcknowledgedAtUtc = nowUtc;
            assignment.AcknowledgedByUserId = actingUserId;
            assignment.StatementText = statementText;
            assignment.CompletedAtUtc = nowUtc;
            assignment.ModifiedAtUtc = nowUtc;
            assignment.ModifiedByUserId = actingUserId;

            _db.CurrentUserId = actingUserId;
            await _db.SaveChangesAsync(cancellationToken);

            // 11. Record attributable GxP audit event (DC-URS-086, DC-URS-189)
            await _audit.RecordUserEventAsync(
                actionCode: "ReadingAssignmentAcknowledged",
                actionCategory: AuditActionCategory.Document,
                recordType: nameof(DocumentAcknowledgementRecord),
                documentMasterId: master.Id,
                documentRevisionId: revision.Id,
                reason: $"Read-and-understand acknowledgement formally submitted for Revision {revision.RevisionNumber}.",
                changes: new[]
                {
                    new AuditFieldChange("Status", priorStatus.ToString(), TrainingAssignmentStatus.Acknowledged.ToString()),
                    new AuditFieldChange("StatementText", null, statementText),
                    new AuditFieldChange("AcknowledgedAtUtc", null, nowUtc.ToString("o")),
                    new AuditFieldChange("ControlledFileHash", null, controlledFile?.ContentSha256)
                },
                entityId: ackRecord.Id.ToString(),
                cancellationToken: cancellationToken);

            if (tx != null)
            {
                await tx.CommitAsync(cancellationToken);
            }

            result = new AcknowledgementResultDto(
                AcknowledgementRecordId: ackRecord.Id,
                AssignmentId: assignment.Id,
                DocumentMasterId: master.Id,
                DocumentRevisionId: revision.Id,
                RevisionNumber: revision.RevisionNumber,
                UserId: actingUserId,
                StatementText: statementText,
                AcknowledgedAtUtc: nowUtc,
                Status: TrainingAssignmentStatus.Acknowledged,
                ControlledFileHash: controlledFile?.ContentSha256
            );

            _logger.LogInformation(
                "Successfully persisted acknowledgement record {RecordId} for User {UserId}, Assignment {AssignmentId}, Rev {RevisionNumber}.",
                ackRecord.Id, actingUserId, assignment.Id, revision.RevisionNumber);
        }

        if (isRelational)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
                await ExecuteAcknowledgeCoreAsync(tx);
            });
        }
        else
        {
            await ExecuteAcknowledgeCoreAsync(null);
        }

        return result!;
    }

    public async Task<AcknowledgementResultDto?> GetAcknowledgementStatusAsync(
        int assignmentId,
        int actingUserId,
        CancellationToken cancellationToken = default)
    {
        var record = await _db.DocumentAcknowledgementRecords
            .Include(r => r.DocumentRevision)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.DocumentTrainingAssignmentId == assignmentId, cancellationToken);

        if (record == null)
            return null;

        return new AcknowledgementResultDto(
            AcknowledgementRecordId: record.Id,
            AssignmentId: record.DocumentTrainingAssignmentId,
            DocumentMasterId: record.DocumentMasterId,
            DocumentRevisionId: record.DocumentRevisionId,
            RevisionNumber: record.DocumentRevision.RevisionNumber,
            UserId: record.AcknowledgedByUserId,
            StatementText: record.StatementText,
            AcknowledgedAtUtc: record.AcknowledgedAtUtc,
            Status: TrainingAssignmentStatus.Acknowledged,
            ControlledFileHash: record.ControlledFileHash
        );
    }

    public async Task<ReadingProgressResultDto> RecordReadingProgressAsync(
        ReadingProgressUpdateRequest request,
        int actingUserId,
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _db.DocumentTrainingAssignments
            .FirstOrDefaultAsync(a => a.Id == request.AssignmentId, cancellationToken);

        if (assignment == null)
            throw new KeyNotFoundException($"Training assignment {request.AssignmentId} not found.");

        if (assignment.AssignedUserId != actingUserId)
            throw new UnauthorizedAccessException("You cannot record reading progress for another user's assignment.");

        if (assignment.DocumentRevisionId != request.DocumentRevisionId)
            throw new InvalidOperationException("Revision ID does not match assigned revision ID.");

        var nowUtc = utcNowOverride ?? DateTime.UtcNow;

        // If assignment was in Assigned state, advance to Reading state
        if (assignment.Status == TrainingAssignmentStatus.Assigned)
        {
            assignment.Status = TrainingAssignmentStatus.Reading;
            assignment.ModifiedAtUtc = nowUtc;
            assignment.ModifiedByUserId = actingUserId;

            _db.CurrentUserId = actingUserId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // NOTE (DC-URS-190): Even if ProgressPercentage is 100%, assignment is NEVER transitioned
        // to Acknowledged. Completion is governed solely by conscious acknowledgement action.
        return new ReadingProgressResultDto(
            AssignmentId: assignment.Id,
            Status: assignment.Status,
            ProgressPercentage: request.ProgressPercentage,
            Note: "Reading progress is strictly informational and does not constitute legal acknowledgement."
        );
    }

    private async Task<string> ResolveStatementTextAsync(
        int documentMasterId,
        int documentTypeId,
        CancellationToken cancellationToken)
    {
        var config = await _db.DocumentTrainingConfigurations
            .AsNoTracking()
            .Where(c => c.DocumentMasterId == documentMasterId || c.DocumentTypeId == documentTypeId)
            .OrderByDescending(c => c.DocumentMasterId.HasValue) // Master-specific overrides DocumentType-specific
            .FirstOrDefaultAsync(cancellationToken);

        if (config != null && !string.IsNullOrWhiteSpace(config.DefaultAcknowledgementStatement))
        {
            return config.DefaultAcknowledgementStatement;
        }

        return DefaultAcknowledgementStatement;
    }
}
