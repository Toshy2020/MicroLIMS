using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

public class KpiSettingsMigrationPostgresTests
{
    private const string MigrationName = "AddKpiSettingsAndAnalystAvailability";

    private static async Task<bool> RejectedAsync(string cs, Action<MicroLimsDbContext> add)
    {
        await using var db = ScratchPostgresDatabase.NewContext(cs);
        add(db);
        try { await db.SaveChangesAsync(); return false; }
        catch (DbUpdateException) { return true; }
    }

    private static KpiSetting Setting(int sectionId, decimal hours = 6.0m, int mask = 31, int horizon = 5) => new()
    {
        SectionId = sectionId, HoursPerAnalystDay = hours, WorkingWeekdays = mask, LoadHorizonDays = horizon,
        UpdatedByUserId = 1, UpdatedAt = DateTime.UtcNow
    };

    private static AnalystAvailability Leave(DateOnly from, DateOnly to, decimal fraction) => new()
    {
        UserId = 1, FromDate = from, ToDate = to, FractionAvailable = fraction, Reason = "Training",
        EnteredByUserId = 1, EnteredAt = DateTime.UtcNow
    };

    [PostgresFact]
    public async Task Migration_EnforcesConstraints_AndDownDropsBothTables()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            string before;
            await using (var db = ScratchPostgresDatabase.NewContext(cs))
            {
                var all = db.Database.GetMigrations().ToList();
                var target = all.Single(m => m.EndsWith("_" + MigrationName));
                before = all[all.IndexOf(target) - 1];
                await db.Database.MigrateAsync();

                // AddOrgSectionSegregation already inserted QC and MICRO into a migrated database; clear them so the explicit ids
                // and codes below cannot collide. Explicit ids on identity columns do not advance the sequence, which is fine here.
                await db.Database.ExecuteSqlRawAsync("DELETE FROM \"DocumentSections\"; DELETE FROM \"DocumentDepartments\";");

                db.DocumentDepartments.Add(new DocumentDepartment { Id = 1, Code = "QC", Name = "QC" });
                for (var i = 1; i <= 5; i++)
                    db.DocumentSections.Add(new DocumentSection { Id = i, Code = "S" + i, Name = "Section " + i, DepartmentId = 1 });
                db.Roles.Add(new Role { Id = 1, Type = RoleType.Analyst, Name = "Analyst" });
                db.Users.Add(new User { Id = 1, FullName = "A", Username = "a", RoleId = 1, PasswordHash = "x" });
                await db.SaveChangesAsync();
            }

            Assert.False(await RejectedAsync(cs, db => db.KpiSettings.Add(Setting(1))));
            Assert.True(await RejectedAsync(cs, db => db.KpiSettings.Add(Setting(1))));              // unique SectionId
            Assert.True(await RejectedAsync(cs, db => db.KpiSettings.Add(Setting(2, hours: 0m))));   // hours > 0
            Assert.True(await RejectedAsync(cs, db => db.KpiSettings.Add(Setting(3, hours: 24.5m)))); // hours <= 24
            Assert.True(await RejectedAsync(cs, db => db.KpiSettings.Add(Setting(4, mask: 0))));     // at least one weekday
            Assert.True(await RejectedAsync(cs, db => db.KpiSettings.Add(Setting(5, horizon: 31)))); // horizon <= 30

            var d = new DateOnly(2026, 10, 14);
            Assert.False(await RejectedAsync(cs, db => db.AnalystAvailabilities.Add(Leave(d, d.AddDays(2), 0.5m))));
            Assert.True(await RejectedAsync(cs, db => db.AnalystAvailabilities.Add(Leave(d.AddDays(2), d, 0.5m)))); // To >= From
            Assert.True(await RejectedAsync(cs, db => db.AnalystAvailabilities.Add(Leave(d, d, 1.5m))));            // fraction <= 1

            await using (var db = ScratchPostgresDatabase.NewContext(cs))
                await db.GetService<IMigrator>().MigrateAsync(before);

            await using var c = new NpgsqlConnection(cs);
            await c.OpenAsync();
            foreach (var table in new[] { "KpiSettings", "AnalystAvailabilities" })
            {
                await using var cmd = new NpgsqlCommand($"SELECT to_regclass('public.\"{table}\"')::text", c);
                Assert.True(await cmd.ExecuteScalarAsync() is null or DBNull, table + " should be gone after Down");
            }
        });
    }
}
