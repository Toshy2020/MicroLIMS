using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class UserSectionScopeServiceTests
{
    private static MicroLimsDbContext NewDb()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CurrentUserId = 1;
        return db;
    }

    // Built-in roles with the seeded permission grants, so the administrator holds exactly what ships.
    private static (User Admin, User Analyst) SeedBuiltInRoles(MicroLimsDbContext db)
    {
        foreach (var type in Enum.GetValues<RoleType>())
            db.Roles.Add(new Role { Name = type.ToString(), Type = type, IsSystemRole = true, IsActive = true });
        db.SaveChanges();
        DbSeeder.SeedPermissionsAndGrants(db);

        var admin = new User { Username = "admin", FullName = "Admin User", RoleId = db.Roles.First(r => r.Type == RoleType.SystemAdministrator).Id };
        var analyst = new User { Username = "analyst", FullName = "Analyst User", RoleId = db.Roles.First(r => r.Type == RoleType.Analyst).Id };
        db.Users.AddRange(admin, analyst);
        db.SaveChanges();
        return (admin, analyst);
    }

    [Fact]
    public async Task Readable_SystemAdministratorHoldingViewAll_ReturnsNull_ButMembersIsEmpty()
    {
        await using var db = NewDb();
        var (admin, analyst) = SeedBuiltInRoles(db);

        var service = new UserSectionScopeService(db);

        Assert.Null(await service.GetReadableSectionIdsAsync(admin.Id));
        // Writes never span every section: the administrator belongs to none.
        Assert.Empty(await service.GetMemberSectionIdsAsync(admin.Id));
        // Everyone else's read scope is their membership.
        var analystScope = await service.GetReadableSectionIdsAsync(analyst.Id);
        Assert.NotNull(analystScope);
        Assert.Empty(analystScope);
    }

    [Fact]
    public async Task Readable_SystemAdministratorWithoutViewAll_IsMembershipOnly()
    {
        await using var db = NewDb();
        var (admin, _) = SeedBuiltInRoles(db);
        // A lab withdraws the permission on the Roles screen.
        var grant = db.RolePermissions.Single(rp => rp.RoleId == admin.RoleId && rp.Permission!.Code == PermissionConstants.SectionsViewAll);
        db.RolePermissions.Remove(grant);
        await db.SaveChangesAsync();

        var scope = await new UserSectionScopeService(db).GetReadableSectionIdsAsync(admin.Id);
        Assert.NotNull(scope);
        Assert.Empty(scope);
    }

    [Fact]
    public async Task Readable_InactiveUserHoldingViewAll_IsNotUnrestricted()
    {
        await using var db = NewDb();
        var (admin, _) = SeedBuiltInRoles(db);
        admin.IsActive = false;
        await db.SaveChangesAsync();

        Assert.NotNull(await new UserSectionScopeService(db).GetReadableSectionIdsAsync(admin.Id));
    }

    [Fact]
    public async Task AdminMemberOfOneSection_WritesOnlyThere_ReadsEverywhere()
    {
        await using var db = NewDb();
        var (admin, _) = SeedBuiltInRoles(db);
        var dept = new DocumentDepartment { Name = "QC", Code = "QC", IsActive = true };
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();
        var own = new DocumentSection { Name = "Own", Code = "OWN", DepartmentId = dept.Id, IsActive = true };
        var other = new DocumentSection { Name = "Other", Code = "OTH", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.AddRange(own, other);
        await db.SaveChangesAsync();
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = admin.Id, DepartmentId = dept.Id, SectionId = own.Id });
        var material = new Material { MaterialName = "M", SectionId = other.Id };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var service = new UserSectionScopeService(db);

        Assert.Equal(new[] { own.Id }, await service.GetMemberSectionIdsAsync(admin.Id));
        // Read passes for another section's record; the default (write) is refused.
        await service.EnsureMaterialAccessAsync(admin.Id, material.Id, access: SectionAccess.Read);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EnsureMaterialAccessAsync(admin.Id, material.Id));
    }

    [Fact]
    public async Task GetMemberSectionIdsAsync_DepartmentMembership_ReturnsAllActiveSections_ExcludingInactive_AndExcludedIfDeptInactive()
    {
        await using var db = NewDb();
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst };
        db.Roles.Add(role);

        var activeDept = new DocumentDepartment { Name = "Active Dept", Code = "ACT", IsActive = true };
        var inactiveDept = new DocumentDepartment { Name = "Inactive Dept", Code = "INACT", IsActive = false };
        db.DocumentDepartments.AddRange(activeDept, inactiveDept);
        await db.SaveChangesAsync();

        var activeSec1 = new DocumentSection { Name = "Sec 1", Code = "S1", DepartmentId = activeDept.Id, IsActive = true };
        var activeSec2 = new DocumentSection { Name = "Sec 2", Code = "S2", DepartmentId = activeDept.Id, IsActive = true };
        var inactiveSec = new DocumentSection { Name = "Sec 3", Code = "S3", DepartmentId = activeDept.Id, IsActive = false };
        var activeSecInInactiveDept = new DocumentSection { Name = "Sec 4", Code = "S4", DepartmentId = inactiveDept.Id, IsActive = true };
        db.DocumentSections.AddRange(activeSec1, activeSec2, inactiveSec, activeSecInInactiveDept);
        await db.SaveChangesAsync();

        var user1 = new User { Username = "user1", FullName = "User One", Role = role };
        var user2 = new User { Username = "user2", FullName = "User Two", Role = role };
        db.Users.AddRange(user1, user2);
        await db.SaveChangesAsync();

        // user1 has department membership for activeDept
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user1.Id, DepartmentId = activeDept.Id, SectionId = null });
        // user2 has department membership for inactiveDept
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user2.Id, DepartmentId = inactiveDept.Id, SectionId = null });
        await db.SaveChangesAsync();

        var service = new UserSectionScopeService(db);

        var scope1 = await service.GetMemberSectionIdsAsync(user1.Id);
        Assert.NotNull(scope1);
        Assert.Equal(2, scope1.Count);
        Assert.Contains(activeSec1.Id, scope1);
        Assert.Contains(activeSec2.Id, scope1);
        Assert.DoesNotContain(inactiveSec.Id, scope1);

        var scope2 = await service.GetMemberSectionIdsAsync(user2.Id);
        Assert.NotNull(scope2);
        Assert.Empty(scope2);
    }

    [Fact]
    public async Task GetMemberSectionIdsAsync_SectionMembership_ReturnsOnlyThatSection()
    {
        await using var db = NewDb();
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst };
        db.Roles.Add(role);

        var dept = new DocumentDepartment { Name = "QC Dept", Code = "QC", IsActive = true };
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var sec1 = new DocumentSection { Name = "Sec 1", Code = "S1", DepartmentId = dept.Id, IsActive = true };
        var sec2 = new DocumentSection { Name = "Sec 2", Code = "S2", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.AddRange(sec1, sec2);
        await db.SaveChangesAsync();

        var user = new User { Username = "user_sec", FullName = "User Sec", Role = role };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = dept.Id, SectionId = sec1.Id });
        await db.SaveChangesAsync();

        var service = new UserSectionScopeService(db);
        var scope = await service.GetMemberSectionIdsAsync(user.Id);

        Assert.NotNull(scope);
        Assert.Single(scope);
        Assert.Contains(sec1.Id, scope);
        Assert.DoesNotContain(sec2.Id, scope);
    }

    [Fact]
    public async Task GetMemberSectionIdsAsync_NoMemberships_ReturnsEmptyList()
    {
        await using var db = NewDb();
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst };
        db.Roles.Add(role);
        var user = new User { Username = "no_member", FullName = "No Member", Role = role };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new UserSectionScopeService(db);
        var scope = await service.GetMemberSectionIdsAsync(user.Id);

        Assert.NotNull(scope);
        Assert.Empty(scope);
    }

    [Fact]
    public async Task ResolveSectionForCreateAsync_SingleSectionUser_AutoResolves()
    {
        await using var db = NewDb();
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst };
        var dept = new DocumentDepartment { Name = "QC", Code = "QC", IsActive = true };
        db.Roles.Add(role);
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var sec = new DocumentSection { Name = "Micro", Code = "MICRO", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.Add(sec);
        await db.SaveChangesAsync();

        var user = new User { Username = "analyst1", FullName = "Analyst One", Role = role };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = dept.Id, SectionId = sec.Id });
        await db.SaveChangesAsync();

        var service = new UserSectionScopeService(db);
        var resolved = await service.ResolveSectionForCreateAsync(user.Id, requestedSectionId: null);

        Assert.Equal(sec.Id, resolved);
    }

    [Fact]
    public async Task ResolveSectionForCreateAsync_MultiSectionUser_WithoutSectionId_ThrowsChooseSection()
    {
        await using var db = NewDb();
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst };
        var dept = new DocumentDepartment { Name = "QC", Code = "QC", IsActive = true };
        db.Roles.Add(role);
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var sec1 = new DocumentSection { Name = "Micro 1", Code = "M1", DepartmentId = dept.Id, IsActive = true };
        var sec2 = new DocumentSection { Name = "Micro 2", Code = "M2", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.AddRange(sec1, sec2);
        await db.SaveChangesAsync();

        var user = new User { Username = "multi_sec", FullName = "Multi Sec", Role = role };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = dept.Id, SectionId = sec1.Id });
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = dept.Id, SectionId = sec2.Id });
        await db.SaveChangesAsync();

        var service = new UserSectionScopeService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResolveSectionForCreateAsync(user.Id, requestedSectionId: null));

        Assert.Equal("Choose a laboratory section - you belong to more than one.", ex.Message);
    }

    [Fact]
    public async Task ResolveSectionForCreateAsync_RequestedSectionOutsideScope_ThrowsNotAMember()
    {
        await using var db = NewDb();
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst };
        var dept = new DocumentDepartment { Name = "QC", Code = "QC", IsActive = true };
        db.Roles.Add(role);
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var sec1 = new DocumentSection { Name = "Micro 1", Code = "M1", DepartmentId = dept.Id, IsActive = true };
        var sec2 = new DocumentSection { Name = "Micro 2", Code = "M2", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.AddRange(sec1, sec2);
        await db.SaveChangesAsync();

        var user = new User { Username = "user_sec1", FullName = "User Sec1", Role = role };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = dept.Id, SectionId = sec1.Id });
        await db.SaveChangesAsync();

        var service = new UserSectionScopeService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResolveSectionForCreateAsync(user.Id, requestedSectionId: sec2.Id));

        Assert.Equal("You are not a member of the selected laboratory section.", ex.Message);
    }

    [Fact]
    public async Task ResolveSectionForCreateAsync_InactiveRequestedSection_ThrowsInactiveOrNotFound()
    {
        await using var db = NewDb();
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst };
        var dept = new DocumentDepartment { Name = "QC", Code = "QC", IsActive = true };
        db.Roles.Add(role);
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var inactiveSec = new DocumentSection { Name = "Inactive Sec", Code = "INACT", DepartmentId = dept.Id, IsActive = false };
        db.DocumentSections.Add(inactiveSec);
        await db.SaveChangesAsync();

        var user = new User { Username = "analyst", FullName = "Analyst", Role = role };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = dept.Id, SectionId = inactiveSec.Id });
        await db.SaveChangesAsync();

        var service = new UserSectionScopeService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResolveSectionForCreateAsync(user.Id, requestedSectionId: inactiveSec.Id));

        Assert.Equal("Selected laboratory section was not found or is inactive.", ex.Message);
    }

    [Fact]
    public async Task ResolveSectionForCreateAsync_AdminIsNotAMemberOfEverySection()
    {
        await using var db = NewDb();
        var (admin, _) = SeedBuiltInRoles(db);
        var dept = new DocumentDepartment { Name = "QC", Code = "QC", IsActive = true };
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();
        var sec = new DocumentSection { Name = "Micro", Code = "MICRO", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.Add(sec);
        await db.SaveChangesAsync();

        var service = new UserSectionScopeService(db);

        // Holding Sections.ViewAll does not let the administrator create records in a section.
        var notMember = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResolveSectionForCreateAsync(admin.Id, requestedSectionId: sec.Id));
        Assert.Equal("You are not a member of the selected laboratory section.", notMember.Message);

        var notAssigned = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResolveSectionForCreateAsync(admin.Id, requestedSectionId: null));
        Assert.Equal("Your account is not assigned to any laboratory section. Ask an administrator to assign one.", notAssigned.Message);
    }

    [Fact]
    public async Task ResolveSectionForCreateAsync_UserWithNoMembership_ThrowsNotAssigned()
    {
        await using var db = NewDb();
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst };
        db.Roles.Add(role);
        var user = new User { Username = "unassigned", FullName = "Unassigned User", Role = role };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new UserSectionScopeService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResolveSectionForCreateAsync(user.Id, requestedSectionId: null));

        Assert.Equal("Your account is not assigned to any laboratory section. Ask an administrator to assign one.", ex.Message);
    }

    [Fact]
    public async Task TestSectionLookup_KnownCodes_MapsToSectionId()
    {
        await using var db = NewDb();
        var dept = new DocumentDepartment { Name = "QC", Code = "QC", IsActive = true };
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var sec1 = new DocumentSection { Name = "Sec 1", Code = "S1", DepartmentId = dept.Id, IsActive = true };
        var sec2 = new DocumentSection { Name = "Sec 2", Code = "S2", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.AddRange(sec1, sec2);
        await db.SaveChangesAsync();

        db.TestDefinitions.AddRange(
            new TestDefinition { Code = "TAMC", DisplayName = "TAMC", SectionId = sec1.Id },
            new TestDefinition { Code = "TYMC", DisplayName = "TYMC", SectionId = sec2.Id }
        );
        await db.SaveChangesAsync();

        var map = await TestSectionLookup.ResolveAsync(db, new[] { "TAMC", "TYMC" });

        Assert.Equal(2, map.Count);
        Assert.Equal(sec1.Id, map["TAMC"]);
        Assert.Equal(sec2.Id, map["TYMC"]);
    }

    [Fact]
    public async Task TestSectionLookup_UnknownCode_ThrowsAndNamesCode()
    {
        await using var db = NewDb();
        var dept = new DocumentDepartment { Name = "QC", Code = "QC", IsActive = true };
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var sec = new DocumentSection { Name = "Sec", Code = "S", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.Add(sec);
        await db.SaveChangesAsync();

        db.TestDefinitions.Add(new TestDefinition { Code = "TAMC", DisplayName = "TAMC", SectionId = sec.Id });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestSectionLookup.ResolveAsync(db, new[] { "TAMC", "UNKNOWN_CODE" }));

        Assert.Contains("'UNKNOWN_CODE'", ex.Message);
    }
}
