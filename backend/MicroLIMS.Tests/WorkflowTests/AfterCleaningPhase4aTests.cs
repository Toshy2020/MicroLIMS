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

public class AfterCleaningPhase4aTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed record TestSetup(
        DocumentSection MicroSection,
        DocumentSection FpSection,
        User MicroUser,
        User FpUser,
        Machine Machine,
        CauseOfTesting Cause,
        Item FinishedProductItem,
        Item RawMaterialItem,
        Item InactiveItem);

    private static TestSetup Seed(MicroLimsDbContext db)
    {
        var dept = new DocumentDepartment { Name = "QC", Code = "QC", IsActive = true };
        db.DocumentDepartments.Add(dept);
        db.SaveChanges();

        var micro = new DocumentSection { Name = "Microbiology Laboratory", Code = LaboratoryCodes.Microbiology, DepartmentId = dept.Id, IsActive = true };
        var fp = new DocumentSection { Name = "Physicochemical Laboratory", Code = PhyschemAreas.SectionCode, DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.AddRange(micro, fp);

        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        db.SaveChanges();

        var microUser = new User { Username = "micro_analyst", FullName = "Micro Analyst", RoleId = role.Id, Role = role, IsActive = true };
        var fpUser = new User { Username = "fp_analyst", FullName = "FP Analyst", RoleId = role.Id, Role = role, IsActive = true };
        db.Users.AddRange(microUser, fpUser);
        db.SaveChanges();

        db.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = microUser.Id, DepartmentId = dept.Id, SectionId = micro.Id },
            new UserOrgMembership { UserId = fpUser.Id, DepartmentId = dept.Id, SectionId = fp.Id, PhyschemArea = PhyschemArea.FinishedProduct });

        var machine = new Machine { Name = "Fluid Bed Dryer 01", IsActive = true };
        db.Machines.Add(machine);

        var cause = new CauseOfTesting { Name = "Routine" };
        db.CausesOfTesting.Add(cause);

        var fpItem = new Item { Name = "Paracetamol 500mg Tablets", Code = "FP-001", Category = SampleCategory.FinishedProduct, IsActive = true };
        var rmItem = new Item { Name = "Starch BP", Code = "RM-001", Category = SampleCategory.RawMaterial, IsActive = true };
        var inactiveItem = new Item { Name = "Old Syrup", Code = "FP-OLD", Category = SampleCategory.FinishedProduct, IsActive = false };
        db.Items.AddRange(fpItem, rmItem, inactiveItem);

        db.SaveChanges();

        return new TestSetup(micro, fp, microUser, fpUser, machine, cause, fpItem, rmItem, inactiveItem);
    }

    [Fact]
    public void CleaningTechniques_Of_MapsWorkflowTypesCorrectly()
    {
        Assert.Equal(CleaningTechniques.Hplc, CleaningTechniques.Of(WorkflowType.HplcMethodAssay));
        Assert.Equal(CleaningTechniques.Icp, CleaningTechniques.Of(WorkflowType.IcpMethodAssay));
        Assert.Equal(CleaningTechniques.Uv, CleaningTechniques.Of(WorkflowType.Measurement));
        Assert.Equal(CleaningTechniques.Uv, CleaningTechniques.Of(WorkflowType.Qualitative));
        Assert.Equal(CleaningTechniques.Uv, CleaningTechniques.Of(WorkflowType.UvSpectrophotometry));
    }

    [Fact]
    public void CleaningTechniques_Normalize_NormalizesAndValidates()
    {
        var norm = CleaningTechniques.Normalize(new[] { "ICP", "uv", "HPLC", "uv" });
        Assert.Equal("UV,HPLC,ICP", norm);

        var norm2 = CleaningTechniques.Normalize(new[] { "HPLC", "UV" });
        Assert.Equal("UV,HPLC", norm2);

        var ex = Assert.Throws<InvalidOperationException>(() => CleaningTechniques.Normalize(new[] { "GC" }));
        Assert.Contains("Invalid cleaning technique 'GC'", ex.Message);

        Assert.Throws<InvalidOperationException>(() => CleaningTechniques.Normalize(Array.Empty<string>()));
        Assert.Throws<InvalidOperationException>(() => CleaningTechniques.Normalize((string[]?)null));
    }

    [Fact]
    public async Task PhyschemReceive_RequiresPreviousProductItemId()
    {
        await using var db = NewDb();
        var s = Seed(db);
        var scope = new UserSectionScopeService(db);
        var refGen = new ReferenceNumberGenerator(db);
        var engine = new AfterCleaningWorkflowEngine(db, refGen, scope);

        var req = new AfterCleaningReceiveRequest(
            MachineId: s.Machine.Id,
            CauseOfTestingId: s.Cause.Id,
            SampledBy: "Tester",
            ControlNumber: "AC-001",
            ReceivedByUserId: s.FpUser.Id,
            PreviousProductName: "Manual Product Name",
            PreviousProductBatchNumber: "BATCH-01",
            LabSectionId: s.FpSection.Id,
            PreviousProductItemId: null,
            Techniques: new() { "UV" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ReceiveAsync(req));
        Assert.Contains("Previous product item is required", ex.Message);
    }

    [Fact]
    public async Task PhyschemReceive_RequiresTechniques()
    {
        await using var db = NewDb();
        var s = Seed(db);
        var scope = new UserSectionScopeService(db);
        var refGen = new ReferenceNumberGenerator(db);
        var engine = new AfterCleaningWorkflowEngine(db, refGen, scope);

        var req = new AfterCleaningReceiveRequest(
            MachineId: s.Machine.Id,
            CauseOfTestingId: s.Cause.Id,
            SampledBy: "Tester",
            ControlNumber: "AC-002",
            ReceivedByUserId: s.FpUser.Id,
            PreviousProductBatchNumber: "BATCH-01",
            LabSectionId: s.FpSection.Id,
            PreviousProductItemId: s.FinishedProductItem.Id,
            Techniques: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ReceiveAsync(req));
        Assert.Contains("At least one cleaning technique is required", ex.Message);

        var reqEmpty = req with { Techniques = new List<string>() };
        var exEmpty = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ReceiveAsync(reqEmpty));
        Assert.Contains("At least one cleaning technique is required", exEmpty.Message);
    }

    [Fact]
    public async Task PhyschemReceive_RejectsBadTechnique()
    {
        await using var db = NewDb();
        var s = Seed(db);
        var scope = new UserSectionScopeService(db);
        var refGen = new ReferenceNumberGenerator(db);
        var engine = new AfterCleaningWorkflowEngine(db, refGen, scope);

        var req = new AfterCleaningReceiveRequest(
            MachineId: s.Machine.Id,
            CauseOfTestingId: s.Cause.Id,
            SampledBy: "Tester",
            ControlNumber: "AC-003",
            ReceivedByUserId: s.FpUser.Id,
            PreviousProductBatchNumber: "BATCH-01",
            LabSectionId: s.FpSection.Id,
            PreviousProductItemId: s.FinishedProductItem.Id,
            Techniques: new() { "UV", "INVALID_TECH" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ReceiveAsync(req));
        Assert.Contains("Invalid cleaning technique 'INVALID_TECH'", ex.Message);
    }

    [Fact]
    public async Task PhyschemReceive_RejectsNonFinishedProductItem()
    {
        await using var db = NewDb();
        var s = Seed(db);
        var scope = new UserSectionScopeService(db);
        var refGen = new ReferenceNumberGenerator(db);
        var engine = new AfterCleaningWorkflowEngine(db, refGen, scope);

        var req = new AfterCleaningReceiveRequest(
            MachineId: s.Machine.Id,
            CauseOfTestingId: s.Cause.Id,
            SampledBy: "Tester",
            ControlNumber: "AC-004",
            ReceivedByUserId: s.FpUser.Id,
            PreviousProductBatchNumber: "BATCH-01",
            LabSectionId: s.FpSection.Id,
            PreviousProductItemId: s.RawMaterialItem.Id,
            Techniques: new() { "UV" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ReceiveAsync(req));
        Assert.Contains("is not a Finished Product", ex.Message);
    }

    [Fact]
    public async Task PhyschemReceive_RejectsInactiveItem()
    {
        await using var db = NewDb();
        var s = Seed(db);
        var scope = new UserSectionScopeService(db);
        var refGen = new ReferenceNumberGenerator(db);
        var engine = new AfterCleaningWorkflowEngine(db, refGen, scope);

        var req = new AfterCleaningReceiveRequest(
            MachineId: s.Machine.Id,
            CauseOfTestingId: s.Cause.Id,
            SampledBy: "Tester",
            ControlNumber: "AC-005",
            ReceivedByUserId: s.FpUser.Id,
            PreviousProductBatchNumber: "BATCH-01",
            LabSectionId: s.FpSection.Id,
            PreviousProductItemId: s.InactiveItem.Id,
            Techniques: new() { "UV" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ReceiveAsync(req));
        Assert.Contains("is inactive and cannot be selected", ex.Message);
    }

    [Fact]
    public async Task PhyschemReceive_CopiesItemName_NormalizesTechniques_MapsToDto()
    {
        await using var db = NewDb();
        var s = Seed(db);
        var scope = new UserSectionScopeService(db);
        var refGen = new ReferenceNumberGenerator(db);
        var engine = new AfterCleaningWorkflowEngine(db, refGen, scope);

        var req = new AfterCleaningReceiveRequest(
            MachineId: s.Machine.Id,
            CauseOfTestingId: s.Cause.Id,
            SampledBy: "Tester",
            ControlNumber: "AC-006",
            ReceivedByUserId: s.FpUser.Id,
            PreviousProductName: "Different Name That Should Be Overwritten",
            PreviousProductBatchNumber: "BATCH-01",
            LabSectionId: s.FpSection.Id,
            PreviousProductItemId: s.FinishedProductItem.Id,
            Techniques: new() { "ICP", "UV" });

        var sample = await engine.ReceiveAsync(req);

        Assert.Equal(s.FinishedProductItem.Id, sample.PreviousProductItemId);
        Assert.Equal(s.FinishedProductItem.Name, sample.PreviousProductName);
        Assert.Equal("UV,ICP", sample.CleaningTechniques);

        var dto = TestingWorkspaceService.ToDto(sample);
        Assert.Equal(s.FinishedProductItem.Id, dto.PreviousProductItemId);
        Assert.Equal(s.FinishedProductItem.Name, dto.PreviousProductName);
        Assert.Equal(new[] { "UV", "ICP" }, dto.CleaningTechniques);
    }

    [Fact]
    public async Task MicroReceive_LeavesMicroUnchanged_TechniquesIgnored_FreeTextAllowed()
    {
        await using var db = NewDb();
        var s = Seed(db);
        var scope = new UserSectionScopeService(db);
        var refGen = new ReferenceNumberGenerator(db);
        var engine = new AfterCleaningWorkflowEngine(db, refGen, scope);

        // Micro with free-text (no item)
        var req1 = new AfterCleaningReceiveRequest(
            MachineId: s.Machine.Id,
            CauseOfTestingId: s.Cause.Id,
            SampledBy: "Tester",
            ControlNumber: "AC-MICRO-01",
            ReceivedByUserId: s.MicroUser.Id,
            PreviousProductName: "Arbitrary Custom Product",
            PreviousProductBatchNumber: "BATCH-M1",
            LabSectionId: s.MicroSection.Id,
            PreviousProductItemId: null,
            Techniques: new() { "UV", "HPLC" }); // Techniques must be ignored

        var sample1 = await engine.ReceiveAsync(req1);
        Assert.Null(sample1.PreviousProductItemId);
        Assert.Equal("Arbitrary Custom Product", sample1.PreviousProductName);
        Assert.Null(sample1.CleaningTechniques);

        var dto1 = TestingWorkspaceService.ToDto(sample1);
        Assert.Null(dto1.PreviousProductItemId);
        Assert.Equal("Arbitrary Custom Product", dto1.PreviousProductName);
        Assert.Null(dto1.CleaningTechniques);

        // Micro with item provided
        var req2 = new AfterCleaningReceiveRequest(
            MachineId: s.Machine.Id,
            CauseOfTestingId: s.Cause.Id,
            SampledBy: "Tester",
            ControlNumber: "AC-MICRO-02",
            ReceivedByUserId: s.MicroUser.Id,
            PreviousProductName: null,
            PreviousProductBatchNumber: "BATCH-M2",
            LabSectionId: s.MicroSection.Id,
            PreviousProductItemId: s.FinishedProductItem.Id,
            Techniques: new() { "UV" });

        var sample2 = await engine.ReceiveAsync(req2);
        Assert.Equal(s.FinishedProductItem.Id, sample2.PreviousProductItemId);
        Assert.Equal(s.FinishedProductItem.Name, sample2.PreviousProductName);
        Assert.Null(sample2.CleaningTechniques);
    }

    [Fact]
    public async Task PhyschemPrepare_FiltersByTechnique_RefusesUnchosenTechnique()
    {
        await using var db = NewDb();
        var s = Seed(db);

        var part = new MachinePart { Name = "Tablet Chute", MachineId = s.Machine.Id, IsActive = true };
        db.MachineParts.Add(part);
        await db.SaveChangesAsync();

        var uvConfig = new MachinePartConfiguration { MachinePartId = part.Id, TestCode = "UV-CLEAN", TestType = "Swab", IsActive = true };
        var hplcConfig = new MachinePartConfiguration { MachinePartId = part.Id, TestCode = "HPLC-CLEAN", TestType = "Rinse", IsActive = true };
        var icpConfig = new MachinePartConfiguration { MachinePartId = part.Id, TestCode = "ICP-CLEAN", TestType = "Rinse", IsActive = true };
        db.MachinePartConfigurations.AddRange(uvConfig, hplcConfig, icpConfig);

        db.TestDefinitions.AddRange(
            new TestDefinition { Code = "UV-CLEAN", DisplayName = "UV Scan", SectionId = s.FpSection.Id, PhyschemArea = PhyschemArea.FinishedProduct, WorkflowType = WorkflowType.Measurement },
            new TestDefinition { Code = "HPLC-CLEAN", DisplayName = "HPLC Residue", SectionId = s.FpSection.Id, PhyschemArea = PhyschemArea.FinishedProduct, WorkflowType = WorkflowType.HplcMethodAssay },
            new TestDefinition { Code = "ICP-CLEAN", DisplayName = "ICP Residue", SectionId = s.FpSection.Id, PhyschemArea = PhyschemArea.FinishedProduct, WorkflowType = WorkflowType.IcpMethodAssay });
        await db.SaveChangesAsync();

        var scope = new UserSectionScopeService(db);
        var refGen = new ReferenceNumberGenerator(db);
        var engine = new AfterCleaningWorkflowEngine(db, refGen, scope);

        // Receive with UV technique only
        var sample = await engine.ReceiveAsync(new AfterCleaningReceiveRequest(
            MachineId: s.Machine.Id,
            CauseOfTestingId: s.Cause.Id,
            SampledBy: "Tester",
            ControlNumber: "AC-TECH-01",
            ReceivedByUserId: s.FpUser.Id,
            PreviousProductBatchNumber: "BATCH-01",
            LabSectionId: s.FpSection.Id,
            PreviousProductItemId: s.FinishedProductItem.Id,
            Techniques: new() { "UV" }));

        // Attempting to prepare HPLC test should be refused
        var exHplc = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.PrepareAsync(sample.Id, new List<int> { hplcConfig.Id }, s.FpUser.Id));
        Assert.Equal("Part test HPLC-CLEAN is HPLC, which was not chosen at receipt.", exHplc.Message);

        // Attempting to prepare ICP test should be refused
        var exIcp = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.PrepareAsync(sample.Id, new List<int> { icpConfig.Id }, s.FpUser.Id));
        Assert.Equal("Part test ICP-CLEAN is ICP, which was not chosen at receipt.", exIcp.Message);

        // Preparing UV test should succeed
        var prepared = await engine.PrepareAsync(sample.Id, new List<int> { uvConfig.Id }, s.FpUser.Id);
        Assert.Equal(SamplePreparationStatus.Ready, prepared.PreparationStatus);
        Assert.Single(prepared.TestOrders);
        Assert.Equal("UV-CLEAN", prepared.TestOrders[0].TestCode);
    }
}
