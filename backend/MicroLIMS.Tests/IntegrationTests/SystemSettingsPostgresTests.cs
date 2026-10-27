using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// WP3.1 database guarantees: SystemSettingVersions is append-only (the shared
// prevent_audit_modification trigger and the TRUNCATE guard), and the migration
// grants System.ConfigureSettings to the built-in administrator and removes the
// duplicate System.TimeZone row.
[Collection("PostgresDatabaseCollection")]
public class SystemSettingsPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public SystemSettingsPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    [PostgresFact]
    public async Task VersionsTable_RejectsUpdateDeleteAndTruncate()
    {
        await using var db = _fixture.CreateDbContext();
        var key = $"test.setting.{Guid.NewGuid():N}";
        var setting = new SystemSetting { Key = key, Value = "7", ValueType = SystemSettingValueType.Integer, Category = "Test", Description = "d", CurrentVersion = 1 };
        db.SystemSettings.Add(setting);
        await db.SaveChangesAsync();
        var version = new SystemSettingVersion
        {
            SettingId = setting.Id, Version = 1, Value = "7", PreviousValue = "5",
            ChangedByUserId = _fixture.SeededUserId, Reason = "append-only test"
        };
        db.SystemSettingVersions.Add(version);
        await db.SaveChangesAsync();

        await using var sqlDb = _fixture.CreateDbContext();
        var update = await Assert.ThrowsAsync<PostgresException>(() =>
            sqlDb.Database.ExecuteSqlRawAsync("UPDATE \"SystemSettingVersions\" SET \"Value\" = '99' WHERE \"Id\" = {0}", version.Id));
        Assert.Contains("append-only", update.Message, StringComparison.OrdinalIgnoreCase);

        var delete = await Assert.ThrowsAsync<PostgresException>(() =>
            sqlDb.Database.ExecuteSqlRawAsync("DELETE FROM \"SystemSettingVersions\" WHERE \"Id\" = {0}", version.Id));
        Assert.Contains("append-only", delete.Message, StringComparison.OrdinalIgnoreCase);

        var truncate = await Assert.ThrowsAsync<PostgresException>(() =>
            sqlDb.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"SystemSettingVersions\""));
        Assert.Contains("TRUNCATE", truncate.Message);

        await using var verify = _fixture.CreateDbContext();
        Assert.Equal("7", (await verify.SystemSettingVersions.AsNoTracking().SingleAsync(v => v.Id == version.Id)).Value);
    }

    [PostgresFact]
    public async Task Key_IsUnique_AndAVersionNumberIsNotReused()
    {
        await using var db = _fixture.CreateDbContext();
        var key = $"test.unique.{Guid.NewGuid():N}";
        db.SystemSettings.Add(new SystemSetting { Key = key, Value = "1", Category = "Test", Description = "d", CurrentVersion = 1 });
        await db.SaveChangesAsync();

        await using var dup = _fixture.CreateDbContext();
        dup.SystemSettings.Add(new SystemSetting { Key = key, Value = "2", Category = "Test", Description = "d", CurrentVersion = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => dup.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task Migration_GrantsConfigureSettingsToTheAdministrator_AndDropsTheDuplicateTimeZoneRow()
    {
        await ScratchPostgresDatabase.RunAsync(async connectionString =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(connectionString);
            var ids = db.GetService<IMigrationsAssembly>().Migrations.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            var target = ids.Single(k => k.EndsWith("_AddSystemSettingsRegistry", StringComparison.Ordinal));
            var before = ids[ids.IndexOf(target) - 1];

            await db.Database.MigrateAsync(before);
            await db.Database.ExecuteSqlRawAsync(@"INSERT INTO ""Roles"" (""Type"", ""Name"", ""IsSystemRole"") VALUES (0, 'System Administrator', TRUE);");
            await db.Database.ExecuteSqlRawAsync(
                @"INSERT INTO ""ConfigurationSettings"" (""SettingKey"", ""SettingValue"", ""DataType"", ""SettingGroup"", ""ModifiedAt"")
                  VALUES ('System.TimeZone', 'UTC', 'string', 'System', now()),
                         ('DocumentControl.Watermark.OnDownload', 'true', 'boolean', 'DocumentControl', now());");
            var versionBefore = await db.Roles.Where(r => r.Type == RoleType.SystemAdministrator).Select(r => r.PermissionVersion).SingleAsync();

            await db.Database.MigrateAsync(target);
            db.ChangeTracker.Clear();

            var role = await db.Roles.AsNoTracking().SingleAsync(r => r.Type == RoleType.SystemAdministrator);
            var held = await db.RolePermissions.Where(rp => rp.RoleId == role.Id).Select(rp => rp.Permission!.Code).ToListAsync();
            Assert.Contains(PermissionConstants.SystemConfigureSettings, held);
            Assert.True(await db.Permissions.AnyAsync(p => p.Code == PermissionConstants.SystemConfigureSettings && p.IsEnforced));
            Assert.True(role.PermissionVersion > versionBefore, "PermissionVersion must be bumped so issued tokens are revalidated");

            var audit = await db.AuditLogs.SingleAsync(a => a.Action == "ROLE_PERMISSIONS_UPDATED" && a.NewValue!.Contains(PermissionConstants.SystemConfigureSettings));
            Assert.Equal(AuditActionCategory.Security, audit.ActionCategory);
            Assert.Equal(role.Id.ToString(), audit.EntityId);

            Assert.False(await db.ConfigurationSettings.AnyAsync(s => s.SettingKey == "System.TimeZone"));
            Assert.True(await db.ConfigurationSettings.AnyAsync(s => s.SettingKey == "DocumentControl.Watermark.OnDownload"));
        });
    }
}
