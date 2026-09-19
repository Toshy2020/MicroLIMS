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

public class DissolutionMasterDataValidationTests
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
    public async Task CreateTestDefinition_Dissolution_RequiresPairing()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        // Workflow Dissolution but Equation None -> fails
        var req1 = new CreateTestDefinitionRequest(
            Code: "DIS-1",
            DisplayName: "Dissolution 1",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Dissolution,
            EquationType: EquationType.None,
            RequiresSystemSuitability: true, MethodAbbreviation: "DIS", SstMaxRsdPercent: 2.0m);

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req1));
        Assert.Contains("Equation type must be Dissolution", ex1.Message);

        // Equation Dissolution but Workflow None/Observation -> fails
        var req2 = new CreateTestDefinitionRequest(
            Code: "DIS-2",
            DisplayName: "Dissolution 2",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Observation,
            EquationType: EquationType.Dissolution,
            RequiresSystemSuitability: true, MethodAbbreviation: "DIS", SstMaxRsdPercent: 2.0m);

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req2));
        Assert.Contains("Workflow type must be Dissolution", ex2.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_Dissolution_RequiresSystemSuitabilityMustBeTrue()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var req = new CreateTestDefinitionRequest(
            Code: "DIS-SST",
            DisplayName: "Dissolution Test",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Dissolution,
            EquationType: EquationType.Dissolution,
            RequiresSystemSuitability: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req));
        Assert.Contains("must require system suitability", ex.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_Dissolution_DefaultOffsetsAppliedWhenOmitted()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var req = new CreateTestDefinitionRequest(
            Code: "DIS-DEF",
            DisplayName: "Dissolution Test Default",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Dissolution,
            EquationType: EquationType.Dissolution,
            RequiresSystemSuitability: true, MethodAbbreviation: "DIS", SstMaxRsdPercent: 2.0m);

        var actionResult = await controller.CreateTestDefinition(req);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        var entity = Assert.IsType<TestDefinition>(response.Data);

        Assert.Equal(5m, entity.DissolutionS1Offset);
        Assert.Equal(15m, entity.DissolutionS2MinOffset);
        Assert.Equal(25m, entity.DissolutionS3MinOffset);
        Assert.Equal(2m, entity.DissolutionS3MaxBelowS2Min);
    }

    [Fact]
    public async Task CreateTestDefinition_Dissolution_NegativeOffsetsRejected()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var req = new CreateTestDefinitionRequest(
            Code: "DIS-NEG",
            DisplayName: "Dissolution Test Neg",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Dissolution,
            EquationType: EquationType.Dissolution,
            RequiresSystemSuitability: true, MethodAbbreviation: "DIS", SstMaxRsdPercent: 2.0m,
            DissolutionS1Offset: -1m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req));
        Assert.Contains("offsets must be greater than or equal to zero", ex.Message);
    }

    [Fact]
    public async Task CreateTestWorkflowStep_Dissolution_RejectsSteps()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var testDef = new TestDefinition
        {
            Code = "DIS-STEPS",
            DisplayName = "Dissolution Steps Test",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.Dissolution,
            EquationType = EquationType.Dissolution,
            RequiresSystemSuitability = true, MethodAbbreviation = "DIS", SstMaxRsdPercent = 2.0m,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        var stepReq = new CreateTestWorkflowStepRequest(
            StepName: "Vessel Plating",
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

    // --- SpecificationService Unit Tests for DissolutionQ ---

    [Fact]
    public void SpecificationService_BuildCanonicalSpecLimit_FormatsDissolutionQ()
    {
        var spec = new Specification
        {
            LimitType = LimitType.DissolutionQ,
            LowerLimit = 80m
        };

        var canonical = SpecificationService.BuildCanonicalSpecLimit(spec);
        Assert.Equal("Q = 80 %", canonical);
    }

    [Fact]
    public void SpecificationService_Validate_DissolutionQ_RequiresValidInputs()
    {
        var specService = new SpecificationService(null!);

        // Missing LowerLimit (Q)
        var specNoQ = new Specification
        {
            ParameterName = "Dissolution",
            LimitType = LimitType.DissolutionQ,
            LabelClaim = 20m,
            LabelClaimUnit = "mg"
        };
        var exNoQ = Assert.Throws<InvalidOperationException>(() => specService.Validate(specNoQ));
        Assert.Contains("Lower limit (Q) must be between 0 and 100", exNoQ.Message);

        // Q > 100
        var specHighQ = new Specification
        {
            ParameterName = "Dissolution",
            LimitType = LimitType.DissolutionQ,
            LowerLimit = 105m,
            LabelClaim = 20m,
            LabelClaimUnit = "mg"
        };
        var exHighQ = Assert.Throws<InvalidOperationException>(() => specService.Validate(specHighQ));
        Assert.Contains("Lower limit (Q) must be between 0 and 100", exHighQ.Message);

        // Missing LabelClaim
        var specNoLc = new Specification
        {
            ParameterName = "Dissolution",
            LimitType = LimitType.DissolutionQ,
            LowerLimit = 80m,
            LabelClaim = null,
            LabelClaimUnit = "mg"
        };
        var exNoLc = Assert.Throws<InvalidOperationException>(() => specService.Validate(specNoLc));
        Assert.Contains("Label claim must be greater than zero", exNoLc.Message);

        // Invalid LabelClaimUnit
        var specBadUnit = new Specification
        {
            ParameterName = "Dissolution",
            LimitType = LimitType.DissolutionQ,
            LowerLimit = 80m,
            LabelClaim = 20m,
            LabelClaimUnit = "g"
        };
        var exBadUnit = Assert.Throws<InvalidOperationException>(() => specService.Validate(specBadUnit));
        Assert.Contains("Label claim unit must be \"mg\"", exBadUnit.Message);

        // Valid
        var validSpec = new Specification
        {
            ParameterName = "Dissolution",
            LimitType = LimitType.DissolutionQ,
            LowerLimit = 80m,
            LabelClaim = 20m,
            LabelClaimUnit = "mg"
        };
        specService.Validate(validSpec); // passes without throwing
    }

    [Fact]
    public async Task SpecificationService_ValidateAsync_OnlyAllowedOnDissolutionTests()
    {
        using var db = NewDb();
        var specService = new SpecificationService(db);

        var item = new Item { Code = "ITM-01", Name = "Tablet 01", IsActive = true };
        db.Items.Add(item);

        var nonDissTest = new TestDefinition
        {
            Code = "TAMC",
            DisplayName = "TAMC",
            WorkflowType = WorkflowType.CountTest,
            IsActive = true
        };
        db.TestDefinitions.Add(nonDissTest);

        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = "TAMC" });
        await db.SaveChangesAsync();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = "TAMC",
            ParameterName = "Dissolution",
            LimitType = LimitType.DissolutionQ,
            LowerLimit = 80m,
            LabelClaim = 20m,
            LabelClaimUnit = "mg"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(spec));
        Assert.Contains("only allowed for Dissolution tests", ex.Message);
    }

    [Fact]
    public async Task SpecificationService_ValidateAsync_AtMostOnePerItemAndTest()
    {
        using var db = NewDb();
        var specService = new SpecificationService(db);

        var item = new Item { Code = "ITM-02", Name = "Tablet 02", IsActive = true };
        db.Items.Add(item);

        var dissTest = new TestDefinition
        {
            Code = "DISS-TAB",
            DisplayName = "Dissolution Tablet",
            WorkflowType = WorkflowType.Dissolution,
            EquationType = EquationType.Dissolution,
            RequiresSystemSuitability = true, MethodAbbreviation = "DIS", SstMaxRsdPercent = 2.0m,
            IsActive = true
        };
        db.TestDefinitions.Add(dissTest);

        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = "DISS-TAB" });

        var existingSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = "DISS-TAB",
            ParameterName = "Dissolution",
            LimitType = LimitType.DissolutionQ,
            LowerLimit = 80m,
            LabelClaim = 20m,
            LabelClaimUnit = "mg"
        };
        db.Specifications.Add(existingSpec);
        await db.SaveChangesAsync();

        // Attempting to add a second spec to the same item + dissolution test
        var secondSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = "DISS-TAB",
            ParameterName = "Dissolution 2",
            LimitType = LimitType.DissolutionQ,
            LowerLimit = 75m,
            LabelClaim = 20m,
            LabelClaimUnit = "mg"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(secondSpec));
        Assert.Contains("Only one specification is allowed for Dissolution test", ex.Message);
    }
}
