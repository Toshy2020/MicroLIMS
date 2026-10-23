namespace MicroLIMS.API.Authorization;

// Caps how many AGENT_AUTH_FAILED security events can be written per minute,
// so a flood of bad keys cannot fill the audit table: at most PerIp rows per
// client address and at most Global rows across all addresses (the global
// ceiling still holds if the address is spoofable, e.g. forwarded headers not
// pinned to the proxy). The events beyond a cap are only logged.
//
// In memory and per process: right for the single instance this runs on
// (a second instance would double the ceiling; move to a shared
// store only if the API is ever scaled out).
public sealed class AgentAuthFailureAuditLimiter
{
    public const int MaxPerWindow = 10;
    public const int MaxGlobalPerWindow = 100;
    public const int MaxTrackedIps = 10_000;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _time;
    private readonly int _perIp;
    private readonly int _global;
    private readonly int _maxTracked;
    private readonly object _gate = new();
    private readonly Dictionary<string, (DateTime Start, int Count)> _byIp = new();
    private DateTime _lastPrune;
    private DateTime _globalStart;
    private int _globalCount;

    public AgentAuthFailureAuditLimiter(
        TimeProvider time, int perIp = MaxPerWindow, int global = MaxGlobalPerWindow, int maxTrackedIps = MaxTrackedIps)
    {
        _time = time;
        _perIp = perIp;
        _global = global;
        _maxTracked = maxTrackedIps;
    }

    // True when this failure may be written to the audit table.
    public bool TryAcquire(string clientIp)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        lock (_gate)
        {
            // Stale entries are dropped at most once per window (O(n) then, not per request).
            if (now - _lastPrune >= Window)
            {
                _lastPrune = now;
                foreach (var stale in _byIp.Where(e => now - e.Value.Start >= Window).Select(e => e.Key).ToList())
                    _byIp.Remove(stale);
            }

            if (now - _globalStart >= Window)
                (_globalStart, _globalCount) = (now, 0);

            var tracked = _byIp.TryGetValue(clientIp, out var entry);
            if (!tracked && _byIp.Count >= _maxTracked)
                return false; // table full of live addresses: untracked counts as over the cap
            if (!tracked || now - entry.Start >= Window)
                entry = (now, 0);

            if (entry.Count >= _perIp || _globalCount >= _global)
            {
                _byIp[clientIp] = entry;
                return false;
            }
            _byIp[clientIp] = (entry.Start, entry.Count + 1);
            _globalCount++;
            return true;
        }
    }
}
