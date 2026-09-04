using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Interfaces;

public record AuditFieldChange(string FieldName, string? PreviousValue, string? NewValue);

public interface IAuditEventService
{
    Task<AuditLog> RecordUserEventAsync(
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
        CancellationToken cancellationToken = default);

    Task<AuditLog> RecordSystemEventAsync(
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
        CancellationToken cancellationToken = default);
}
