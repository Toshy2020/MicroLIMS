using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class WeightVariationMasterDataValidationTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (DocumentSection fpSec, User fpHead, MasterDataController controller) SetupController(MicroLimsDbContext db)
    {
        var microSec = TestServiceFactory.EnsureMicroSection(db);
        var fpSec = db.DocumentSections.FirstOrDefault(s => s.Code == "FP");
        if (fpSec == null)
        {
            fpSec = new DocumentSection
            {
                Name = "Finished Product Laboratory",
                Code = "FP",
                DepartmentId = microSec.DepartmentId,
                IsActive = true
            };
            db.DocumentSections.Add(fpSec);
            db.SaveChanges();
        }

        var headRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.SectionHead);
        if (headRole == null)
        {
            headRole = new Role { Name = "Section Head", Type = RoleType.SectionHead, IsActive = true };
            db.Roles.Add(headRole);
            db.SaveChanges();
        }

        var fpHead = new User
        {
            Username = "fpHead_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "FP Section Head",
            RoleId = headRole.Id,
            Role = headRole,
            IsActive = true
        };
        db.Users.Add(fpHead);
        db.SaveChanges();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = fpHead.Id,
            DepartmentId = fpSec.DepartmentId,
            SectionId = fpSec.Id
        });
        db.SaveChanges();

        var scope = new UserSectionScopeService(db);
        var colService = new ChromatographyColumnService(db, scope);
        var controller = new MasterDataController(
            db,
            new EquipmentConfigurationService(db),
            TestServiceFactory.MediaProduct(db),
            TestServiceFactory.MediaIncubationCondition(db),
            scope,
            colService);

        var httpContext = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, fpHead.Id.ToString()) },
                    "TestAuth"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (fpSec, fpHead, controller);
    }

    [Fact]
    public async Task CreateTestDefinition_WeightVariation_RequiresPairing()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        // Workflow WeightVariation but Equation None -> fails
        var req1 = new CreateTestDefinitionRequest(
            Code: "WV-1",
            DisplayName: "Weight Variation 1",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.WeightVariation,
            EquationType: EquationType.None,
            RequiresSystemSuitability: false);

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req1));
        Assert.Contains("Equation type must be WeightVariation", ex1.Message);

        // Equation WeightVariation but Workflow Observation -> fails
        var req2 = new CreateTestDefinitionRequest(
            Code: "WV-2",
            DisplayName: "Weight Variation 2",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Observation,
            EquationType: EquationType.WeightVariation,
            RequiresSystemSuitability: false);

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req2));
        Assert.Contains("Workflow type must be WeightVariation", ex2.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_WeightVariation_RequiresSystemSuitabilityMustBeFalse()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var req = new CreateTestDefinitionRequest(
            Code: "WV-SST",
            DisplayName: "Weight Variation Test",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.WeightVariation,
            EquationType: EquationType.WeightVariation,
            RequiresSystemSuitability: true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req));
        Assert.Contains("must not require system suitability", ex.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_WeightVariation_DefaultConfigAppliedWhenOmitted()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var req = new CreateTestDefinitionRequest(
            Code: "WV-DEF",
            DisplayName: "Weight Variation Default",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.WeightVariation,
            EquationType: EquationType.WeightVariation,
            RequiresSystemSuitability: false);

        var actionResult = await controller.CreateTestDefinition(req);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        var entity = Assert.IsType<TestDefinition>(response.Data);

        Assert.Equal(20, entity.WvUnitCount);
        Assert.Equal(130m, entity.WvTabletBand1MaxMg);
        Assert.Equal(10m, entity.WvTabletBand1Percent);
        Assert.Equal(324m, entity.WvTabletBand2MaxMg);
        Assert.Equal(7.5m, entity.WvTabletBand2Percent);
        Assert.Equal(5m, entity.WvTabletBand3Percent);
        Assert.Equal(2, entity.WvTabletMaxOutside);
        Assert.Equal(10m, entity.WvCapsuleInnerPercent);
        Assert.Equal(25m, entity.WvCapsuleOuterPercent);
        Assert.Equal(2, entity.WvCapsuleS1MaxOutside);
        Assert.Equal(6, entity.WvCapsuleS1MaxForRetest);
        Assert.Equal(40, entity.WvCapsuleS2ExtraUnits);
        Assert.Equal(6, entity.WvCapsuleS2MaxOutside);
    }

    [Fact]
    public async Task CreateTestDefinition_WeightVariation_InvalidConfigRejected()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        // WvUnitCount < 1
        var req1 = new CreateTestDefinitionRequest(
            Code: "WV-BAD1",
            DisplayName: "Bad Count",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.WeightVariation,
            EquationType: EquationType.WeightVariation,
            RequiresSystemSuitability: false,
            WvUnitCount: 0);

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req1));
        Assert.Contains("must be greater than or equal to 1", ex1.Message);

        // Band1MaxMg >= Band2MaxMg
        var req2 = new CreateTestDefinitionRequest(
            Code: "WV-BAD2",
            DisplayName: "Bad Bands",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.WeightVariation,
            EquationType: EquationType.WeightVariation,
            RequiresSystemSuitability: false,
            WvTabletBand1MaxMg: 350m,
            WvTabletBand2MaxMg: 324m);

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req2));
        Assert.Contains("Tablet Band 1 Max Mg must be less than Band 2 Max Mg", ex2.Message);

        // CapsuleInnerPercent >= CapsuleOuterPercent
        var req3 = new CreateTestDefinitionRequest(
            Code: "WV-BAD3",
            DisplayName: "Bad InnerOuter",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.WeightVariation,
            EquationType: EquationType.WeightVariation,
            RequiresSystemSuitability: false,
            WvCapsuleInnerPercent: 25m,
            WvCapsuleOuterPercent: 25m);

        var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req3));
        Assert.Contains("capsule inner percentage must be less than outer percentage", ex3.Message);

        // CapsuleS1MaxOutside >= CapsuleS1MaxForRetest
        var req4 = new CreateTestDefinitionRequest(
            Code: "WV-BAD4",
            DisplayName: "Bad S1 Retest",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.WeightVariation,
            EquationType: EquationType.WeightVariation,
            RequiresSystemSuitability: false,
            WvCapsuleS1MaxOutside: 6,
            WvCapsuleS1MaxForRetest: 6);

        var ex4 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req4));
        Assert.Contains("Stage 1 max outside must be less than Stage 1 max for retest", ex4.Message);

        // CapsuleS2MaxOutside >= unit count + extra units (20 + 40 = 60)
        var req5 = new CreateTestDefinitionRequest(
            Code: "WV-BAD5",
            DisplayName: "Bad S2 Outside",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.WeightVariation,
            EquationType: EquationType.WeightVariation,
            RequiresSystemSuitability: false,
            WvUnitCount: 20,
            WvCapsuleS2ExtraUnits: 40,
            WvCapsuleS2MaxOutside: 60);

        var ex5 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req5));
        Assert.Contains("Stage 2 max outside must be less than total units (60)", ex5.Message);
    }

    [Fact]
    public async Task CreateTestWorkflowStep_WeightVariation_RejectsSteps()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var testDef = new TestDefinition
        {
            Code = "WV-STEPS",
            DisplayName = "Weight Variation Steps Test",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.WeightVariation,
            EquationType = EquationType.WeightVariation,
            RequiresSystemSuitability = false,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        var stepReq = new CreateTestWorkflowStepRequest(
            StepName: "WV Step",
            IncubationMinHours: 0,
            IncubationMaxHours: 0,
            TemperatureMin: 0,
            TemperatureMax: 0,
            IsFinalStep: true,
            StepType: StepType.BrothEnrichment,
            TargetOrganismId: null,
            StepMedia: new List<StepMediaRequest>(),
            RequiresIncubationTransfer: false,
            IncubationStages: null,
            ConfirmatoryMediaCount: null,
            PhenotypicTestType: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestWorkflowStep(testDef.Id, stepReq));
        Assert.Contains("no workflow steps", ex.Message);
    }

    [Fact]
    public async Task UpdateTestDefinition_WeightVariation_RequiresPairingAndNoSst()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var testDef = new TestDefinition
        {
            Code = "WV-UPD",
            DisplayName = "WV Upd",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.WeightVariation,
            EquationType = EquationType.WeightVariation,
            RequiresSystemSuitability = false,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        // Mismatched equation
        var req1 = new UpdateTestDefinitionRequest(
            Code: "WV-UPD",
            DisplayName: "WV Upd",
            EquationType: EquationType.None);

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.UpdateTestDefinition(testDef.Id, req1));
        Assert.Contains("Equation type must be WeightVariation", ex1.Message);

        // SST set to true
        var req2 = new UpdateTestDefinitionRequest(
            Code: "WV-UPD",
            DisplayName: "WV Upd",
            RequiresSystemSuitability: true);

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.UpdateTestDefinition(testDef.Id, req2));
        Assert.Contains("must not require system suitability", ex2.Message);
    }

    // --- SpecificationService Unit Tests for WeightVariation ---

    [Fact]
    public void SpecificationService_BuildCanonicalSpecLimit_FormatsWeightVariation()
    {
        var specTab = new Specification
        {
            LimitType = LimitType.WeightVariation,
            DosageForm = DosageForm.Tablet,
            Unit = "mg"
        };
        Assert.Equal("USP <2091>: tablets, limit by average weight", SpecificationService.BuildCanonicalSpecLimit(specTab));

        var specHard = new Specification
        {
            LimitType = LimitType.WeightVariation,
            DosageForm = DosageForm.HardCapsule,
            Unit = "mg"
        };
        Assert.Equal("USP <2091>: net content 90-110 % of average", SpecificationService.BuildCanonicalSpecLimit(specHard));

        var specSoft = new Specification
        {
            LimitType = LimitType.WeightVariation,
            DosageForm = DosageForm.SoftCapsule,
            Unit = "mg"
        };
        Assert.Equal("USP <2091>: net content 90-110 % of average", SpecificationService.BuildCanonicalSpecLimit(specSoft));
    }

    [Fact]
    public void SpecificationService_Validate_WeightVariation_RequiresValidInputs()
    {
        var specService = new SpecificationService(null!);

        // Missing DosageForm
        var specNoForm = new Specification
        {
            ParameterName = "Weight Variation",
            LimitType = LimitType.WeightVariation,
            Unit = "mg",
            DosageForm = null
        };
        var exNoForm = Assert.Throws<InvalidOperationException>(() => specService.Validate(specNoForm));
        Assert.Contains("Dosage form is required for WeightVariation specifications", exNoForm.Message);

        // Wrong Unit
        var specWrongUnit = new Specification
        {
            ParameterName = "Weight Variation",
            LimitType = LimitType.WeightVariation,
            Unit = "g",
            DosageForm = DosageForm.Tablet
        };
        var exWrongUnit = Assert.Throws<InvalidOperationException>(() => specService.Validate(specWrongUnit));
        Assert.Contains("Unit must be \"mg\"", exWrongUnit.Message);

        // LowerLimit present
        var specLower = new Specification
        {
            ParameterName = "Weight Variation",
            LimitType = LimitType.WeightVariation,
            Unit = "mg",
            DosageForm = DosageForm.Tablet,
            LowerLimit = 100m
        };
        var exLower = Assert.Throws<InvalidOperationException>(() => specService.Validate(specLower));
        Assert.Contains("Limits are not allowed for WeightVariation specifications", exLower.Message);

        // UpperLimit present
        var specUpper = new Specification
        {
            ParameterName = "Weight Variation",
            LimitType = LimitType.WeightVariation,
            Unit = "mg",
            DosageForm = DosageForm.Tablet,
            UpperLimit = 100m
        };
        var exUpper = Assert.Throws<InvalidOperationException>(() => specService.Validate(specUpper));
        Assert.Contains("Limits are not allowed for WeightVariation specifications", exUpper.Message);

        // LabelClaim present
        var specLc = new Specification
        {
            ParameterName = "Weight Variation",
            LimitType = LimitType.WeightVariation,
            Unit = "mg",
            DosageForm = DosageForm.Tablet,
            LabelClaim = 100m
        };
        var exLc = Assert.Throws<InvalidOperationException>(() => specService.Validate(specLc));
        Assert.Contains("Label claim is not allowed", exLc.Message);

        // DosageForm present on non-WeightVariation spec
        var specNonWvForm = new Specification
        {
            ParameterName = "Assay",
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m,
            Unit = "%",
            DosageForm = DosageForm.Tablet
        };
        var exNonWv = Assert.Throws<InvalidOperationException>(() => specService.Validate(specNonWvForm));
        Assert.Contains("Dosage form is only allowed for WeightVariation specifications", exNonWv.Message);

        // Valid
        var validSpec = new Specification
        {
            ParameterName = "Weight Variation",
            LimitType = LimitType.WeightVariation,
            Unit = "mg",
            DosageForm = DosageForm.Tablet
        };
        specService.Validate(validSpec);
    }

    [Fact]
    public async Task SpecificationService_ValidateAsync_OnlyAllowedOnWeightVariationTests()
    {
        using var db = NewDb();
        var specService = new SpecificationService(db);

        var item = new Item { Code = "ITM-WV-01", Name = "Tablet WV 01", IsActive = true };
        db.Items.Add(item);

        var nonWvTest = new TestDefinition
        {
            Code = "TAMC",
            DisplayName = "TAMC",
            WorkflowType = WorkflowType.CountTest,
            IsActive = true
        };
        db.TestDefinitions.Add(nonWvTest);

        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = "TAMC" });
        await db.SaveChangesAsync();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = "TAMC",
            ParameterName = "Weight Variation",
            LimitType = LimitType.WeightVariation,
            DosageForm = DosageForm.Tablet,
            Unit = "mg"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(spec));
        Assert.Contains("only allowed for WeightVariation tests", ex.Message);
    }

    [Fact]
    public async Task SpecificationService_ValidateAsync_WeightVariationTestRequiresWeightVariationLimit()
    {
        using var db = NewDb();
        var specService = new SpecificationService(db);

        var item = new Item { Code = "ITM-WV-02", Name = "Tablet WV 02", IsActive = true };
        db.Items.Add(item);

        var wvTest = new TestDefinition
        {
            Code = "WV-TEST-01",
            DisplayName = "WV Test",
            WorkflowType = WorkflowType.WeightVariation,
            EquationType = EquationType.WeightVariation,
            RequiresSystemSuitability = false,
            IsActive = true
        };
        db.TestDefinitions.Add(wvTest);

        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = "WV-TEST-01" });
        await db.SaveChangesAsync();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = "WV-TEST-01",
            ParameterName = "Weight Variation",
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 100m,
            Unit = "mg"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(spec));
        Assert.Contains("Only WeightVariation specifications are allowed for WeightVariation tests", ex.Message);
    }

    [Fact]
    public async Task SpecificationService_ValidateAsync_AtMostOnePerItemAndTest()
    {
        using var db = NewDb();
        var specService = new SpecificationService(db);

        var item = new Item { Code = "ITM-WV-03", Name = "Tablet WV 03", IsActive = true };
        db.Items.Add(item);

        var wvTest = new TestDefinition
        {
            Code = "WV-TEST-03",
            DisplayName = "WV Test 3",
            WorkflowType = WorkflowType.WeightVariation,
            EquationType = EquationType.WeightVariation,
            RequiresSystemSuitability = false,
            IsActive = true
        };
        db.TestDefinitions.Add(wvTest);

        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = "WV-TEST-03" });

        var spec1 = new Specification
        {
            ItemId = item.Id,
            TestCode = "WV-TEST-03",
            ParameterName = "Weight Variation 1",
            LimitType = LimitType.WeightVariation,
            DosageForm = DosageForm.Tablet,
            Unit = "mg"
        };
        db.Specifications.Add(spec1);
        await db.SaveChangesAsync();

        var spec2 = new Specification
        {
            ItemId = item.Id,
            TestCode = "WV-TEST-03",
            ParameterName = "Weight Variation 2",
            LimitType = LimitType.WeightVariation,
            DosageForm = DosageForm.Tablet,
            Unit = "mg"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(spec2));
        Assert.Contains("Only one specification is allowed for WeightVariation test", ex.Message);
    }

    [Fact]
    public async Task CreateSpecification_WeightVariation_PersistsDosageForm()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var item = new Item { Code = "ITM-SPEC-WV", Name = "Item Spec WV", IsActive = true };
        db.Items.Add(item);

        var wvTest = new TestDefinition
        {
            Code = "WV-FOR-SPEC",
            DisplayName = "WV Test",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.WeightVariation,
            EquationType = EquationType.WeightVariation,
            RequiresSystemSuitability = false,
            IsActive = true
        };
        db.TestDefinitions.Add(wvTest);
        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = "WV-FOR-SPEC" });
        await db.SaveChangesAsync();

        var req = new CreateSpecificationRequest(
            ItemId: item.Id,
            TestCode: "WV-FOR-SPEC",
            ParameterName: "Weight Variation",
            LimitType: LimitType.WeightVariation,
            Unit: "mg",
            DosageForm: DosageForm.HardCapsule);

        var actionResult = await controller.CreateSpecification(req);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        var entity = Assert.IsType<Specification>(response.Data);

        Assert.Equal(DosageForm.HardCapsule, entity.DosageForm);
        Assert.Equal("USP <2091>: net content 90-110 % of average", entity.SpecLimit);
    }
}
