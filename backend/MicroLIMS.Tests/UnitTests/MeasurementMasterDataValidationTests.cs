using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class MeasurementMasterDataValidationTests
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
    public async Task CreateTestDefinition_MeasurementEquation_RequiresMeasurementWorkflow()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var req = new CreateTestDefinitionRequest(
            Code: "MEAS-01",
            DisplayName: "Measurement Test",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Observation,
            EquationType: EquationType.Measurement,
            ReplicateCount: 3,
            EvaluationBasis: MeasurementEvaluationBasis.Mean);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req));
        Assert.Contains("Workflow type must be Measurement when equation type is Measurement.", ex.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_MeasurementEquation_RequiresReplicateCount1To30()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        // Missing ReplicateCount
        var req1 = new CreateTestDefinitionRequest(
            Code: "MEAS-01",
            DisplayName: "Measurement Test",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Measurement,
            EquationType: EquationType.Measurement,
            ReplicateCount: null,
            EvaluationBasis: MeasurementEvaluationBasis.Mean);

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req1));
        Assert.Contains("Replicate count must be between 1 and 30", ex1.Message);

        // ReplicateCount = 0
        var req2 = req1 with { ReplicateCount = 0 };
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req2));
        Assert.Contains("Replicate count must be between 1 and 30", ex2.Message);

        // ReplicateCount = 31
        var req3 = req1 with { ReplicateCount = 31 };
        var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req3));
        Assert.Contains("Replicate count must be between 1 and 30", ex3.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_MeasurementEquation_RequiresEvaluationBasis()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var req = new CreateTestDefinitionRequest(
            Code: "MEAS-01",
            DisplayName: "Measurement Test",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Measurement,
            EquationType: EquationType.Measurement,
            ReplicateCount: 3,
            EvaluationBasis: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req));
        Assert.Contains("Evaluation basis is required when equation type is Measurement.", ex.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_MeasurementWorkflow_RequiresMeasurementEquation()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var req = new CreateTestDefinitionRequest(
            Code: "MEAS-01",
            DisplayName: "Measurement Test",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Measurement,
            EquationType: EquationType.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req));
        Assert.Contains("Equation type must be Measurement when workflow type is Measurement.", ex.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_GravimetricPair_ValidatedBothDirections()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        // Equation GravimetricLoss requires Gravimetric workflow
        var req1 = new CreateTestDefinitionRequest(
            Code: "GRAV-01",
            DisplayName: "Gravimetric Test",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Observation,
            EquationType: EquationType.GravimetricLoss);
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req1));
        Assert.Contains("Workflow type must be Gravimetric", ex1.Message);

        // Workflow Gravimetric requires GravimetricLoss or GravimetricResidue
        var req2 = new CreateTestDefinitionRequest(
            Code: "GRAV-02",
            DisplayName: "Gravimetric Test",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Gravimetric,
            EquationType: EquationType.None);
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req2));
        Assert.Contains("Equation type must be GravimetricLoss or GravimetricResidue", ex2.Message);
    }

    [Fact]
    public async Task CreateTestDefinition_QualitativePair_ValidatedBothDirections()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        // Equation Qualitative requires Qualitative workflow
        var req1 = new CreateTestDefinitionRequest(
            Code: "QUAL-01",
            DisplayName: "Qualitative Test",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Observation,
            EquationType: EquationType.Qualitative);
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req1));
        Assert.Contains("Workflow type must be Qualitative when equation type is Qualitative.", ex1.Message);

        // Workflow Qualitative requires Qualitative equation
        var req2 = new CreateTestDefinitionRequest(
            Code: "QUAL-02",
            DisplayName: "Qualitative Test",
            SectionId: fpSec.Id,
            WorkflowType: WorkflowType.Qualitative,
            EquationType: EquationType.None);
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateTestDefinition(req2));
        Assert.Contains("Equation type must be Qualitative when workflow type is Qualitative.", ex2.Message);
    }

    [Fact]
    public async Task CreateTestWorkflowStep_RefusesStepsForMeasurementWorkflow()
    {
        using var db = NewDb();
        var (fpSec, _, controller) = SetupController(db);

        var testDef = new TestDefinition
        {
            Code = "PH-MEAS",
            DisplayName = "pH Measurement",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.Measurement,
            EquationType = EquationType.Measurement,
            ReplicateCount = 3,
            EvaluationBasis = MeasurementEvaluationBasis.Mean,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        var stepReq = new CreateTestWorkflowStepRequest(
            StepName: "Measurement Step",
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
        Assert.Contains("Measurement tests have no workflow steps.", ex.Message);
    }
}
