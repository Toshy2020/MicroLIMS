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

public class GravimetricQualitativeMasterDataValidationTests
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
    public async Task CreateTestDefinition_Gravimetric_RequiresReplicateCount1To30()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        // Missing ReplicateCount
        var reqNull = new CreateTestDefinitionRequest(
            Code: "GRAV-LOD",
            DisplayName: "Loss on Drying",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Gravimetric,
            EquationType: EquationType.GravimetricLoss,
            ReplicateCount: null);

        var exNull = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(reqNull));
        Assert.Contains("Replicate count must be between 1 and 30", exNull.Message);

        // ReplicateCount = 0
        var reqZero = reqNull with { ReplicateCount = 0 };
        var exZero = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(reqZero));
        Assert.Contains("Replicate count must be between 1 and 30", exZero.Message);

        // ReplicateCount = 31
        var req31 = reqNull with { ReplicateCount = 31 };
        var ex31 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req31));
        Assert.Contains("Replicate count must be between 1 and 30", ex31.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_Gravimetric_DefaultsUsesTareToFalse()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var req = new CreateTestDefinitionRequest(
            Code: "GRAV-RES",
            DisplayName: "Residue on Evaporation",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Gravimetric,
            EquationType: EquationType.GravimetricResidue,
            ReplicateCount: 1,
            UsesTare: null);

        var actionResult = await controller.CreateTestDefinition(req);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        var created = Assert.IsType<TestDefinition>(response.Data);

        Assert.False(created.UsesTare);
    }

    [Fact]
    public async Task CreateTestDefinition_Gravimetric_SetsConditionFieldsAndUsesTare()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var req = new CreateTestDefinitionRequest(
            Code: "GRAV-LOD-2",
            DisplayName: "Loss on Drying with Tare",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Gravimetric,
            EquationType: EquationType.GravimetricLoss,
            ReplicateCount: 2,
            ConditionFields: "Temperature (°C), Time (h)",
            UsesTare: true);

        var actionResult = await controller.CreateTestDefinition(req);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        var created = Assert.IsType<TestDefinition>(response.Data);

        Assert.True(created.UsesTare);
        Assert.Equal("Temperature (°C), Time (h)", created.ConditionFields);
    }

    [Fact]
    public async Task CreateTestDefinition_ConditionFieldsExceeding500Chars_Refused()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var longConditions = new string('C', 501);
        var req = new CreateTestDefinitionRequest(
            Code: "GRAV-LONG",
            DisplayName: "Long Conditions Test",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Gravimetric,
            EquationType: EquationType.GravimetricLoss,
            ReplicateCount: 1,
            ConditionFields: longConditions);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req));
        Assert.Contains("Condition fields cannot exceed 500 characters", ex.Message);
    }

    [Theory]
    [InlineData(WorkflowType.Gravimetric, EquationType.GravimetricLoss)]
    [InlineData(WorkflowType.Qualitative, EquationType.Qualitative)]
    public async Task CreateTestWorkflowStep_RefusesStepsForGravimetricAndQualitative(WorkflowType wf, EquationType eq)
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var testDef = new TestDefinition
        {
            Code = $"TEST-{wf}",
            DisplayName = $"{wf} Test",
            SectionId = fpSec.Id,
            WorkflowType = wf,
            EquationType = eq,
            ReplicateCount = wf == WorkflowType.Gravimetric ? 1 : null,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        var stepReq = new CreateTestWorkflowStepRequest(
            StepName: "Any Step",
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
        Assert.Contains($"{wf} tests have no workflow steps", ex.Message);
    }
}
