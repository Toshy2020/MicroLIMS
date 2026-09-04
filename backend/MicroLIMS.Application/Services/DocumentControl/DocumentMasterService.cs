using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;

namespace MicroLIMS.Application.Services.DocumentControl;

public class DocumentMasterService : IDocumentMasterService
{
    private readonly MicroLimsDbContext _db;
    private readonly IDatabaseSequenceHelper _sequenceHelper;
    private readonly IAuditEventService _auditEventService;
    private readonly IDocumentAuthorizationService _authService;

    public DocumentMasterService(
        MicroLimsDbContext db,
        IDatabaseSequenceHelper sequenceHelper,
        IAuditEventService auditEventService,
        IDocumentAuthorizationService authService)
    {
        _db = db;
        _sequenceHelper = sequenceHelper;
        _auditEventService = auditEventService;
        _authService = authService;
    }

    public async Task<DocumentMasterDto> RegisterDocumentMasterAsync(RegisterDocumentMasterRequest request, int userId)
    {
        // 1. Validation
        if (string.IsNullOrWhiteSpace(request.CompanyDocumentCode))
            throw new ArgumentException("Company Document Code is required.", nameof(request.CompanyDocumentCode));

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Document Title is required.", nameof(request.Title));

        if (request.DocumentTypeId <= 0)
            throw new ArgumentException("A valid Document Type is required.", nameof(request.DocumentTypeId));

        if (request.DepartmentId <= 0)
            throw new ArgumentException("A valid Department is required.", nameof(request.DepartmentId));

        if (request.SectionId <= 0)
            throw new ArgumentException("A valid Section is required.", nameof(request.SectionId));

        if (request.DocumentOwnerUserId <= 0)
            throw new ArgumentException("A valid Document Owner is required.", nameof(request.DocumentOwnerUserId));

        var trimmedCode = request.CompanyDocumentCode.Trim();

        // 2. Validate Foreign Keys
        var docType = await _db.DocumentTypes.FindAsync(request.DocumentTypeId)
            ?? throw new InvalidOperationException($"Document Type {request.DocumentTypeId} not found.");
        if (!docType.IsActive)
            throw new InvalidOperationException($"Document Type '{docType.Name}' is deactivated.");

        var dept = await _db.DocumentDepartments.FindAsync(request.DepartmentId)
            ?? throw new InvalidOperationException($"Department {request.DepartmentId} not found.");
        if (!dept.IsActive)
            throw new InvalidOperationException($"Department '{dept.Name}' is deactivated.");

        var section = await _db.DocumentSections.FindAsync(request.SectionId)
            ?? throw new InvalidOperationException($"Section {request.SectionId} not found.");
        if (!section.IsActive)
            throw new InvalidOperationException($"Section '{section.Name}' is deactivated.");

        if (section.DepartmentId != dept.Id)
            throw new InvalidOperationException($"Section '{section.Name}' does not belong to department '{dept.Name}'.");

        var ownerUser = await _db.Users.FindAsync(request.DocumentOwnerUserId)
            ?? throw new InvalidOperationException($"Owner user {request.DocumentOwnerUserId} not found.");
        if (!ownerUser.IsActive)
            throw new InvalidOperationException($"Owner user '{ownerUser.FullName}' is inactive.");

        // 3. Uniqueness of CompanyDocumentCode among Active documents
        var codeExists = await _db.DocumentMasters
            .AnyAsync(d => d.RecordStatus == DocumentRecordStatus.Active &&
                           d.CompanyDocumentCode.ToLower() == trimmedCode.ToLower());
        if (codeExists)
            throw new InvalidOperationException($"A document with Company Document Code '{trimmedCode}' already exists and is active.");

        // 4. Generate MicroLimsDocumentId
        var seqVal = await _sequenceHelper.GetNextSequenceValueAsync("document_number_seq");
        var numConfig = await _db.DocumentNumberingConfigurations.FirstOrDefaultAsync(c => c.IsEnabled)
            ?? new DocumentNumberingConfiguration { Prefix = "DOC-", NumberFormat = "0000000" };

        var microLimsId = $"{numConfig.Prefix}{seqVal.ToString(numConfig.NumberFormat)}";
        var now = DateTime.UtcNow;

        // 5. Create DocumentMaster & Initial Draft Revision
        var master = new DocumentMaster
        {
            MicroLimsDocumentId = microLimsId,
            CompanyDocumentCode = trimmedCode,
            Title = request.Title.Trim(),
            DocumentTypeId = request.DocumentTypeId,
            DepartmentId = request.DepartmentId,
            SectionId = request.SectionId,
            DocumentOwnerUserId = request.DocumentOwnerUserId,
            Confidentiality = request.Confidentiality,
            Category = request.Category?.Trim(),
            RecordOrigin = RecordOrigin.Native,
            RecordStatus = DocumentRecordStatus.Active,
            CurrentEffectiveRevisionId = null,
            CreatedAt = now,
            CreatedByUserId = userId
        };
        _db.DocumentMasters.Add(master);
        await _db.SaveChangesAsync(); // Generates master.Id

        var initialRevNumber = string.IsNullOrWhiteSpace(request.InitialRevisionNumber) ? "01" : request.InitialRevisionNumber.Trim();
        var revision = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = initialRevNumber,
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Draft,
            ReviewCycleMonths = request.ReviewCycleMonths ?? docType.DefaultReviewCycleMonths,
            RevisionType = RevisionType.Major,
            ReasonForRevision = "Initial Document Registration",
            RecordOrigin = RecordOrigin.Native,
            CreatedAt = now,
            CreatedByUserId = userId
        };
        _db.DocumentRevisions.Add(revision);

        // 6. Keywords
        if (request.Keywords != null && request.Keywords.Count > 0)
        {
            foreach (var kw in request.Keywords.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct())
            {
                _db.DocumentKeywords.Add(new DocumentKeyword
                {
                    DocumentMasterId = master.Id,
                    Keyword = kw.Trim()
                });
            }
        }

        // 7. Initial Assignments
        if (request.InitialAssignments != null && request.InitialAssignments.Count > 0)
        {
            foreach (var assign in request.InitialAssignments)
            {
                var targetUser = await _db.Users.FindAsync(assign.UserId);
                if (targetUser != null && targetUser.IsActive)
                {
                    _db.DocumentMasterAssignments.Add(new DocumentMasterAssignment
                    {
                        DocumentMasterId = master.Id,
                        UserId = assign.UserId,
                        AssignmentRole = assign.AssignmentRole,
                        IsActive = true,
                        AssignedAt = now,
                        AssignedByUserId = userId
                    });
                }
            }
        }

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        // 8. Record Audit Event
        var changes = new List<AuditFieldChange>
        {
            new("MicroLimsDocumentId", null, master.MicroLimsDocumentId),
            new("CompanyDocumentCode", null, master.CompanyDocumentCode),
            new("Title", null, master.Title),
            new("DocumentTypeId", null, master.DocumentTypeId.ToString()),
            new("DepartmentId", null, master.DepartmentId.ToString()),
            new("SectionId", null, master.SectionId.ToString()),
            new("DocumentOwnerUserId", null, master.DocumentOwnerUserId.ToString()),
            new("Confidentiality", null, master.Confidentiality.ToString()),
            new("RecordOrigin", null, master.RecordOrigin.ToString()),
            new("InitialRevisionNumber", null, revision.RevisionNumber)
        };

        await _auditEventService.RecordUserEventAsync(
            actionCode: "DocumentMasterRegistered",
            actionCategory: AuditActionCategory.Document,
            recordType: nameof(DocumentMaster),
            documentMasterId: master.Id,
            documentRevisionId: revision.Id,
            reason: "Initial Document Registration",
            changes: changes,
            entityId: master.Id.ToString());

        return await GetByIdAsync(master.Id, userId);
    }

    public async Task<DocumentMasterDto> GetByIdAsync(int id, int userId)
    {
        var canView = await _authService.CanViewDocumentAsync(id, userId);
        if (!canView)
            throw new UnauthorizedAccessException("You do not have permission to view this document.");

        var master = await _db.DocumentMasters
            .Include(d => d.DocumentType)
            .Include(d => d.Department)
            .Include(d => d.Section)
            .Include(d => d.DocumentOwnerUser)
            .Include(d => d.CreatedByUser)
            .Include(d => d.ModifiedByUser)
            .Include(d => d.VoidedByUser)
            .Include(d => d.Keywords)
            .Include(d => d.Assignments).ThenInclude(a => a.User)
            .Include(d => d.Assignments).ThenInclude(a => a.AssignedByUser)
            .Include(d => d.Revisions).ThenInclude(r => r.CreatedByUser)
            .Include(d => d.Revisions).ThenInclude(r => r.CancelledByUser)
            .Include(d => d.Revisions).ThenInclude(r => r.Files).ThenInclude(f => f.UploadedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException($"Document Master {id} not found.");

        return MapToDto(master);
    }

    public async Task<DocumentLibraryResponse> GetLibraryAsync(DocumentLibraryFilterRequest filter, int userId)
    {
        var query = _db.DocumentMasters
            .Include(d => d.DocumentType)
            .Include(d => d.Department)
            .Include(d => d.Section)
            .Include(d => d.DocumentOwnerUser)
            .Include(d => d.Keywords)
            .Include(d => d.Revisions).ThenInclude(r => r.Files)
            .AsNoTracking()
            .AsQueryable();

        // Check if user is allowed to view cancelled/voided documents
        var canViewPrivileged = await _authService.CanQueryGlobalAuditAsync(userId);
        if (!filter.IncludeCancelledAndVoided || !canViewPrivileged)
        {
            query = query.Where(d => d.RecordStatus == DocumentRecordStatus.Active);
        }

        // Filters
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(d => d.CompanyDocumentCode.ToLower().Contains(term) ||
                                     d.MicroLimsDocumentId.ToLower().Contains(term) ||
                                     d.Title.ToLower().Contains(term) ||
                                     d.Keywords.Any(k => k.Keyword.ToLower().Contains(term)));
        }

        if (filter.DocumentTypeId.HasValue)
            query = query.Where(d => d.DocumentTypeId == filter.DocumentTypeId.Value);

        if (filter.DepartmentId.HasValue)
            query = query.Where(d => d.DepartmentId == filter.DepartmentId.Value);

        if (filter.SectionId.HasValue)
            query = query.Where(d => d.SectionId == filter.SectionId.Value);

        if (filter.OwnerUserId.HasValue)
            query = query.Where(d => d.DocumentOwnerUserId == filter.OwnerUserId.Value);

        if (filter.RecordStatus.HasValue)
            query = query.Where(d => d.RecordStatus == filter.RecordStatus.Value);

        if (filter.RecordOrigin.HasValue)
            query = query.Where(d => d.RecordOrigin == filter.RecordOrigin.Value);

        if (filter.RevisionStatus.HasValue)
        {
            query = query.Where(d => d.Revisions.Any(r => r.RevisionStatus == filter.RevisionStatus.Value));
        }

        if (filter.IsOverdue.HasValue)
        {
            var nowUtc = DateTime.UtcNow;
            if (filter.IsOverdue.Value)
            {
                query = query.Where(d => d.CurrentEffectiveRevisionId != null &&
                                         d.Revisions.Any(r => r.Id == d.CurrentEffectiveRevisionId &&
                                                              r.RevisionStatus == DocumentRevisionStatus.Effective &&
                                                              r.NextReviewDate != null &&
                                                              r.NextReviewDate < nowUtc));
            }
            else
            {
                query = query.Where(d => d.CurrentEffectiveRevisionId == null ||
                                         !d.Revisions.Any(r => r.Id == d.CurrentEffectiveRevisionId &&
                                                               r.RevisionStatus == DocumentRevisionStatus.Effective &&
                                                               r.NextReviewDate != null &&
                                                               r.NextReviewDate < nowUtc));
            }
        }

        // Sorting
        query = filter.SortBy?.ToLower() switch
        {
            "code" => filter.SortDescending ? query.OrderByDescending(d => d.CompanyDocumentCode) : query.OrderBy(d => d.CompanyDocumentCode),
            "id" => filter.SortDescending ? query.OrderByDescending(d => d.MicroLimsDocumentId) : query.OrderBy(d => d.MicroLimsDocumentId),
            "title" => filter.SortDescending ? query.OrderByDescending(d => d.Title) : query.OrderBy(d => d.Title),
            _ => filter.SortDescending ? query.OrderByDescending(d => d.CreatedAt) : query.OrderBy(d => d.CreatedAt)
        };

        var totalCount = await query.CountAsync();
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var masters = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = masters.Select(m =>
        {
            var curRev = m.CurrentEffectiveRevisionId.HasValue
                ? m.Revisions.FirstOrDefault(r => r.Id == m.CurrentEffectiveRevisionId.Value)
                : m.Revisions.OrderByDescending(r => r.RevisionSequence).FirstOrDefault();

            var hasPdf = curRev?.Files.Any(f => f.FileRole == FileRole.ControlledPdf && f.IsActive) ?? false;
            var hasDocx = curRev?.Files.Any(f => f.FileRole == FileRole.SourceFile && f.IsActive) ?? false;

            return new DocumentMasterSummaryDto(
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
                curRev?.EffectiveDate,
                curRev?.NextReviewDate,
                hasPdf,
                hasDocx,
                m.CreatedAt
            );
        }).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new DocumentLibraryResponse(items, totalCount, page, pageSize, totalPages);
    }

    public async Task<DocumentMasterDto> UpdateDraftMetadataAsync(int id, UpdateDocumentMasterDraftRequest request, int userId)
    {
        var canEdit = await _authService.CanEditDraftMetadataAsync(id, userId);
        if (!canEdit)
            throw new UnauthorizedAccessException("You do not have permission to edit metadata for this document.");

        var master = await _db.DocumentMasters
            .Include(d => d.Revisions)
            .Include(d => d.Keywords)
            .FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException($"Document Master {id} not found.");

        if (master.RecordStatus == DocumentRecordStatus.Void)
            throw new InvalidOperationException("A voided document cannot be edited.");

        // In Release 1a, master metadata can only be directly edited if the document has never held an effective revision
        if (master.CurrentEffectiveRevisionId.HasValue || master.Revisions.Any(r => r.RevisionStatus == DocumentRevisionStatus.Effective || r.RevisionStatus == DocumentRevisionStatus.Superseded))
        {
            throw new InvalidOperationException("Metadata for an effective document cannot be edited directly. Create a new revision instead.");
        }

        var trimmedCode = request.CompanyDocumentCode.Trim();
        if (string.IsNullOrWhiteSpace(trimmedCode))
            throw new ArgumentException("Company Document Code is required.", nameof(request.CompanyDocumentCode));

        if (!string.Equals(master.CompanyDocumentCode, trimmedCode, StringComparison.OrdinalIgnoreCase))
        {
            var codeExists = await _db.DocumentMasters
                .AnyAsync(d => d.Id != id &&
                               d.RecordStatus == DocumentRecordStatus.Active &&
                               d.CompanyDocumentCode.ToLower() == trimmedCode.ToLower());
            if (codeExists)
                throw new InvalidOperationException($"Company Document Code '{trimmedCode}' is already in use by another active document.");
        }

        var docType = await _db.DocumentTypes.FindAsync(request.DocumentTypeId)
            ?? throw new InvalidOperationException($"Document Type {request.DocumentTypeId} not found.");
        if (!docType.IsActive)
            throw new InvalidOperationException($"Document Type '{docType.Name}' is deactivated.");

        var dept = await _db.DocumentDepartments.FindAsync(request.DepartmentId)
            ?? throw new InvalidOperationException($"Department {request.DepartmentId} not found.");
        if (!dept.IsActive)
            throw new InvalidOperationException($"Department '{dept.Name}' is deactivated.");

        var section = await _db.DocumentSections.FindAsync(request.SectionId)
            ?? throw new InvalidOperationException($"Section {request.SectionId} not found.");
        if (!section.IsActive)
            throw new InvalidOperationException($"Section '{section.Name}' is deactivated.");

        if (section.DepartmentId != dept.Id)
            throw new InvalidOperationException($"Section '{section.Name}' does not belong to department '{dept.Name}'.");

        var ownerUser = await _db.Users.FindAsync(request.DocumentOwnerUserId)
            ?? throw new InvalidOperationException($"Owner user {request.DocumentOwnerUserId} not found.");
        if (!ownerUser.IsActive)
            throw new InvalidOperationException($"Owner user '{ownerUser.FullName}' is inactive.");

        var changes = new List<AuditFieldChange>();
        if (master.CompanyDocumentCode != trimmedCode)
        {
            changes.Add(new("CompanyDocumentCode", master.CompanyDocumentCode, trimmedCode));
            master.CompanyDocumentCode = trimmedCode;
        }

        if (master.Title != request.Title.Trim())
        {
            changes.Add(new("Title", master.Title, request.Title.Trim()));
            master.Title = request.Title.Trim();
        }

        if (master.DocumentTypeId != request.DocumentTypeId)
        {
            changes.Add(new("DocumentTypeId", master.DocumentTypeId.ToString(), request.DocumentTypeId.ToString()));
            master.DocumentTypeId = request.DocumentTypeId;
        }

        if (master.DepartmentId != request.DepartmentId)
        {
            changes.Add(new("DepartmentId", master.DepartmentId.ToString(), request.DepartmentId.ToString()));
            master.DepartmentId = request.DepartmentId;
        }

        if (master.SectionId != request.SectionId)
        {
            changes.Add(new("SectionId", master.SectionId.ToString(), request.SectionId.ToString()));
            master.SectionId = request.SectionId;
        }

        if (master.DocumentOwnerUserId != request.DocumentOwnerUserId)
        {
            changes.Add(new("DocumentOwnerUserId", master.DocumentOwnerUserId.ToString(), request.DocumentOwnerUserId.ToString()));
            master.DocumentOwnerUserId = request.DocumentOwnerUserId;
        }

        if (master.Confidentiality != request.Confidentiality)
        {
            changes.Add(new("Confidentiality", master.Confidentiality.ToString(), request.Confidentiality.ToString()));
            master.Confidentiality = request.Confidentiality;
        }

        if (master.Category != request.Category?.Trim())
        {
            changes.Add(new("Category", master.Category, request.Category?.Trim()));
            master.Category = request.Category?.Trim();
        }

        // Keywords update
        if (request.Keywords != null)
        {
            _db.DocumentKeywords.RemoveRange(master.Keywords);
            foreach (var kw in request.Keywords.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct())
            {
                master.Keywords.Add(new DocumentKeyword
                {
                    DocumentMasterId = master.Id,
                    Keyword = kw.Trim()
                });
            }
        }

        master.ModifiedAt = DateTime.UtcNow;
        master.ModifiedByUserId = userId;

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        if (changes.Count > 0)
        {
            await _auditEventService.RecordUserEventAsync(
                actionCode: "DocumentMasterMetadataUpdated",
                actionCategory: AuditActionCategory.Document,
                recordType: nameof(DocumentMaster),
                documentMasterId: master.Id,
                changes: changes,
                entityId: master.Id.ToString());
        }

        return await GetByIdAsync(master.Id, userId);
    }

    public async Task<DocumentMasterDto> VoidMasterAsync(int id, VoidDocumentMasterRequest request, int userId)
    {
        var canVoid = await _authService.CanVoidDocumentMasterAsync(id, userId);
        if (!canVoid)
            throw new UnauthorizedAccessException("Only the Document Controller can void a Document Master.");

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
            throw new ArgumentException("A mandatory void reason of at least 10 characters is required.", nameof(request.Reason));

        var master = await _db.DocumentMasters
            .Include(d => d.Revisions)
            .FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException($"Document Master {id} not found.");

        if (master.RecordStatus == DocumentRecordStatus.Void)
            throw new InvalidOperationException("Document Master is already voided.");

        if (master.CurrentEffectiveRevisionId.HasValue || master.Revisions.Any(r => r.RevisionStatus == DocumentRevisionStatus.Effective || r.RevisionStatus == DocumentRevisionStatus.Superseded))
        {
            throw new InvalidOperationException("A Document Master that has held an effective revision cannot be voided. It must be retired through obsolescence.");
        }

        var trimmedReason = request.Reason.Trim();
        var previousStatus = master.RecordStatus.ToString();

        master.RecordStatus = DocumentRecordStatus.Void;
        master.VoidedAt = DateTime.UtcNow;
        master.VoidedByUserId = userId;
        master.VoidReason = trimmedReason;

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var changes = new List<AuditFieldChange>
        {
            new("RecordStatus", previousStatus, DocumentRecordStatus.Void.ToString()),
            new("VoidReason", null, trimmedReason)
        };

        await _auditEventService.RecordUserEventAsync(
            actionCode: "DocumentMasterVoided",
            actionCategory: AuditActionCategory.Document,
            recordType: nameof(DocumentMaster),
            documentMasterId: master.Id,
            reason: trimmedReason,
            changes: changes,
            entityId: master.Id.ToString());

        return await GetByIdAsync(master.Id, userId);
    }

    public async Task<DocumentRevisionDto> CancelDraftRevisionAsync(int revisionId, CancelDraftRevisionRequest request, int userId)
    {
        var canCancel = await _authService.CanCancelDraftRevisionAsync(revisionId, userId);
        if (!canCancel)
            throw new UnauthorizedAccessException("You do not have permission to cancel this draft revision.");

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
            throw new ArgumentException("A mandatory cancellation reason of at least 10 characters is required.", nameof(request.Reason));

        var revision = await _db.DocumentRevisions
            .Include(r => r.Files)
            .Include(r => r.CreatedByUser)
            .Include(r => r.CancelledByUser)
            .FirstOrDefaultAsync(r => r.Id == revisionId)
            ?? throw new KeyNotFoundException($"Document Revision {revisionId} not found.");

        if (revision.RevisionStatus != DocumentRevisionStatus.Draft)
            throw new InvalidOperationException($"Only revisions in Draft state can be cancelled. Current status is {revision.RevisionStatus}.");

        var trimmedReason = request.Reason.Trim();
        var previousStatus = revision.RevisionStatus.ToString();

        revision.RevisionStatus = DocumentRevisionStatus.Cancelled;
        revision.CancelledAt = DateTime.UtcNow;
        revision.CancelledByUserId = userId;
        revision.CancelReason = trimmedReason;

        // Deactivate all files on cancelled revision
        foreach (var file in revision.Files.Where(f => f.IsActive))
        {
            file.IsActive = false;
        }

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var changes = new List<AuditFieldChange>
        {
            new("RevisionStatus", previousStatus, DocumentRevisionStatus.Cancelled.ToString()),
            new("CancelReason", null, trimmedReason)
        };

        await _auditEventService.RecordUserEventAsync(
            actionCode: "DraftRevisionCancelled",
            actionCategory: AuditActionCategory.Document,
            recordType: nameof(DocumentRevision),
            documentMasterId: revision.DocumentMasterId,
            documentRevisionId: revision.Id,
            reason: trimmedReason,
            changes: changes,
            entityId: revision.Id.ToString());

        return MapRevisionToDto(revision);
    }

    public async Task<DocumentMasterAssignmentDto> AddOrUpdateAssignmentAsync(int documentMasterId, CreateAssignmentRequest request, int userId)
    {
        var canManage = await _authService.CanEditDraftMetadataAsync(documentMasterId, userId);
        if (!canManage)
            throw new UnauthorizedAccessException("You do not have permission to manage assignments for this document.");

        var master = await _db.DocumentMasters.FindAsync(documentMasterId)
            ?? throw new KeyNotFoundException($"Document Master {documentMasterId} not found.");

        if (master.RecordStatus == DocumentRecordStatus.Void)
            throw new InvalidOperationException("Cannot add assignments to a voided document.");

        var targetUser = await _db.Users.FindAsync(request.UserId)
            ?? throw new InvalidOperationException($"User {request.UserId} not found.");
        if (!targetUser.IsActive)
            throw new InvalidOperationException($"User '{targetUser.FullName}' is inactive.");

        var existing = await _db.DocumentMasterAssignments
            .FirstOrDefaultAsync(a => a.DocumentMasterId == documentMasterId &&
                                      a.UserId == request.UserId &&
                                      a.AssignmentRole == request.AssignmentRole);

        var now = DateTime.UtcNow;
        if (existing != null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.AssignedAt = now;
                existing.AssignedByUserId = userId;
            }
        }
        else
        {
            existing = new DocumentMasterAssignment
            {
                DocumentMasterId = documentMasterId,
                UserId = request.UserId,
                AssignmentRole = request.AssignmentRole,
                IsActive = true,
                AssignedAt = now,
                AssignedByUserId = userId
            };
            _db.DocumentMasterAssignments.Add(existing);
        }

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var changes = new List<AuditFieldChange>
        {
            new("AssignmentRole", null, request.AssignmentRole.ToString()),
            new("AssignedUserId", null, request.UserId.ToString()),
            new("AssignedUserName", null, targetUser.FullName)
        };

        await _auditEventService.RecordUserEventAsync(
            actionCode: "WorkflowAssignmentAdded",
            actionCategory: AuditActionCategory.Document,
            recordType: nameof(DocumentMasterAssignment),
            documentMasterId: documentMasterId,
            changes: changes,
            entityId: existing.Id.ToString());

        return new DocumentMasterAssignmentDto(
            existing.Id,
            existing.DocumentMasterId,
            existing.UserId,
            targetUser.Username,
            targetUser.FullName,
            existing.AssignmentRole,
            existing.IsActive,
            existing.AssignedAt,
            existing.AssignedByUserId,
            (await _db.Users.FindAsync(userId))?.FullName ?? "Unknown"
        );
    }

    public async Task RemoveAssignmentAsync(int documentMasterId, int assignmentId, int userId)
    {
        var canManage = await _authService.CanEditDraftMetadataAsync(documentMasterId, userId);
        if (!canManage)
            throw new UnauthorizedAccessException("You do not have permission to manage assignments for this document.");

        var assignment = await _db.DocumentMasterAssignments
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.DocumentMasterId == documentMasterId)
            ?? throw new KeyNotFoundException($"Assignment {assignmentId} not found on Document Master {documentMasterId}.");

        assignment.IsActive = false;
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var changes = new List<AuditFieldChange>
        {
            new("IsActive", "true", "false"),
            new("AssignmentRole", assignment.AssignmentRole.ToString(), null),
            new("UserId", assignment.UserId.ToString(), null)
        };

        await _auditEventService.RecordUserEventAsync(
            actionCode: "WorkflowAssignmentRemoved",
            actionCategory: AuditActionCategory.Document,
            recordType: nameof(DocumentMasterAssignment),
            documentMasterId: documentMasterId,
            changes: changes,
            entityId: assignment.Id.ToString());
    }

    private static DocumentMasterDto MapToDto(DocumentMaster m)
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
            m.Revisions.OrderByDescending(r => r.RevisionSequence).Select(MapRevisionToDto).ToList()
        );
    }

    private static DocumentRevisionDto MapRevisionToDto(DocumentRevision r)
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
            r.Files.OrderByDescending(f => f.UploadedAt).Select(f => new RevisionFileDto(
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
            )).ToList()
        );
    }
}
