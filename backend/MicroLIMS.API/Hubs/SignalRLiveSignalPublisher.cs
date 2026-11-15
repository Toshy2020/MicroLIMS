using MicroLIMS.Application.Abstractions.Live;
using Microsoft.AspNetCore.SignalR;

namespace MicroLIMS.API.Hubs;

/// <summary>
/// Publishes committed <see cref="LiveSignal"/> batches to the corresponding
/// SignalR groups on <see cref="LiveHub"/>. Failures are caught and logged so
/// a live-signal hiccup never fails a committed database operation.
/// </summary>
public sealed class SignalRLiveSignalPublisher : ILiveSignalPublisher
{
    public const string ClientEventName = "signal";

    private readonly IHubContext<LiveHub> _hubContext;
    private readonly ILogger<SignalRLiveSignalPublisher> _logger;

    public SignalRLiveSignalPublisher(
        IHubContext<LiveHub> hubContext,
        ILogger<SignalRLiveSignalPublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task PublishAsync(IReadOnlyCollection<LiveSignal> signals, CancellationToken ct = default)
    {
        foreach (var signal in signals)
        {
            try
            {
                var groupName = ResolveGroupName(signal);
                await _hubContext.Clients.Group(groupName).SendAsync(ClientEventName, signal, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to publish live signal {Kind}:{Key}",
                    signal.Kind,
                    signal.Key);
            }
        }
    }

    private static string ResolveGroupName(LiveSignal signal) =>
        signal.Kind == LiveSignalKinds.Record && !signal.Key.StartsWith("record:", StringComparison.Ordinal)
            ? $"record:{signal.Key}"
            : signal.Key;
}
