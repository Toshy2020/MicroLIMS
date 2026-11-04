using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;

namespace MicroLIMS.Application.Services;

// Shapes the Overview: database size, the largest tables, and the most recent
// backup run. Reads only; nothing here changes the database.
public class DatabaseOverviewService : IDatabaseOverviewService
{
    public const int TopTableCount = 10;

    private readonly IDatabaseStatsQuery _stats;
    private readonly IMicroLimsDbContext _db;

    public DatabaseOverviewService(IDatabaseStatsQuery stats, IMicroLimsDbContext db)
    {
        _stats = stats;
        _db = db;
    }

    public async Task<DatabaseOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _stats.GetSizeSnapshotAsync(TopTableCount, cancellationToken);

        var latest = await _db.BackupRuns
            .AsNoTracking()
            .OrderByDescending(b => b.StartedAt)
            .Select(b => new BackupRunDto(
                b.Id,
                b.StartedAt,
                b.FinishedAt,
                b.Succeeded,
                b.FileName,
                b.SizeBytes,
                b.Error,
                b.Host))
            .FirstOrDefaultAsync(cancellationToken);

        var tables = snapshot.Tables
            .OrderByDescending(t => t.TotalSizeBytes)
            .Take(TopTableCount)
            .Select(t => new DatabaseTableSizeDto(
                t.Schema,
                t.Name,
                t.TotalSizeBytes,
                t.TotalSizePretty,
                EstimateRows(t.ReltuplesEstimate)))
            .ToList();

        return new DatabaseOverviewDto(
            snapshot.DatabaseSizeBytes,
            snapshot.DatabaseSizePretty,
            tables,
            latest);
    }

    // reltuples is -1 until the first ANALYZE; that is "unknown", not zero.
    private static long? EstimateRows(float reltuples) =>
        reltuples < 0 ? null : (long)Math.Round(reltuples, MidpointRounding.AwayFromZero);
}
