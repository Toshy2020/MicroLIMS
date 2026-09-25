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

public class DisintegrationMasterDataValidationTests
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
    public async Task CreateTestDefinition_Disintegration_RequiresPairing()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        // Workflow Disintegration but Equation None -> fails
        var req1 = new CreateTestDefinitionRequest(
            Code: "DT-1",
            DisplayName: "Disintegration 1",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Disintegration,
            EquationType: EquationType.None,
            RequiresSystemSuitability: false);

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req1));
        Assert.Contains("Equation type must be Disintegration", ex1.Message);

        // Equation Disintegration but Workflow Observation -> fails
        var req2 = new CreateTestDefinitionRequest(
            Code: "DT-2",
            DisplayName: "Disintegration 2",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Observation,
            EquationType: EquationType.Disintegration,
            RequiresSystemSuitability: false);

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req2));
        Assert.Contains("Workflow type must be Disintegration", ex2.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_Disintegration_RequiresSystemSuitabilityMustBeFalse()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var req = new CreateTestDefinitionRequest(
            Code: "DT-SST",
            DisplayName: "Disintegration Test",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Disintegration,
            EquationType: EquationType.Disintegration,
            RequiresSystemSuitability: true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req));
        Assert.Contains("must not require system suitability", ex.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_Disintegration_DefaultConfigAppliedWhenOmitted()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var req = new CreateTestDefinitionRequest(
            Code: "DT-DEF",
            DisplayName: "Disintegration Test Default",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Disintegration,
            EquationType: EquationType.Disintegration,
            RequiresSystemSuitability: false);

        var actionResult = await controller.CreateTestDefinition(req);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        var entity = Assert.IsType<TestDefinition>(response.Data);

        Assert.Equal(6, entity.DisintegrationStage1Units);
        Assert.Equal(12, entity.DisintegrationStage2Units);
        Assert.Equal(2, entity.DisintegrationMaxStage1Failures);
        Assert.Equal(16, entity.DisintegrationMinPassTotal);
    }

    [Fact]
    public async Task CreateTestDefinition_Disintegration_InvalidConfigRejected()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        // Stage 1 units < 1
        var req1 = new CreateTestDefinitionRequest(
            Code: "DT-BAD1",
            DisplayName: "Disintegration Bad 1",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Disintegration,
            EquationType: EquationType.Disintegration,
            RequiresSystemSuitability: false,
            DisintegrationStage1Units: 0);

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req1));
        Assert.Contains("stage units must be greater than or equal to 1", ex1.Message);

        // MaxStage1Failures >= Stage1Units
        var req2 = new CreateTestDefinitionRequest(
            Code: "DT-BAD2",
            DisplayName: "Disintegration Bad 2",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Disintegration,
            EquationType: EquationType.Disintegration,
            RequiresSystemSuitability: false,
            DisintegrationStage1Units: 6,
            DisintegrationMaxStage1Failures: 6);

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req2));
        Assert.Contains("maximum Stage 1 failures must be between", ex2.Message);

        // MinPassTotal > Stage1Units + Stage2Units
        var req3 = new CreateTestDefinitionRequest(
            Code: "DT-BAD3",
            DisplayName: "Disintegration Bad 3",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Disintegration,
            EquationType: EquationType.Disintegration,
            RequiresSystemSuitability: false,
            DisintegrationStage1Units: 6,
            DisintegrationStage2Units: 12,
            DisintegrationMinPassTotal: 19);

        var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req3));
        Assert.Contains("minimum pass total must be between", ex3.Message);
    }

    [Fact]
    public async Task CreateTestWorkflowStep_Disintegration_RejectsSteps()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var testDef = new TestDefinition
        {
            Code = "DT-STEPS",
            DisplayName = "Disintegration Steps Test",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.Disintegration,
            EquationType = EquationType.Disintegration,
            RequiresSystemSuitability = false,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        var stepReq = new CreateTestWorkflowStepRequest(
            StepName: "Disintegration Step",
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
    public async Task UpdateTestDefinition_Disintegration_RequiresPairingAndNoSst()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var testDef = new TestDefinition
        {
            Code = "DT-UPD",
            DisplayName = "Disintegration Upd",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.Disintegration,
            EquationType = EquationType.Disintegration,
            RequiresSystemSuitability = false,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        // Mismatched equation
        var req1 = new UpdateTestDefinitionRequest(
            Code: "DT-UPD",
            DisplayName: "Disintegration Upd",
            EquationType: EquationType.None);

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.UpdateTestDefinition(testDef.Id, req1));
        Assert.Contains("Equation type must be Disintegration", ex1.Message);

        // SST set to true
        var req2 = new UpdateTestDefinitionRequest(
            Code: "DT-UPD",
            DisplayName: "Disintegration Upd",
            RequiresSystemSuitability: true);

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.UpdateTestDefinition(testDef.Id, req2));
        Assert.Contains("must not require system suitability", ex2.Message);
    }

    // --- SpecificationService Unit Tests for DisintegrationTime ---

    [Fact]
    public void SpecificationService_BuildCanonicalSpecLimit_FormatsDisintegrationTime()
    {
        var spec = new Specification
        {
            LimitType = LimitType.DisintegrationTime,
            UpperLimit = 30m,
            Unit = "min"
        };

        var canonical = SpecificationService.BuildCanonicalSpecLimit(spec);
        Assert.Equal("NMT 30 min", canonical);
    }

    [Fact]
    public void SpecificationService_Validate_DisintegrationTime_RequiresValidInputs()
    {
        var specService = new SpecificationService(null!);

        // Missing UpperLimit
        var specNoUpper = new Specification
        {
            ParameterName = "Disintegration",
            LimitType = LimitType.DisintegrationTime,
            UpperLimit = null,
            Unit = "min"
        };
        var exNoUpper = Assert.Throws<InvalidOperationException>(() => specService.Validate(specNoUpper));
        Assert.Contains("Upper limit (time in minutes) must be greater than zero", exNoUpper.Message);

        // UpperLimit <= 0
        var specZeroUpper = new Specification
        {
            ParameterName = "Disintegration",
            LimitType = LimitType.DisintegrationTime,
            UpperLimit = 0m,
            Unit = "min"
        };
        var exZeroUpper = Assert.Throws<InvalidOperationException>(() => specService.Validate(specZeroUpper));
        Assert.Contains("Upper limit (time in minutes) must be greater than zero", exZeroUpper.Message);

        // Wrong Unit
        var specWrongUnit = new Specification
        {
            ParameterName = "Disintegration",
            LimitType = LimitType.DisintegrationTime,
            UpperLimit = 30m,
            Unit = "sec"
        };
        var exWrongUnit = Assert.Throws<InvalidOperationException>(() => specService.Validate(specWrongUnit));
        Assert.Contains("Unit must be \"min\"", exWrongUnit.Message);

        // LabelClaim present -> rejected
        var specWithLc = new Specification
        {
            ParameterName = "Disintegration",
            LimitType = LimitType.DisintegrationTime,
            UpperLimit = 30m,
            Unit = "min",
            LabelClaim = 10m
        };
        var exWithLc = Assert.Throws<InvalidOperationException>(() => specService.Validate(specWithLc));
        Assert.Contains("Label claim is not allowed", exWithLc.Message);

        // Valid
        var validSpec = new Specification
        {
            ParameterName = "Disintegration",
            LimitType = LimitType.DisintegrationTime,
            UpperLimit = 30m,
            Unit = "min"
        };
        specService.Validate(validSpec);
    }

    [Fact]
    public async Task SpecificationService_ValidateAsync_OnlyAllowedOnDisintegrationTests()
    {
        using var db = NewDb();
        var specService = new SpecificationService(db);

        var item = new Item { Code = "ITM-DT-01", Name = "Tablet DT 01", IsActive = true };
        db.Items.Add(item);

        var nonDtTest = new TestDefinition
        {
            Code = "TAMC",
            DisplayName = "TAMC",
            WorkflowType = WorkflowType.CountTest,
            IsActive = true
        };
        db.TestDefinitions.Add(nonDtTest);

        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = "TAMC" });
        await db.SaveChangesAsync();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = "TAMC",
            ParameterName = "Disintegration",
            LimitType = LimitType.DisintegrationTime,
            UpperLimit = 30m,
            Unit = "min"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(spec));
        Assert.Contains("only allowed for Disintegration tests", ex.Message);
    }

    [Fact]
    public async Task SpecificationService_ValidateAsync_DisintegrationTestRequiresDisintegrationTime()
    {
        using var db = NewDb();
        var specService = new SpecificationService(db);

        var item = new Item { Code = "ITM-DT-02", Name = "Tablet DT 02", IsActive = true };
        db.Items.Add(item);

        var dtTest = new TestDefinition
        {
            Code = "DISINT-01",
            DisplayName = "Disintegration Test",
            WorkflowType = WorkflowType.Disintegration,
            EquationType = EquationType.Disintegration,
            RequiresSystemSuitability = false,
            IsActive = true
        };
        db.TestDefinitions.Add(dtTest);

        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = "DISINT-01" });
        await db.SaveChangesAsync();

        // Trying to assign NotMoreThan limit type to Disintegration test
        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = "DISINT-01",
            ParameterName = "Disintegration",
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 30m,
            Unit = "min"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(spec));
        Assert.Contains("Only DisintegrationTime specifications are allowed for Disintegration tests", ex.Message);
    }

    [Fact]
    public async Task SpecificationService_ValidateAsync_AtMostOnePerItemAndTest()
    {
        using var db = NewDb();
        var specService = new SpecificationService(db);

        var item = new Item { Code = "ITM-DT-03", Name = "Tablet DT 03", IsActive = true };
        db.Items.Add(item);

        var dtTest = new TestDefinition
        {
            Code = "DISINT-TAB",
            DisplayName = "Disintegration Tablet",
            WorkflowType = WorkflowType.Disintegration,
            EquationType = EquationType.Disintegration,
            RequiresSystemSuitability = false,
            IsActive = true
        };
        db.TestDefinitions.Add(dtTest);

        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = "DISINT-TAB" });

        var existingSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = "DISINT-TAB",
            ParameterName = "Disintegration",
            LimitType = LimitType.DisintegrationTime,
            UpperLimit = 30m,
            Unit = "min"
        };
        db.Specifications.Add(existingSpec);
        await db.SaveChangesAsync();

        // Attempting to add a second spec to the same item + disintegration test
        var secondSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = "DISINT-TAB",
            ParameterName = "Disintegration 2",
            LimitType = LimitType.DisintegrationTime,
            UpperLimit = 15m,
            Unit = "min"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(secondSpec));
        Assert.Contains("Only one specification is allowed for Disintegration test", ex.Message);
    }

    [Fact]
    public async Task SpecificationService_ValidateAsync_ConversionFactorAndMetadataChecked()
    {
        using var db = NewDb();
        var specService = new SpecificationService(db);

        var item = new Item { Code = "ITM-DT-04", Name = "Tablet DT 04", IsActive = true };
        db.Items.Add(item);

        var dtTest = new TestDefinition
        {
            Code = "DISINT-CF",
            DisplayName = "Disintegration CF",
            WorkflowType = WorkflowType.Disintegration,
            EquationType = EquationType.Disintegration,
            RequiresSystemSuitability = false,
            IsActive = true
        };
        db.TestDefinitions.Add(dtTest);
        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = "DISINT-CF" });
        await db.SaveChangesAsync();

        // Conversion factor != 1.0
        var specBadCf = new Specification
        {
            ItemId = item.Id,
            TestCode = "DISINT-CF",
            ParameterName = "Disintegration",
            LimitType = LimitType.DisintegrationTime,
            UpperLimit = 30m,
            Unit = "min",
            ConversionFactor = 2.0m
        };
        var exBadCf = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(specBadCf));
        Assert.Contains("Conversion factor must be 1.0 for Disintegration specifications", exBadCf.Message);

        // Result basis set
        var specBadRb = new Specification
        {
            ItemId = item.Id,
            TestCode = "DISINT-CF",
            ParameterName = "Disintegration",
            LimitType = LimitType.DisintegrationTime,
            UpperLimit = 30m,
            Unit = "min",
            ConversionFactor = 1.0m,
            ResultBasis = ResultBasis.PercentLabelClaim
        };
        var exBadRb = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(specBadRb));
        Assert.Contains("Result basis is only allowed for Calibration Curve specifications", exBadRb.Message);
    }

    [Fact]
    public void SpecificationEvaluator_ThrowsOnDisintegrationTime()
    {
        var spec = new Specification
        {
            LimitType = LimitType.DisintegrationTime,
            UpperLimit = 30m
        };
        var ex = Assert.Throws<InvalidOperationException>(() => SpecificationEvaluator.Evaluate(spec, 25m));
        Assert.Contains("is not numeric and cannot be evaluated against a numeric value", ex.Message);
    }
}
