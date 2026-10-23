namespace MicroLIMS.API.Authorization;

// Caps how many AGENT_AUTH_FAILED security events one client address can
// write per minute, so a flood of bad keys cannot fill the audit table. The
// events beyond the cap are only logged.
//
// In memory and per process: right for the single instance this runs on
// (a second instance would double the ceiling; move to a shared
// store only if the API is ever scaled out).
public sealed class AgentAuthFailureAuditLimiter
{
    public const int MaxPerWindow = 10;
    private const int PruneAbove = 1000;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly Dictionary<string, (DateTime Start, int Count)> _byIp = new();

    public AgentAuthFailureAuditLimiter(TimeProvider time) => _time = time;

    // True when this failure may be written to the audit table.
    public bool TryAcquire(string clientIp)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        lock (_gate)
        {
            if (_byIp.Count > PruneAbove)
                foreach (var stale in _byIp.Where(e => now - e.Value.Start >= Window).Select(e => e.Key).ToList())
                    _byIp.Remove(stale);

            if (!_byIp.TryGetValue(clientIp, out var entry) || now - entry.Start >= Window)
                entry = (now, 0);
            if (entry.Count >= MaxPerWindow)
            {
                _byIp[clientIp] = entry;
                return false;
            }
            _byIp[clientIp] = (entry.Start, entry.Count + 1);
            return true;
        }
    }
}
