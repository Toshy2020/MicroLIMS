using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class PerLabReceivingWaterAndACTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed record TestSeed(
        DocumentSection Micro,
        DocumentSection Fp,
        User MicroUser,
        User FpRmUser,
        User FpFpUser,
        WaterDepartment WaterDept,
        Machine TestMachine,
        CauseOfTesting Cause);

    private static TestSeed SeedEnvironment(MicroLimsDbContext db)
    {
        var dept = new DocumentDepartment { Name = "Quality Control", Code = "QC", IsActive = true };
        db.DocumentDepartments.Add(dept);
        db.SaveChanges();

        var micro = new DocumentSection { Name = "Microbiology Laboratory", Code = LaboratoryCodes.Microbiology, DepartmentId = dept.Id, IsActive = true };
        var fp = new DocumentSection { Name = "Physicochemical Laboratory", Code = PhyschemAreas.SectionCode, DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.AddRange(micro, fp);

        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        db.SaveChanges();

        var microUser = new User { Username = "micro_user", FullName = "Micro User", RoleId = role.Id, Role = role, IsActive = true };
        var fpRmUser = new User { Username = "fp_rm_user", FullName = "FP RM User", RoleId = role.Id, Role = role, IsActive = true };
        var fpFpUser = new User { Username = "fp_fp_user", FullName = "FP FP User", RoleId = role.Id, Role = role, IsActive = true };
        db.Users.AddRange(microUser, fpRmUser, fpFpUser);
        db.SaveChanges();

        db.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = microUser.Id, DepartmentId = dept.Id, SectionId = micro.Id },
            new UserOrgMembership { UserId = fpRmUser.Id, DepartmentId = dept.Id, SectionId = fp.Id, PhyschemArea = PhyschemArea.RawPackaging },
            new UserOrgMembership { UserId = fpFpUser.Id, DepartmentId = dept.Id, SectionId = fp.Id, PhyschemArea = PhyschemArea.FinishedProduct });

        var cause = new CauseOfTesting { Id = 1, Name = "Routine" };
        db.CausesOfTesting.Add(cause);

        var waterDept = new WaterDepartment { Name = "Purified Water System", IsActive = true };
        db.WaterDepartments.Add(waterDept);

        var machine = new Machine { Name = "Tablet Press 01", IsActive = true };
        db.Machines.Add(machine);

        db.SaveChanges();

        return new TestSeed(micro, fp, microUser, fpRmUser, fpFpUser, waterDept, machine, cause);
    }

    [Fact]
    public void A6_Test6_PhyschemAreas_Mapping_WaterIsRmPm_AndAfterCleaningIsFp()
    {
        // Water belongs to the RM & PM area
        Assert.Equal(WorkspaceArea.RmPm, PhyschemAreas.OfCategory(SampleCategory.Water));
        Assert.Contains(SampleCategory.Water, PhyschemAreas.CategoriesOf(WorkspaceArea.RmPm));
        Assert.DoesNotContain(SampleCategory.Water, PhyschemAreas.CategoriesOf(WorkspaceArea.Fp));

        // After cleaning stays in FP
        Assert.Equal(WorkspaceArea.Fp, PhyschemAreas.OfCategory(SampleCategory.AfterCleaning));
        Assert.Contains(SampleCategory.AfterCleaning, PhyschemAreas.CategoriesOf(WorkspaceArea.Fp));
        Assert.DoesNotContain(SampleCategory.AfterCleaning, PhyschemAreas.CategoriesOf(WorkspaceArea.RmPm));
    }

    [Fact]
    public async Task A6_Test1_MicroPrepare_AtPointAssignedTamcAndRmChemistry_CreatesOnlyTamcOrder()
    {
        await using var db = NewDb();
        var seed = SeedEnvironment(db);

        var point = new WaterSamplingPoint
        {
            Code = "WSP-01",
            Location = "Loop A",
            WaterDepartmentId = seed.WaterDept.Id,
            AssignedTestCodes = new() { "TAMC", "COND" },
            IsActive = true
        };
        db.WaterSamplingPoints.Add(point);

        db.TestDefinitions.AddRange(
            new TestDefinition { Code = "TAMC", DisplayName = "TAMC", SectionId = seed.Micro.Id, WorkflowType = WorkflowType.CountTest },
            new TestDefinition { Code = "COND", DisplayName = "Conductivity", SectionId = seed.Fp.Id, PhyschemArea = PhyschemArea.RawPackaging, WorkflowType = WorkflowType.Measurement });
        await db.SaveChangesAsync();

        var scope = new UserSectionScopeService(db);
        var refGen = new ReferenceNumberGenerator(db);
        var engine = new WaterWorkflowEngine(db, refGen, scope);

        // Micro receives water
        var sample = await engine.ReceiveAsync(new WaterReceiveRequest(
            WaterDepartmentId: seed.WaterDept.Id,
            CauseOfTestingId: seed.Cause.Id,
            SampleQuantity: "500ml",
            SampledBy: "Sampler",
            ControlNumber: "CTRL-W-01",
            ReceivedByUserId: seed.MicroUser.Id,
            LabSectionId: seed.Micro.Id));

        Assert.Equal(seed.Micro.Id, sample.LabSectionId);

        // Micro prepares water
        var prepared = await engine.PrepareAsync(sample.Id, new List<int> { point.Id }, seed.MicroUser.Id, "RoomTemperature");

        // Creates only TAMC order, ignores COND
        var order = Assert.Single(prepared.TestOrders);
        Assert.Equal("TAMC", order.TestCode);
        Assert.Equal(seed.Micro.Id, order.SectionId);

        var loc = Assert.Single(prepared.Locations);
        Assert.Equal(point.Id, loc.WaterSamplingPointId);
        Assert.Equal(order.Id, loc.TestOrderId);
        Assert.Equal(SamplePreparationStatus.Ready, prepared.PreparationStatus);
    }

    [Fact]
    public async Task A6_Test2_RmAreaUser_ReceivesWaterIntoFpLab_PrepareCreatesOnlyRmAreaFpCode()
    {
        await using var db = NewDb();
        var seed = SeedEnvironment(db);

        var point = new WaterSamplingPoint
        {
            Code = "WSP-02",
            Location = "Loop B",
            WaterDepartmentId = seed.WaterDept.Id,
            AssignedTestCodes = new() { "TAMC", "COND", "DISS" },
            IsActive = true
        };
        db.WaterSamplingPoints.Add(point);

        db.TestDefinitions.AddRange(
            new TestDefinition { Code = "TAMC", DisplayName = "TAMC", SectionId = seed.Micro.Id, WorkflowType = WorkflowType.CountTest },
            new TestDefinition { Code = "COND", DisplayName = "Conductivity", SectionId = seed.Fp.Id, PhyschemArea = PhyschemArea.RawPackaging, WorkflowType = WorkflowType.Measurement },
            new TestDefinition { Code = "DISS", DisplayName = "Dissolution", SectionId = seed.Fp.Id, PhyschemArea = PhyschemArea.FinishedProduct, WorkflowType = WorkflowType.Measurement });
        await db.SaveChangesAsync();

        var scope = new UserSectionScopeService(db);
        var refGen = new ReferenceNumberGenerator(db);
        var engine = new WaterWorkflowEngine(db, refGen, scope);

        // RM-area FP user receives water into FP lab
        var sample = await engine.ReceiveAsync(new WaterReceiveRequest(
            WaterDepartmentId: seed.WaterDept.Id,
            CauseOfTestingId: seed.Cause.Id,
            SampleQuantity: "500ml",
            SampledBy: "Sampler",
            ControlNumber: "CTRL-W-02",
            ReceivedByUserId: seed.FpRmUser.Id,
            LabSectionId: seed.Fp.Id));

        Assert.Equal(seed.Fp.Id, sample.LabSectionId);

        // RM-area user prepares water
        var prepared = await engine.PrepareAsync(sample.Id, new List<int> { point.Id }, seed.FpRmUser.Id, "RoomTemperature");

        // Creates only the RM-area FP code (COND), ignores TAMC (Micro) and DISS (FP area)
        var order = Assert.Single(prepared.TestOrders);
        Assert.Equal("COND", order.TestCode);
        Assert.Equal(seed.Fp.Id, order.SectionId);

        var loc = Assert.Single(prepared.Locations);
        Assert.Equal(point.Id, loc.WaterSamplingPointId);
        Assert.Equal(order.Id, loc.TestOrderId);
        Assert.Equal(SamplePreparationStatus.Ready, prepared.PreparationStatus);
    }

    [Fact]
    public async Task A6_Test3_FpAreaOnlyUser_ReceivingWater_Refused_And_RmAreaUser_ReceivingAC_Refused()
    {
        await using var db = NewDb();
        var seed = SeedEnvironment(db);

        var scope = new UserSectionScopeService(db);
        var refGen = new ReferenceNumberGenerator(db);
        var waterEngine = new WaterWorkflowEngine(db, refGen, scope);
        var acEngine = new AfterCleaningWorkflowEngine(db, refGen, scope);

        // 1. FP-area-only user receiving water into FP lab -> refused
        var waterReq = new WaterReceiveRequest(
            WaterDepartmentId: seed.WaterDept.Id,
            CauseOfTestingId: seed.Cause.Id,
            SampleQuantity: "500ml",
            SampledBy: "Sampler",
            ControlNumber: "CTRL-W-03",
            ReceivedByUserId: seed.FpFpUser.Id,
            LabSectionId: seed.Fp.Id);

        var waterEx = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => waterEngine.ReceiveAsync(waterReq));
        Assert.Equal("Water samples are received by the RM & PM area.", waterEx.Message);

        // 2. RM-area user receiving AC into FP lab -> refused
        var acReq = new AfterCleaningReceiveRequest(
            MachineId: seed.TestMachine.Id,
            CauseOfTestingId: seed.Cause.Id,
            SampledBy: "Sampler",
            ControlNumber: "CTRL-AC-03",
            ReceivedByUserId: seed.FpRmUser.Id,
            PreviousProductName: "Product X",
            PreviousProductBatchNumber: "BATCH-001",
            LabSectionId: seed.Fp.Id);

        var acEx = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => acEngine.ReceiveAsync(acReq));
        Assert.Equal("After-cleaning samples are received by the FP area.", acEx.Message);
    }

    [Fact]
    public async Task A6_Test4_ACPrepare_WithConfigOfOtherLab_Refused()
    {
        await using var db = NewDb();
        var seed = SeedEnvironment(db);

        var part = new MachinePart { Name = "Hopper", MachineId = seed.TestMachine.Id, IsActive = true };
        db.MachineParts.Add(part);
        await db.SaveChangesAsync();

        var microConfig = new MachinePartConfiguration { MachinePartId = part.Id, TestCode = "TAMC", TestType = "Swab", IsActive = true };
        var fpConfig = new MachinePartConfiguration { MachinePartId = part.Id, TestCode = "TOC", TestType = "Swab", IsActive = true };
        db.MachinePartConfigurations.AddRange(microConfig, fpConfig);

        db.TestDefinitions.AddRange(
            new TestDefinition { Code = "TAMC", DisplayName = "TAMC", SectionId = seed.Micro.Id, WorkflowType = WorkflowType.Observation },
            new TestDefinition { Code = "TOC", DisplayName = "Total Organic Carbon", SectionId = seed.Fp.Id, PhyschemArea = PhyschemArea.FinishedProduct, WorkflowType = WorkflowType.Measurement });
        await db.SaveChangesAsync();

        var scope = new UserSectionScopeService(db);
        var refGen = new ReferenceNumberGenerator(db);
        var acEngine = new AfterCleaningWorkflowEngine(db, refGen, scope);

        // Case A: Sample received into Micro lab, but prepared with FP config
        var microSample = await acEngine.ReceiveAsync(new AfterCleaningReceiveRequest(
            MachineId: seed.TestMachine.Id,
            CauseOfTestingId: seed.Cause.Id,
            SampledBy: "Sampler",
            ControlNumber: "AC-MICRO-01",
            ReceivedByUserId: seed.MicroUser.Id,
            PreviousProductName: "Product A",
            PreviousProductBatchNumber: "BATCH-A",
            LabSectionId: seed.Micro.Id));

        var exA = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            acEngine.PrepareAsync(microSample.Id, new List<int> { fpConfig.Id }, seed.MicroUser.Id));
        Assert.Equal("Part test TOC belongs to another laboratory.", exA.Message);

        // Case B: Sample received into FP lab, but prepared with Micro config
        var fpSample = await acEngine.ReceiveAsync(new AfterCleaningReceiveRequest(
            MachineId: seed.TestMachine.Id,
            CauseOfTestingId: seed.Cause.Id,
            SampledBy: "Sampler",
            ControlNumber: "AC-FP-01",
            ReceivedByUserId: seed.FpFpUser.Id,
            PreviousProductName: "Product B",
            PreviousProductBatchNumber: "BATCH-B",
            LabSectionId: seed.Fp.Id));

        var exB = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            acEngine.PrepareAsync(fpSample.Id, new List<int> { microConfig.Id }, seed.FpFpUser.Id));
        Assert.Equal("Part test TAMC belongs to another laboratory.", exB.Message);
    }

    [Fact]
    public async Task A6_Test5_Queue_FpLabWaterSample_ListedForFpRmArea_NotMicro_LegacySampleListedForMicroOnly()
    {
        await using var db = NewDb();
        var seed = SeedEnvironment(db);

        // 1. Water sample received into FP lab
        var fpWaterSample = new Sample
        {
            ReferenceNumber = "WT-FP-001",
            ControlNumber = "CTRL-WT-FP",
            Category = SampleCategory.Water,
            WaterDepartmentId = seed.WaterDept.Id,
            LabSectionId = seed.Fp.Id,
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.NeedsPreparation,
            CauseOfTestingId = seed.Cause.Id,
            ReceivedAt = DateTime.UtcNow
        };

        // 2. Legacy water sample with LabSectionId = null
        var legacyWaterSample = new Sample
        {
            ReferenceNumber = "WT-LEGACY-001",
            ControlNumber = "CTRL-WT-LEGACY",
            Category = SampleCategory.Water,
            WaterDepartmentId = seed.WaterDept.Id,
            LabSectionId = null,
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.NeedsPreparation,
            CauseOfTestingId = seed.Cause.Id,
            ReceivedAt = DateTime.UtcNow
        };

        db.Samples.AddRange(fpWaterSample, legacyWaterSample);
        await db.SaveChangesAsync();

        var scope = new UserSectionScopeService(db);
        var workspaceService = new TestingWorkspaceService(db, scope);

        // FP section, RM & PM area view: lists fpWaterSample, does not list legacyWaterSample
        var fpRmList = await workspaceService.GetActiveSamplesAsync(
            new TestingWorkspaceFilterDto { LabSectionId = seed.Fp.Id, Area = "rmpm" },
            seed.FpRmUser.Id);
        Assert.Contains(fpRmList.Items, s => s.SampleId == fpWaterSample.Id);
        Assert.DoesNotContain(fpRmList.Items, s => s.SampleId == legacyWaterSample.Id);

        // FP section, FP area view: lists neither (category filter excludes Water)
        var fpFpList = await workspaceService.GetActiveSamplesAsync(
            new TestingWorkspaceFilterDto { LabSectionId = seed.Fp.Id, Area = "fp" },
            seed.FpFpUser.Id);
        Assert.DoesNotContain(fpFpList.Items, s => s.SampleId == fpWaterSample.Id);
        Assert.DoesNotContain(fpFpList.Items, s => s.SampleId == legacyWaterSample.Id);

        // Micro section view: lists legacyWaterSample, does not list fpWaterSample
        var microList = await workspaceService.GetActiveSamplesAsync(
            new TestingWorkspaceFilterDto { LabSectionId = seed.Micro.Id },
            seed.MicroUser.Id);
        Assert.Contains(microList.Items, s => s.SampleId == legacyWaterSample.Id);
        Assert.DoesNotContain(microList.Items, s => s.SampleId == fpWaterSample.Id);

        // Workload counts needsPreparation
        var fpRmCounts = await workspaceService.GetWorkloadCountsAsync(seed.FpRmUser.Id, seed.Fp.Id, "rmpm");
        Assert.Equal(1, fpRmCounts.NeedsPreparation);

        var microCounts = await workspaceService.GetWorkloadCountsAsync(seed.MicroUser.Id, seed.Micro.Id);
        Assert.Equal(1, microCounts.NeedsPreparation);
    }

    [Fact]
    public async Task WaterPrepare_WhenNoPointHasTestsForLab_ThrowsDescriptiveException()
    {
        await using var db = NewDb();
        var seed = SeedEnvironment(db);

        var point = new WaterSamplingPoint
        {
            Code = "WSP-CHEM-ONLY",
            Location = "Loop C",
            WaterDepartmentId = seed.WaterDept.Id,
            AssignedTestCodes = new() { "COND" },
            IsActive = true
        };
        db.WaterSamplingPoints.Add(point);

        db.TestDefinitions.Add(
            new TestDefinition { Code = "COND", DisplayName = "Conductivity", SectionId = seed.Fp.Id, PhyschemArea = PhyschemArea.RawPackaging, WorkflowType = WorkflowType.Measurement });
        await db.SaveChangesAsync();

        var scope = new UserSectionScopeService(db);
        var refGen = new ReferenceNumberGenerator(db);
        var engine = new WaterWorkflowEngine(db, refGen, scope);

        var sample = await engine.ReceiveAsync(new WaterReceiveRequest(
            WaterDepartmentId: seed.WaterDept.Id,
            CauseOfTestingId: seed.Cause.Id,
            SampleQuantity: "500ml",
            SampledBy: "Sampler",
            ControlNumber: "CTRL-W-04",
            ReceivedByUserId: seed.MicroUser.Id,
            LabSectionId: seed.Micro.Id));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.PrepareAsync(sample.Id, new List<int> { point.Id }, seed.MicroUser.Id, "RoomTemperature"));
        Assert.Equal("None of the selected sampling points has Microbiology Laboratory tests assigned.", ex.Message);
    }

    [Fact]
    public async Task EnsureSampleAccessAsync_WaterSampleWithLabSectionId_GrantsAccessToMembersOfThatLabOnly()
    {
        await using var db = NewDb();
        var seed = SeedEnvironment(db);

        var sample = new Sample
        {
            ReferenceNumber = "WT-ACCESS-001",
            ControlNumber = "CTRL-WT-ACCESS",
            Category = SampleCategory.Water,
            WaterDepartmentId = seed.WaterDept.Id,
            LabSectionId = seed.Fp.Id,
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.NeedsPreparation,
            CauseOfTestingId = seed.Cause.Id,
            ReceivedAt = DateTime.UtcNow
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var scope = new UserSectionScopeService(db);

        // FP user has access because sample's LabSectionId is FP
        await scope.EnsureSampleAccessAsync(seed.FpRmUser.Id, sample.Id);

        // Micro user does not have access and throws UnauthorizedAccessException
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            scope.EnsureSampleAccessAsync(seed.MicroUser.Id, sample.Id));
    }
}
