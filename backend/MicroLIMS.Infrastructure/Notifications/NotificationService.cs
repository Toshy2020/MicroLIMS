using System.Collections.Concurrent;
using System.Threading.Channels;

namespace MicroLIMS.Infrastructure.Notifications;

public record LiveNotification(int UserId, string Message, DateTime Timestamp);

// Real in-process pub/sub via System.Threading.Channels - no external
// broker needed for a single-instance deployment. A controller endpoint
// (or SignalR hub, if added later) can subscribe with GetReaderFor(userId)
// to stream notifications to a connected client in real time.
//
// For a multi-instance production deployment, swap the channel-per-user
// dictionary below for Redis pub/sub or Azure SignalR - INotificationService
// is the seam; nothing else in the app needs to change.
public class NotificationService : INotificationService
{
    private static readonly ConcurrentDictionary<int, Channel<LiveNotification>> _channels = new();

    public Task NotifyAsync(int userId, string message)
    {
        var channel = _channels.GetOrAdd(userId, _ => Channel.CreateUnbounded<LiveNotification>());
        channel.Writer.TryWrite(new LiveNotification(userId, message, DateTime.UtcNow));
        return Task.CompletedTask;
    }

    public ChannelReader<LiveNotification> GetReaderFor(int userId)
    {
        var channel = _channels.GetOrAdd(userId, _ => Channel.CreateUnbounded<LiveNotification>());
        return channel.Reader;
    }
}
