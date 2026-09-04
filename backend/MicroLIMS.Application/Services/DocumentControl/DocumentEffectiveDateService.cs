using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services.DocumentControl;

public class DocumentEffectiveDateService : IDocumentEffectiveDateService
{
    private readonly MicroLimsDbContext _db;
    private readonly IAuditEventService _audit;
    private readonly ILogger<DocumentEffectiveDateService> _logger;
    private readonly ITrainingAssignmentService? _trainingAssignmentService;

    public const string SystemProcessName = "DocumentEffectiveDateWorker";

    public DocumentEffectiveDateService(
        MicroLimsDbContext db,
        IAuditEventService audit,
        ILogger<DocumentEffectiveDateService> logger,
        ITrainingAssignmentService? trainingAssignmentService = null)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
        _trainingAssignmentService = trainingAssignmentService;
    }

    public async Task<EffectiveDateProcessingResultDto> ProcessMaturedRevisionsAsync(
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = utcNowOverride ?? DateTime.UtcNow;

        _logger.LogInformation(
            "[{Worker}] Commencing evaluation of matured FutureEffective revisions at UTC timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss 'UTC'}",
            SystemProcessName, nowUtc);

        // 1. Efficient Database Query: strictly select FutureEffective revisions with EffectiveDate <= nowUtc (DC-URS-177, DC-URS-182)
        var eligibleRevisions = await _db.DocumentRevisions
            .Include(r => r.DocumentMaster)
                .ThenInclude(m => m.Revisions)
            .Where(r => r.RevisionStatus == DocumentRevisionStatus.FutureEffective
                     && r.EffectiveDate != null
                     && r.EffectiveDate <= nowUtc)
            .OrderBy(r => r.EffectiveDate)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        int eligibleCount = eligibleRevisions.Count;
        var activatedTransitions = new List<EffectiveDateTransitionDetail>();
        var failures = new List<EffectiveDateFailureDetail>();

        if (eligibleCount == 0)
        {
            _logger.LogDebug("[{Worker}] No eligible FutureEffective revisions found at {Timestamp}", SystemProcessName, nowUtc);
            return new EffectiveDateProcessingResultDto(
                nowUtc,
                EligibleRevisionsCount: 0,
                SuccessfullyActivatedCount: 0,
                FailedRevisionsCount: 0,
                activatedTransitions,
                failures);
        }

        _logger.LogInformation(
            "[{Worker}] Found {Count} eligible FutureEffective revision(s) for automatic activation.",
            SystemProcessName, eligibleCount);

        // Group by DocumentMasterId to process each master in its own isolated atomic transaction (DC-URS-183)
        var groupedByMaster = eligibleRevisions.GroupBy(r => r.DocumentMasterId).ToList();

        foreach (var group in groupedByMaster)
        {
            int masterId = group.Key;
            var masterRevisions = group.OrderBy(r => r.EffectiveDate).ThenBy(r => r.RevisionSequence).ToList();

            foreach (var targetRevision in masterRevisions)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("[{Worker}] Cancellation requested during processing cycle.", SystemProcessName);
                    break;
                }

                try
                {
                    var transitionDetail = await ProcessSingleMasterRevisionAsync(targetRevision, nowUtc, cancellationToken);
                    if (transitionDetail != null)
                    {
                        activatedTransitions.Add(transitionDetail);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[{Worker}] Critical failure during automatic activation of Revision Id {RevisionId} (Master Id: {MasterId}, Code: {DocCode})",
                        targetRevision.Id, masterId, targetRevision.DocumentMaster?.CompanyDocumentCode ?? "UNKNOWN");

                    failures.Add(new EffectiveDateFailureDetail(
                        masterId,
                        targetRevision.Id,
                        targetRevision.DocumentMaster?.CompanyDocumentCode ?? "UNKNOWN",
                        ex.Message,
                        ex.GetType().Name));

                    // DC-URS-183: Log SystemProcessError event in audit trail for administration alerting
                    await LogProcessFailureAuditAsync(targetRevision, ex, nowUtc, cancellationToken);
                }
            }
        }

        _logger.LogInformation(
            "[{Worker}] Completed evaluation cycle. Eligible: {Eligible}, Activated: {Activated}, Failures: {Failures}",
            SystemProcessName, eligibleCount, activatedTransitions.Count, failures.Count);

        return new EffectiveDateProcessingResultDto(
            nowUtc,
            eligibleCount,
            activatedTransitions.Count,
            failures.Count,
            activatedTransitions,
            failures);
    }

    private async Task<EffectiveDateTransitionDetail?> ProcessSingleMasterRevisionAsync(
        DocumentRevision targetRevision,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        // Re-read with fresh change tracker state inside execution boundary
        var freshRevision = await _db.DocumentRevisions
            .Include(r => r.DocumentMaster)
                .ThenInclude(m => m.Revisions)
            .FirstOrDefaultAsync(r => r.Id == targetRevision.Id, cancellationToken);

        if (freshRevision == null)
        {
            throw new InvalidOperationException($"DocumentRevision {targetRevision.Id} not found in database.");
        }

        // Idempotency check: if status has already changed (e.g. concurrent execution or manual intervention), skip harmlessly
        if (freshRevision.RevisionStatus != DocumentRevisionStatus.FutureEffective)
        {
            _logger.LogInformation(
                "[{Worker}] Revision Id {RevisionId} is no longer in FutureEffective status (current: {Status}). Skipping.",
                freshRevision.Id, freshRevision.RevisionStatus);
            return null;
        }

        var master = freshRevision.DocumentMaster;
        if (master == null)
        {
            throw new InvalidOperationException($"DocumentMaster missing for Revision {freshRevision.Id}.");
        }

        if (master.RecordStatus != DocumentRecordStatus.Active)
        {
            throw new InvalidOperationException($"Cannot activate Revision {freshRevision.Id} because DocumentMaster {master.Id} is in {master.RecordStatus} status.");
        }

        var effectiveDate = freshRevision.EffectiveDate!.Value;
        bool isDelayedExecution = (nowUtc - effectiveDate).TotalHours >= 1.0;

        // Use database transaction if underlying provider supports it (relational like PostgreSQL)
        var isRelational = _db.Database.IsRelational();
        EffectiveDateTransitionDetail? result = null;

        async Task ExecuteCoreAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx)
        {
            try
            {
                // 1. Identify prior effective revision(s) for the same master
                var priorEffectiveRevisions = master.Revisions
                    .Where(r => r.Id != freshRevision.Id && r.RevisionStatus == DocumentRevisionStatus.Effective)
                    .ToList();

                int? supersededRevisionId = null;
                string? supersededRevisionNumber = null;

                // 2. DC-URS-178: Supersede prior effective revision(s)
                foreach (var prior in priorEffectiveRevisions)
                {
                    prior.RevisionStatus = DocumentRevisionStatus.Superseded;
                    supersededRevisionId = prior.Id;
                    supersededRevisionNumber = prior.RevisionNumber;

                    _logger.LogInformation(
                        "[{Worker}] Automatically superseding prior Revision Id {PriorId} (Rev {RevNo}) for Document {DocCode}",
                        prior.Id, prior.RevisionNumber, master.CompanyDocumentCode);
                }

                // 3. DC-URS-177: Transition target revision to Effective
                freshRevision.RevisionStatus = DocumentRevisionStatus.Effective;

                // Calculate next periodic review date if review cycle is defined
                int reviewCycle = freshRevision.ReviewCycleMonths ?? master.DocumentType?.DefaultReviewCycleMonths ?? 24;
                freshRevision.NextReviewDate = effectiveDate.AddMonths(reviewCycle);

                // 4. Update master pointer to the newly effective revision
                master.CurrentEffectiveRevisionId = freshRevision.Id;

                // Set CurrentUserId to null to ensure system-attributed SaveChanges
                _db.CurrentUserId = null;
                await _db.SaveChangesAsync(cancellationToken);

                // 5. DC-URS-180: Record system-attributed audit events in the same transactional unit
                string activationReason = isDelayedExecution
                    ? $"Automated effective-date activation executed following system downtime recovery (scheduled: {effectiveDate:yyyy-MM-dd HH:mm:ss 'UTC'}, executed: {nowUtc:yyyy-MM-dd HH:mm:ss 'UTC'})."
                    : $"Automated effective-date activation on scheduled date {effectiveDate:yyyy-MM-dd HH:mm:ss 'UTC'}.";

                await _audit.RecordSystemEventAsync(
                    systemProcessName: SystemProcessName,
                    actionCode: "RevisionAutomaticallyActivated",
                    actionCategory: AuditActionCategory.Document,
                    recordType: nameof(DocumentRevision),
                    documentMasterId: master.Id,
                    documentRevisionId: freshRevision.Id,
                    reason: activationReason,
                    changes: new[]
                    {
                        new AuditFieldChange("RevisionStatus", DocumentRevisionStatus.FutureEffective.ToString(), DocumentRevisionStatus.Effective.ToString()),
                        new AuditFieldChange("IsDelayedExecution", (!isDelayedExecution).ToString(), isDelayedExecution.ToString())
                    },
                    entityId: freshRevision.Id.ToString(),
                    cancellationToken: cancellationToken);

                if (supersededRevisionId.HasValue)
                {
                    await _audit.RecordSystemEventAsync(
                        systemProcessName: SystemProcessName,
                        actionCode: "RevisionAutomaticallySuperseded",
                        actionCategory: AuditActionCategory.Document,
                        recordType: nameof(DocumentRevision),
                        documentMasterId: master.Id,
                        documentRevisionId: supersededRevisionId.Value,
                        reason: $"Prior effective revision automatically superseded upon activation of Revision {freshRevision.RevisionNumber}.",
                        changes: new[]
                        {
                            new AuditFieldChange("RevisionStatus", DocumentRevisionStatus.Effective.ToString(), DocumentRevisionStatus.Superseded.ToString())
                        },
                        entityId: supersededRevisionId.Value.ToString(),
                        cancellationToken: cancellationToken);
                }

                if (tx != null)
                {
                    await tx.CommitAsync(cancellationToken);
                }

                result = new EffectiveDateTransitionDetail(
                    master.Id,
                    master.CompanyDocumentCode,
                    freshRevision.Id,
                    freshRevision.RevisionNumber,
                    effectiveDate,
                    isDelayedExecution,
                    supersededRevisionId,
                    supersededRevisionNumber);

                _logger.LogInformation(
                    "[{Worker}] Successfully committed activation of Revision Id {RevId} (Rev {RevNo}) for Document {DocCode}. (Delayed: {Delayed})",
                    freshRevision.Id, freshRevision.RevisionNumber, master.CompanyDocumentCode, isDelayedExecution);
            }
            catch
            {
                if (tx != null)
                {
                    await tx.RollbackAsync(cancellationToken);
                }
                throw;
            }
        }

        if (isRelational)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
                await ExecuteCoreAsync(tx);
            });
        }
        else
        {
            await ExecuteCoreAsync(null);
        }

        // Release 1c CC-DC-R1C-001: Automatic Reading & Training Assignment Cascade
        if (result != null && _trainingAssignmentService != null)
        {
            try
            {
                await _trainingAssignmentService.ProcessEffectiveRevisionCascadeAsync(
                    effectiveRevisionId: result.ActivatedRevisionId,
                    supersededRevisionId: result.SupersededRevisionId,
                    utcNowOverride: nowUtc,
                    cancellationToken: cancellationToken);
            }
            catch (Exception cascadeEx)
            {
                _logger.LogError(cascadeEx,
                    "[{Worker}] Training assignment cascade failed for activated Revision Id {RevisionId}: {Message}",
                    SystemProcessName, result.ActivatedRevisionId, cascadeEx.Message);

                await LogProcessFailureAuditAsync(
                    freshRevision,
                    cascadeEx,
                    nowUtc,
                    cancellationToken);
            }
        }

        return result;
    }

    private async Task LogProcessFailureAuditAsync(
        DocumentRevision revision,
        Exception ex,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            _db.CurrentUserId = null;
            await _audit.RecordSystemEventAsync(
                systemProcessName: SystemProcessName,
                actionCode: "SystemProcessError",
                actionCategory: AuditActionCategory.Security,
                recordType: nameof(DocumentRevision),
                documentMasterId: revision.DocumentMasterId,
                documentRevisionId: revision.Id,
                reason: $"CRITICAL: Automated effective date processing failed for Revision {revision.Id}: {ex.Message}",
                changes: new[]
                {
                    new AuditFieldChange("ErrorMessage", null, ex.Message),
                    new AuditFieldChange("ExceptionType", null, ex.GetType().FullName)
                },
                entityId: revision.Id.ToString(),
                cancellationToken: cancellationToken);
        }
        catch (Exception auditEx)
        {
            _logger.LogError(auditEx, "[{Worker}] Failed to record SystemProcessError audit log for Revision {RevisionId}",
                SystemProcessName, revision.Id);
        }
    }
}
