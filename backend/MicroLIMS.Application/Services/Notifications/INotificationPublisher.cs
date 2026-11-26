namespace MicroLIMS.Application.Services.Notifications;

public sealed record NotificationRequest(
    string Type,
    IReadOnlyCollection<int> UserIds,
    string Message,
    string? TargetType,
    int? TargetId,
    int? SectionId,
    string? DedupeKey,
    int? SampleId = null,
    int? TestOrderId = null,
    string? Severity = null,
    DateTime? DueAtUtc = null);

public interface INotificationPublisher
{
    Task PublishAsync(NotificationRequest request, CancellationToken ct = default);
    Task ResolveAsync(string dedupeKey, CancellationToken ct = default);

    async Task PublishBatchAsync(IReadOnlyList<NotificationRequest> requests, CancellationToken ct = default)
    {
        foreach (var request in requests)
            await PublishAsync(request, ct);
    }
}
