using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Physicochemical areas (FP vs RM & PM) on a real database: workspace filtering by sample
// category, per-user area access (403), and the membership area round trip. Every row is
// owned by the test (unique user + control-number prefix) because the database is shared.
[Collection("PostgresDatabaseCollection")]
public class PhyschemAreaWorkspacePostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public PhyschemAreaWorkspacePostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private record Setup(DocumentSection Fp, DocumentSection Micro, int BothUserId, int FpOnlyUserId, int FpSampleId, int RmSampleId, int PkgSampleId, string Prefix);

    private async Task<Setup> ArrangeAsync(MicroLimsDbContext db)
    {
        var fp = await db.DocumentSections.FirstAsync(s => s.Code == "FP");
        var micro = await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");
        var prefix = "PHA-" + Guid.NewGuid().ToString("N")[..8];
        var role = await db.Roles.FirstAsync(r => r.Type == RoleType.Analyst);
        var cause = await db.CausesOfTesting.FirstOrDefaultAsync();
        if (cause is null) { cause = new CauseOfTesting { Name = "Routine Release" }; db.CausesOfTesting.Add(cause); await db.SaveChangesAsync(); }

        async Task<int> NewUser(string tag, PhyschemArea? area)
        {
            var u = new User { FullName = "Area " + tag, Username = $"{prefix}-{tag}", PasswordHash = "hashed_dummy", RoleId = role.Id, IsActive = true };
            db.Users.Add(u);
            await db.SaveChangesAsync();
            db.UserOrgMemberships.Add(new UserOrgMembership { UserId = u.Id, DepartmentId = fp.DepartmentId, SectionId = fp.Id, PhyschemArea = area });
            await db.SaveChangesAsync();
            return u.Id;
        }
        var both = await NewUser("both", null);
        var fpOnly = await NewUser("fponly", PhyschemArea.FinishedProduct);

        async Task<int> NewSample(string tag, SampleCategory cat)
        {
            var s = new Sample
            {
                ReferenceNumber = $"{prefix}-{tag}", ControlNumber = $"{prefix}-{tag}", SampledBy = "Postgres Integration", Category = cat,
                Status = SampleStatus.InTesting, PreparationStatus = SamplePreparationStatus.Ready,
                ReceivedByUserId = _fixture.SeededUserId, CauseOfTestingId = cause.Id, ReceivedAt = DateTime.UtcNow
            };
            s.TestOrders.Add(new TestOrder { Sample = s, TestCode = "ASSAY", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running, SectionId = fp.Id, AssignedAnalystId = both });
            db.Samples.Add(s);
            await db.SaveChangesAsync();
            return s.Id;
        }
        return new Setup(fp, micro, both, fpOnly, await NewSample("fp", SampleCategory.FinishedProduct), await NewSample("rm", SampleCategory.RawMaterial),
            await NewSample("pkg", SampleCategory.PackagingMaterial), prefix);
    }

    [PostgresFact]
    public async Task FpOnlyUser_IsRefusedTheRmPmArea_OnSampleListAndCounts()
    {
        await using var db = _fixture.CreateDbContext();
        var s = await ArrangeAsync(db);
        var service = TestServiceFactory.TestingWorkspace(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetSampleAsync(s.RmSampleId, s.FpOnlyUserId, s.Fp.Id, "rmpm"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { LabSectionId = s.Fp.Id, Area = "rmpm" }, s.FpOnlyUserId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetWorkloadCountsAsync(s.FpOnlyUserId, s.Fp.Id, "rmpm"));

        // Own area still works, and the RM sample is not found through it.
        Assert.NotNull(await service.GetSampleAsync(s.FpSampleId, s.FpOnlyUserId, s.Fp.Id, "fp"));
        Assert.Null(await service.GetSampleAsync(s.RmSampleId, s.FpOnlyUserId, s.Fp.Id, "fp"));
    }

    [PostgresFact]
    public async Task BothUser_AreaFiltersSamplesByCategory_AndCountsAddUp()
    {
        await using var db = _fixture.CreateDbContext();
        var s = await ArrangeAsync(db);
        var service = TestServiceFactory.TestingWorkspace(db);

        async Task<List<int>> Ids(string area) =>
            (await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { LabSectionId = s.Fp.Id, Area = area, Search = s.Prefix }, s.BothUserId))
            .Items.Select(i => i.SampleId).ToList();

        Assert.Equal(new[] { s.FpSampleId }, await Ids("fp"));
        Assert.Equal(new[] { s.RmSampleId, s.PkgSampleId }.OrderBy(x => x), (await Ids("rmpm")).OrderBy(x => x));

        // Mine counts the tests assigned to this user: 1 in FP, 2 in RM & PM, 3 for the whole lab.
        var fpCounts = await service.GetWorkloadCountsAsync(s.BothUserId, s.Fp.Id, "fp");
        var rmCounts = await service.GetWorkloadCountsAsync(s.BothUserId, s.Fp.Id, "rmpm");
        Assert.Equal(1, fpCounts.Mine);
        Assert.Equal(2, rmCounts.Mine);
        Assert.Equal(3, await db.TestOrders.CountAsync(t => t.AssignedAnalystId == s.BothUserId && t.SectionId == s.Fp.Id));
    }

    [PostgresFact]
    public async Task AreaIsRequiredForFp_ButNotForOtherLabs()
    {
        await using var db = _fixture.CreateDbContext();
        var s = await ArrangeAsync(db);
        var service = TestServiceFactory.TestingWorkspace(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { LabSectionId = s.Fp.Id }, s.BothUserId));
        Assert.Equal("area is required for the Physicochemical Laboratory workspace.", ex.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetWorkloadCountsAsync(s.BothUserId, s.Fp.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { LabSectionId = s.Fp.Id, Area = "x" }, s.BothUserId));

        // Microbiology: no area needed, behaviour unchanged (admin user passes the scope check).
        var admin = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { LabSectionId = s.Micro.Id, Search = s.Prefix });
        Assert.Empty(admin.Items);
    }

    [PostgresFact]
    public async Task Membership_AreaRoundTrips_AndIsRefusedOutsideFp()
    {
        await using var db = _fixture.CreateDbContext();
        var s = await ArrangeAsync(db);
        var org = new LaboratoryOrganizationService(db, new UserSectionScopeService(db));

        var saved = await org.ReplaceMembershipsAsync(s.BothUserId,
            new[] { new UserOrgMembershipDto(s.Fp.DepartmentId, s.Fp.Id, PhyschemArea.RawPackaging) }, _fixture.SeededUserId);
        Assert.Equal(PhyschemArea.RawPackaging, Assert.Single(saved).PhyschemArea);
        Assert.Equal(PhyschemArea.RawPackaging, (await org.GetMembershipsAsync(s.BothUserId)).Single().PhyschemArea);

        // Changing the area on the existing row updates it (no new row).
        saved = await org.ReplaceMembershipsAsync(s.BothUserId,
            new[] { new UserOrgMembershipDto(s.Fp.DepartmentId, s.Fp.Id, PhyschemArea.Both) }, _fixture.SeededUserId);
        Assert.Equal(PhyschemArea.Both, Assert.Single(saved).PhyschemArea);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => org.ReplaceMembershipsAsync(s.BothUserId,
            new[] { new UserOrgMembershipDto(s.Micro.DepartmentId, s.Micro.Id, PhyschemArea.RawPackaging) }, _fixture.SeededUserId));
        Assert.Equal("An area can only be set on a Physicochemical Laboratory membership.", ex.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => org.ReplaceMembershipsAsync(s.BothUserId,
            new[] { new UserOrgMembershipDto(s.Fp.DepartmentId, null, PhyschemArea.RawPackaging) }, _fixture.SeededUserId));
    }

    [PostgresFact]
    public async Task MySections_ListsAreasPerUser_AndEmptyForOtherSections()
    {
        await using var db = _fixture.CreateDbContext();
        var s = await ArrangeAsync(db);
        var org = new LaboratoryOrganizationService(db, new UserSectionScopeService(db));

        var both = (await org.GetMySectionsAsync(s.BothUserId)).Single(x => x.SectionCode == "FP");
        Assert.Equal(new[] { "fp", "rmpm" }, both.PhyschemAreas);

        await org.ReplaceMembershipsAsync(s.FpOnlyUserId, new[] { new UserOrgMembershipDto(s.Fp.DepartmentId, s.Fp.Id, PhyschemArea.RawPackaging) }, _fixture.SeededUserId);
        var rm = (await org.GetMySectionsAsync(s.FpOnlyUserId)).Single(x => x.SectionCode == "FP");
        Assert.Equal(new[] { "rmpm" }, rm.PhyschemAreas);

        // A department-wide membership covers both areas, and other sections carry no areas.
        await org.ReplaceMembershipsAsync(s.FpOnlyUserId, new[] { new UserOrgMembershipDto(s.Fp.DepartmentId, null) }, _fixture.SeededUserId);
        var mine = await org.GetMySectionsAsync(s.FpOnlyUserId);
        Assert.Equal(new[] { "fp", "rmpm" }, mine.Single(x => x.SectionCode == "FP").PhyschemAreas);
        Assert.All(mine.Where(x => x.SectionCode != "FP"), x => Assert.Empty(x.PhyschemAreas));
    }
}
