using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class SopCaptureStartDatePostgresTests(PostgresTestFixture fixture)
{
    private const string StartDateKey = "sopCapture.startDateUtc";

    [PostgresFact]
    public async Task Migration_RecordSopCaptureStartDate_InsertsSingleUtcRow_IsIdempotent_AndDownRemovesIt()
    {
        await ScratchPostgresDatabase.RunAsync(async connectionString =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(connectionString);
            var ids = db.GetService<IMigrationsAssembly>().Migrations.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            var target = ids.Single(k => k.EndsWith("_RecordSopCaptureStartDate", StringComparison.Ordinal));
            var before = ids[ids.IndexOf(target) - 1];

            await db.Database.MigrateAsync(before);
            Assert.False(await db.SystemSettings.AsNoTracking().AnyAsync(s => s.Key == StartDateKey));

            var lowerBound = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.Database.MigrateAsync(target);
            var upperBound = DateTimeOffset.UtcNow.AddMinutes(1);

            var rows = await db.SystemSettings.AsNoTracking().Where(s => s.Key == StartDateKey).ToListAsync();
            var row = Assert.Single(rows);
            Assert.True(
                DateTimeOffset.TryParse(row.Value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed),
                $"Expected ISO-8601 UTC timestamp, got '{row.Value}'.");
            Assert.Equal(TimeSpan.Zero, parsed.Offset);
            Assert.InRange(parsed, lowerBound, upperBound);

            // Re-running the migration's Up SQL must not duplicate or fail.
            var upSql = db.GetService<IMigrator>().GenerateScript(before, target);
            var insertOnlySql = string.Join("\n",
                upSql.Split('\n').Where(l => !l.Contains("__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase)));
            await db.Database.ExecuteSqlRawAsync(insertOnlySql);
            Assert.Equal(1, await db.SystemSettings.AsNoTracking().CountAsync(s => s.Key == StartDateKey));

            // Down removes the row.
            await db.Database.MigrateAsync(before);
            Assert.False(await db.SystemSettings.AsNoTracking().AnyAsync(s => s.Key == StartDateKey));
        });
    }

    [PostgresFact]
    public async Task SystemSettingsService_DoesNotList_OrAllowEditing_SopCaptureStartDate()
    {
        await using var db = fixture.CreateDbContext();
        var cache = new SystemSettingsCache();
        var signatures = new ElectronicSignatureService(db);
        var service = new SystemSettingsService(db, cache, signatures);

        var overview = await service.GetOverviewAsync();
        Assert.DoesNotContain(overview.Settings, s => s.Key == StartDateKey);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateAsync(StartDateKey, "2020-01-01T00:00:00Z", "attempt edit", fixture.SeededUserId, "Admin@123", null));
    }
}
