using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// CLAUDE.md Rule 3.2 / Principle 1: every pathogen runs through the same
// configuration-driven workflow. The number of confirmatory media comes only
// from the test's configured ConfirmatoryPlating step - never from the test
// code or organism name (the old fallback gave any code containing
// "SALMONELLA" two media).
//
// In-memory is acceptable here (Rule 4.3): what is proved is application
// validation and DTO projection, not relational behaviour. Arrange, Act and
// Assert use separate contexts over the same named store (Rule 4.1).
public class PathogenConfirmatoryMediaConfigurationTests
{
    private const int AnalystId = 5;
    private const int IncubatorId = 10;
    private static readonly DateTime Now = new(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc);
    private readonly string _store = Guid.NewGuid().ToString();

    private MicroLimsDbContext NewContext() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(_store).Options);

    private static PathogenSessionService Service(MicroLimsDbContext db) =>
        new(db, timeProvider: new FixedTimeProvider(new DateTimeOffset(Now)));

    // One sample with one pathogen test order and one location whose primary
    // observation shows growth, so the location is eligible for confirmation.
    // Returns (sampleId, testOrderId, locationId).
    private (int SampleId, int OrderId, int LocationId) Seed(
        string testCode, bool withConfirmatoryStep, int confirmatoryMediaCount, int idOffset = 0)
    {
        var sampleId = 100 + idOffset;
        var orderId = 50 + idOffset;
        var locationId = 200 + idOffset;

        using var db = NewContext();

        if (idOffset == 0)
        {
            var role = new Role { Id = 1, Name = "Analyst", Type = RoleType.Analyst };
            db.Roles.Add(role);
            db.Users.Add(new User { Id = AnalystId, FullName = "Sarah Analyst", Username = "sarah.a", PasswordHash = "hash", RoleId = 1, Role = role, IsActive = true });
            db.Organisms.Add(new Organism { Id = 1, ScientificName = "Target organism" });
            db.Materials.AddRange(
                new Material { Id = 101, MediaProductId = 101, MaterialName = "Confirmatory Medium A", MaterialType = MaterialType.DehydratedMedia },
                new Material { Id = 102, MediaProductId = 102, MaterialName = "Confirmatory Medium B", MaterialType = MaterialType.DehydratedMedia });
            db.Equipment.Add(new Equipment { Id = IncubatorId, Name = "INC-01", Code = "INC-01", Type = EquipmentType.Incubator, SetPointTemperature = 36 });
            db.CausesOfTesting.Add(new CauseOfTesting { Id = 1, Name = "Routine Monitoring" });
            db.Machines.Add(new Machine { Id = 1, Name = "Packaging Line A" });
        }

        var steps = new List<TestWorkflowStep>
        {
            new()
            {
                StepOrder = 1, StepName = "Selective Plating", StepType = StepType.SelectivePlating,
                IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, TargetOrganismId = 1,
                StepMedia = new List<TestWorkflowStepMedia>
                {
                    new() { MediaProductId = 101, IncubationMinHours = 18, IncubationMaxHours = 24, TempMin = 35, TempMax = 37, IsRequired = true, DisplayOrder = 1 }
                }
            }
        };
        if (withConfirmatoryStep)
        {
            steps.Add(new TestWorkflowStep
            {
                StepOrder = 2, StepName = "Confirmatory Plating", StepType = StepType.ConfirmatoryPlating,
                IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, TargetOrganismId = 1,
                ConfirmatoryMediaCount = confirmatoryMediaCount,
                StepMedia = new List<TestWorkflowStepMedia>
                {
                    new() { MediaProductId = 101, IncubationMinHours = 18, IncubationMaxHours = 24, TempMin = 35, TempMax = 37, DisplayOrder = 1 },
                    new() { MediaProductId = 102, IncubationMinHours = 18, IncubationMaxHours = 24, TempMin = 35, TempMax = 37, DisplayOrder = 2 }
                }
            });
        }
        db.TestDefinitions.Add(new TestDefinition
        {
            Id = 1 + idOffset, Code = testCode, DisplayName = testCode, WorkflowType = WorkflowType.Observation, Steps = steps
        });

        db.Samples.Add(new Sample
        {
            Id = sampleId, ReferenceNumber = $"EM-2026-{sampleId}", Category = SampleCategory.AfterCleaning,
            MachineId = 1, CauseOfTestingId = 1, ControlNumber = $"CTRL-{sampleId}",
            Status = SampleStatus.InTesting, ReceivedAt = Now.AddDays(-1), ReceivedByUserId = AnalystId
        });
        db.TestOrders.Add(new TestOrder
        {
            Id = orderId, SampleId = sampleId, TestCode = testCode, Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Running, AssignedAnalystId = AnalystId
        });

        var part = new MachinePart { Id = 1 + idOffset, MachineId = 1, Name = $"Sampling Point {sampleId}" };
        var partConfig = new MachinePartConfiguration { Id = 1 + idOffset, MachinePartId = part.Id, MachinePart = part, TestCode = "ALL" };
        db.MachineParts.Add(part);
        db.MachinePartConfigurations.Add(partConfig);
        db.SampleLocations.Add(new SampleLocation
        {
            Id = locationId, SampleId = sampleId, TestOrderId = orderId, LocationType = LocationType.MachinePart,
            MachinePartConfigurationId = partConfig.Id
        });
        db.LocationPathogenObservations.Add(new LocationPathogenObservation
        {
            SampleLocationId = locationId, TestOrderId = orderId, GrowthObservation = GrowthObservation.GrowthConforming,
            ObservedAt = Now.AddHours(-1), ObservedByUserId = AnalystId
        });

        db.SaveChanges();
        return (sampleId, orderId, locationId);
    }

    private static BatchConfirmatorySetupRequest SetupRequest(int orderId, int locationId, params int[] materialIds) =>
        new(TestOrderId: orderId, LocationIds: new List<int> { locationId }, MediaMaterialIds: materialIds.ToList(),
            MediaLotIds: null, IncubatorEquipmentId: IncubatorId, IncubationStartUtc: Now);

    // The test code must not change what is required: two tests configured
    // identically (no confirmatory step yet), one named after Salmonella,
    // report the same confirmatory media count everywhere it is projected.
    [Fact]
    public async Task RequiredConfirmatoryMedia_DoesNotDependOnTheTestCode()
    {
        var salmonella = Seed("PATHOGEN_SALMONELLA", withConfirmatoryStep: false, confirmatoryMediaCount: 0, idOffset: 0);
        var other = Seed("PATHOGEN_ECOLI", withConfirmatoryStep: false, confirmatoryMediaCount: 0, idOffset: 1);

        await using var actDb = NewContext();
        var service = Service(actDb);

        var salmonellaSession = await service.GetSessionAsync(salmonella.SampleId);
        var otherSession = await service.GetSessionAsync(other.SampleId);
        var salmonellaEligible = await service.GetEligibleLocationsForConfirmationAsync(salmonella.SampleId, salmonella.OrderId);
        var otherEligible = await service.GetEligibleLocationsForConfirmationAsync(other.SampleId, other.OrderId);

        Assert.Equal(
            otherSession!.AssignedTests.Single().ConfirmatoryMediaCount,
            salmonellaSession!.AssignedTests.Single().ConfirmatoryMediaCount);
        Assert.Equal(
            otherEligible.Single().RequiredConfirmatoryMediaCount,
            salmonellaEligible.Single().RequiredConfirmatoryMediaCount);
    }

    // A Salmonella-named test configured for ONE confirmatory medium needs
    // exactly one: configuration, not the organism, decides.
    [Fact]
    public async Task SalmonellaNamedTest_ConfiguredForOneMedium_RequiresOne()
    {
        var (sampleId, orderId, locationId) = Seed("PATHOGEN_SALMONELLA", withConfirmatoryStep: true, confirmatoryMediaCount: 1);

        await using (var actDb = NewContext())
        {
            var eligible = await Service(actDb).GetEligibleLocationsForConfirmationAsync(sampleId, orderId);
            Assert.Equal(1, eligible.Single().RequiredConfirmatoryMediaCount);

            var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
                Service(actDb).StartSharedConfirmatorySetupAsync(sampleId, SetupRequest(orderId, locationId, 101, 102), AnalystId));
            Assert.Equal("InvalidMediaCount", ex.ErrorCode);
        }

        await using var assertDb = NewContext();
        Assert.False(await assertDb.Incubations.AsNoTracking().AnyAsync(i => i.TestOrderId == orderId));
    }

    // A pathogen with no confirmatory step configured is rejected with an
    // explicit code instead of the count being guessed from its name.
    [Fact]
    public async Task ConfirmatorySetup_WithoutConfiguredConfirmatoryStep_IsRejectedAndRecordsNothing()
    {
        var (sampleId, orderId, locationId) = Seed("PATHOGEN_SALMONELLA", withConfirmatoryStep: false, confirmatoryMediaCount: 0);

        await using (var actDb = NewContext())
        {
            var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
                Service(actDb).StartSharedConfirmatorySetupAsync(sampleId, SetupRequest(orderId, locationId, 101, 102), AnalystId));
            Assert.Equal(WorkflowErrorCodes.ConfirmatoryStepNotConfigured, ex.ErrorCode);
        }

        await using var assertDb = NewContext();
        Assert.False(await assertDb.Incubations.AsNoTracking().AnyAsync(i => i.TestOrderId == orderId));
        var order = await assertDb.TestOrders.AsNoTracking().SingleAsync(t => t.Id == orderId);
        Assert.Equal(WorkflowStep.Running, order.CurrentStep);
    }

    // Any organism configured for two confirmatory media gets the same
    // treatment Salmonella used to get by name: two media are required, and
    // a correct request persists the incubation with its audit trail.
    [Fact]
    public async Task NonSalmonellaTest_ConfiguredForTwoMedia_RequiresTwoAndPersistsWithAudit()
    {
        var (sampleId, orderId, locationId) = Seed("PATHOGEN_ECOLI", withConfirmatoryStep: true, confirmatoryMediaCount: 2);

        await using (var rejectDb = NewContext())
        {
            var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
                Service(rejectDb).StartSharedConfirmatorySetupAsync(sampleId, SetupRequest(orderId, locationId, 101), AnalystId));
            Assert.Equal("InvalidMediaCount", ex.ErrorCode);
        }

        await using (var actDb = NewContext())
        {
            actDb.CurrentUserId = AnalystId;
            await Service(actDb).StartSharedConfirmatorySetupAsync(sampleId, SetupRequest(orderId, locationId, 101, 102), AnalystId);
        }

        await using var assertDb = NewContext();
        var incubation = await assertDb.Incubations.AsNoTracking().SingleAsync(i => i.TestOrderId == orderId);
        Assert.Equal("Confirmatory Plating", incubation.StepName);
        Assert.Equal(AnalystId, incubation.StartedByUserId);
        Assert.Equal(Now, incubation.IncubationStartUtc);

        var order = await assertDb.TestOrders.AsNoTracking().SingleAsync(t => t.Id == orderId);
        Assert.Equal(WorkflowStep.Incubating, order.CurrentStep);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(Incubation) && a.EntityId == incubation.Id.ToString());
        Assert.Equal("Create", audit.Action);
        Assert.Equal(AnalystId, audit.UserId);
        Assert.Null(audit.PreviousValue);
        Assert.Contains("Confirmatory Plating", audit.NewValue);

        // The seed's own Create row is also there; the act wrote one Update.
        var orderAudit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(TestOrder) && a.EntityId == orderId.ToString() && a.Action == "Update");
        Assert.Equal(AnalystId, orderAudit.UserId);
        // Enums are serialised as numbers in the audit payload.
        Assert.Contains($"\"CurrentStep\":{(int)WorkflowStep.Running}", orderAudit.PreviousValue);
        Assert.Contains($"\"CurrentStep\":{(int)WorkflowStep.Incubating}", orderAudit.NewValue);
    }
}
