namespace MicroLIMS.Infrastructure.Audit;

public class AuditEventPublisher : IAuditEventPublisher
{
    public Task PublishAsync(string entityName, string entityId, string action, int userId)
    {
        // TODO: integrate external SIEM/log aggregator if required.
        return Task.CompletedTask;
    }
}
