using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services.DocumentControl;

public class TrainingAssignmentService : ITrainingAssignmentService
{
    private readonly MicroLimsDbContext _db;
    private readonly IAuditEventService _audit;
    private readonly ILogger<TrainingAssignmentService> _logger;

    public const string SystemProcessName = "DocumentEffectiveDateWorker";
    public const int DefaultFallbackGracePeriodDays = 14;

    public TrainingAssignmentService(
        MicroLimsDbContext db,
        IAuditEventService audit,
        ILogger<TrainingAssignmentService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<TrainingAssignmentGenerationResultDto> ProcessEffectiveRevisionCascadeAsync(
        int effectiveRevisionId,
        int? supersededRevisionId = null,
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = utcNowOverride ?? DateTime.UtcNow;

        var revision = await _db.DocumentRevisions
            .Include(r => r.DocumentMaster)
                .ThenInclude(m => m.DocumentType)
            .FirstOrDefaultAsync(r => r.Id == effectiveRevisionId, cancellationToken);

        if (revision == null)
            throw new KeyNotFoundException($"DocumentRevision {effectiveRevisionId} not found.");

        if (revision.RevisionStatus != DocumentRevisionStatus.Effective)
        {
            _logger.LogWarning("Revision {RevisionId} is not in Effective state (current: {Status}). Cascade skipped.",
                effectiveRevisionId, revision.RevisionStatus);
            return new TrainingAssignmentGenerationResultDto(
                revision.DocumentMasterId, effectiveRevisionId, 0, 0, 0, 0, new List<int>(),
                new List<string> { $"Revision {effectiveRevisionId} is not in Effective state." });
        }

        var master = revision.DocumentMaster;
        if (master == null)
            throw new InvalidOperationException($"DocumentMaster missing for Revision {effectiveRevisionId}.");

        // 1. Check DocumentTrainingConfiguration
        var config = await _db.DocumentTrainingConfigurations
            .AsNoTracking()
            .Where(c => c.DocumentMasterId == master.Id || c.DocumentTypeId == master.DocumentTypeId)
            .OrderByDescending(c => c.DocumentMasterId.HasValue) // Master-specific overrides Type-specific
            .FirstOrDefaultAsync(cancellationToken);

        bool requiresReading = config?.RequiresReading ?? true;
        bool requiresRetraining = config?.RequiresRetrainingOnRevision ?? true;

        // If reading is disabled for this document, skip assignment generation
        if (!requiresReading)
        {
            _logger.LogInformation("Document {DocCode} does not require reading according to configuration. Cascade skipped.",
                master.CompanyDocumentCode);
            return new TrainingAssignmentGenerationResultDto(
                master.Id, revision.Id, 0, 0, 0, 0, new List<int>(), new List<string>());
        }

        int closedSupersededCount = 0;

        // 2. Terminally close open assignments on superseded revision (DC-URS-172)
        if (supersededRevisionId.HasValue)
        {
            var openOldAssignments = await _db.DocumentTrainingAssignments
                .Where(a => a.DocumentRevisionId == supersededRevisionId.Value
                         && (a.Status == TrainingAssignmentStatus.Assigned
                             || a.Status == TrainingAssignmentStatus.Reading
                             || a.Status == TrainingAssignmentStatus.Overdue
                             || a.Status == TrainingAssignmentStatus.KafPending))
                .ToListAsync(cancellationToken);

            foreach (var oldAssignment in openOldAssignments)
            {
                oldAssignment.Status = TrainingAssignmentStatus.SupersededIncomplete;
                oldAssignment.SupersededAtUtc = nowUtc;
                oldAssignment.ClosedReason = $"Superseded by Revision {revision.RevisionNumber}";
                oldAssignment.ModifiedAtUtc = nowUtc;
                closedSupersededCount++;
            }

            if (openOldAssignments.Count > 0)
            {
                _db.CurrentUserId = null;
                await _db.SaveChangesAsync(cancellationToken);

                await _audit.RecordSystemEventAsync(
                    systemProcessName: SystemProcessName,
                    actionCode: "SupersededAssignmentsClosed",
                    actionCategory: AuditActionCategory.Document,
                    recordType: nameof(DocumentTrainingAssignment),
                    documentMasterId: master.Id,
                    documentRevisionId: supersededRevisionId.Value,
                    reason: $"Closed {openOldAssignments.Count} open assignment(s) as SupersededIncomplete upon activation of Rev {revision.RevisionNumber}.",
                    changes: new[]
                    {
                        new AuditFieldChange("Count", null, openOldAssignments.Count.ToString()),
                        new AuditFieldChange("Status", null, TrainingAssignmentStatus.SupersededIncomplete.ToString())
                    },
                    entityId: supersededRevisionId.Value.ToString(),
                    cancellationToken: cancellationToken);
            }
        }

        // 3. Determine Target Users for the New Revision
        var targetUserIds = new HashSet<int>();
        var sourceAssignmentMap = new Dictionary<int, int>(); // UserId -> Prior Completed Assignment Id

        // A. Prior Completers (Retraining cascade: DC-URS-170, DC-URS-171)
        if (supersededRevisionId.HasValue && requiresRetraining)
        {
            var completedPriorAssignments = await _db.DocumentTrainingAssignments
                .AsNoTracking()
                .Where(a => a.DocumentRevisionId == supersededRevisionId.Value
                         && (a.Status == TrainingAssignmentStatus.Acknowledged || a.Status == TrainingAssignmentStatus.CompletedPassed))
                .ToListAsync(cancellationToken);

            foreach (var prior in completedPriorAssignments)
            {
                targetUserIds.Add(prior.AssignedUserId);
                sourceAssignmentMap[prior.AssignedUserId] = prior.Id;
            }
        }

        // B. Active Curricula Users (DC-URS-082, DC-URS-115)
        var curriculumItems = await _db.DocumentRoleCurriculumItems
            .Include(i => i.DocumentRoleCurriculum)
            .Where(i => i.DocumentMasterId == master.Id && i.DocumentRoleCurriculum.IsActive)
            .ToListAsync(cancellationToken);

        int? customCurriculumGracePeriod = null;
        foreach (var item in curriculumItems)
        {
            if (item.CustomGracePeriodDays.HasValue)
            {
                customCurriculumGracePeriod = item.CustomGracePeriodDays.Value;
            }

            var roleId = item.DocumentRoleCurriculum.RoleId;
            var deptId = item.DocumentRoleCurriculum.DepartmentId;

            // Query active users with matching Role (and matching Department if specified)
            var query = _db.Users.AsNoTracking().Where(u => u.IsActive && u.RoleId == roleId);

            var matchingUsers = await query.Select(u => u.Id).ToListAsync(cancellationToken);
            foreach (var uid in matchingUsers)
            {
                targetUserIds.Add(uid);
            }
        }

        // C. If no curricula or prior completers exist, evaluate active document master assignments if any
        if (targetUserIds.Count == 0)
        {
            var masterAssignedUsers = await _db.DocumentMasterAssignments
                .AsNoTracking()
                .Where(a => a.DocumentMasterId == master.Id && a.IsActive)
                .Select(a => a.UserId)
                .ToListAsync(cancellationToken);

            foreach (var uid in masterAssignedUsers)
            {
                targetUserIds.Add(uid);
            }
        }

        // 4. Calculate Grace Period and Due Date (DC-URS-083)
        var (gracePeriodDays, dueDateUtc) = await CalculateDueDateAsync(
            master.Id, customCurriculumGracePeriod, revision.EffectiveDate ?? nowUtc, cancellationToken);

        // 5. Idempotently generate assignments for target users
        var createdIds = new List<int>();
        int skippedCount = 0;
        var errors = new List<string>();

        // Load existing assignments for this exact revision to prevent duplicates
        var existingAssignedUserIds = (await _db.DocumentTrainingAssignments
            .Where(a => a.DocumentRevisionId == revision.Id && a.AssignmentType == AssignmentType.Reading)
            .Select(a => a.AssignedUserId)
            .ToListAsync(cancellationToken)).ToHashSet();

        // Filter for active users only
        var activeTargetUserIds = await _db.Users
            .AsNoTracking()
            .Where(u => targetUserIds.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var addedAssignments = new List<DocumentTrainingAssignment>();
        foreach (var userId in activeTargetUserIds)
        {
            if (existingAssignedUserIds.Contains(userId))
            {
                skippedCount++;
                continue;
            }

            sourceAssignmentMap.TryGetValue(userId, out int sourceAssignmentId);

            var newAssignment = new DocumentTrainingAssignment
            {
                DocumentMasterId = master.Id,
                DocumentRevisionId = revision.Id,
                AssignedUserId = userId,
                AssignmentType = AssignmentType.Reading,
                Status = TrainingAssignmentStatus.Assigned,
                AssignedDateUtc = nowUtc,
                DueDateUtc = dueDateUtc,
                SourceAssignmentId = sourceAssignmentId > 0 ? sourceAssignmentId : null,
                AssignmentReason = sourceAssignmentId > 0
                    ? $"Retraining cascade from Rev {supersededRevisionId}"
                    : "Curriculum assignment on revision effective activation",
                CreatedByUserId = master.DocumentOwnerUserId > 0 ? master.DocumentOwnerUserId : master.CreatedByUserId,
                CreatedAtUtc = nowUtc
            };

            _db.DocumentTrainingAssignments.Add(newAssignment);
            addedAssignments.Add(newAssignment);
        }

        if (addedAssignments.Count > 0)
        {
            _db.CurrentUserId = null;
            await _db.SaveChangesAsync(cancellationToken);
            createdIds = addedAssignments.Select(a => a.Id).ToList();

            await _audit.RecordSystemEventAsync(
                systemProcessName: SystemProcessName,
                actionCode: "TrainingCascadeAssignmentsCreated",
                actionCategory: AuditActionCategory.Document,
                recordType: nameof(DocumentTrainingAssignment),
                documentMasterId: master.Id,
                documentRevisionId: revision.Id,
                reason: $"Generated {createdIds.Count} reading assignment(s) for Rev {revision.RevisionNumber}.",
                changes: new[]
                {
                    new AuditFieldChange("NewAssignmentsCount", null, createdIds.Count.ToString()),
                    new AuditFieldChange("SkippedDuplicatesCount", null, skippedCount.ToString()),
                    new AuditFieldChange("GracePeriodDays", null, gracePeriodDays.ToString()),
                    new AuditFieldChange("DueDateUtc", null, dueDateUtc.ToString("o"))
                },
                entityId: revision.Id.ToString(),
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation(
            "Cascade completed for Rev {RevisionId} ({DocCode}). Eligible: {Eligible}, Created: {Created}, Skipped: {Skipped}, ClosedSuperseded: {Closed}",
            revision.Id, master.CompanyDocumentCode, activeTargetUserIds.Count, createdIds.Count, skippedCount, closedSupersededCount);

        return new TrainingAssignmentGenerationResultDto(
            master.Id,
            revision.Id,
            activeTargetUserIds.Count,
            createdIds.Count,
            skippedCount,
            closedSupersededCount,
            createdIds,
            errors);
    }

    public async Task<TrainingAssignmentGenerationResultDto> AssignToUsersAsync(
        ManualTrainingAssignmentRequest request,
        int actingUserId,
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = utcNowOverride ?? DateTime.UtcNow;

        var revision = await _db.DocumentRevisions
            .Include(r => r.DocumentMaster)
            .FirstOrDefaultAsync(r => r.Id == request.DocumentRevisionId, cancellationToken);

        if (revision == null)
            throw new KeyNotFoundException($"DocumentRevision {request.DocumentRevisionId} not found.");

        if (revision.RevisionStatus != DocumentRevisionStatus.Effective)
            throw new InvalidOperationException($"Assignments can only be issued for Effective revisions. Current status: {revision.RevisionStatus}.");

        var master = revision.DocumentMaster;
        var (gracePeriodDays, dueDateUtc) = await CalculateDueDateAsync(
            master.Id, request.CustomGracePeriodDays, nowUtc, cancellationToken);

        var existingAssignedUserIds = (await _db.DocumentTrainingAssignments
            .Where(a => a.DocumentRevisionId == revision.Id && a.AssignmentType == request.AssignmentType)
            .Select(a => a.AssignedUserId)
            .ToListAsync(cancellationToken)).ToHashSet();

        var activeUsers = await _db.Users
            .AsNoTracking()
            .Where(u => request.UserIds.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var createdIds = new List<int>();
        int skippedCount = 0;

        var addedAssignments = new List<DocumentTrainingAssignment>();
        foreach (var userId in activeUsers)
        {
            if (existingAssignedUserIds.Contains(userId))
            {
                skippedCount++;
                continue;
            }

            var assignment = new DocumentTrainingAssignment
            {
                DocumentMasterId = master.Id,
                DocumentRevisionId = revision.Id,
                AssignedUserId = userId,
                AssignmentType = request.AssignmentType,
                Status = TrainingAssignmentStatus.Assigned,
                AssignedDateUtc = nowUtc,
                DueDateUtc = dueDateUtc,
                AssignmentReason = request.AssignmentReason ?? "Manual assignment by authorized user",
                CreatedByUserId = actingUserId,
                CreatedAtUtc = nowUtc
            };

            _db.DocumentTrainingAssignments.Add(assignment);
            addedAssignments.Add(assignment);
        }

        if (addedAssignments.Count > 0)
        {
            _db.CurrentUserId = actingUserId;
            await _db.SaveChangesAsync(cancellationToken);
            createdIds = addedAssignments.Select(a => a.Id).ToList();

            await _audit.RecordUserEventAsync(
                actionCode: "ManualTrainingAssignmentsCreated",
                actionCategory: AuditActionCategory.Document,
                recordType: nameof(DocumentTrainingAssignment),
                documentMasterId: master.Id,
                documentRevisionId: revision.Id,
                reason: request.AssignmentReason ?? $"Manually assigned to {createdIds.Count} user(s)",
                changes: new[]
                {
                    new AuditFieldChange("CreatedCount", null, createdIds.Count.ToString()),
                    new AuditFieldChange("SkippedCount", null, skippedCount.ToString())
                },
                entityId: revision.Id.ToString(),
                cancellationToken: cancellationToken);
        }

        return new TrainingAssignmentGenerationResultDto(
            master.Id,
            revision.Id,
            activeUsers.Count,
            createdIds.Count,
            skippedCount,
            0,
            createdIds,
            new List<string>());
    }

    public async Task<TrainingAssignmentGenerationResultDto> AssignToGroupAsync(
        BulkGroupAssignmentRequest request,
        int actingUserId,
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Users.AsNoTracking().Where(u => u.IsActive);
        if (request.RoleId.HasValue)
            query = query.Where(u => u.RoleId == request.RoleId.Value);

        var userIds = await query.Select(u => u.Id).ToListAsync(cancellationToken);

        var manualReq = new ManualTrainingAssignmentRequest(
            request.DocumentRevisionId,
            userIds,
            request.AssignmentType,
            request.CustomGracePeriodDays,
            request.AssignmentReason ?? $"Group assignment for Role {request.RoleId}, Dept {request.DepartmentId}"
        );

        return await AssignToUsersAsync(manualReq, actingUserId, utcNowOverride, cancellationToken);
    }

    public async Task<int> HandleDocumentObsolescenceAsync(
        int documentMasterId,
        int? documentRevisionId = null,
        string? reason = null,
        int? actingUserId = null,
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = utcNowOverride ?? DateTime.UtcNow;

        var query = _db.DocumentTrainingAssignments
            .Where(a => a.DocumentMasterId == documentMasterId
                     && (a.Status == TrainingAssignmentStatus.Assigned
                         || a.Status == TrainingAssignmentStatus.Reading
                         || a.Status == TrainingAssignmentStatus.Overdue
                         || a.Status == TrainingAssignmentStatus.KafPending));

        if (documentRevisionId.HasValue)
        {
            query = query.Where(a => a.DocumentRevisionId == documentRevisionId.Value);
        }

        var openAssignments = await query.ToListAsync(cancellationToken);
        if (openAssignments.Count == 0) return 0;

        foreach (var assignment in openAssignments)
        {
            assignment.Status = TrainingAssignmentStatus.Cancelled;
            assignment.ClosedReason = reason ?? "Document Obsoleted / Cancelled";
            assignment.SupersededAtUtc = nowUtc;
            assignment.ModifiedAtUtc = nowUtc;
            assignment.ModifiedByUserId = actingUserId;
        }

        _db.CurrentUserId = actingUserId;
        await _db.SaveChangesAsync(cancellationToken);

        string closureReason = reason ?? "Document Obsoleted / Retired";
        if (actingUserId.HasValue)
        {
            await _audit.RecordUserEventAsync(
                actionCode: "TrainingAssignmentsCancelledDueToObsolescence",
                actionCategory: AuditActionCategory.Document,
                recordType: nameof(DocumentTrainingAssignment),
                documentMasterId: documentMasterId,
                documentRevisionId: documentRevisionId,
                reason: closureReason,
                changes: new[]
                {
                    new AuditFieldChange("CancelledCount", null, openAssignments.Count.ToString())
                },
                entityId: documentMasterId.ToString(),
                cancellationToken: cancellationToken);
        }
        else
        {
            await _audit.RecordSystemEventAsync(
                systemProcessName: SystemProcessName,
                actionCode: "TrainingAssignmentsCancelledDueToObsolescence",
                actionCategory: AuditActionCategory.Document,
                recordType: nameof(DocumentTrainingAssignment),
                documentMasterId: documentMasterId,
                documentRevisionId: documentRevisionId,
                reason: closureReason,
                changes: new[]
                {
                    new AuditFieldChange("CancelledCount", null, openAssignments.Count.ToString())
                },
                entityId: documentMasterId.ToString(),
                cancellationToken: cancellationToken);
        }

        return openAssignments.Count;
    }

    public async Task<(int GracePeriodDays, DateTime DueDateUtc)> CalculateDueDateAsync(
        int documentMasterId,
        int? customGracePeriodDays = null,
        DateTime? effectiveDate = null,
        CancellationToken cancellationToken = default)
    {
        var startDate = effectiveDate ?? DateTime.UtcNow;

        if (customGracePeriodDays.HasValue && customGracePeriodDays.Value > 0)
        {
            return (customGracePeriodDays.Value, startDate.AddDays(customGracePeriodDays.Value));
        }

        var config = await _db.DocumentTrainingConfigurations
            .AsNoTracking()
            .Where(c => c.DocumentMasterId == documentMasterId)
            .FirstOrDefaultAsync(cancellationToken);

        if (config != null && config.DefaultGracePeriodDays > 0)
        {
            return (config.DefaultGracePeriodDays, startDate.AddDays(config.DefaultGracePeriodDays));
        }

        var master = await _db.DocumentMasters
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == documentMasterId, cancellationToken);

        if (master != null)
        {
            var typeConfig = await _db.DocumentTrainingConfigurations
                .AsNoTracking()
                .Where(c => c.DocumentTypeId == master.DocumentTypeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (typeConfig != null && typeConfig.DefaultGracePeriodDays > 0)
            {
                return (typeConfig.DefaultGracePeriodDays, startDate.AddDays(typeConfig.DefaultGracePeriodDays));
            }
        }

        return (DefaultFallbackGracePeriodDays, startDate.AddDays(DefaultFallbackGracePeriodDays));
    }

    public async Task<bool> IsTrainedOnSupersededOnlyAsync(
        int userId,
        int documentMasterId,
        CancellationToken cancellationToken = default)
    {
        var master = await _db.DocumentMasters
            .Include(m => m.Revisions)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == documentMasterId, cancellationToken);

        if (master == null || !master.CurrentEffectiveRevisionId.HasValue)
            return false;

        int currentEffectiveRevId = master.CurrentEffectiveRevisionId.Value;

        // Check if user has acknowledged current effective revision
        bool isTrainedOnCurrent = await _db.DocumentTrainingAssignments
            .AnyAsync(a => a.DocumentRevisionId == currentEffectiveRevId
                        && a.AssignedUserId == userId
                        && (a.Status == TrainingAssignmentStatus.Acknowledged || a.Status == TrainingAssignmentStatus.CompletedPassed),
                      cancellationToken);

        if (isTrainedOnCurrent) return false;

        // Check if user completed ANY prior superseded revision of the same master
        var priorRevIds = master.Revisions
            .Where(r => r.Id != currentEffectiveRevId && r.RevisionStatus == DocumentRevisionStatus.Superseded)
            .Select(r => r.Id)
            .ToList();

        if (priorRevIds.Count == 0) return false;

        bool isTrainedOnPrior = await _db.DocumentTrainingAssignments
            .AnyAsync(a => priorRevIds.Contains(a.DocumentRevisionId)
                        && a.AssignedUserId == userId
                        && (a.Status == TrainingAssignmentStatus.Acknowledged || a.Status == TrainingAssignmentStatus.CompletedPassed),
                      cancellationToken);

        return isTrainedOnPrior;
    }

    public async Task<DocumentTrainingAssignmentDto?> GetAssignmentByIdAsync(
        int assignmentId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _db.DocumentTrainingAssignments
            .Include(a => a.DocumentMaster)
            .Include(a => a.DocumentRevision)
            .Include(a => a.AssignedUser)
            .Include(a => a.AcknowledgedByUser)
            .Include(a => a.CreatedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);

        return assignment == null ? null : MapToDto(assignment, DateTime.UtcNow);
    }

    public async Task<PagedResult<DocumentTrainingAssignmentDto>> GetAssignmentsAsync(
        DocumentTrainingAssignmentFilter filter,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var query = _db.DocumentTrainingAssignments
            .Include(a => a.DocumentMaster)
            .Include(a => a.DocumentRevision)
            .Include(a => a.AssignedUser)
            .Include(a => a.AcknowledgedByUser)
            .Include(a => a.CreatedByUser)
            .AsNoTracking()
            .AsQueryable();

        if (filter.DocumentMasterId.HasValue)
            query = query.Where(a => a.DocumentMasterId == filter.DocumentMasterId.Value);

        if (filter.DocumentRevisionId.HasValue)
            query = query.Where(a => a.DocumentRevisionId == filter.DocumentRevisionId.Value);

        if (filter.AssignedUserId.HasValue)
            query = query.Where(a => a.AssignedUserId == filter.AssignedUserId.Value);

        if (filter.Status.HasValue)
            query = query.Where(a => a.Status == filter.Status.Value);

        if (filter.AssignmentType.HasValue)
            query = query.Where(a => a.AssignmentType == filter.AssignmentType.Value);

        if (filter.OverdueOnly == true)
            query = query.Where(a => a.DueDateUtc < nowUtc
                                 && (a.Status == TrainingAssignmentStatus.Assigned
                                     || a.Status == TrainingAssignmentStatus.Reading
                                     || a.Status == TrainingAssignmentStatus.Overdue));

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var items = await query
            .OrderByDescending(a => a.DueDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<DocumentTrainingAssignmentDto>
        {
            Items = items.Select(a => MapToDto(a, nowUtc)).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<DocumentTrainingAssignmentDto>> GetUserAssignmentsAsync(
        int userId,
        TrainingAssignmentStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var query = _db.DocumentTrainingAssignments
            .Include(a => a.DocumentMaster)
            .Include(a => a.DocumentRevision)
            .Include(a => a.AssignedUser)
            .Include(a => a.AcknowledgedByUser)
            .Include(a => a.CreatedByUser)
            .AsNoTracking()
            .Where(a => a.AssignedUserId == userId);

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        var items = await query
            .OrderBy(a => a.DueDateUtc)
            .ToListAsync(cancellationToken);

        return items.Select(a => MapToDto(a, nowUtc)).ToList();
    }

    private static DocumentTrainingAssignmentDto MapToDto(DocumentTrainingAssignment a, DateTime nowUtc)
    {
        var isOverdue = a.DueDateUtc < nowUtc
            && a.Status != TrainingAssignmentStatus.Acknowledged
            && a.Status != TrainingAssignmentStatus.CompletedPassed
            && a.Status != TrainingAssignmentStatus.Cancelled
            && a.Status != TrainingAssignmentStatus.SupersededIncomplete;

        var deltaDays = Math.Round((a.DueDateUtc - nowUtc).TotalDays, 1);

        return new DocumentTrainingAssignmentDto(
            Id: a.Id,
            DocumentMasterId: a.DocumentMasterId,
            MicroLimsDocumentId: a.DocumentMaster?.MicroLimsDocumentId ?? string.Empty,
            CompanyDocumentCode: a.DocumentMaster?.CompanyDocumentCode ?? string.Empty,
            DocumentTitle: a.DocumentMaster?.Title ?? string.Empty,
            DocumentRevisionId: a.DocumentRevisionId,
            RevisionNumber: a.DocumentRevision?.RevisionNumber ?? string.Empty,
            AssignedUserId: a.AssignedUserId,
            AssignedUserName: a.AssignedUser?.FullName ?? string.Empty,
            AssignmentType: a.AssignmentType,
            Status: a.Status,
            AssignedDateUtc: a.AssignedDateUtc,
            DueDateUtc: a.DueDateUtc,
            AcknowledgedAtUtc: a.AcknowledgedAtUtc,
            StatementText: a.StatementText,
            AcknowledgedByUserId: a.AcknowledgedByUserId,
            AcknowledgedByUserName: a.AcknowledgedByUser?.FullName,
            CompletedAtUtc: a.CompletedAtUtc,
            SupersededAtUtc: a.SupersededAtUtc,
            ClosedReason: a.ClosedReason,
            SourceAssignmentId: a.SourceAssignmentId,
            AssignmentReason: a.AssignmentReason,
            CreatedByUserId: a.CreatedByUserId,
            CreatedByUserName: a.CreatedByUser?.FullName ?? string.Empty,
            CreatedAtUtc: a.CreatedAtUtc,
            IsOverdue: isOverdue,
            DaysRemainingOrOverdue: deltaDays
        );
    }
}
