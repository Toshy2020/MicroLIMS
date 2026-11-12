using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class QaRoleIndependencePostgresTests
{
    private const string AdminPassword = "QaIndepAdminP@ss1!";
    private readonly PostgresTestFixture _fixture;

    public QaRoleIndependencePostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static List<string> VisibleQaGrants() =>
        PermissionConstants.QaGrants
            .Where(c => !PermissionConstants.Hidden.Contains(c))
            .ToList();

    private static async Task<int> CreateActingAdminAsync(MicroLimsDbContext db)
    {
        var adminRole = await db.Roles
            .AsNoTracking()
            .FirstAsync(r => r.Type == RoleType.SystemAdministrator && r.IsSystemRole);

        var actor = new User
        {
            FullName = "QA Independence Acting Admin",
            Username = $"qa_indep_admin_{Guid.NewGuid():N}",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword, 4),
            RoleId = adminRole.Id,
            IsActive = true
        };
        db.Users.Add(actor);
        await db.SaveChangesAsync();
        return actor.Id;
    }

    private static async Task<int> CreateRoleWithGrantsAsync(
        MicroLimsDbContext db,
        RoleType roleType,
        bool isSystemRole,
        string namePrefix,
        IEnumerable<string> permissionCodes)
    {
        var role = new Role
        {
            Name = $"{namePrefix}_{Guid.NewGuid():N}",
            Type = roleType,
            IsSystemRole = isSystemRole,
            IsActive = true
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var codeList = permissionCodes.Distinct(StringComparer.Ordinal).ToList();
        var permissionIds = await db.Permissions
            .AsNoTracking()
            .Where(p => codeList.Contains(p.Code))
            .Select(p => p.Id)
            .ToListAsync();

        foreach (var permissionId in permissionIds)
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });

        if (permissionIds.Count > 0)
            await db.SaveChangesAsync();

        return role.Id;
    }

    private static async Task<List<string>> GetRoleGrantsSortedAsync(MicroLimsDbContext db, int roleId)
    {
        var codes = await db.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission!.Code)
            .ToListAsync();

        return codes.OrderBy(c => c, StringComparer.Ordinal).ToList();
    }

    private static async Task<int> GetPermissionVersionAsync(MicroLimsDbContext db, int roleId) =>
        (await db.Roles.AsNoTracking().SingleAsync(r => r.Id == roleId)).PermissionVersion;

    private static Task<int> CountRoleAuditLogsAsync(MicroLimsDbContext db, int roleId) =>
        db.AuditLogs.AsNoTracking()
            .CountAsync(a => a.EntityName == nameof(Role) && a.EntityId == roleId.ToString());

    private static Task<int> CountRoleSignaturesAsync(MicroLimsDbContext db, int roleId) =>
        db.ElectronicSignatures.AsNoTracking()
            .CountAsync(s => s.EntityType == nameof(Role) && s.EntityId == roleId);

    [PostgresFact]
    public async Task Update_permissions_of_qa_role_with_forbidden_code_refused_and_nothing_persisted()
    {
        int adminId;
        int qaRoleId;
        int auditCountBefore;
        int sigCountBefore;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            adminId = await CreateActingAdminAsync(arrangeDb);
            qaRoleId = await CreateRoleWithGrantsAsync(
                arrangeDb,
                RoleType.Qa,
                isSystemRole: true,
                "QA_Sys",
                PermissionConstants.QaGrants);
            auditCountBefore = await CountRoleAuditLogsAsync(arrangeDb, qaRoleId);
            sigCountBefore = await CountRoleSignaturesAsync(arrangeDb, qaRoleId);
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = new RoleService(actDb, new PermissionService(actDb));
            var requested = VisibleQaGrants().Append(PermissionConstants.SamplesReview).ToList();

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.UpdatePermissionsAsync(
                    qaRoleId,
                    requested,
                    adminId,
                    "Attempt to grant Samples.Review to QA",
                    AdminPassword));

            Assert.Equal("A Quality Assurance role cannot hold: Samples.Review.", ex.Message);
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Equal(
                PermissionConstants.QaGrants.OrderBy(c => c, StringComparer.Ordinal),
                await GetRoleGrantsSortedAsync(assertDb, qaRoleId));
            Assert.Equal(1, await GetPermissionVersionAsync(assertDb, qaRoleId));
            Assert.Equal(auditCountBefore, await CountRoleAuditLogsAsync(assertDb, qaRoleId));
            Assert.Equal(sigCountBefore, await CountRoleSignaturesAsync(assertDb, qaRoleId));
        }
    }

    [PostgresFact]
    public async Task Custom_role_based_on_qa_refuses_forbidden_code()
    {
        int adminId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            adminId = await CreateActingAdminAsync(arrangeDb);
            await CreateRoleWithGrantsAsync(
                arrangeDb,
                RoleType.Qa,
                isSystemRole: true,
                "QA_Base",
                PermissionConstants.QaGrants);
        }

        int customRoleId;
        await using (var createDb = _fixture.CreateDbContext())
        {
            var service = new RoleService(createDb, new PermissionService(createDb));
            var created = await service.CreateAsync(
                $"QA_Custom_{Guid.NewGuid():N}",
                null,
                RoleType.Qa,
                "Create custom QA-based role");
            customRoleId = created.Id;
        }

        int auditCountBefore;
        int sigCountBefore;
        await using (var verifyCreatedDb = _fixture.CreateDbContext())
        {
            Assert.Equal(
                PermissionConstants.QaGrants.OrderBy(c => c, StringComparer.Ordinal),
                await GetRoleGrantsSortedAsync(verifyCreatedDb, customRoleId));
            auditCountBefore = await CountRoleAuditLogsAsync(verifyCreatedDb, customRoleId);
            sigCountBefore = await CountRoleSignaturesAsync(verifyCreatedDb, customRoleId);
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = new RoleService(actDb, new PermissionService(actDb));
            var requested = VisibleQaGrants().Append(PermissionConstants.TestWorkflowExecute).ToList();

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.UpdatePermissionsAsync(
                    customRoleId,
                    requested,
                    adminId,
                    "Attempt to grant TestWorkflow.Execute to custom QA role",
                    AdminPassword));

            Assert.Equal(
                $"A Quality Assurance role cannot hold: {PermissionConstants.TestWorkflowExecute}.",
                ex.Message);
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Equal(
                PermissionConstants.QaGrants.OrderBy(c => c, StringComparer.Ordinal),
                await GetRoleGrantsSortedAsync(assertDb, customRoleId));
            Assert.Equal(1, await GetPermissionVersionAsync(assertDb, customRoleId));
            Assert.Equal(auditCountBefore, await CountRoleAuditLogsAsync(assertDb, customRoleId));
            Assert.Equal(sigCountBefore, await CountRoleSignaturesAsync(assertDb, customRoleId));
        }
    }

    [PostgresFact]
    public async Task Non_qa_role_cannot_gain_qa_only_code_while_holding_forbidden_code()
    {
        int adminId;
        int reviewerRoleId;
        int auditCountBefore;
        int sigCountBefore;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            adminId = await CreateActingAdminAsync(arrangeDb);
            reviewerRoleId = await CreateRoleWithGrantsAsync(
                arrangeDb,
                RoleType.Reviewer,
                isSystemRole: false,
                "Custom_Reviewer",
                new[] { PermissionConstants.SamplesReview });
            auditCountBefore = await CountRoleAuditLogsAsync(arrangeDb, reviewerRoleId);
            sigCountBefore = await CountRoleSignaturesAsync(arrangeDb, reviewerRoleId);
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = new RoleService(actDb, new PermissionService(actDb));

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.UpdatePermissionsAsync(
                    reviewerRoleId,
                    new List<string> { PermissionConstants.SamplesReview, PermissionConstants.AuditReview },
                    adminId,
                    "Attempt to add Audit.Review to Reviewer",
                    AdminPassword));

            Assert.Equal("A Quality Assurance role cannot hold: Samples.Review.", ex.Message);
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Equal(
                new[] { PermissionConstants.SamplesReview },
                await GetRoleGrantsSortedAsync(assertDb, reviewerRoleId));
            Assert.Equal(1, await GetPermissionVersionAsync(assertDb, reviewerRoleId));
            Assert.Equal(auditCountBefore, await CountRoleAuditLogsAsync(assertDb, reviewerRoleId));
            Assert.Equal(sigCountBefore, await CountRoleSignaturesAsync(assertDb, reviewerRoleId));
        }
    }

    [PostgresFact]
    public async Task Allowed_change_to_qa_role_persists_with_audit()
    {
        int adminId;
        int qaRoleId;
        int auditCountBefore;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            adminId = await CreateActingAdminAsync(arrangeDb);
            qaRoleId = await CreateRoleWithGrantsAsync(
                arrangeDb,
                RoleType.Qa,
                isSystemRole: true,
                "QA_AllowedEdit",
                PermissionConstants.QaGrants);
            auditCountBefore = await CountRoleAuditLogsAsync(arrangeDb, qaRoleId);
        }

        const string reason = "Remove Kpi.ViewTeam from QA role";
        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = new RoleService(actDb, new PermissionService(actDb));
            var desired = VisibleQaGrants()
                .Where(c => c != PermissionConstants.KpiViewTeam)
                .ToList();

            await service.UpdatePermissionsAsync(
                qaRoleId,
                desired,
                adminId,
                reason,
                AdminPassword);
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var expectedGrants = PermissionConstants.QaGrants
                .Where(c => c != PermissionConstants.KpiViewTeam)
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(expectedGrants, await GetRoleGrantsSortedAsync(assertDb, qaRoleId));
            Assert.Equal(2, await GetPermissionVersionAsync(assertDb, qaRoleId));

            var auditLogs = await assertDb.AuditLogs
                .AsNoTracking()
                .Where(a => a.EntityName == nameof(Role) && a.EntityId == qaRoleId.ToString())
                .ToListAsync();
            Assert.True(auditLogs.Count > auditCountBefore);

            var permissionAudit = Assert.Single(auditLogs, a => a.Action == "ROLE_PERMISSIONS_UPDATED");
            Assert.Equal(adminId, permissionAudit.UserId);
            Assert.Equal(reason, permissionAudit.Reason);

            using var prevDoc = JsonDocument.Parse(permissionAudit.PreviousValue!);
            var prevCodes = prevDoc.RootElement.GetProperty("PermissionCodes").EnumerateArray()
                .Select(e => e.GetString()!)
                .ToList();
            Assert.Contains(PermissionConstants.KpiViewTeam, prevCodes);

            using var newDoc = JsonDocument.Parse(permissionAudit.NewValue!);
            var newCodes = newDoc.RootElement.GetProperty("PermissionCodes").EnumerateArray()
                .Select(e => e.GetString()!)
                .ToList();
            var revoked = newDoc.RootElement.GetProperty("Revoked").EnumerateArray()
                .Select(e => e.GetString()!)
                .ToList();
            var granted = newDoc.RootElement.GetProperty("Granted").EnumerateArray()
                .Select(e => e.GetString()!)
                .ToList();
            Assert.DoesNotContain(PermissionConstants.KpiViewTeam, newCodes);
            Assert.Equal(new[] { PermissionConstants.KpiViewTeam }, revoked);
            Assert.Empty(granted);

            var signature = await assertDb.ElectronicSignatures
                .AsNoTracking()
                .SingleAsync(s => s.EntityType == nameof(Role) && s.EntityId == qaRoleId);
            Assert.Equal(SignatureMeaning.RolePermissionsChanged, signature.MeaningOfSignature);
            Assert.Equal(adminId, signature.UserId);
            Assert.Equal(nameof(Role), signature.EntityType);
            Assert.Equal(qaRoleId, signature.EntityId);
        }
    }

    [PostgresFact]
    public async Task Wrong_password_on_allowed_change_is_refused_and_nothing_persisted()
    {
        int adminId;
        int qaRoleId;
        int auditCountBefore;
        int sigCountBefore;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            adminId = await CreateActingAdminAsync(arrangeDb);
            qaRoleId = await CreateRoleWithGrantsAsync(
                arrangeDb,
                RoleType.Qa,
                isSystemRole: true,
                "QA_WrongPw",
                PermissionConstants.QaGrants);
            auditCountBefore = await CountRoleAuditLogsAsync(arrangeDb, qaRoleId);
            sigCountBefore = await CountRoleSignaturesAsync(arrangeDb, qaRoleId);
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = new RoleService(actDb, new PermissionService(actDb));
            var desired = VisibleQaGrants()
                .Where(c => c != PermissionConstants.KpiViewTeam)
                .ToList();

            await Assert.ThrowsAsync<SignatureVerificationException>(() =>
                service.UpdatePermissionsAsync(
                    qaRoleId,
                    desired,
                    adminId,
                    "Attempt allowed change with wrong password",
                    "WrongPassword!"));
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Equal(
                PermissionConstants.QaGrants.OrderBy(c => c, StringComparer.Ordinal),
                await GetRoleGrantsSortedAsync(assertDb, qaRoleId));
            Assert.Equal(1, await GetPermissionVersionAsync(assertDb, qaRoleId));
            Assert.Equal(auditCountBefore, await CountRoleAuditLogsAsync(assertDb, qaRoleId));
            Assert.Equal(sigCountBefore, await CountRoleSignaturesAsync(assertDb, qaRoleId));
        }
    }

    // The base system role itself breaks the rule (a grant made before the rule existed, or
    // directly in the database): a role copied from it is refused and never created.
    [PostgresFact]
    public async Task Create_based_on_qa_is_refused_when_the_base_role_holds_a_forbidden_code()
    {
        int brokenBaseId;
        int rolesBefore;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            brokenBaseId = await CreateRoleWithGrantsAsync(
                arrangeDb, RoleType.Qa, isSystemRole: true, "QA_BrokenBase",
                PermissionConstants.QaGrants.Append(PermissionConstants.SamplesApprove));
            rolesBefore = await arrangeDb.Roles.AsNoTracking().CountAsync();
        }

        var name = $"QA_FromBroken_{Guid.NewGuid():N}";
        try
        {
            await using (var actDb = _fixture.CreateDbContext())
            {
                var service = new RoleService(actDb, new PermissionService(actDb));
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    service.CreateAsync(name, null, RoleType.Qa, "Create custom QA-based role"));
                Assert.Equal("A Quality Assurance role cannot hold: Samples.Approve.", ex.Message);
            }

            await using (var assertDb = _fixture.CreateDbContext())
            {
                Assert.False(await assertDb.Roles.AsNoTracking().AnyAsync(r => r.Name == name));
                Assert.Equal(rolesBefore, await assertDb.Roles.AsNoTracking().CountAsync());
            }
        }
        finally
        {
            // The fixture database is shared: a broken QA base role would poison every later
            // test that creates a role based on QA.
            await using var cleanupDb = _fixture.CreateDbContext();
            await cleanupDb.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"RolePermissions\" WHERE \"RoleId\" = {brokenBaseId}; DELETE FROM \"Roles\" WHERE \"Id\" = {brokenBaseId};");
        }
    }
}
