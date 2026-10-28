using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Authentication;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// WP2.2: every administrative user and role action carries a reason that
// lands in AuditLog.Reason, and the D7 actions (user role
// change, role permission edit, role delete) are also electronically signed.
public class AdminReasonAndSignatureTests
{
    private const string AdminPassword = "AdminP@ssw0rd!";
    private const string Reason = "Quarterly access review";

    private static MicroLimsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        db.Roles.AddRange(
            new Role { Id = 1, Type = RoleType.SystemAdministrator, Name = "System Administrator", IsSystemRole = true, IsActive = true },
            new Role { Id = 2, Type = RoleType.SectionHead, Name = "Section Head", IsSystemRole = true, IsActive = true },
            new Role { Id = 3, Type = RoleType.Reviewer, Name = "Reviewer", IsSystemRole = true, IsActive = true },
            new Role { Id = 4, Type = RoleType.Analyst, Name = "Analyst", IsSystemRole = true, IsActive = true });
        db.SaveChanges();
        DbSeeder.SeedPermissionsAndGrants(db);
        // Two administrators: one acts, the other keeps the system administrable.
        db.Users.AddRange(
            new User { Id = 1, FullName = "Acting Admin", Username = "actor", RoleId = 1, IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword, 4) },
            new User { Id = 2, FullName = "Standing Admin", Username = "standing", RoleId = 1, IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword, 4) },
            new User { Id = 10, FullName = "Target User", Username = "target", Email = "t@test.com", RoleId = 4, IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword("Whatever1!", 4) });
        db.SaveChanges();
        return db;
    }

    private static UserService CreateUsers(MicroLimsDbContext db)
    {
        var emailSender = new EmailSender("", 587, "", "", "no-reply@microlims.local", false);
        var securityAudit = new SecurityAuditService(db, new SystemSecurityRequestContext());
        AccessTokenIssuer tokenIssuer = _ => "fake-jwt-token";
        var auth = new AuthenticationService(db, tokenIssuer, new PermissionService(db), emailSender, NullLogger<AuthenticationService>.Instance, securityAudit);
        return new UserService(db, auth, securityAudit);
    }

    private static RoleService CreateRoles(MicroLimsDbContext db) => new(db, new PermissionService(db));

    // ---- a reason is required on every action --------------------------------

    public static IEnumerable<object?[]> BlankReasons() =>
        new[] { null, "", "   " }.Select(r => new object?[] { r });

    private static async Task AssertRefusedForReason(Func<Task> action)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Contains("reason", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, MemberData(nameof(BlankReasons))]
    public async Task UserActions_WithoutAReason_AreRefused(string? reason)
    {
        var db = CreateDb();
        var users = CreateUsers(db);

        await AssertRefusedForReason(() => users.CreateAsync(new User { FullName = "N", Username = "newone", RoleId = 4 }, "SecuredP@ss123!", reason));
        await AssertRefusedForReason(() => users.UpdateProfileAsync(10, "New", "target", "t@test.com", null, 1, reason));
        await AssertRefusedForReason(() => users.UpdateEmailAsync(10, "x@test.com", reason));
        await AssertRefusedForReason(() => users.UnlockUserAsync(10, reason, 1));
        await AssertRefusedForReason(() => users.InitiatePasswordResetAsync(10, reason, 1));
        await AssertRefusedForReason(() => users.ForcePasswordChangeAsync(10, reason, 1));
        await AssertRefusedForReason(() => users.SetStatusAsync(10, false, reason, 1));
        await AssertRefusedForReason(() => users.SetStatusAsync(10, true, reason, 1));
        await AssertRefusedForReason(() => users.ChangeRoleAsync(10, 3, reason, 1, AdminPassword));

        // Nothing was changed or signed.
        Assert.Equal(4, (await db.Users.AsNoTracking().FirstAsync(u => u.Id == 10)).RoleId);
        Assert.Empty(db.ElectronicSignatures);
        Assert.Empty(db.AuditLogs.Where(a => a.Action == "SignatureFailed"));
    }

    [Theory, MemberData(nameof(BlankReasons))]
    public async Task RoleActions_WithoutAReason_AreRefused(string? reason)
    {
        var db = CreateDb();
        var roles = CreateRoles(db);
        var created = await roles.CreateAsync("Disposable", null, RoleType.Analyst, Reason);

        await AssertRefusedForReason(() => roles.CreateAsync("Other", null, RoleType.Analyst, reason));
        await AssertRefusedForReason(() => roles.UpdateAsync(created.Id, "Renamed", null, reason));
        await AssertRefusedForReason(() => roles.UpdatePermissionsAsync(created.Id, new List<string> { PermissionConstants.AuditView }, 1, reason, AdminPassword));
        await AssertRefusedForReason(() => roles.DeleteAsync(created.Id, reason, 1, AdminPassword));

        Assert.Equal("Disposable", (await db.Roles.AsNoTracking().FirstAsync(r => r.Id == created.Id)).Name);
        Assert.Empty(db.ElectronicSignatures);
    }

    [Fact]
    public async Task AReasonLongerThanTheLimit_IsRefused()
    {
        var db = CreateDb();
        var users = CreateUsers(db);
        var tooLong = new string('x', MicroLIMS.Application.Helpers.AdminActionReason.MaxLength + 1);

        await AssertRefusedForReason(() => users.UnlockUserAsync(10, tooLong, 1));
    }

    // ---- the reason is stored in AuditLog.Reason -----------------------------

    private static async Task AssertAuditedWithReason(MicroLimsDbContext db, string entityName, string entityId, string reason)
    {
        var rows = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == entityName && a.EntityId == entityId).ToListAsync();
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal(reason, r.Reason));
    }

    [Fact]
    public async Task UserActions_StoreTheReasonInTheAuditLogReasonColumn()
    {
        var db = CreateDb();
        var users = CreateUsers(db);

        var created = await users.CreateAsync(new User { FullName = "N", Username = "newone", RoleId = 4 }, "SecuredP@ss123!", "  New starter  ");
        await AssertAuditedWithReason(db, nameof(User), created.Id.ToString(), "New starter");

        var steps = new (string Reason, Func<Task> Act)[]
        {
            ("profile reason", () => users.UpdateProfileAsync(10, "Renamed", "target", "t@test.com", null, 1, "profile reason")),
            ("email reason", () => users.UpdateEmailAsync(10, "new@test.com", "email reason")),
            ("unlock reason", () => users.UnlockUserAsync(10, "unlock reason", 1)),
            ("force reason", () => users.ForcePasswordChangeAsync(10, "force reason", 1)),
            ("disable reason", () => users.SetStatusAsync(10, false, "disable reason", 1)),
            ("enable reason", () => users.SetStatusAsync(10, true, "enable reason", 1)),
            ("role reason", () => users.ChangeRoleAsync(10, 3, "role reason", 1, AdminPassword)),
        };
        foreach (var (reason, act) in steps)
        {
            var before = await db.AuditLogs.AsNoTracking().Select(a => a.Id).ToListAsync();
            await act();
            var added = await db.AuditLogs.AsNoTracking()
                .Where(a => !before.Contains(a.Id) && a.EntityName == nameof(User) && a.EntityId == "10").ToListAsync();
            Assert.NotEmpty(added);
            Assert.All(added, a => Assert.Equal(reason, a.Reason));
        }
    }

    [Fact]
    public async Task SecurityEvents_CarryTheSameReason()
    {
        var db = CreateDb();
        var users = CreateUsers(db);

        await users.SetStatusAsync(10, false, "Left the company", 1);

        Assert.Contains(db.SecurityAuditEvents, e => e.TargetUserId == 10 && e.Reason == "Left the company");
    }

    [Fact]
    public async Task RoleActions_StoreTheReasonInTheAuditLogReasonColumn()
    {
        var db = CreateDb();
        var roles = CreateRoles(db);

        var created = await roles.CreateAsync("QC Trainee", null, RoleType.Analyst, "create reason");
        await AssertAuditedWithReason(db, nameof(Role), created.Id.ToString(), "create reason");

        var before = await db.AuditLogs.AsNoTracking().Select(a => a.Id).ToListAsync();
        await roles.UpdateAsync(created.Id, "QC Trainee 2", "d", "rename reason");
        var renamed = await db.AuditLogs.AsNoTracking().Where(a => !before.Contains(a.Id)).ToListAsync();
        Assert.NotEmpty(renamed);
        Assert.All(renamed, a => Assert.Equal("rename reason", a.Reason));

        before = await db.AuditLogs.AsNoTracking().Select(a => a.Id).ToListAsync();
        await roles.UpdatePermissionsAsync(created.Id, new List<string> { PermissionConstants.AuditView }, 1, "permission reason", AdminPassword);
        var permissionRows = await db.AuditLogs.AsNoTracking().Where(a => !before.Contains(a.Id)).ToListAsync();
        Assert.Contains(permissionRows, a => a.Action == "ROLE_PERMISSIONS_UPDATED" && a.Reason == "permission reason");
        Assert.All(permissionRows, a => Assert.Equal("permission reason", a.Reason));

        before = await db.AuditLogs.AsNoTracking().Select(a => a.Id).ToListAsync();
        await roles.DeleteAsync(created.Id, "delete reason", 1, AdminPassword);
        var deleted = await db.AuditLogs.AsNoTracking().Where(a => !before.Contains(a.Id)).ToListAsync();
        Assert.Contains(deleted, a => a.EntityName == nameof(Role) && a.Action == "Delete");
        Assert.All(deleted, a => Assert.Equal("delete reason", a.Reason));
    }

    // ---- D7: electronic signature --------------------------------------------

    private static async Task AssertRefusedAndRecorded(MicroLimsDbContext db, Func<Task> action, string entityType, int entityId)
    {
        await Assert.ThrowsAsync<SignatureVerificationException>(action);
        Assert.Empty(db.ElectronicSignatures);
        var refused = Assert.Single(db.AuditLogs.Where(a => a.EntityName == "ElectronicSignature" && a.Action == "SignatureFailed"));
        Assert.Equal($"{entityType}:{entityId}", refused.EntityId);
        Assert.Equal(1, refused.UserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong password")]
    public async Task ChangeRole_WithoutTheRightPassword_IsRefusedAndNothingChanges(string? password)
    {
        var db = CreateDb();
        var users = CreateUsers(db);

        await AssertRefusedAndRecorded(db, () => users.ChangeRoleAsync(10, 3, Reason, 1, password), nameof(User), 10);

        Assert.Equal(4, (await db.Users.AsNoTracking().FirstAsync(u => u.Id == 10)).RoleId);
        Assert.DoesNotContain(db.AuditLogs, a => a.Action == "USER_ROLE_CHANGED");
    }

    [Fact]
    public async Task ChangeRole_WithTheRightPassword_SignsAgainstTheUser()
    {
        var db = CreateDb();
        var users = CreateUsers(db);

        await users.ChangeRoleAsync(10, 3, Reason, 1, AdminPassword, "10.0.0.5");

        var signature = Assert.Single(db.ElectronicSignatures);
        Assert.Equal(SignatureMeaning.UserRoleChanged, signature.MeaningOfSignature);
        Assert.Equal(nameof(User), signature.EntityType);
        Assert.Equal(10, signature.EntityId);
        Assert.Equal(1, signature.UserId);
        Assert.Equal("10.0.0.5", signature.IpAddress);
        Assert.Contains(Reason, signature.Comment);
        Assert.Equal(3, (await db.Users.AsNoTracking().FirstAsync(u => u.Id == 10)).RoleId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong password")]
    public async Task UpdatePermissions_WithoutTheRightPassword_IsRefusedAndNothingChanges(string? password)
    {
        var db = CreateDb();
        var roles = CreateRoles(db);
        var created = await roles.CreateAsync("QC Trainee", null, RoleType.Analyst, Reason);
        var before = (await roles.GetByIdAsync(created.Id))!.PermissionCodes.OrderBy(c => c).ToList();

        await AssertRefusedAndRecorded(db,
            () => roles.UpdatePermissionsAsync(created.Id, new List<string> { PermissionConstants.AuditView }, 1, Reason, password),
            nameof(Role), created.Id);

        Assert.Equal(before, (await roles.GetByIdAsync(created.Id))!.PermissionCodes.OrderBy(c => c).ToList());
    }

    [Fact]
    public async Task UpdatePermissions_WithTheRightPassword_SignsAgainstTheRole()
    {
        var db = CreateDb();
        var roles = CreateRoles(db);
        var created = await roles.CreateAsync("QC Trainee", null, RoleType.Analyst, Reason);

        await roles.UpdatePermissionsAsync(created.Id, new List<string> { PermissionConstants.AuditView }, 1, Reason, AdminPassword);

        var signature = Assert.Single(db.ElectronicSignatures);
        Assert.Equal(SignatureMeaning.RolePermissionsChanged, signature.MeaningOfSignature);
        Assert.Equal(nameof(Role), signature.EntityType);
        Assert.Equal(created.Id, signature.EntityId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong password")]
    public async Task DeleteRole_WithoutTheRightPassword_IsRefusedAndTheRoleStays(string? password)
    {
        var db = CreateDb();
        var roles = CreateRoles(db);
        var created = await roles.CreateAsync("Disposable", null, RoleType.Analyst, Reason);

        await AssertRefusedAndRecorded(db, () => roles.DeleteAsync(created.Id, Reason, 1, password), nameof(Role), created.Id);

        Assert.True(await db.Roles.AnyAsync(r => r.Id == created.Id));
    }

    [Fact]
    public async Task DeleteRole_WithTheRightPassword_SignsAgainstTheDeletedRole()
    {
        var db = CreateDb();
        var roles = CreateRoles(db);
        var created = await roles.CreateAsync("Disposable", null, RoleType.Analyst, Reason);

        await roles.DeleteAsync(created.Id, Reason, 1, AdminPassword);

        Assert.False(await db.Roles.AnyAsync(r => r.Id == created.Id));
        var signature = Assert.Single(db.ElectronicSignatures);
        Assert.Equal(SignatureMeaning.RoleDeleted, signature.MeaningOfSignature);
        Assert.Equal(nameof(Role), signature.EntityType);
        Assert.Equal(created.Id, signature.EntityId);
    }

    [Fact]
    public async Task NonSignedActions_NeverCreateASignature()
    {
        var db = CreateDb();
        var users = CreateUsers(db);
        var roles = CreateRoles(db);

        await users.UnlockUserAsync(10, Reason, 1);
        await users.SetStatusAsync(10, false, Reason, 1);
        var created = await roles.CreateAsync("Plain", null, RoleType.Analyst, Reason);
        await roles.UpdateAsync(created.Id, "Plain 2", null, Reason);

        Assert.Empty(db.ElectronicSignatures);
    }
}
