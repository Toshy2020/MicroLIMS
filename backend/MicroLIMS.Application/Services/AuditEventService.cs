using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;

namespace MicroLIMS.Application.Services;

public class AuditEventService : IAuditEventService
{
    private readonly MicroLimsDbContext _db;
    private readonly IDatabaseSequenceHelper _sequenceHelper;

    public AuditEventService(MicroLimsDbContext db, IDatabaseSequenceHelper sequenceHelper)
    {
        _db = db;
        _sequenceHelper = sequenceHelper;
    }

    public async Task<AuditLog> RecordUserEventAsync(
        string actionCode,
        AuditActionCategory actionCategory,
        string recordType,
        int? documentMasterId = null,
        int? documentRevisionId = null,
        string? reason = null,
        Guid? correlationId = null,
        string? sourceContext = null,
        IEnumerable<AuditFieldChange>? changes = null,
        string? entityId = null,
        CancellationToken cancellationToken = default)
    {
        var seqVal = await _sequenceHelper.GetNextSequenceValueAsync("audit_event_seq", cancellationToken);
        var eventUid = $"EVT-{seqVal:D7}";

        var log = new AuditLog
        {
            EventUid = eventUid,
            ActorType = ActorType.User,
            SystemProcessName = null,
            ActionCode = actionCode,
            ActionCategory = actionCategory,
            EntityName = recordType,
            EntityId = entityId ?? documentMasterId?.ToString() ?? "unknown",
            Action = actionCode,
            Reason = reason,
            SourceContext = sourceContext,
            CorrelationId = correlationId,
            DocumentMasterId = documentMasterId,
            DocumentRevisionId = documentRevisionId,
            UserId = _db.CurrentUserId,
            Timestamp = DateTime.UtcNow
        };

        if (changes != null)
        {
            foreach (var chg in changes)
            {
                log.Changes.Add(new AuditEventChange
                {
                    FieldName = chg.FieldName,
                    PreviousValue = chg.PreviousValue,
                    NewValue = chg.NewValue
                });
            }
        }

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);
        return log;
    }

    public async Task<AuditLog> RecordSystemEventAsync(
        string systemProcessName,
        string actionCode,
        AuditActionCategory actionCategory,
        string recordType,
        int? documentMasterId = null,
        int? documentRevisionId = null,
        string? reason = null,
        Guid? correlationId = null,
        string? sourceContext = null,
        IEnumerable<AuditFieldChange>? changes = null,
        string? entityId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(systemProcessName))
            throw new ArgumentException("System process name is required for system-initiated events.", nameof(systemProcessName));

        var seqVal = await _sequenceHelper.GetNextSequenceValueAsync("audit_event_seq", cancellationToken);
        var eventUid = $"EVT-{seqVal:D7}";

        var log = new AuditLog
        {
            EventUid = eventUid,
            ActorType = ActorType.System,
            SystemProcessName = systemProcessName,
            ActionCode = actionCode,
            ActionCategory = actionCategory,
            EntityName = recordType,
            EntityId = entityId ?? documentMasterId?.ToString() ?? "unknown",
            Action = actionCode,
            Reason = reason,
            SourceContext = sourceContext,
            CorrelationId = correlationId,
            DocumentMasterId = documentMasterId,
            DocumentRevisionId = documentRevisionId,
            UserId = null,
            Timestamp = DateTime.UtcNow
        };

        if (changes != null)
        {
            foreach (var chg in changes)
            {
                log.Changes.Add(new AuditEventChange
                {
                    FieldName = chg.FieldName,
                    PreviousValue = chg.PreviousValue,
                    NewValue = chg.NewValue
                });
            }
        }

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);
        return log;
    }
}
