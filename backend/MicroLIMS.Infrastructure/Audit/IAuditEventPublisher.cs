namespace MicroLIMS.Infrastructure.Audit;

// Optional hook for streaming audit events to an external SIEM/log
// aggregator in addition to the database AuditLog table.
public interface IAuditEventPublisher
{
    Task PublishAsync(string entityName, string entityId, string action, int userId);
}
