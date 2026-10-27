using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DatabaseOverviewServiceTests
{
    private sealed class FakeDatabaseStatsQuery : IDatabaseStatsQuery
    {
        public DatabaseSizeSnapshot Snapshot { get; set; } = new(0, "0 bytes", Array.Empty<TableSizeRow>());
        public int? RequestedTopCount { get; private set; }

        public Task<DatabaseSizeSnapshot> GetSizeSnapshotAsync(int topTableCount, CancellationToken cancellationToken = default)
        {
            RequestedTopCount = topTableCount;
            return Task.FromResult(Snapshot);
        }
    }

    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Overview_ShapesSizesRowEstimatesAndTopTables()
    {
        var stats = new FakeDatabaseStatsQuery
        {
            Snapshot = new DatabaseSizeSnapshot(10_485_760, "10 MB", new[]
            {
                new TableSizeRow("public", "Small", 8192, "8192 bytes", 3f),
                new TableSizeRow("public", "NeverAnalyzed", 900_000, "879 kB", -1f),
                new TableSizeRow("public", "Fractional", 4096, "4096 bytes", 2.6f),
            })
        };
        var service = new DatabaseOverviewService(stats, NewDb());

        var overview = await service.GetOverviewAsync();

        Assert.Equal(10_485_760, overview.DatabaseSizeBytes);
        Assert.Equal("10 MB", overview.DatabaseSizePretty);
        Assert.Equal(10, stats.RequestedTopCount);

        // Ordered by total size, largest first.
        Assert.Equal(new[] { "NeverAnalyzed", "Small", "Fractional" }, overview.TopTables.Select(t => t.Name));
        // reltuples = -1 means "never analyzed": unknown, not zero.
        Assert.Null(overview.TopTables[0].EstimatedRows);
        Assert.Equal(3, overview.TopTables[1].EstimatedRows);
        Assert.Equal(3, overview.TopTables[2].EstimatedRows);
        Assert.Null(overview.LatestBackup);
    }

    [Fact]
    public async Task Overview_CapsTheTableListAtTen()
    {
        var rows = Enumerable.Range(1, 12)
            .Select(i => new TableSizeRow("public", $"T{i:00}", i * 1000L, $"{i} kB", 1f))
            .ToList();
        var service = new DatabaseOverviewService(
            new FakeDatabaseStatsQuery { Snapshot = new DatabaseSizeSnapshot(1, "1 bytes", rows) },
            NewDb());

        var overview = await service.GetOverviewAsync();

        Assert.Equal(10, overview.TopTables.Count);
        Assert.Equal("T12", overview.TopTables[0].Name);
        Assert.DoesNotContain(overview.TopTables, t => t.Name == "T01");
    }

    [Fact]
    public async Task Overview_ReturnsTheMostRecentBackupRun()
    {
        var db = NewDb();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                StartedAt = new DateTime(2026, 10, 25, 2, 0, 0, DateTimeKind.Utc),
                FinishedAt = new DateTime(2026, 10, 25, 2, 5, 0, DateTimeKind.Utc),
                Succeeded = true, FileName = "old.dump", SizeBytes = 100, Host = "h"
            },
            new BackupRun
            {
                StartedAt = new DateTime(2026, 10, 27, 2, 0, 0, DateTimeKind.Utc),
                FinishedAt = new DateTime(2026, 10, 27, 2, 0, 9, DateTimeKind.Utc),
                Succeeded = false, FileName = "new.dump", SizeBytes = null,
                Error = "pg_dump failed with exit code 1", Host = "h"
            });
        await db.SaveChangesAsync();

        var service = new DatabaseOverviewService(new FakeDatabaseStatsQuery(), db);

        var latest = (await service.GetOverviewAsync()).LatestBackup;

        Assert.NotNull(latest);
        Assert.Equal("new.dump", latest!.FileName);
        Assert.False(latest.Succeeded);
        Assert.Null(latest.SizeBytes);
        Assert.Equal("pg_dump failed with exit code 1", latest.Error);
    }
}
