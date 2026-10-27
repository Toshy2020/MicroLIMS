using MicroLIMS.Persistence.Diagnostics;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// The real catalog SQL, against a freshly migrated PostgreSQL database.
public class DatabaseStatsQueryPostgresTests
{
    [PostgresFact]
    public async Task Snapshot_ReportsDatabaseSizeAndTheLargestUserTables()
    {
        await ScratchPostgresDatabase.RunAsync(async connectionString =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(connectionString);
            var query = new DatabaseStatsQuery(db);

            var snapshot = await query.GetSizeSnapshotAsync(topTableCount: 3);

            Assert.True(snapshot.DatabaseSizeBytes > 0);
            Assert.False(string.IsNullOrWhiteSpace(snapshot.DatabaseSizePretty));

            Assert.InRange(snapshot.Tables.Count, 1, 3);
            Assert.DoesNotContain(snapshot.Tables, t => t.Schema is "pg_catalog" or "information_schema" || t.Schema.StartsWith("pg_toast", StringComparison.Ordinal));
            Assert.All(snapshot.Tables, t => Assert.False(string.IsNullOrWhiteSpace(t.Name)));
            Assert.Equal(
                snapshot.Tables.OrderByDescending(t => t.TotalSizeBytes).Select(t => t.Name),
                snapshot.Tables.Select(t => t.Name));

            // The migrated schema contains the BackupRuns table; it is a user
            // table, so it must be reachable through the same query.
            var backupRuns = await query.GetSizeSnapshotAsync(topTableCount: 500);
            Assert.Contains(backupRuns.Tables, t => t.Name == "BackupRuns" && t.Schema == "public");
        });
    }
}
