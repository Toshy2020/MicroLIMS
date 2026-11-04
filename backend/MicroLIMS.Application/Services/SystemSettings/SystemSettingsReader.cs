using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;

namespace MicroLIMS.Application.Services.SystemSettings;

// Process-wide snapshot of the stored values (key -> value), shared by every
// scoped reader. Invalidated by the management service on a change; the short
// lifetime also picks up a change made through another API instance.
public sealed class SystemSettingsCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private IReadOnlyDictionary<string, string>? _values;
    private DateTimeOffset _loadedAt;

    public bool TryGet(DateTimeOffset now, out IReadOnlyDictionary<string, string> values)
    {
        lock (_gate)
        {
            if (_values is not null && now - _loadedAt < Lifetime)
            {
                values = _values;
                return true;
            }
        }
        values = new Dictionary<string, string>();
        return false;
    }

    public void Set(DateTimeOffset now, IReadOnlyDictionary<string, string> values)
    {
        lock (_gate)
        {
            _values = values;
            _loadedAt = now;
        }
    }

    public void Invalidate()
    {
        lock (_gate) { _values = null; }
    }
}

public class SystemSettingsReader : ISystemSettingsReader
{
    private readonly IMicroLimsDbContext _db;
    private readonly SystemSettingsCache _cache;
    private readonly IConfiguration? _configuration;
    private readonly TimeProvider _time;

    public SystemSettingsReader(IMicroLimsDbContext db, SystemSettingsCache cache, IConfiguration? configuration = null, TimeProvider? timeProvider = null)
    {
        _db = db;
        _cache = cache;
        _configuration = configuration;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default)
    {
        var (value, _) = await ResolveAsync(key, cancellationToken);
        return int.Parse(value, CultureInfo.InvariantCulture);
    }

    public async Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default)
    {
        var (value, _) = await ResolveAsync(key, cancellationToken);
        return bool.Parse(value);
    }

    public async Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default)
        => (await ResolveAsync(key, cancellationToken)).Value;

    // The stored values, from the cache when fresh.
    internal async Task<IReadOnlyDictionary<string, string>> LoadStoredAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        if (_cache.TryGet(now, out var cached)) return cached;

        var rows = await _db.SystemSettings.AsNoTracking()
            .Select(s => new { s.Key, s.Value })
            .ToListAsync(cancellationToken);
        var values = rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);
        _cache.Set(now, values);
        return values;
    }

    // Database, then the legacy configuration key, then the code default. A
    // value that no longer validates (edited outside the application, or a typo
    // in configuration) is skipped, so a bad value can never break a consumer.
    internal async Task<(string Value, string Source)> ResolveAsync(string key, CancellationToken cancellationToken)
    {
        var definition = SystemSettingDefinitions.Get(key);
        var stored = await LoadStoredAsync(cancellationToken);
        return Resolve(definition, stored, _configuration);
    }

    // A legacy configuration value keeps working as it did before the registry,
    // which only required a positive number: the registry's bounds apply to
    // values entered through the application, not to what a deployment already has.
    private static bool TryReadLegacy(SystemSettingDefinition definition, string raw, out string value)
    {
        value = string.Empty;
        if (definition.Type != Domain.Enums.SystemSettingValueType.Integer)
            return definition.TryNormalize(raw, out value);
        if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || number <= 0)
            return false;
        value = number.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    internal static (string Value, string Source) Resolve(
        SystemSettingDefinition definition, IReadOnlyDictionary<string, string> stored, IConfiguration? configuration)
    {
        if (stored.TryGetValue(definition.Key, out var raw) && definition.TryNormalize(raw, out var fromDb))
            return (fromDb, "Database");

        if (definition.LegacyConfigKey is not null
            && configuration?[definition.LegacyConfigKey] is { Length: > 0 } configured
            && TryReadLegacy(definition, configured, out var fromConfig))
            return (fromConfig, "Configuration");

        return (definition.Default, "Default");
    }
}
