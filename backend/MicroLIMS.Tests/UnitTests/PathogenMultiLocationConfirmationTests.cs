using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class PathogenMultiLocationConfirmationTests
{
    private static (MicroLimsDbContext db, int sampleId, int salmonellaOrderId, List<int> locationIds) SetupSalmonellaBatchEnvironment()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        // Seed Analyst User
        var role = new Role { Id = 1, Name = "Analyst", Type = RoleType.Analyst };
        db.Roles.Add(role);
        var user = new User { Id = 5, FullName = "Sarah Analyst", Username = "sarah.a", PasswordHash = "hash", RoleId = 1, Role = role, IsActive = true };
        db.Users.Add(user);

        // Seed Organism
        var organism = new Organism { Id = 1, ScientificName = "Salmonella enterica" };
        db.Organisms.Add(organism);

        // Seed MediaTypes
        var brothType = new MediaType { Id = 1, Class = MediaClass.GeneralBroth, IncubationMinHours = 18, IncubationMaxHours = 24, RequiredTemperatureMin = 30, RequiredTemperatureMax = 35 };
        var agarType = new MediaType { Id = 2, Class = MediaClass.SelectiveAgar, IncubationMinHours = 18, IncubationMaxHours = 24, RequiredTemperatureMin = 35, RequiredTemperatureMax = 37 };
        db.MediaTypes.AddRange(brothType, agarType);

        // Seed Materials
        var tsbMat = new Material { Id = 10, MaterialName = "Tryptic Soy Broth", MaterialType = MaterialType.DehydratedMedia };
        var xldMat = new Material { Id = 101, MaterialName = "XLD Agar", MaterialType = MaterialType.DehydratedMedia };
        var tsiMat = new Material { Id = 102, MaterialName = "TSI Agar", MaterialType = MaterialType.DehydratedMedia };
        db.Materials.AddRange(tsbMat, xldMat, tsiMat);

        var tsbMedia = new Media
        {
            Id = 20,
            MediaTypeId = 1,
            MediaType = brothType,
            MaterialId = 10,
            Material = tsbMat,
            LotNumber = "TSB-LOT-01",
            Status = MediaStatus.Prepared,
            IsReleasedForUse = true,
            ExpiryDate = DateTime.UtcNow.AddMonths(2),
            PreparedAt = DateTime.UtcNow.AddDays(-5)
        };
        db.Media.Add(tsbMedia);

        // Seed Incubator
        var inc = new EquipmentInventory { Id = 10, Code = "INC-01", InstrumentType = "Incubator", Status = EquipmentOperationalStatus.InService };
        db.EquipmentInventories.Add(inc);

        // Seed Test Definition for Salmonella with ConfirmatoryMediaCount = 2
        var testSalm = new TestDefinition
        {
            Id = 1,
            Code = "Salmonella",
            DisplayName = "Salmonella spp.",
            WorkflowType = WorkflowType.Observation,
            Steps = new List<TestWorkflowStep>
            {
                new() { StepOrder = 1, StepName = "TSB Enrichment", StepType = StepType.BrothEnrichment, MediaTypeId = 1, MediaType = brothType, IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 30, TemperatureMax = 35 },
                new() { StepOrder = 2, StepName = "Selective Plating (XLD)", StepType = StepType.SelectivePlating, MediaTypeId = 2, MediaType = agarType, IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, TargetOrganismId = 1 },
                new()
                {
                    StepOrder = 3,
                    StepName = "Confirmatory Plating",
                    StepType = StepType.ConfirmatoryPlating,
                    MediaTypeId = 2,
                    MediaType = agarType,
                    IncubationMinHours = 18,
                    IncubationMaxHours = 24,
                    TemperatureMin = 35,
                    TemperatureMax = 37,
                    TargetOrganismId = 1,
                    ConfirmatoryMediaCount = 2, // 2 confirmatory media required (e.g. XLD + TSI)
                    StepMedia = new List<TestWorkflowStepMedia>
                    {
                        new() { MaterialId = 101, TempMin = 35, TempMax = 37, DisplayOrder = 1 },
                        new() { MaterialId = 102, TempMin = 35, TempMax = 37, DisplayOrder = 2 }
                    }
                }
            }
        };
        db.TestDefinitions.Add(testSalm);

        // Seed CauseOfTesting & Machine
        var cause = new CauseOfTesting { Id = 1, Name = "Routine Monitoring" };
        db.CausesOfTesting.Add(cause);

        var machine = new Machine { Id = 1, Name = "Packaging Line A" };
        db.Machines.Add(machine);

        // Seed Sample with 3 locations
        var sample = new Sample
        {
            Id = 100,
            ReferenceNumber = "EM-2026-001",
            Category = SampleCategory.AfterCleaning,
            MachineId = 1,
            Machine = machine,
            CauseOfTestingId = 1,
            CauseOfTesting = cause,
            ControlNumber = "CTRL-EM-001",
            Status = SampleStatus.InTesting,
            ReceivedAt = DateTime.UtcNow,
            ReceivedByUserId = 5
        };
        db.Samples.Add(sample);

        var testOrder = new TestOrder
        {
            Id = 50,
            SampleId = 100,
            TestCode = "Salmonella",
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Running,
            AssignedAnalystId = 5
        };
        db.TestOrders.Add(testOrder);

        var locIds = new List<int>();
        for (int i = 1; i <= 3; i++)
        {
            var part = new MachinePart { Id = i, MachineId = 1, Name = $"Sampling Point {i}" };
            db.MachineParts.Add(part);

            var partConfig = new MachinePartConfiguration { Id = i, MachinePartId = i, MachinePart = part, TestCode = "ALL" };
            db.MachinePartConfigurations.Add(partConfig);

            var sloc = new SampleLocation
            {
                Id = 200 + i,
                SampleId = 100,
                TestOrderId = 50,
                TestOrder = testOrder,
                LocationType = LocationType.MachinePart,
                MachinePartConfigurationId = partConfig.Id,
                MachinePartConfiguration = partConfig
            };
            db.SampleLocations.Add(sloc);
            locIds.Add(sloc.Id);
        }

        // Seed completed TSB incubation and completed Step 2 so test is in AWAITING_RESULTS state
        var tsbInc = new Incubation
        {
            TestOrderId = 50,
            StepNumber = 1,
            StepName = "TSB Enrichment",
            MediaId = 20,
            IncubatorEquipmentId = 10,
            StartedAt = DateTime.UtcNow.AddHours(-25),
            IncubationStartUtc = DateTime.UtcNow.AddHours(-25),
            IncubationEndUtc = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow.AddHours(-1),
            Outcome = "Passed"
        };
        db.Incubations.Add(tsbInc);

        var step2Result = new WorkflowStepResult
        {
            TestOrderId = 50,
            StepName = "Selective Plating (XLD)",
            StepType = StepType.SelectivePlating,
            SelectivePlatingObservation = GrowthObservation.GrowthConforming,
            SubmittedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        db.WorkflowStepResults.Add(step2Result);

        db.SaveChanges();

        return (db, 100, 50, locIds);
    }

    [Fact]
    public async Task SavePrimaryObservations_NoGrowth_ResolvesDirectlyToNotDetected()
    {
        var (db, sampleId, orderId, locIds) = SetupSalmonellaBatchEnvironment();
        var service = new PathogenSessionService(db);

        var request = new SavePrimaryObservationsRequest(new List<PrimaryObservationInput>
        {
            new(locIds[0], "Salmonella", GrowthObservation.NoGrowth),
            new(locIds[1], "Salmonella", GrowthObservation.GrowthConforming),
            new(locIds[2], "Salmonella", GrowthObservation.GrowthNonConforming)
        });

        var session = await service.SavePrimaryObservationsAsync(sampleId, request, userId: 5);

        // Location 0 (NoGrowth) must be immediately resolved to Not Detected (-)
        var loc0 = await db.SampleLocations.FindAsync(locIds[0]);
        Assert.NotNull(loc0);
        Assert.Equal("Not Detected (-)", loc0.ReportedResult);
        Assert.Equal("Absent", loc0.Status);

        // Location 1 & 2 must be flagged as PendingConfirmation
        var loc1 = await db.SampleLocations.FindAsync(locIds[1]);
        Assert.NotNull(loc1);
        Assert.Equal("PendingConfirmation", loc1.Status);

        var loc2 = await db.SampleLocations.FindAsync(locIds[2]);
        Assert.NotNull(loc2);
        Assert.Equal("PendingConfirmation", loc2.Status);
    }

    [Fact]
    public async Task GetEligibleLocationsForConfirmation_ExcludesNoGrowthLocations()
    {
        var (db, sampleId, orderId, locIds) = SetupSalmonellaBatchEnvironment();
        var service = new PathogenSessionService(db);

        await service.SavePrimaryObservationsAsync(sampleId, new SavePrimaryObservationsRequest(new List<PrimaryObservationInput>
        {
            new(locIds[0], "Salmonella", GrowthObservation.NoGrowth),
            new(locIds[1], "Salmonella", GrowthObservation.GrowthConforming),
            new(locIds[2], "Salmonella", GrowthObservation.GrowthNonConforming)
        }), userId: 5);

        var eligible = await service.GetEligibleLocationsForConfirmationAsync(sampleId, orderId);

        // Only loc1 and loc2 are eligible; loc0 (NoGrowth) is strictly excluded
        Assert.Equal(2, eligible.Count);
        Assert.DoesNotContain(eligible, e => e.LocationId == locIds[0]);
        Assert.Contains(eligible, e => e.LocationId == locIds[1]);
        Assert.Contains(eligible, e => e.LocationId == locIds[2]);
        Assert.All(eligible, e => Assert.Equal(2, e.RequiredConfirmatoryMediaCount));
    }

    [Fact]
    public async Task StartSharedConfirmatorySetup_IneligibleLocation_ThrowsLocationNotEligible()
    {
        var (db, sampleId, orderId, locIds) = SetupSalmonellaBatchEnvironment();
        var service = new PathogenSessionService(db);

        // Only record NoGrowth for loc0
        await service.SavePrimaryObservationsAsync(sampleId, new SavePrimaryObservationsRequest(new List<PrimaryObservationInput>
        {
            new(locIds[0], "Salmonella", GrowthObservation.NoGrowth)
        }), userId: 5);

        var setupRequest = new BatchConfirmatorySetupRequest(
            TestOrderId: orderId,
            LocationIds: new List<int> { locIds[0] }, // loc0 is NOT eligible
            MediaMaterialIds: new List<int> { 101, 102 },
            MediaLotIds: null,
            IncubatorEquipmentId: 10,
            IncubationStartUtc: DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
            service.StartSharedConfirmatorySetupAsync(sampleId, setupRequest, userId: 5));

        Assert.Equal("LocationNotEligible", ex.ErrorCode);
    }

    [Fact]
    public async Task StartSharedConfirmatorySetup_IncorrectMediaCount_ThrowsInvalidMediaCount()
    {
        var (db, sampleId, orderId, locIds) = SetupSalmonellaBatchEnvironment();
        var service = new PathogenSessionService(db);

        await service.SavePrimaryObservationsAsync(sampleId, new SavePrimaryObservationsRequest(new List<PrimaryObservationInput>
        {
            new(locIds[1], "Salmonella", GrowthObservation.GrowthConforming)
        }), userId: 5);

        var setupRequest = new BatchConfirmatorySetupRequest(
            TestOrderId: orderId,
            LocationIds: new List<int> { locIds[1] },
            MediaMaterialIds: new List<int> { 101 }, // Salmonella requires 2 media, but only 1 provided!
            MediaLotIds: null,
            IncubatorEquipmentId: 10,
            IncubationStartUtc: DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
            service.StartSharedConfirmatorySetupAsync(sampleId, setupRequest, userId: 5));

        Assert.Equal("InvalidMediaCount", ex.ErrorCode);
    }

    [Fact]
    public async Task ConfirmatoryPlateReadings_MultiMediaAgreement_EvaluatesCorrectly()
    {
        var (db, sampleId, orderId, locIds) = SetupSalmonellaBatchEnvironment();
        var service = new PathogenSessionService(db);

        // Record primary observations
        await service.SavePrimaryObservationsAsync(sampleId, new SavePrimaryObservationsRequest(new List<PrimaryObservationInput>
        {
            new(locIds[0], "Salmonella", GrowthObservation.NoGrowth),
            new(locIds[1], "Salmonella", GrowthObservation.GrowthConforming),
            new(locIds[2], "Salmonella", GrowthObservation.GrowthNonConforming)
        }), userId: 5);

        var primaryObsLoc1 = await db.LocationPathogenObservations.FirstAsync(o => o.SampleLocationId == locIds[1]);
        var primaryObsLoc2 = await db.LocationPathogenObservations.FirstAsync(o => o.SampleLocationId == locIds[2]);

        // Start shared setup
        await service.StartSharedConfirmatorySetupAsync(sampleId, new BatchConfirmatorySetupRequest(
            TestOrderId: orderId,
            LocationIds: new List<int> { locIds[1], locIds[2] },
            MediaMaterialIds: new List<int> { 101, 102 },
            MediaLotIds: null,
            IncubatorEquipmentId: 10,
            IncubationStartUtc: DateTime.UtcNow), userId: 5);

        // Submit plate readings:
        // Location 1: Both media agree on Conforming -> Detected (+)
        // Location 2: Media 1 is Conforming, Media 2 is NonConforming -> Inconclusive
        var readingsRequest = new SaveBatchConfirmatoryPlateReadingsRequest(new List<BatchConfirmatoryPlateReadingInput>
        {
            // Location 1: XLD Conforming + TSI Conforming
            new(primaryObsLoc1.Id, MediumIndex: 0, MaterialId: 101, Observation: GrowthObservation.GrowthConforming),
            new(primaryObsLoc1.Id, MediumIndex: 1, MaterialId: 102, Observation: GrowthObservation.GrowthConforming),

            // Location 2: XLD Conforming + TSI NonConforming (Disagreement!)
            new(primaryObsLoc2.Id, MediumIndex: 0, MaterialId: 101, Observation: GrowthObservation.GrowthConforming),
            new(primaryObsLoc2.Id, MediumIndex: 1, MaterialId: 102, Observation: GrowthObservation.GrowthNonConforming)
        });

        var session = await service.SaveBatchConfirmatoryPlateReadingsAsync(sampleId, readingsRequest, userId: 5);

        // Location 1: Confirmed Positive
        var loc1 = await db.SampleLocations.FindAsync(locIds[1]);
        Assert.NotNull(loc1);
        Assert.Equal("Detected (+)", loc1.ReportedResult);
        Assert.Equal("Detected", loc1.Status);

        // Location 2: Disagreement -> Inconclusive
        var loc2 = await db.SampleLocations.FindAsync(locIds[2]);
        Assert.NotNull(loc2);
        Assert.Equal("Inconclusive (Retest)", loc2.ReportedResult);
        Assert.Equal("Inconclusive", loc2.Status);

        // Session Matrix reflects all details
        var matrixCell1 = session.ResultMatrix.First(c => c.SampleLocationId == locIds[1] && c.TestCode == "Salmonella");
        Assert.Equal("Detected (+)", matrixCell1.ResultDisplay);
        Assert.Equal("Completed", matrixCell1.ConfirmationStatus);
        Assert.Equal(2, matrixCell1.ConfirmatoryPlates?.Count);

        var matrixCell2 = session.ResultMatrix.First(c => c.SampleLocationId == locIds[2] && c.TestCode == "Salmonella");
        Assert.Equal("Inconclusive (Retest)", matrixCell2.ResultDisplay);
        Assert.Equal("Completed", matrixCell2.ConfirmationStatus);
    }
}
