using System.Collections.Concurrent;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Integrations;

// One call per window per named action, process-wide: enough to stop an admin
// page (or a script holding an admin token) from using the test buttons to
// flood a mailbox or hammer the storage provider. Register as a singleton.
public class AdminActionThrottle
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(1);

    private readonly Dictionary<string, DateTimeOffset> _last = new();
    private readonly TimeProvider _time;
    private readonly TimeSpan _window;

    public AdminActionThrottle(TimeProvider? timeProvider = null, TimeSpan? window = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _window = window ?? DefaultWindow;
    }

    public void Enter(string action)
    {
        var now = _time.GetUtcNow();
        lock (_last)
        {
            if (_last.TryGetValue(action, out var previous) && now - previous < _window)
            {
                var wait = (int)Math.Ceiling((_window - (now - previous)).TotalSeconds);
                throw new RateLimitedException($"This action can be run once every {(int)_window.TotalSeconds} seconds. Try again in {wait} s.", wait);
            }
            _last[action] = now;
        }
    }
}
