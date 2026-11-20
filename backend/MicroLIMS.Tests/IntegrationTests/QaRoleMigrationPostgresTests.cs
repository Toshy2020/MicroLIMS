using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Migrations;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class QaRoleMigrationPostgresTests
{
    private static readonly string[] QaOnlyCodes =
    {
        PermissionConstants.AuditReview,
        PermissionConstants.AccessReviewSign
    };

    // This suite replays AddQaRole alone. Exclude the grants from later migrations.
    private static readonly string[] LaterQaGrants =
        [PermissionConstants.CompetencyView, PermissionConstants.OosView, PermissionConstants.OosRequestDeviation];
    private static readonly string[] OriginalQaGrants = PermissionConstants.QaGrants
        .Except(LaterQaGrants)
        .OrderBy(code => code, StringComparer.Ordinal)
        .ToArray();

    private readonly PostgresTestFixture _fixture;

    public QaRoleMigrationPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static async Task RunAsync(MicroLimsDbContext db, IEnumerable<MigrationOperation> operations)
    {
        foreach (var op in operations.Cast<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(op.Sql);
    }

    private static async Task ResetToPreMigrationAsync(MicroLimsDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            @"DELETE FROM ""RolePermissions""
              WHERE ""RoleId"" IN (SELECT ""Id"" FROM ""Roles"" WHERE ""Type"" = 5 AND ""IsSystemRole"")
                 OR ""PermissionId"" IN (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" = ANY({0}));
              DELETE FROM ""Permissions"" WHERE ""Code"" = ANY({0});
              DELETE FROM ""Roles"" WHERE ""Type"" = 5 AND ""IsSystemRole"";",
            new object[] { QaOnlyCodes });
        db.ChangeTracker.Clear();
    }

    [PostgresFact]
    public async Task Migration_creates_qa_role_with_exact_grants()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        await ResetToPreMigrationAsync(db);

        var up = new AddQaRole().UpOperations;
        await RunAsync(db, up);
        db.ChangeTracker.Clear();

        var qaRole = await db.Roles.AsNoTracking()
            .SingleAsync(r => r.Type == RoleType.Qa && r.IsSystemRole);
        Assert.Equal("QA", qaRole.Name);
        Assert.True(qaRole.IsSystemRole);
        Assert.True(qaRole.IsActive);

        var grantedCodes = await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == qaRole.Id)
            .Select(rp => rp.Permission!.Code)
            .ToListAsync();
        Assert.Equal(OriginalQaGrants, grantedCodes.OrderBy(c => c, StringComparer.Ordinal));

        var newPermissions = await db.Permissions.AsNoTracking()
            .Where(p => QaOnlyCodes.Contains(p.Code))
            .ToListAsync();
        Assert.Equal(2, newPermissions.Count);
        Assert.All(newPermissions, p =>
        {
            Assert.True(p.IsEnforced);
            Assert.False(string.IsNullOrWhiteSpace(p.Description));
        });

        // Idempotent on re-run.
        var rolesCount = await db.Roles.AsNoTracking().CountAsync();
        var permissionsCount = await db.Permissions.AsNoTracking().CountAsync();
        var grantsCount = await db.RolePermissions.AsNoTracking().CountAsync();

        await RunAsync(db, up);
        db.ChangeTracker.Clear();

        Assert.Equal(rolesCount, await db.Roles.AsNoTracking().CountAsync());
        Assert.Equal(permissionsCount, await db.Permissions.AsNoTracking().CountAsync());
        Assert.Equal(grantsCount, await db.RolePermissions.AsNoTracking().CountAsync());

        await tx.RollbackAsync();
    }

    [PostgresFact]
    public async Task Migration_writes_role_permissions_updated_audit_row()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        await ResetToPreMigrationAsync(db);
        var maxAuditIdBefore = await db.AuditLogs.AsNoTracking().MaxAsync(a => (int?)a.Id) ?? 0;

        await RunAsync(db, new AddQaRole().UpOperations);
        db.ChangeTracker.Clear();

        var qaRole = await db.Roles.AsNoTracking()
            .SingleAsync(r => r.Type == RoleType.Qa && r.IsSystemRole);

        var audit = await db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.Id > maxAuditIdBefore
                && a.EntityName == nameof(Role)
                && a.Action == "ROLE_PERMISSIONS_UPDATED"
                && a.EntityId == qaRole.Id.ToString());

        Assert.Equal(ActorType.System, audit.ActorType);
        Assert.Equal(0, audit.UserId);
        Assert.Contains("AddQaRole", audit.SystemProcessName ?? string.Empty, StringComparison.Ordinal);
        Assert.NotNull(audit.NewValue);

        using var doc = JsonDocument.Parse(audit.NewValue!);
        var recordedCodes = doc.RootElement.GetProperty("PermissionCodes").EnumerateArray()
            .Select(e => e.GetString()!)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(OriginalQaGrants, recordedCodes);

        await tx.RollbackAsync();
    }

    [PostgresFact]
    public async Task Migration_bumps_permission_version()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        await ResetToPreMigrationAsync(db);

        await RunAsync(db, new AddQaRole().UpOperations);
        db.ChangeTracker.Clear();

        var qaRole = await db.Roles.AsNoTracking()
            .SingleAsync(r => r.Type == RoleType.Qa && r.IsSystemRole);
        Assert.True(qaRole.PermissionVersion > 1);

        var versionAfterFirstUp = qaRole.PermissionVersion;
        await RunAsync(db, new AddQaRole().UpOperations);
        db.ChangeTracker.Clear();

        var qaRoleAfterSecondUp = await db.Roles.AsNoTracking()
            .SingleAsync(r => r.Id == qaRole.Id);
        Assert.Equal(versionAfterFirstUp, qaRoleAfterSecondUp.PermissionVersion);

        await tx.RollbackAsync();
    }

    [PostgresFact]
    public async Task Migration_changes_no_other_roles_grants()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        await ResetToPreMigrationAsync(db);

        var customReviewer = new Role { Name = "Custom Snapshot Reviewer", Type = RoleType.Reviewer, IsSystemRole = false, IsActive = true };
        var customQa = new Role { Name = "Custom Snapshot QA", Type = RoleType.Qa, IsSystemRole = false, IsActive = true };
        db.Roles.AddRange(customReviewer, customQa);
        await db.SaveChangesAsync();

        var sampleReviewPerm = await db.Permissions.AsNoTracking().SingleAsync(p => p.Code == PermissionConstants.SamplesReview);
        db.RolePermissions.Add(new RolePermission { RoleId = customReviewer.Id, PermissionId = sampleReviewPerm.Id });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var rolesBefore = await db.Roles.AsNoTracking()
            .OrderBy(r => r.Id)
            .Select(r => new { r.Id, r.PermissionVersion })
            .ToListAsync();
        var grantsBefore = await db.RolePermissions.AsNoTracking()
            .Select(rp => new { rp.RoleId, rp.Permission!.Code })
            .ToListAsync();
        var codesByRoleBefore = grantsBefore
            .GroupBy(g => g.RoleId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Code).OrderBy(c => c, StringComparer.Ordinal).ToList());

        await RunAsync(db, new AddQaRole().UpOperations);
        db.ChangeTracker.Clear();

        foreach (var before in rolesBefore)
        {
            var after = await db.Roles.AsNoTracking().SingleAsync(r => r.Id == before.Id);
            Assert.Equal(before.PermissionVersion, after.PermissionVersion);

            var expectedCodes = codesByRoleBefore.TryGetValue(before.Id, out var list) ? list : new List<string>();
            var actualCodes = await db.RolePermissions.AsNoTracking()
                .Where(rp => rp.RoleId == before.Id)
                .Select(rp => rp.Permission!.Code)
                .ToListAsync();
            Assert.Equal(expectedCodes, actualCodes.OrderBy(c => c, StringComparer.Ordinal).ToList());
        }

        await tx.RollbackAsync();
    }

    [PostgresFact]
    public async Task Fresh_seed_includes_the_original_qa_migration_grants()
    {
        List<string> freshCodes = new();
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            await using var freshDb = ScratchPostgresDatabase.NewContext(cs);
            await freshDb.Database.MigrateAsync();
            DbSeeder.Seed(freshDb);
            freshDb.ChangeTracker.Clear();

            var freshQa = await freshDb.Roles.AsNoTracking()
                .SingleAsync(r => r.Type == RoleType.Qa && r.IsSystemRole);
            freshCodes = await freshDb.RolePermissions.AsNoTracking()
                .Where(rp => rp.RoleId == freshQa.Id)
                .Select(rp => rp.Permission!.Code)
                .ToListAsync();
        });

        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        await ResetToPreMigrationAsync(db);
        await RunAsync(db, new AddQaRole().UpOperations);
        db.ChangeTracker.Clear();

        var migratedQa = await db.Roles.AsNoTracking()
            .SingleAsync(r => r.Type == RoleType.Qa && r.IsSystemRole);
        var migratedCodes = await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == migratedQa.Id)
            .Select(rp => rp.Permission!.Code)
            .ToListAsync();

        Assert.Equal(freshCodes.Except(LaterQaGrants).OrderBy(c => c, StringComparer.Ordinal),
            migratedCodes.OrderBy(c => c, StringComparer.Ordinal));
        Assert.Equal(OriginalQaGrants.Length, migratedCodes.Count);

        await tx.RollbackAsync();
    }

    [PostgresFact]
    public async Task Down_removes_role_when_unused_and_keeps_it_when_a_user_holds_it()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        // Case 1: unused QA role is removed by Down.
        await ResetToPreMigrationAsync(db);
        await RunAsync(db, new AddQaRole().UpOperations);
        db.ChangeTracker.Clear();

        var unusedQaId = await db.Roles.AsNoTracking()
            .Where(r => r.Type == RoleType.Qa && r.IsSystemRole)
            .Select(r => r.Id)
            .SingleAsync();

        await RunAsync(db, new AddQaRole().DownOperations);
        db.ChangeTracker.Clear();

        Assert.False(await db.Permissions.AsNoTracking().AnyAsync(p => QaOnlyCodes.Contains(p.Code)));
        Assert.False(await db.RolePermissions.AsNoTracking().AnyAsync(rp => QaOnlyCodes.Contains(rp.Permission!.Code)));
        Assert.False(await db.RolePermissions.AsNoTracking().AnyAsync(rp => rp.RoleId == unusedQaId));
        Assert.False(await db.Roles.AsNoTracking().AnyAsync(r => r.Id == unusedQaId));

        // Case 2: QA role held by a user is kept by Down, while permissions and grants are removed.
        await RunAsync(db, new AddQaRole().UpOperations);
        db.ChangeTracker.Clear();

        var heldQaId = await db.Roles.AsNoTracking()
            .Where(r => r.Type == RoleType.Qa && r.IsSystemRole)
            .Select(r => r.Id)
            .SingleAsync();

        db.Users.Add(new User
        {
            FullName = "QA Holder",
            Username = "qa_holder_" + Guid.NewGuid().ToString("N")[..8],
            PasswordHash = "hashed_dummy",
            RoleId = heldQaId,
            IsActive = true
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await RunAsync(db, new AddQaRole().DownOperations);
        db.ChangeTracker.Clear();

        Assert.False(await db.Permissions.AsNoTracking().AnyAsync(p => QaOnlyCodes.Contains(p.Code)));
        Assert.False(await db.RolePermissions.AsNoTracking().AnyAsync(rp => QaOnlyCodes.Contains(rp.Permission!.Code)));
        Assert.False(await db.RolePermissions.AsNoTracking().AnyAsync(rp => rp.RoleId == heldQaId));
        Assert.True(await db.Roles.AsNoTracking().AnyAsync(r => r.Id == heldQaId));

        await tx.RollbackAsync();
    }
}
