using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class TestingWorkspaceSectionScopeTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static CauseOfTesting SeedCause(MicroLimsDbContext db, int id = 1, string name = "Routine Release")
    {
        var existing = db.CausesOfTesting.Find(id);
        if (existing != null) return existing;
        var cause = new CauseOfTesting { Id = id, Name = name };
        db.CausesOfTesting.Add(cause);
        db.SaveChanges();
        return cause;
    }

    private static (DocumentSection secA, DocumentSection secB) SeedSections(MicroLimsDbContext db)
    {
        var dept = new DocumentDepartment { Name = "Quality Control", Code = "QC", IsActive = true };
        db.DocumentDepartments.Add(dept);
        db.SaveChanges();

        var secA = new DocumentSection { Name = "Micro Section A", Code = "SEC_A", DepartmentId = dept.Id, IsActive = true };
        var secB = new DocumentSection { Name = "Micro Section B", Code = "SEC_B", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.AddRange(secA, secB);
        db.SaveChanges();

        return (secA, secB);
    }

    private static (User userA, User userB, User admin) SeedUsers(MicroLimsDbContext db, DocumentSection secA, DocumentSection secB)
    {
        var analystRole = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        var adminRole = new Role { Name = "System Administrator", Type = RoleType.SystemAdministrator, IsActive = true };
        db.Roles.AddRange(analystRole, adminRole);
        db.SaveChanges();

        var userA = new User { Username = "analystA", FullName = "Analyst Section A", RoleId = analystRole.Id, Role = analystRole, IsActive = true };
        var userB = new User { Username = "analystB", FullName = "Analyst Section B", RoleId = analystRole.Id, Role = analystRole, IsActive = true };
        var admin = new User { Username = "admin", FullName = "System Administrator", RoleId = adminRole.Id, Role = adminRole, IsActive = true };
        db.Users.AddRange(userA, userB, admin);
        db.SaveChanges();

        db.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = userA.Id, DepartmentId = secA.DepartmentId, SectionId = secA.Id },
            new UserOrgMembership { UserId = userB.Id, DepartmentId = secB.DepartmentId, SectionId = secB.Id }
        );
        db.SaveChanges();

        return (userA, userB, admin);
    }

    [Fact]
    public async Task User_In_SectionA_SeesSampleWithOnlyATests_NotSampleWithOnlyBTests()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);
        var (secA, secB) = SeedSections(db);
        var (userA, userB, admin) = SeedUsers(db, secA, secB);

        var sampleA = new Sample
        {
            ReferenceNumber = "SMP-A-001",
            ControlNumber = "CTRL-A",
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.InTesting,
            CauseOfTestingId = cause.Id,
            CauseOfTesting = cause,
            ReceivedAt = DateTime.UtcNow
        };
        sampleA.TestOrders.Add(new TestOrder
        {
            TestCode = "TAMC",
            SectionId = secA.Id,
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        });

        var sampleB = new Sample
        {
            ReferenceNumber = "SMP-B-001",
            ControlNumber = "CTRL-B",
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.InTesting,
            CauseOfTestingId = cause.Id,
            CauseOfTesting = cause,
            ReceivedAt = DateTime.UtcNow
        };
        sampleB.TestOrders.Add(new TestOrder
        {
            TestCode = "TYMC",
            SectionId = secB.Id,
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        });

        db.Samples.AddRange(sampleA, sampleB);
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db, new UserSectionScopeService(db));

        // User A should only see sampleA
        var pagedA = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto(), userA.Id);
        Assert.Single(pagedA.Items);
        Assert.Equal(sampleA.Id, pagedA.Items[0].SampleId);

        // User B should only see sampleB
        var pagedB = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto(), userB.Id);
        Assert.Single(pagedB.Items);
        Assert.Equal(sampleB.Id, pagedB.Items[0].SampleId);
    }

    [Fact]
    public async Task Sample_With_Both_A_And_B_Tests_ShowsOnlyATestOrder_InDto_ForUserA()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);
        var (secA, secB) = SeedSections(db);
        var (userA, userB, admin) = SeedUsers(db, secA, secB);

        var sampleAB = new Sample
        {
            ReferenceNumber = "SMP-AB-001",
            ControlNumber = "CTRL-AB",
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.InTesting,
            CauseOfTestingId = cause.Id,
            CauseOfTesting = cause,
            ReceivedAt = DateTime.UtcNow
        };
        var orderA = new TestOrder
        {
            TestCode = "TAMC",
            SectionId = secA.Id,
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        };
        var orderB = new TestOrder
        {
            TestCode = "TYMC",
            SectionId = secB.Id,
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        };
        sampleAB.TestOrders.Add(orderA);
        sampleAB.TestOrders.Add(orderB);

        db.Samples.Add(sampleAB);
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db, new UserSectionScopeService(db));

        // When retrieved via GetActiveSamplesAsync for User A:
        var paged = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto(), userA.Id);
        Assert.Single(paged.Items);
        var sampleDto = paged.Items[0];
        Assert.Single(sampleDto.AssignedTests);
        Assert.Equal(orderA.Id, sampleDto.AssignedTests[0].TestOrderId);
        Assert.Equal("TAMC", sampleDto.AssignedTests[0].TestCode);

        // When retrieved via GetSampleAsync for User A:
        var singleDto = await service.GetSampleAsync(sampleAB.Id, userA.Id);
        Assert.NotNull(singleDto);
        Assert.Single(singleDto.AssignedTests);
        Assert.Equal(orderA.Id, singleDto.AssignedTests[0].TestOrderId);
    }

    [Fact]
    public async Task Admin_SeesEverything_Unfiltered()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);
        var (secA, secB) = SeedSections(db);
        var (userA, userB, admin) = SeedUsers(db, secA, secB);

        var sampleA = new Sample
        {
            ReferenceNumber = "SMP-A-001",
            ControlNumber = "CTRL-A",
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.InTesting,
            CauseOfTestingId = cause.Id,
            CauseOfTesting = cause,
            ReceivedAt = DateTime.UtcNow
        };
        sampleA.TestOrders.Add(new TestOrder
        {
            TestCode = "TAMC",
            SectionId = secA.Id,
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        });

        var sampleB = new Sample
        {
            ReferenceNumber = "SMP-B-001",
            ControlNumber = "CTRL-B",
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.InTesting,
            CauseOfTestingId = cause.Id,
            CauseOfTesting = cause,
            ReceivedAt = DateTime.UtcNow
        };
        sampleB.TestOrders.Add(new TestOrder
        {
            TestCode = "TYMC",
            SectionId = secB.Id,
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        });

        var sampleAB = new Sample
        {
            ReferenceNumber = "SMP-AB-001",
            ControlNumber = "CTRL-AB",
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.InTesting,
            CauseOfTestingId = cause.Id,
            CauseOfTesting = cause,
            ReceivedAt = DateTime.UtcNow
        };
        sampleAB.TestOrders.Add(new TestOrder
        {
            TestCode = "EC",
            SectionId = secA.Id,
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        });
        sampleAB.TestOrders.Add(new TestOrder
        {
            TestCode = "SALM",
            SectionId = secB.Id,
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        });

        db.Samples.AddRange(sampleA, sampleB, sampleAB);
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db, new UserSectionScopeService(db));

        // Admin sees all 3 samples
        var paged = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto(), admin.Id);
        Assert.Equal(3, paged.TotalCount);
        Assert.Equal(3, paged.Items.Count);

        // For sampleAB, Admin sees both test orders
        var dtoAB = await service.GetSampleAsync(sampleAB.Id, admin.Id);
        Assert.NotNull(dtoAB);
        Assert.Equal(2, dtoAB.AssignedTests.Count);
    }

    [Fact]
    public async Task ReadyToRead_And_Mine_Filters_Ignore_BSection_Orders_For_UserA()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);
        var (secA, secB) = SeedSections(db);
        var (userA, userB, admin) = SeedUsers(db, secA, secB);

        var now = DateTime.UtcNow;

        // Sample 1: Ready to read in Section B, assigned to User A in Section B
        var sampleB = new Sample
        {
            ReferenceNumber = "SMP-B-RTR",
            ControlNumber = "CTRL-B-RTR",
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.InTesting,
            CauseOfTestingId = cause.Id,
            CauseOfTesting = cause,
            ReceivedAt = now
        };
        var orderB = new TestOrder
        {
            TestCode = "TYMC",
            SectionId = secB.Id,
            Status = ApprovalStatus.InProgress,
            CurrentStep = WorkflowStep.Running,
            AssignedAnalystId = userA.Id // Assigned to userA, but in Section B!
        };
        orderB.Incubations.Add(new Incubation
        {
            ExpectedReadingAt = now.AddHours(-1),
            CompletedAt = null,
            StepName = "Incubation"
        });
        sampleB.TestOrders.Add(orderB);

        // Sample 2: Ready to read in Section A, assigned to User A in Section A
        var sampleA = new Sample
        {
            ReferenceNumber = "SMP-A-RTR",
            ControlNumber = "CTRL-A-RTR",
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.InTesting,
            CauseOfTestingId = cause.Id,
            CauseOfTesting = cause,
            ReceivedAt = now
        };
        var orderA = new TestOrder
        {
            TestCode = "TAMC",
            SectionId = secA.Id,
            Status = ApprovalStatus.InProgress,
            CurrentStep = WorkflowStep.Running,
            AssignedAnalystId = userA.Id
        };
        orderA.Incubations.Add(new Incubation
        {
            ExpectedReadingAt = now.AddHours(-1),
            CompletedAt = null,
            StepName = "Incubation"
        });
        sampleA.TestOrders.Add(orderA);

        db.Samples.AddRange(sampleB, sampleA);
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db, new UserSectionScopeService(db));

        // Ready to read filter for User A: must only return sampleA
        var rtrPaged = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { WorkloadFilter = "readyToRead" }, userA.Id);
        Assert.Single(rtrPaged.Items);
        Assert.Equal(sampleA.Id, rtrPaged.Items[0].SampleId);

        // Mine filter for User A: must only return sampleA
        var minePaged = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { WorkloadFilter = "mine" }, userA.Id);
        Assert.Single(minePaged.Items);
        Assert.Equal(sampleA.Id, minePaged.Items[0].SampleId);

        // Workload counts for User A: ReadyToRead = 1, Mine = 1
        var countsA = await service.GetWorkloadCountsAsync(userA.Id);
        Assert.Equal(1, countsA.ReadyToRead);
        Assert.Equal(1, countsA.Mine);

        // For Admin: ReadyToRead = 2
        var countsAdmin = await service.GetWorkloadCountsAsync(admin.Id);
        Assert.Equal(2, countsAdmin.ReadyToRead);
    }

    [Fact]
    public async Task GetSampleAsync_ReturnsNull_For_BOnlySample_ForUserA()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);
        var (secA, secB) = SeedSections(db);
        var (userA, userB, admin) = SeedUsers(db, secA, secB);

        var sampleB = new Sample
        {
            ReferenceNumber = "SMP-B-ONLY",
            ControlNumber = "CTRL-B-ONLY",
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.InTesting,
            CauseOfTestingId = cause.Id,
            CauseOfTesting = cause,
            ReceivedAt = DateTime.UtcNow
        };
        sampleB.TestOrders.Add(new TestOrder
        {
            TestCode = "TYMC",
            SectionId = secB.Id,
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        });
        db.Samples.Add(sampleB);
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db, new UserSectionScopeService(db));

        // User A (in Section A) must get null
        var dtoForUserA = await service.GetSampleAsync(sampleB.Id, userA.Id);
        Assert.Null(dtoForUserA);

        // User B (in Section B) sees the sample
        var dtoForUserB = await service.GetSampleAsync(sampleB.Id, userB.Id);
        Assert.NotNull(dtoForUserB);
        Assert.Equal(sampleB.Id, dtoForUserB.SampleId);

        // Admin sees the sample
        var dtoForAdmin = await service.GetSampleAsync(sampleB.Id, admin.Id);
        Assert.NotNull(dtoForAdmin);
        Assert.Equal(sampleB.Id, dtoForAdmin.SampleId);
    }

    [Fact]
    public async Task MyTasks_Excludes_B_Orders_ForUserA()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);
        var (secA, secB) = SeedSections(db);
        var (userA, userB, admin) = SeedUsers(db, secA, secB);

        var now = DateTime.UtcNow;

        var sample = new Sample
        {
            ReferenceNumber = "SMP-TASKS-01",
            ControlNumber = "CTRL-TASKS",
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.InTesting,
            CauseOfTestingId = cause.Id,
            CauseOfTesting = cause,
            ReceivedAt = now
        };
        var orderA = new TestOrder
        {
            TestCode = "TAMC",
            SectionId = secA.Id,
            Status = ApprovalStatus.InProgress,
            CurrentStep = WorkflowStep.Running,
            AssignedAnalystId = userA.Id
        };
        orderA.Incubations.Add(new Incubation
        {
            StepName = "TAMC Incubation",
            ExpectedReadingAt = now.AddHours(2),
            CompletedAt = null
        });

        var orderB = new TestOrder
        {
            TestCode = "TYMC",
            SectionId = secB.Id,
            Status = ApprovalStatus.InProgress,
            CurrentStep = WorkflowStep.Running,
            AssignedAnalystId = userA.Id // User A is assigned, but Section B is out of scope!
        };
        orderB.Incubations.Add(new Incubation
        {
            StepName = "TYMC Incubation",
            ExpectedReadingAt = now.AddHours(2),
            CompletedAt = null
        });

        sample.TestOrders.Add(orderA);
        sample.TestOrders.Add(orderB);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var myTasksService = new MyTasksService(db, new UserSectionScopeService(db));

        var tasks = await myTasksService.GetMyTasksAsync(userA.Id);

        // Only the Section A test order task should be returned
        var task = Assert.Single(tasks);
        Assert.Equal(orderA.Id, task.TestOrderId);
        Assert.Contains("TAMC", task.Title);
    }

    [Fact]
    public async Task SampleWorkflowQueues_HasTestInSections_MatchesOnlyNonSupersededInScope()
    {
        await using var db = NewDb();
        var (secA, secB) = SeedSections(db);

        var sampleWithActiveA = new Sample { ReferenceNumber = "S1", ControlNumber = "C1", Status = SampleStatus.InTesting };
        sampleWithActiveA.TestOrders.Add(new TestOrder { TestCode = "T1", SectionId = secA.Id, IsSuperseded = false });

        var sampleWithSupersededA = new Sample { ReferenceNumber = "S2", ControlNumber = "C2", Status = SampleStatus.InTesting };
        sampleWithSupersededA.TestOrders.Add(new TestOrder { TestCode = "T2", SectionId = secA.Id, IsSuperseded = true });

        var sampleWithActiveB = new Sample { ReferenceNumber = "S3", ControlNumber = "C3", Status = SampleStatus.InTesting };
        sampleWithActiveB.TestOrders.Add(new TestOrder { TestCode = "T3", SectionId = secB.Id, IsSuperseded = false });

        db.Samples.AddRange(sampleWithActiveA, sampleWithSupersededA, sampleWithActiveB);
        await db.SaveChangesAsync();

        var inSectionA = await db.Samples
            .Where(SampleWorkflowQueues.HasTestInSections(new[] { secA.Id }))
            .Select(s => s.ReferenceNumber)
            .ToListAsync();

        Assert.Single(inSectionA);
        Assert.Equal("S1", inSectionA[0]);
    }
}
