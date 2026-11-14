using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.Migrations;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class NotificationSchemaPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public NotificationSchemaPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static readonly DateTime FixedUtc = new(2026, 11, 15, 8, 0, 0, DateTimeKind.Utc);

    [PostgresFact]
    public async Task SecondUnresolvedRowWithSameUserAndDedupeKey_Rejected()
    {
        var dedupeKey = $"ReviewWaiting:sample:{Guid.NewGuid():N}";
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.NotificationLogs.Add(new NotificationLog
            {
                UserId = _fixture.SeededUserId,
                Type = "ReviewWaiting",
                Message = "Sample awaiting review.",
                DedupeKey = dedupeKey,
                CreatedAt = FixedUtc,
                ResolvedAt = null
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        actDb.NotificationLogs.Add(new NotificationLog
        {
            UserId = _fixture.SeededUserId,
            Type = "ReviewWaiting",
            Message = "Sample awaiting review (duplicate).",
            DedupeKey = dedupeKey,
            CreatedAt = FixedUtc.AddMinutes(1),
            ResolvedAt = null
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => actDb.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pg.SqlState);
    }

    [PostgresFact]
    public async Task SameKeyAfterResolve_Allowed()
    {
        var dedupeKey = $"ReviewWaiting:sample:{Guid.NewGuid():N}";
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.NotificationLogs.Add(new NotificationLog
            {
                UserId = _fixture.SeededUserId,
                Type = "ReviewWaiting",
                Message = "First review.",
                DedupeKey = dedupeKey,
                CreatedAt = FixedUtc,
                ResolvedAt = FixedUtc.AddMinutes(10)
            });
            await arrangeDb.SaveChangesAsync();
        }

        int secondId;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var second = new NotificationLog
            {
                UserId = _fixture.SeededUserId,
                Type = "ReviewWaiting",
                Message = "Re-submitted review.",
                DedupeKey = dedupeKey,
                CreatedAt = FixedUtc.AddMinutes(20),
                ResolvedAt = null
            };
            actDb.NotificationLogs.Add(second);
            await actDb.SaveChangesAsync();
            secondId = second.Id;
        }

        await using var assertDb = _fixture.CreateDbContext();
        var count = await assertDb.NotificationLogs.AsNoTracking()
            .CountAsync(n => n.UserId == _fixture.SeededUserId && n.DedupeKey == dedupeKey);
        Assert.Equal(2, count);
        var open = await assertDb.NotificationLogs.AsNoTracking().SingleAsync(n => n.Id == secondId);
        Assert.Null(open.ResolvedAt);
    }

    [PostgresFact]
    public async Task NullDedupeKey_NotLimited()
    {
        var marker = $"null-dedupe-{Guid.NewGuid():N}";
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.NotificationLogs.Add(new NotificationLog
            {
                UserId = _fixture.SeededUserId,
                Type = "DirectMessage",
                Message = $"{marker} 1",
                DedupeKey = null,
                CreatedAt = FixedUtc
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.NotificationLogs.Add(new NotificationLog
            {
                UserId = _fixture.SeededUserId,
                Type = "DirectMessage",
                Message = $"{marker} 2",
                DedupeKey = null,
                CreatedAt = FixedUtc.AddSeconds(1)
            });
            await actDb.SaveChangesAsync();
        }

        await using var assertDb = _fixture.CreateDbContext();
        var count = await assertDb.NotificationLogs.AsNoTracking()
            .CountAsync(n => n.UserId == _fixture.SeededUserId && n.Message.StartsWith(marker));
        Assert.Equal(2, count);
    }

    [PostgresFact]
    public async Task SecondEscalationEventForSameRuleAndRecord_Rejected()
    {
        int ruleId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var rule = new EscalationRule
            {
                SectionId = _fixture.SeededSectionId,
                Trigger = EscalationTrigger.ReviewWaiting,
                ThresholdWorkingHours = 4m,
                NotifyRoleType = RoleType.SectionHead,
                IsActive = true,
                CreatedByUserId = _fixture.SeededUserId,
                CreatedAt = FixedUtc
            };
            arrangeDb.EscalationRules.Add(rule);
            await arrangeDb.SaveChangesAsync();
            ruleId = rule.Id;

            arrangeDb.EscalationEvents.Add(new EscalationEvent
            {
                EscalationRuleId = ruleId,
                RecordType = "Sample",
                RecordId = 9001,
                FiredAt = FixedUtc,
                RecipientUserIds = $"[{_fixture.SeededUserId}]",
                NoRecipient = false
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        actDb.EscalationEvents.Add(new EscalationEvent
        {
            EscalationRuleId = ruleId,
            RecordType = "Sample",
            RecordId = 9001,
            FiredAt = FixedUtc.AddMinutes(5),
            RecipientUserIds = $"[{_fixture.SeededUserId}]",
            NoRecipient = false
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => actDb.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pg.SqlState);
    }

    [PostgresFact]
    public async Task WorkingDayWithStartAfterEnd_Rejected()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        await db.Database.ExecuteSqlRawAsync(@"DELETE FROM ""LabWorkingHours"" WHERE ""DayOfWeek"" = 1;");
        db.LabWorkingHours.Add(new LabWorkingHours
        {
            DayOfWeek = DayOfWeek.Monday,
            IsWorkingDay = true,
            StartMinute = 16 * 60,
            EndMinute = 8 * 60
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, pg.SqlState);

        await tx.RollbackAsync();
    }

    [PostgresFact]
    public async Task ExistingNotificationRows_StillLoad()
    {
        var marker = $"legacy-row-{Guid.NewGuid():N}";
        int insertedId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            // Insert using only the pre-C5 columns, leaving Title, TargetType, TargetId,
            // SectionId, DedupeKey, ReadAt, ResolvedAt NULL as existing production rows have.
            var insertedIds = await arrangeDb.Database.SqlQueryRaw<int>(
                """
                INSERT INTO "NotificationLogs"
                    ("UserId", "Type", "Message", "SampleId", "TestOrderId", "Severity", "IsRead", "EmailSent", "CreatedAt")
                VALUES
                    ({0}, 'MediaExpiry', {1}, NULL, NULL, 'warning', FALSE, FALSE, {2})
                RETURNING "Id" AS "Value";
                """,
                _fixture.SeededUserId, marker, FixedUtc)
                .ToListAsync();
            insertedId = insertedIds.Single();
        }

        await using var assertDb = _fixture.CreateDbContext();
        var loaded = await assertDb.NotificationLogs.AsNoTracking().SingleAsync(n => n.Id == insertedId);
        Assert.Equal(_fixture.SeededUserId, loaded.UserId);
        Assert.Equal("MediaExpiry", loaded.Type);
        Assert.Equal(marker, loaded.Message);
        Assert.Equal("warning", loaded.Severity);
        Assert.False(loaded.IsRead);
        Assert.Null(loaded.Title);
        Assert.Null(loaded.TargetType);
        Assert.Null(loaded.TargetId);
        Assert.Null(loaded.SectionId);
        Assert.Null(loaded.DedupeKey);
        Assert.Null(loaded.ReadAt);
        Assert.Null(loaded.ResolvedAt);
    }

    [PostgresFact]
    public async Task Migration_GrantsNotificationsConfigureToSectionHeadAndQcManager()
    {
        const string code = PermissionConstants.NotificationsConfigure;
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        // Reset to the pre-migration state for this permission code.
        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM "RolePermissions" WHERE "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" = {0});
            DELETE FROM "Permissions" WHERE "Code" = {0};
            """,
            new object[] { code });

        var sectionHead = new Role { Type = RoleType.SectionHead, Name = $"Head (system) {Guid.NewGuid():N}", IsSystemRole = true, IsActive = true };
        var qcManager = new Role { Type = RoleType.QcManager, Name = $"QC Manager (system) {Guid.NewGuid():N}", IsSystemRole = true, IsActive = true };
        var customHead = new Role { Type = RoleType.SectionHead, Name = $"Head (custom) {Guid.NewGuid():N}", IsSystemRole = false, IsActive = true };
        var analyst = new Role { Type = RoleType.Analyst, Name = $"Analyst (system) {Guid.NewGuid():N}", IsSystemRole = true, IsActive = true };
        var admin = new Role { Type = RoleType.SystemAdministrator, Name = $"Admin (system) {Guid.NewGuid():N}", IsSystemRole = true, IsActive = true };
        var taintedHead = new Role { Type = RoleType.SectionHead, Name = $"Head (with Users.Manage) {Guid.NewGuid():N}", IsSystemRole = true, IsActive = true };
        db.Roles.AddRange(sectionHead, qcManager, customHead, analyst, admin, taintedHead);
        await db.SaveChangesAsync();

        var usersManagePerm = await db.Permissions.AsNoTracking().SingleOrDefaultAsync(p => p.Code == PermissionConstants.UsersManage);
        if (usersManagePerm is null)
        {
            usersManagePerm = new Permission { Code = PermissionConstants.UsersManage, Description = "Manage users", IsEnforced = true };
            db.Permissions.Add(usersManagePerm);
            await db.SaveChangesAsync();
        }
        db.RolePermissions.Add(new RolePermission { RoleId = taintedHead.Id, PermissionId = usersManagePerm.Id });
        await db.SaveChangesAsync();

        var sqlOps = new AddNotificationCentre().UpOperations.OfType<SqlOperation>().ToList();
        foreach (var op in sqlOps)
            await db.Database.ExecuteSqlRawAsync(op.Sql);

        var permission = await db.Permissions.AsNoTracking().SingleAsync(p => p.Code == code);
        Assert.False(string.IsNullOrWhiteSpace(permission.Description));

        var holderIds = await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.Permission!.Code == code)
            .Select(rp => rp.RoleId)
            .ToListAsync();

        Assert.Contains(sectionHead.Id, holderIds);
        Assert.Contains(qcManager.Id, holderIds);
        Assert.DoesNotContain(customHead.Id, holderIds);
        Assert.DoesNotContain(analyst.Id, holderIds);
        Assert.DoesNotContain(admin.Id, holderIds);
        Assert.DoesNotContain(taintedHead.Id, holderIds);

        // Idempotent on re-run.
        var totalGrants = await db.RolePermissions.CountAsync();
        foreach (var op in sqlOps)
            await db.Database.ExecuteSqlRawAsync(op.Sql);
        Assert.Equal(totalGrants, await db.RolePermissions.CountAsync());

        // Down revokes and deletes the permission row cleanly.
        foreach (var op in new AddNotificationCentre().DownOperations.OfType<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(op.Sql);
        Assert.False(await db.Permissions.AnyAsync(p => p.Code == code));
        Assert.False(await db.RolePermissions.AnyAsync(rp => rp.Permission!.Code == code));

        await tx.RollbackAsync();
    }
}
