using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.SystemSettings;

namespace MicroLIMS.Application.Services;

/// <summary>
/// Scoped reader (cached per request) for the UTC instant when SOP revision capture
/// was installed on this database.
/// </summary>
public sealed class SopCaptureStartDateReader : ISopCaptureStartDateReader
{
    private readonly IMicroLimsDbContext _db;
    private bool _loaded;
    private DateTime? _cachedUtc;

    public SopCaptureStartDateReader(IMicroLimsDbContext db) => _db = db;

    public async Task<DateTime?> GetStartDateUtcAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded) return _cachedUtc;

        var raw = await _db.SystemSettings.AsNoTracking()
            .Where(s => s.Key == SystemSettingKeys.SopCaptureStartDateUtc)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (raw is not null && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            _cachedUtc = parsed.UtcDateTime;
        }

        _loaded = true;
        return _cachedUtc;
    }
}
