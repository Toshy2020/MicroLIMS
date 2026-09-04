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

public class DocumentEscalationService : IDocumentEscalationService
{
    private readonly MicroLimsDbContext _db;
    private readonly IAuditEventService _audit;
    private readonly ILogger<DocumentEscalationService> _logger;

    public const string DefaultProcessName = "DocumentEffectiveDateWorker";

    public DocumentEscalationService(
        MicroLimsDbContext db,
        IAuditEventService audit,
        ILogger<DocumentEscalationService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<EscalationProcessingResultDto> ProcessDueEscalationsAsync(
        DateTime? utcNowOverride = null,
        string processName = DefaultProcessName,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = utcNowOverride ?? DateTime.UtcNow;
        var createdIds = new List<int>();
        var errors = new List<string>();

        int evaluatedCount = 0;
        int approachingCreated = 0;
        int dueCreated = 0;
        int overdueCreated = 0;
        int skippedAlreadyEscalated = 0;
        int skippedExemptOrTerminal = 0;
        int failedCount = 0;

        _logger.LogInformation(
            "[{Process}] Starting Document Training Escalation evaluation at {NowUtc} UTC.",
            processName, nowUtc);

        // Fetch all active assignments that are NOT in terminal/completed states
        // Terminal/exempt states: Acknowledged, CompletedPassed, Cancelled, SupersededIncomplete, TrainedOnSupersededOnly
        var candidateAssignments = await _db.DocumentTrainingAssignments
            .Include(a => a.DocumentMaster)
                .ThenInclude(m => m.DocumentType)
            .Include(a => a.DocumentRevision)
            .Include(a => a.AssignedUser)
            .Where(a => a.Status == TrainingAssignmentStatus.Assigned
                     || a.Status == TrainingAssignmentStatus.Reading
                     || a.Status == TrainingAssignmentStatus.Overdue
                     || a.Status == TrainingAssignmentStatus.KafPending)
            .ToListAsync(cancellationToken);

        // Load all active training configurations
        var configurations = await _db.DocumentTrainingConfigurations
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Load existing escalation records for candidate assignments to evaluate idempotency
        var candidateAssignmentIds = candidateAssignments.Select(a => a.Id).ToList();
        var existingEscalations = await _db.DocumentEscalationRecords
            .AsNoTracking()
            .Where(e => candidateAssignmentIds.Contains(e.DocumentTrainingAssignmentId))
            .ToListAsync(cancellationToken);

        var escalationLookup = existingEscalations
            .GroupBy(e => (e.DocumentTrainingAssignmentId, e.EscalationLevel))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var assignment in candidateAssignments)
        {
            evaluatedCount++;

            try
            {
                // Verify Master and Revision states:
                // If master is voided or revision is cancelled/obsolete, do not escalate
                if (assignment.DocumentMaster.RecordStatus != DocumentRecordStatus.Active
                    || assignment.DocumentRevision.RevisionStatus != DocumentRevisionStatus.Effective)
                {
                    skippedExemptOrTerminal++;
                    continue;
                }

                // Resolve configuration (Hierarchy: Master override > Type override > Default)
                var config = configurations
                    .Where(c => c.DocumentMasterId == assignment.DocumentMasterId
                             || c.DocumentTypeId == assignment.DocumentMaster.DocumentTypeId)
                    .OrderByDescending(c => c.DocumentMasterId.HasValue)
                    .FirstOrDefault();

                // If training/reading is disabled for this document, skip escalation
                if (config != null && !config.RequiresReading)
                {
                    skippedExemptOrTerminal++;
                    continue;
                }

                int daysBefore = config?.EscalationDaysBeforeDue ?? 3;
                int daysAfter = config?.EscalationDaysAfterDue ?? 1;

                // Define scheduled trigger points (UTC)
                var triggerApproachingUtc = assignment.DueDateUtc.AddDays(-daysBefore);
                var triggerDueUtc = assignment.DueDateUtc;
                var triggerOverdueUtc = assignment.DueDateUtc.AddDays(daysAfter);

                // Determine eligibility across the 3 standard levels (Catch-up / downtime recovery evaluated)
                var eligibleLevels = new List<(DocumentEscalationLevel Level, DateTime TriggerUtc, string Recipient, string Reason)>();

                // Level 1: Approaching Due (e.g. T-3 days)
                if (nowUtc >= triggerApproachingUtc && nowUtc < triggerDueUtc)
                {
                    eligibleLevels.Add((
                        DocumentEscalationLevel.ApproachingDue,
                        triggerApproachingUtc,
                        "AssignedUser",
                        $"Training assignment for {assignment.DocumentMaster.CompanyDocumentCode} Rev {assignment.DocumentRevision.RevisionNumber} is approaching due date ({assignment.DueDateUtc:yyyy-MM-dd HH:mm:ss} UTC)."
                    ));
                }

                // Level 2: Due Date reached (e.g. Due Date)
                if (nowUtc >= triggerDueUtc)
                {
                    // Due Date escalation
                    eligibleLevels.Add((
                        DocumentEscalationLevel.Due,
                        triggerDueUtc,
                        "AssignedUser",
                        $"Training assignment for {assignment.DocumentMaster.CompanyDocumentCode} Rev {assignment.DocumentRevision.RevisionNumber} is due today ({assignment.DueDateUtc:yyyy-MM-dd HH:mm:ss} UTC)."
                    ));

                    // Automatically update assignment status to Overdue if not already Overdue or Acknowledged
                    if (assignment.Status != TrainingAssignmentStatus.Overdue)
                    {
                        assignment.Status = TrainingAssignmentStatus.Overdue;
                        assignment.ModifiedAtUtc = nowUtc;
                    }
                }

                // Level 3: Overdue (e.g. T+1 day)
                if (nowUtc >= triggerOverdueUtc)
                {
                    eligibleLevels.Add((
                        DocumentEscalationLevel.Overdue,
                        triggerOverdueUtc,
                        "Manager;QACompliance",
                        $"Training assignment for {assignment.DocumentMaster.CompanyDocumentCode} Rev {assignment.DocumentRevision.RevisionNumber} is OVERDUE past grace period (+{daysAfter} day(s))."
                    ));
                }

                // Process each eligible escalation point with strict idempotency
                foreach (var (level, triggerUtc, recipient, reason) in eligibleLevels)
                {
                    var lookupKey = (assignment.Id, level);
                    if (escalationLookup.ContainsKey(lookupKey))
                    {
                        // Already escalated for this level
                        skippedAlreadyEscalated++;
                        continue;
                    }

                    // Create new immutable escalation record
                    var record = new DocumentEscalationRecord
                    {
                        DocumentTrainingAssignmentId = assignment.Id,
                        DocumentMasterId = assignment.DocumentMasterId,
                        DocumentRevisionId = assignment.DocumentRevisionId,
                        AssignedUserId = assignment.AssignedUserId,
                        AssignmentType = assignment.AssignmentType,
                        DueDateUtc = assignment.DueDateUtc,
                        EscalationLevel = level,
                        Status = DocumentEscalationStatus.Raised,
                        ScheduledTriggerUtc = triggerUtc,
                        ExecutedAtUtc = nowUtc,
                        RecipientRoleOrTarget = recipient,
                        EscalationReason = reason,
                        ProcessName = processName,
                        CreatedAtUtc = nowUtc
                    };

                    _db.DocumentEscalationRecords.Add(record);
                    await _db.SaveChangesAsync(cancellationToken);

                    // Track in local cache for duplicate prevention
                    escalationLookup[lookupKey] = record;
                    createdIds.Add(record.Id);

                    switch (level)
                    {
                        case DocumentEscalationLevel.ApproachingDue:
                            approachingCreated++;
                            break;
                        case DocumentEscalationLevel.Due:
                            dueCreated++;
                            break;
                        case DocumentEscalationLevel.Overdue:
                        case DocumentEscalationLevel.CriticalOverdue:
                            overdueCreated++;
                            break;
                    }

                    // Record attributable audit event
                    await _audit.RecordSystemEventAsync(
                        systemProcessName: processName,
                        actionCode: $"TrainingEscalationRaised_{level}",
                        actionCategory: AuditActionCategory.Training,
                        recordType: nameof(DocumentEscalationRecord),
                        documentMasterId: assignment.DocumentMasterId,
                        documentRevisionId: assignment.DocumentRevisionId,
                        reason: reason,
                        changes: new[]
                        {
                            new AuditFieldChange("EscalationLevel", null, level.ToString()),
                            new AuditFieldChange("RecipientRoleOrTarget", null, recipient),
                            new AuditFieldChange("DueDateUtc", null, assignment.DueDateUtc.ToString("o")),
                            new AuditFieldChange("ScheduledTriggerUtc", null, triggerUtc.ToString("o"))
                        },
                        entityId: record.Id.ToString(),
                        cancellationToken: cancellationToken);

                    _logger.LogInformation(
                        "[{Process}] Generated {Level} escalation for Assignment {AssignmentId} (User {UserId}, Doc {DocCode}).",
                        processName, level, assignment.Id, assignment.AssignedUserId, assignment.DocumentMaster.CompanyDocumentCode);
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                var errorMsg = $"Error escalating assignment {assignment.Id}: {ex.Message}";
                _logger.LogError(ex, "[{Process}] {Error}", processName, errorMsg);
                errors.Add(errorMsg);
            }
        }

        return new EscalationProcessingResultDto(
            EvaluationTimestampUtc: nowUtc,
            TotalEvaluatedCount: evaluatedCount,
            TotalEscalationsCreated: createdIds.Count,
            ApproachingDueCreated: approachingCreated,
            DueCreated: dueCreated,
            OverdueCreated: overdueCreated,
            SkippedAlreadyEscalatedCount: skippedAlreadyEscalated,
            SkippedExemptOrTerminalCount: skippedExemptOrTerminal,
            FailedCount: failedCount,
            CreatedEscalationRecordIds: createdIds,
            Errors: errors
        );
    }

    public async Task<IReadOnlyList<DocumentEscalationSummaryDto>> GetAssignmentEscalationHistoryAsync(
        int assignmentId,
        CancellationToken cancellationToken = default)
    {
        var records = await _db.DocumentEscalationRecords
            .Include(e => e.DocumentMaster)
            .Include(e => e.DocumentRevision)
            .Include(e => e.AssignedUser)
            .Include(e => e.ResolvedByUser)
            .Where(e => e.DocumentTrainingAssignmentId == assignmentId)
            .OrderBy(e => e.ExecutedAtUtc)
            .ToListAsync(cancellationToken);

        return records.Select(MapToSummary).ToList();
    }

    public async Task<OverdueTrainingSummaryDto> GetOverdueTrainingSummaryAsync(
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = utcNowOverride ?? DateTime.UtcNow;

        var overdueAssignments = await _db.DocumentTrainingAssignments
            .Include(a => a.DocumentMaster)
            .Include(a => a.DocumentRevision)
            .Include(a => a.AssignedUser)
            .Where(a => a.DueDateUtc < nowUtc
                     && (a.Status == TrainingAssignmentStatus.Assigned
                         || a.Status == TrainingAssignmentStatus.Reading
                         || a.Status == TrainingAssignmentStatus.Overdue
                         || a.Status == TrainingAssignmentStatus.KafPending)
                     && a.DocumentMaster.RecordStatus == DocumentRecordStatus.Active
                     && a.DocumentRevision.RevisionStatus == DocumentRevisionStatus.Effective)
            .OrderBy(a => a.DueDateUtc)
            .ToListAsync(cancellationToken);

        var assignmentIds = overdueAssignments.Select(a => a.Id).ToList();

        var escalations = await _db.DocumentEscalationRecords
            .Where(e => assignmentIds.Contains(e.DocumentTrainingAssignmentId))
            .ToListAsync(cancellationToken);

        var highestEscalations = escalations
            .GroupBy(e => e.DocumentTrainingAssignmentId)
            .ToDictionary(
                g => g.Key,
                g => (DocumentEscalationLevel?)g.Max(x => (int)x.EscalationLevel)
            );

        var items = overdueAssignments.Select(a =>
        {
            var daysOverdue = Math.Max(0, (nowUtc - a.DueDateUtc).TotalDays);
            highestEscalations.TryGetValue(a.Id, out var level);

            return new OverdueAssignmentItemDto(
                AssignmentId: a.Id,
                DocumentMasterId: a.DocumentMasterId,
                CompanyDocumentCode: a.DocumentMaster.CompanyDocumentCode,
                DocumentTitle: a.DocumentMaster.Title,
                DocumentRevisionId: a.DocumentRevisionId,
                RevisionNumber: a.DocumentRevision.RevisionNumber,
                AssignedUserId: a.AssignedUserId,
                AssignedUserName: a.AssignedUser.FullName,
                DueDateUtc: a.DueDateUtc,
                DaysOverdue: Math.Round(daysOverdue, 1),
                Status: a.Status,
                CurrentEscalationLevel: level
            );
        }).ToList();

        return new OverdueTrainingSummaryDto(
            OverdueAssignmentsCount: items.Count,
            EscalatedCount: items.Count(i => i.CurrentEscalationLevel.HasValue),
            OverdueItems: items
        );
    }

    public async Task<DocumentEscalationSummaryDto> ResolveEscalationAsync(
        ResolveEscalationRequest request,
        int actingUserId,
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ResolutionReason))
        {
            throw new InvalidOperationException("A resolution reason is mandatory to resolve an escalation record.");
        }

        var record = await _db.DocumentEscalationRecords
            .Include(e => e.DocumentMaster)
            .Include(e => e.DocumentRevision)
            .Include(e => e.AssignedUser)
            .Include(e => e.ResolvedByUser)
            .FirstOrDefaultAsync(e => e.Id == request.EscalationRecordId, cancellationToken);

        if (record == null)
            throw new KeyNotFoundException($"Escalation record {request.EscalationRecordId} not found.");

        if (record.Status == DocumentEscalationStatus.Resolved)
        {
            _logger.LogInformation("Escalation record {RecordId} is already resolved.", record.Id);
            return MapToSummary(record);
        }

        var nowUtc = utcNowOverride ?? DateTime.UtcNow;

        record.Status = DocumentEscalationStatus.Resolved;
        record.ResolvedAtUtc = nowUtc;
        record.ResolvedByUserId = actingUserId;
        record.ResolutionReason = request.ResolutionReason;

        _db.CurrentUserId = actingUserId;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordUserEventAsync(
            actionCode: "TrainingEscalationResolved",
            actionCategory: AuditActionCategory.Training,
            recordType: nameof(DocumentEscalationRecord),
            documentMasterId: record.DocumentMasterId,
            documentRevisionId: record.DocumentRevisionId,
            reason: request.ResolutionReason,
            changes: new[]
            {
                new AuditFieldChange("Status", DocumentEscalationStatus.Raised.ToString(), DocumentEscalationStatus.Resolved.ToString()),
                new AuditFieldChange("ResolutionReason", null, request.ResolutionReason)
            },
            entityId: record.Id.ToString(),
            cancellationToken: cancellationToken);

        return MapToSummary(record);
    }

    private static DocumentEscalationSummaryDto MapToSummary(DocumentEscalationRecord e)
    {
        return new DocumentEscalationSummaryDto(
            Id: e.Id,
            DocumentTrainingAssignmentId: e.DocumentTrainingAssignmentId,
            DocumentMasterId: e.DocumentMasterId,
            CompanyDocumentCode: e.DocumentMaster?.CompanyDocumentCode ?? string.Empty,
            DocumentTitle: e.DocumentMaster?.Title ?? string.Empty,
            DocumentRevisionId: e.DocumentRevisionId,
            RevisionNumber: e.DocumentRevision?.RevisionNumber ?? string.Empty,
            AssignedUserId: e.AssignedUserId,
            AssignedUserName: e.AssignedUser?.FullName ?? string.Empty,
            DueDateUtc: e.DueDateUtc,
            EscalationLevel: e.EscalationLevel,
            Status: e.Status,
            ScheduledTriggerUtc: e.ScheduledTriggerUtc,
            ExecutedAtUtc: e.ExecutedAtUtc,
            RecipientRoleOrTarget: e.RecipientRoleOrTarget,
            EscalationReason: e.EscalationReason,
            ResolvedAtUtc: e.ResolvedAtUtc,
            ResolvedByUserId: e.ResolvedByUserId,
            ResolvedByUserName: e.ResolvedByUser?.FullName,
            ResolutionReason: e.ResolutionReason,
            ProcessName: e.ProcessName
        );
    }
}
