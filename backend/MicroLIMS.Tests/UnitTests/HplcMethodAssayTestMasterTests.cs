using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class HplcMethodAssayTestMasterTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        db.CurrentUserId = 1;
        return db;
    }

    private static async Task<(DocumentSection section, int userId)> SeedAsync(MicroLimsDbContext db)
    {
        var section = TestServiceFactory.EnsureMicroSection(db);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { Username = "u_" + Guid.NewGuid().ToString("N")[..6], FullName = "Test User", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        return (section, user.Id);
    }

    private static async Task<DocumentSection> AddOtherSectionAsync(MicroLimsDbContext db, int departmentId)
    {
        var section = new DocumentSection { Name = "Other Lab", Code = "OTH-" + Guid.NewGuid().ToString("N")[..4], DepartmentId = departmentId, IsActive = true };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();
        return section;
    }

    // Section-unrestricted user for seeding solutions/methods in whichever
    // section a test needs, independent of the analyst under test.
    private static async Task<int> EnsureAdminUserAsync(MicroLimsDbContext db)
    {
        var existing = db.Users.FirstOrDefault(u => u.Username == "admin_test_setup");
        if (existing != null)
            return existing.Id;

        var adminRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.SystemAdministrator);
        if (adminRole == null)
        {
            adminRole = new Role { Name = "System Administrator", Type = RoleType.SystemAdministrator, IsActive = true };
            db.Roles.Add(adminRole);
            await db.SaveChangesAsync();
        }

        var admin = new User { Username = "admin_test_setup", FullName = "Admin", RoleId = adminRole.Id, IsActive = true };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        return admin.Id;
    }

    private static async Task<MaterialMasterEntry> AddEntryAsync(
        MicroLimsDbContext db, int sectionId, string code, MaterialMasterCategory category = MaterialMasterCategory.ReferenceStandard)
    {
        var entry = new MaterialMasterEntry
        {
            SectionId = sectionId,
            Code = code,
            Name = code,
            Category = category,
            BaseUnit = MaterialUnit.Gram,
            IsActive = true,
            CreatedByUserId = 1,
            CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = 1,
            LastModifiedAt = DateTime.UtcNow
        };
        db.MaterialMasterEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private static async Task<SolutionMaster> AddSolutionAsync(MicroLimsDbContext db, int sectionId, string name, SolutionType type)
    {
        var adminId = await EnsureAdminUserAsync(db);
        var componentEntry = await AddEntryAsync(db, sectionId, "COMP-" + Guid.NewGuid().ToString("N")[..6], MaterialMasterCategory.Reagent);
        var service = TestServiceFactory.SolutionMaster(db);
        var created = await service.CreateAsync(new SaveSolutionMasterRequest(
            name, type, 7, ShelfLifeUnit.Days, "Room temperature", 1000m, "Mix and filter.",
            new List<SolutionComponentInput> { new(componentEntry.Id, 500m, SolutionComponentUnit.Milliliter) },
            SectionId: sectionId), adminId);
        return await db.SolutionMasters.FirstAsync(s => s.Id == created.Id);
    }

    // The `userId` parameter is unused for creation (an unrestricted admin
    // seeds the method regardless of section) - it exists only so callers
    // read naturally at call sites that also need a user for other calls.
    private static async Task<HplcMethodResponse> AddMethodAsync(MicroLimsDbContext db, int sectionId, int userId, string abbreviation = "AM-01", bool active = true)
    {
        var adminId = await EnsureAdminUserAsync(db);
        var diluent = await AddSolutionAsync(db, sectionId, "Diluent " + abbreviation, SolutionType.Diluent);
        var mp = await AddSolutionAsync(db, sectionId, "Mobile Phase " + abbreviation, SolutionType.MobilePhase);
        var standard = await AddEntryAsync(db, sectionId, "STD-" + abbreviation);
        var service = TestServiceFactory.HplcMethod(db);

        var req = new SaveHplcMethodRequest(
            "Method " + abbreviation, abbreviation, DateTime.UtcNow,
            "L1", 150m, 4.6m, 5m, 30m, ElutionMode.Isocratic, 1m,
            HplcDetectorType.UV, 20m, 15m, diluent.Id,
            new List<HplcMobilePhaseInput> { new("A", mp.Id, null) },
            new List<HplcGradientStepInput>(),
            new List<HplcAnalyteInput> { new(null, "Analyte 1", 254m, standard.Id, 50m, 50m, 5) },
            SectionId: sectionId);

        var created = await service.CreateAsync(req, adminId);

        if (!active)
            await service.SetActiveAsync(created.Id, false, "Test setup", adminId);

        return await service.GetByIdAsync(created.Id, adminId);
    }

    private static CreateTestDefinitionRequest AssayReq(int sectionId, int? hplcMethodId, string code = "HPLC-T1", bool requiresSst = true) =>
        new(
            Code: code,
            DisplayName: "HPLC Assay Test",
            SectionId: sectionId,
            WorkflowType: WorkflowType.HplcMethodAssay,
            EquationType: EquationType.HplcMethodAssay,
            RequiresSystemSuitability: requiresSst,
            MethodAbbreviation: code,
            HplcMethodId: hplcMethodId);

    private static CreateTestDefinitionRequest DissolutionReq(int sectionId, int? hplcMethodId, string code = "DISS-T1") =>
        new(
            Code: code,
            DisplayName: "Dissolution Test",
            SectionId: sectionId,
            WorkflowType: WorkflowType.Dissolution,
            EquationType: EquationType.Dissolution,
            RequiresSystemSuitability: true,
            MethodAbbreviation: code,
            SstMaxRsdPercent: 2m,
            HplcMethodId: hplcMethodId);

    [Fact]
    public async Task TestDef_Dissolution_WithMethod_SavesMethod()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var method = await AddMethodAsync(db, section.Id, userId);
        var service = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));

        var created = await service.CreateTestDefinitionAsync(userId, DissolutionReq(section.Id, method.Id));

        Assert.Equal(method.Id, (await db.TestDefinitions.FirstAsync(t => t.Id == created.Id)).HplcMethodId);
    }

    [Fact]
    public async Task TestDef_Dissolution_MethodFromOtherSection_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var otherSection = await AddOtherSectionAsync(db, section.DepartmentId);
        var method = await AddMethodAsync(db, otherSection.Id, userId);
        var service = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateTestDefinitionAsync(userId, DissolutionReq(section.Id, method.Id)));
        Assert.Contains("another laboratory", ex.Message);
    }

    [Fact]
    public async Task TestDef_HplcMethodAssay_WithoutMethod_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var service = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));

        var req = AssayReq(section.Id, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateTestDefinitionAsync(userId, req));
        Assert.Contains("HPLC method is required", ex.Message);
    }

    [Fact]
    public async Task TestDef_HplcMethodAssay_InactiveMethod_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var method = await AddMethodAsync(db, section.Id, userId, active: false);
        var service = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));

        var req = AssayReq(section.Id, method.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateTestDefinitionAsync(userId, req));
        Assert.Contains("inactive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestDef_HplcMethodAssay_MethodFromOtherSection_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var otherSection = await AddOtherSectionAsync(db, section.DepartmentId);
        var method = await AddMethodAsync(db, otherSection.Id, userId);
        var service = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));

        var req = AssayReq(section.Id, method.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateTestDefinitionAsync(userId, req));
        Assert.Contains("another laboratory", ex.Message);
    }

    [Fact]
    public async Task TestDef_HplcMethodAssay_EquationMustMatchWorkflow_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var method = await AddMethodAsync(db, section.Id, userId);
        var service = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));

        var req = AssayReq(section.Id, method.Id) with { EquationType = EquationType.None };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateTestDefinitionAsync(userId, req));
        Assert.Contains("HplcMethodAssay", ex.Message);
    }

    [Fact]
    public async Task TestDef_OtherType_WithMethodId_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var method = await AddMethodAsync(db, section.Id, userId);
        var service = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));

        var req = new CreateTestDefinitionRequest(
            Code: "OTH-1", DisplayName: "Other test", SectionId: section.Id,
            WorkflowType: WorkflowType.Measurement, EquationType: EquationType.Measurement,
            ReplicateCount: 3, EvaluationBasis: MeasurementEvaluationBasis.Mean,
            HplcMethodId: method.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateTestDefinitionAsync(userId, req));
        Assert.Contains("only allowed for HPLC method assay and dissolution tests", ex.Message);
    }

    [Fact]
    public async Task TestDef_HplcMethodAssay_Update_KeepsInactiveMethodIfUnchanged()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var method = await AddMethodAsync(db, section.Id, userId);
        var service = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));

        var created = await service.CreateTestDefinitionAsync(userId, AssayReq(section.Id, method.Id));

        var hplcService = TestServiceFactory.HplcMethod(db);
        await hplcService.SetActiveAsync(method.Id, false, "Retire", userId);

        var updateReq = new UpdateTestDefinitionRequest(created.Code, "HPLC Assay Test Renamed");
        var updated = await service.UpdateTestDefinitionAsync(userId, created.Id, updateReq);

        Assert.Equal(method.Id, updated.HplcMethodId);
        Assert.Equal("HPLC Assay Test Renamed", updated.DisplayName);
    }

    [Fact]
    public async Task Spec_HplcMethodAssay_RequiresAnalyteOfThatMethod()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var method = await AddMethodAsync(db, section.Id, userId);
        var testService = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));
        var testDef = await testService.CreateTestDefinitionAsync(userId, AssayReq(section.Id, method.Id));

        var item = new Item
        {
            Code = "ITEM-" + Guid.NewGuid().ToString("N")[..6],
            Name = "Item 1",
            AssignedTests = { new SampleTest { TestCode = testDef.Code, DisplayName = testDef.DisplayName } }
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var specService = new SpecificationMasterDataService(db, new UserSectionScopeService(db));

        var req = new CreateSpecificationRequest(
            ItemId: item.Id, TestCode: testDef.Code, ParameterName: "Assay",
            LimitType: LimitType.Range, LowerLimit: 90m, UpperLimit: 110m,
            ResultBasis: ResultBasis.PercentLabelClaim, HplcMethodAnalyteId: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.CreateSpecificationAsync(userId, req));
        Assert.Contains("Method analyte is required", ex.Message);
    }

    [Fact]
    public async Task Spec_HplcMethodAssay_RequiresResultBasisPercentOrMgPerUnit()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var method = await AddMethodAsync(db, section.Id, userId);
        var testService = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));
        var testDef = await testService.CreateTestDefinitionAsync(userId, AssayReq(section.Id, method.Id));
        var analyteId = method.Analytes[0].Id;

        var item = new Item
        {
            Code = "ITEM-" + Guid.NewGuid().ToString("N")[..6],
            Name = "Item 1",
            AssignedTests = { new SampleTest { TestCode = testDef.Code, DisplayName = testDef.DisplayName } }
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var specService = new SpecificationMasterDataService(db, new UserSectionScopeService(db));

        var req = new CreateSpecificationRequest(
            ItemId: item.Id, TestCode: testDef.Code, ParameterName: "Assay",
            LimitType: LimitType.Range, LowerLimit: 90m, UpperLimit: 110m,
            ResultBasis: ResultBasis.MgPerKg, HplcMethodAnalyteId: analyteId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.CreateSpecificationAsync(userId, req));
        Assert.Contains("Result basis must be assay %", ex.Message);
    }

    [Fact]
    public async Task Spec_HplcMethodAssay_MgPerUnit_RequiresLabelClaimAndUnit()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var method = await AddMethodAsync(db, section.Id, userId);
        var testService = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));
        var testDef = await testService.CreateTestDefinitionAsync(userId, AssayReq(section.Id, method.Id));
        var analyteId = method.Analytes[0].Id;

        var item = new Item
        {
            Code = "ITEM-" + Guid.NewGuid().ToString("N")[..6],
            Name = "Item 1",
            AssignedTests = { new SampleTest { TestCode = testDef.Code, DisplayName = testDef.DisplayName } }
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var specService = new SpecificationMasterDataService(db, new UserSectionScopeService(db));

        var req = new CreateSpecificationRequest(
            ItemId: item.Id, TestCode: testDef.Code, ParameterName: "Assay",
            LimitType: LimitType.Range, LowerLimit: 90m, UpperLimit: 110m,
            ResultBasis: ResultBasis.MgPerUnit, HplcMethodAnalyteId: analyteId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.CreateSpecificationAsync(userId, req));
        Assert.Contains("needs a label claim and its unit", ex.Message);
    }

    [Fact]
    public async Task Spec_HplcMethodAssay_Percent_RejectsLabelClaim()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var method = await AddMethodAsync(db, section.Id, userId);
        var testService = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));
        var testDef = await testService.CreateTestDefinitionAsync(userId, AssayReq(section.Id, method.Id));
        var analyteId = method.Analytes[0].Id;

        var item = new Item
        {
            Code = "ITEM-" + Guid.NewGuid().ToString("N")[..6],
            Name = "Item 1",
            AssignedTests = { new SampleTest { TestCode = testDef.Code, DisplayName = testDef.DisplayName } }
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var specService = new SpecificationMasterDataService(db, new UserSectionScopeService(db));

        var req = new CreateSpecificationRequest(
            ItemId: item.Id, TestCode: testDef.Code, ParameterName: "Assay",
            LimitType: LimitType.Range, LowerLimit: 90m, UpperLimit: 110m,
            ResultBasis: ResultBasis.PercentLabelClaim, HplcMethodAnalyteId: analyteId,
            LabelClaim: 500m, LabelClaimUnit: "mg");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.CreateSpecificationAsync(userId, req));
        Assert.Contains("Label claim belongs on the amount-per-unit row", ex.Message);
    }

    [Fact]
    public async Task Spec_HplcMethodAssay_DuplicateAnalyteAndBasis_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var method = await AddMethodAsync(db, section.Id, userId);
        var testService = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));
        var testDef = await testService.CreateTestDefinitionAsync(userId, AssayReq(section.Id, method.Id));
        var analyteId = method.Analytes[0].Id;

        var item = new Item
        {
            Code = "ITEM-" + Guid.NewGuid().ToString("N")[..6],
            Name = "Item 1",
            AssignedTests = { new SampleTest { TestCode = testDef.Code, DisplayName = testDef.DisplayName } }
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var specService = new SpecificationMasterDataService(db, new UserSectionScopeService(db));

        var req1 = new CreateSpecificationRequest(
            ItemId: item.Id, TestCode: testDef.Code, ParameterName: "Assay",
            LimitType: LimitType.Range, LowerLimit: 90m, UpperLimit: 110m,
            ResultBasis: ResultBasis.PercentLabelClaim, HplcMethodAnalyteId: analyteId);
        await specService.CreateSpecificationAsync(userId, req1);

        var req2 = new CreateSpecificationRequest(
            ItemId: item.Id, TestCode: testDef.Code, ParameterName: "Assay 2",
            LimitType: LimitType.Range, LowerLimit: 91m, UpperLimit: 111m,
            ResultBasis: ResultBasis.PercentLabelClaim, HplcMethodAnalyteId: analyteId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.CreateSpecificationAsync(userId, req2));
        Assert.Contains("analyte and basis already exists", ex.Message);
    }

    [Fact]
    public async Task Spec_OtherTestType_WithHplcMethodAnalyteId_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var method = await AddMethodAsync(db, section.Id, userId);
        var analyteId = method.Analytes[0].Id;

        var testDef = new TestDefinition
        {
            Code = "OTH-QUAL",
            DisplayName = "Other Qualitative Test",
            SectionId = section.Id,
            WorkflowType = WorkflowType.Qualitative,
            EquationType = EquationType.Qualitative
        };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        var item = new Item
        {
            Code = "ITEM-" + Guid.NewGuid().ToString("N")[..6],
            Name = "Item 1",
            AssignedTests = { new SampleTest { TestCode = testDef.Code, DisplayName = testDef.DisplayName } }
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var specService = new SpecificationMasterDataService(db, new UserSectionScopeService(db));

        var req = new CreateSpecificationRequest(
            ItemId: item.Id, TestCode: testDef.Code, ParameterName: "Identity",
            LimitType: LimitType.Qualitative, ExpectedResultText: "Conforms",
            HplcMethodAnalyteId: analyteId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.CreateSpecificationAsync(userId, req));
        Assert.Contains("only allowed for HPLC method assay specifications", ex.Message);
    }
}
