using System.Collections.Concurrent;

namespace MicroLIMS.Application.Services;

// Remembers when each user's notifications were last recomputed, so the
// header's 60-second poll reruns DashboardNotificationService's computation
// (6-8 queries, NotificationLog inserts, alert emails) at most once per window
// and otherwise only reads the persisted list. Kept in memory: production runs
// a single API instance, and a restart only costs one early recompute.
public class NotificationRecomputeThrottle
{
    private readonly ConcurrentDictionary<int, DateTime> _lastComputedUtc = new();

    public NotificationRecomputeThrottle(TimeSpan window)
    {
        Window = window;
    }

    public TimeSpan Window { get; }

    public bool IsDue(int userId, DateTime nowUtc) =>
        !_lastComputedUtc.TryGetValue(userId, out var last) || nowUtc - last >= Window;

    public void MarkComputed(int userId, DateTime nowUtc) => _lastComputedUtc[userId] = nowUtc;
}
