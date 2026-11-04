using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Water, EM and After Cleaning samples are received without test orders; the
// Microbiology lab creates them at preparation. A lab workspace used to list
// only samples with a test in its section, so a sample received from the
// Microbiology workspace vanished from it (only the audit trail showed it) and
// could never be prepared there.
public class WorkspaceUntestedLocationSampleTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed record Seed(DocumentSection Micro, DocumentSection Fp, User MicroAnalyst, User FpAnalyst, CauseOfTesting Cause);

    private static Seed SeedLabs(MicroLimsDbContext db)
    {
        var dept = new DocumentDepartment { Name = "Quality Control", Code = "QC", IsActive = true };
        db.DocumentDepartments.Add(dept);
        db.SaveChanges();

        var micro = new DocumentSection { Name = "Microbiology Laboratory", Code = "MICRO", DepartmentId = dept.Id, IsActive = true };
        var fp = new DocumentSection { Name = "Physicochemical Laboratory", Code = "FP", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.AddRange(micro, fp);

        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        db.SaveChanges();

        var microAnalyst = new User { Username = "micro", FullName = "Micro Analyst", RoleId = role.Id, Role = role, IsActive = true };
        var fpAnalyst = new User { Username = "fp", FullName = "FP Analyst", RoleId = role.Id, Role = role, IsActive = true };
        db.Users.AddRange(microAnalyst, fpAnalyst);
        db.SaveChanges();

        db.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = microAnalyst.Id, DepartmentId = dept.Id, SectionId = micro.Id },
            new UserOrgMembership { UserId = fpAnalyst.Id, DepartmentId = dept.Id, SectionId = fp.Id });

        var cause = new CauseOfTesting { Id = 1, Name = "Routine" };
        db.CausesOfTesting.Add(cause);
        db.SaveChanges();

        return new Seed(micro, fp, microAnalyst, fpAnalyst, cause);
    }

    private static Sample ReceivedWithoutTests(Seed seed, SampleCategory category, string reference) => new()
    {
        ReferenceNumber = reference,
        ControlNumber = reference,
        Category = category,
        Status = SampleStatus.Received,
        PreparationStatus = SamplePreparationStatus.NeedsPreparation,
        CauseOfTestingId = seed.Cause.Id,
        CauseOfTesting = seed.Cause,
        ReceivedAt = DateTime.UtcNow
    };

    [Theory]
    [InlineData(SampleCategory.Water)]
    [InlineData(SampleCategory.EnvironmentalMonitoring)]
    [InlineData(SampleCategory.AfterCleaning)]
    public async Task MicrobiologyWorkspace_ListsReceivedLocationSample_BeforeItHasTests(SampleCategory category)
    {
        await using var db = NewDb();
        var seed = SeedLabs(db);
        var sample = ReceivedWithoutTests(seed, category, "LOC-001");
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db, new UserSectionScopeService(db));

        var page = await service.GetActiveSamplesAsync(
            new TestingWorkspaceFilterDto { LabSectionId = seed.Micro.Id }, seed.MicroAnalyst.Id);
        Assert.Equal(sample.Id, Assert.Single(page.Items).SampleId);
        Assert.Equal(1, page.TotalCount);

        var needsPrep = await service.GetActiveSamplesAsync(
            new TestingWorkspaceFilterDto { LabSectionId = seed.Micro.Id, WorkloadFilter = "needsPreparation" }, seed.MicroAnalyst.Id);
        Assert.Single(needsPrep.Items);

        var counts = await service.GetWorkloadCountsAsync(seed.MicroAnalyst.Id, seed.Micro.Id);
        Assert.Equal(1, counts.NeedsPreparation);

        Assert.NotNull(await service.GetSampleAsync(sample.Id, seed.MicroAnalyst.Id, seed.Micro.Id));
    }

    [Fact]
    public async Task OtherLab_DoesNotListUntestedLocationSample()
    {
        await using var db = NewDb();
        var seed = SeedLabs(db);
        var sample = ReceivedWithoutTests(seed, SampleCategory.Water, "WTR-001");
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db, new UserSectionScopeService(db));

        var page = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto(), seed.FpAnalyst.Id);
        Assert.Empty(page.Items);

        var counts = await service.GetWorkloadCountsAsync(seed.FpAnalyst.Id);
        Assert.Equal(0, counts.NeedsPreparation);

        Assert.Null(await service.GetSampleAsync(sample.Id, seed.FpAnalyst.Id));
    }

    [Fact]
    public async Task PreparedLocationSample_IsListedByItsTests_NotByTheUntestedRule()
    {
        await using var db = NewDb();
        var seed = SeedLabs(db);
        var sample = ReceivedWithoutTests(seed, SampleCategory.Water, "WTR-002");
        sample.PreparationStatus = SamplePreparationStatus.Ready;
        sample.TestOrders.Add(new TestOrder
        {
            TestCode = "TAMC",
            SectionId = seed.Fp.Id,
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db, new UserSectionScopeService(db));

        var micro = await service.GetActiveSamplesAsync(
            new TestingWorkspaceFilterDto { LabSectionId = seed.Micro.Id }, seed.MicroAnalyst.Id);
        Assert.Empty(micro.Items);
    }
}
