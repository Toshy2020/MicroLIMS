using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class PathogenSessionServiceTests
{
    private static (MicroLimsDbContext db, int sampleId, List<int> locationIds) SetupTestEnvironment(int locationCount = 20)
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        // Seed Role & Users
        var role = new Role { Id = 1, Name = "Analyst", Type = RoleType.Analyst };
        db.Roles.Add(role);
        var user = new User
        {
            Id = 5,
            FullName = "Mazen Asharaf",
            Username = "mazen.asharaf",
            PasswordHash = "hash",
            RoleId = 1,
            Role = role,
            IsActive = true
        };
        db.Users.Add(user);

        // Seed Incubators - Laboratory Configuration equipment, whose set point
        // is checked against each test's own TSB medium range (30-35 °C).
        var inc3 = new Equipment { Id = 3, Name = "INC-03", Code = "INC-03", Type = EquipmentType.Incubator, SetPointTemperature = 32.5m };
        var inc4 = new Equipment { Id = 4, Name = "INC-04", Code = "INC-04", Type = EquipmentType.Incubator, SetPointTemperature = 32.5m };
        db.Equipment.AddRange(inc3, inc4);

        // Seed Material & Released TSB Media Lot
        var tsbMat = new Material { Id = 10, MaterialName = "Tryptic Soy Broth Powder", MaterialType = MaterialType.DehydratedMedia, BatchNumber = "TSB-MAT-01" };
        db.Materials.Add(tsbMat);

        var tsbMedia = new Media
        {
            Id = 20,
            MaterialId = 10,
            Material = tsbMat,
            LotNumber = "TSB-LOT-25113",
            Status = MediaStatus.Prepared,
            IsReleasedForUse = true,
            ExpiryDate = DateTime.UtcNow.AddMonths(2),
            PreparedAt = DateTime.UtcNow.AddDays(-5)
        };
        db.Media.Add(tsbMedia);

        var bcaMat = new Material { Id = 11, MaterialName = "BCA Selective Medium Powder", MaterialType = MaterialType.DehydratedMedia, BatchNumber = "BCA-MAT-01" };
        db.Materials.Add(bcaMat);

        var bcaMedia = new Media
        {
            Id = 21,
            MaterialId = 11,
            Material = bcaMat,
            LotNumber = "BCA-LOT-25114",
            Status = MediaStatus.Prepared,
            IsReleasedForUse = true,
            ExpiryDate = DateTime.UtcNow.AddMonths(2),
            PreparedAt = DateTime.UtcNow.AddDays(-5)
        };
        db.Media.Add(bcaMedia);

        var rvsMat = new Material { Id = 12, MaterialName = "RVS Selective Broth Powder", MaterialType = MaterialType.DehydratedMedia, BatchNumber = "RVS-MAT-01" };
        db.Materials.Add(rvsMat);

        var rvsMedia = new Media
        {
            Id = 22,
            MaterialId = 12,
            Material = rvsMat,
            LotNumber = "RVS-LOT-25115",
            Status = MediaStatus.Prepared,
            IsReleasedForUse = true,
            ExpiryDate = DateTime.UtcNow.AddMonths(2),
            PreparedAt = DateTime.UtcNow.AddDays(-5)
        };
        db.Media.Add(rvsMedia);

        var plateMat = new Material { Id = 13, MaterialName = "Plate Incubation Agar Powder", MaterialType = MaterialType.DehydratedMedia, BatchNumber = "PLATE-MAT-01" };
        db.Materials.Add(plateMat);

        var plateMedia = new Media
        {
            Id = 23,
            MaterialId = 13,
            Material = plateMat,
            LotNumber = "PLATE-LOT-25116",
            Status = MediaStatus.Prepared,
            IsReleasedForUse = true,
            ExpiryDate = DateTime.UtcNow.AddMonths(2),
            PreparedAt = DateTime.UtcNow.AddDays(-5)
        };
        db.Media.Add(plateMedia);

        var eval = new MediaEvaluation
        {
            Id = 30,
            MediaId = 20,
            Outcome = EvaluationOutcome.Conform,
            Status = MediaEvaluationStatus.Completed
        };
        db.MediaEvaluations.Add(eval);

        // Seed Test Definitions from Test Master: 6 Assigned Tests
        // 5 pathogen tests with TSB first step, 1 TAMC without TSB
        var testBcc = new TestDefinition
        {
            Id = 1,
            Code = "BCC",
            DisplayName = "Burkholderia cepacia complex",
            WorkflowType = WorkflowType.Observation,
            Steps = new List<TestWorkflowStep>
            {
                new()
                {
                    StepOrder = 1,
                    StepName = "TSB Enrichment",
                    StepType = StepType.BrothEnrichment,
                    IncubationMinHours = 18,
                    IncubationMaxHours = 24,
                    TemperatureMin = 30,
                    TemperatureMax = 35,
                    StepMedia = new List<TestWorkflowStepMedia>
                    {
                        new() { MaterialId = 10, IncubationMinHours = 18, IncubationMaxHours = 24, TempMin = 30, TempMax = 35, IsRequired = true, DisplayOrder = 1 }
                    }
                },
                new()
                {
                    StepOrder = 2,
                    StepName = "BCA Selective Medium",
                    StepType = StepType.SelectivePlating,
                    IncubationMinHours = 24,
                    IncubationMaxHours = 48,
                    TemperatureMin = 35,
                    TemperatureMax = 37,
                    StepMedia = new List<TestWorkflowStepMedia>
                    {
                        new() { MaterialId = 11, IncubationMinHours = 24, IncubationMaxHours = 48, TempMin = 35, TempMax = 37, IsRequired = true, DisplayOrder = 1 }
                    }
                }
            }
        };

        var testSalm = new TestDefinition
        {
            Id = 2,
            Code = "Salmonella",
            DisplayName = "Salmonella spp.",
            WorkflowType = WorkflowType.Observation,
            Steps = new List<TestWorkflowStep>
            {
                new()
                {
                    StepOrder = 1,
                    StepName = "TSB Pre-enrichment",
                    StepType = StepType.BrothEnrichment,
                    IncubationMinHours = 18,
                    IncubationMaxHours = 24,
                    TemperatureMin = 30,
                    TemperatureMax = 35,
                    StepMedia = new List<TestWorkflowStepMedia>
                    {
                        new() { MaterialId = 10, IncubationMinHours = 18, IncubationMaxHours = 24, TempMin = 30, TempMax = 35, IsRequired = true, DisplayOrder = 1 }
                    }
                },
                new()
                {
                    StepOrder = 2,
                    StepName = "RVS Selective Broth",
                    StepType = StepType.SelectiveBroth,
                    IncubationMinHours = 24,
                    IncubationMaxHours = 24,
                    TemperatureMin = 41.5m,
                    TemperatureMax = 42.5m,
                    StepMedia = new List<TestWorkflowStepMedia>
                    {
                        new() { MaterialId = 12, IncubationMinHours = 24, IncubationMaxHours = 24, TempMin = 41.5m, TempMax = 42.5m, IsRequired = true, DisplayOrder = 1 }
                    }
                }
            }
        };

        var testTamc = new TestDefinition
        {
            Id = 3,
            Code = "TAMC-Water",
            DisplayName = "Total Aerobic Microbial Count - Water",
            WorkflowType = WorkflowType.CountTest,
            Steps = new List<TestWorkflowStep>
            {
                new()
                {
                    StepOrder = 1,
                    StepName = "Plate Incubation",
                    StepType = StepType.SelectivePlating,
                    IncubationMinHours = 48,
                    IncubationMaxHours = 72,
                    TemperatureMin = 30,
                    TemperatureMax = 35,
                    StepMedia = new List<TestWorkflowStepMedia>
                    {
                        new() { MaterialId = 13, IncubationMinHours = 48, IncubationMaxHours = 72, TempMin = 30, TempMax = 35, IsRequired = true, DisplayOrder = 1 }
                    }
                }
            }
        };

        var testSa = new TestDefinition
        {
            Id = 4,
            Code = "S. aureus",
            DisplayName = "Staphylococcus aureus",
            WorkflowType = WorkflowType.Observation,
            Steps = new List<TestWorkflowStep>
            {
                new()
                {
                    StepOrder = 1,
                    StepName = "TSB Enrichment",
                    StepType = StepType.BrothEnrichment,
                    IncubationMinHours = 18,
                    IncubationMaxHours = 24,
                    TemperatureMin = 30,
                    TemperatureMax = 35,
                    StepMedia = new List<TestWorkflowStepMedia>
                    {
                        new() { MaterialId = 10, IncubationMinHours = 18, IncubationMaxHours = 24, TempMin = 30, TempMax = 35, IsRequired = true, DisplayOrder = 1 }
                    }
                }
            }
        };

        var testPa = new TestDefinition
        {
            Id = 5,
            Code = "P. aeruginosa",
            DisplayName = "Pseudomonas aeruginosa",
            WorkflowType = WorkflowType.Observation,
            Steps = new List<TestWorkflowStep>
            {
                new()
                {
                    StepOrder = 1,
                    StepName = "TSB Enrichment",
                    StepType = StepType.BrothEnrichment,
                    IncubationMinHours = 18,
                    IncubationMaxHours = 24,
                    TemperatureMin = 30,
                    TemperatureMax = 35,
                    StepMedia = new List<TestWorkflowStepMedia>
                    {
                        new() { MaterialId = 10, IncubationMinHours = 18, IncubationMaxHours = 24, TempMin = 30, TempMax = 35, IsRequired = true, DisplayOrder = 1 }
                    }
                }
            }
        };

        var testEc = new TestDefinition
        {
            Id = 6,
            Code = "E. coli",
            DisplayName = "Escherichia coli",
            WorkflowType = WorkflowType.Observation,
            Steps = new List<TestWorkflowStep>
            {
                new()
                {
                    StepOrder = 1,
                    StepName = "TSB Enrichment",
                    StepType = StepType.BrothEnrichment,
                    IncubationMinHours = 18,
                    IncubationMaxHours = 24,
                    TemperatureMin = 30,
                    TemperatureMax = 35,
                    StepMedia = new List<TestWorkflowStepMedia>
                    {
                        new() { MaterialId = 10, IncubationMinHours = 18, IncubationMaxHours = 24, TempMin = 30, TempMax = 35, IsRequired = true, DisplayOrder = 1 }
                    }
                }
            }
        };

        db.TestDefinitions.AddRange(testBcc, testSalm, testTamc, testSa, testPa, testEc);

        // Seed CauseOfTesting
        var cause = new CauseOfTesting { Id = 1, Name = "Routine Monitoring" };
        db.CausesOfTesting.Add(cause);

        // Seed Sample
        var machine = new Machine { Id = 1, Name = "OSD II Line" };
        db.Machines.Add(machine);

        var sample = new Sample
        {
            Id = 100,
            ReferenceNumber = "AC-2026-0817-04",
            Category = SampleCategory.AfterCleaning,
            MachineId = 1,
            Machine = machine,
            CauseOfTestingId = 1,
            CauseOfTesting = cause,
            ControlNumber = "CTRL-AC-04",
            Status = SampleStatus.InTesting,
            ReceivedAt = DateTime.UtcNow.AddDays(-1),
            ReceivedByUserId = 5
        };
        db.Samples.Add(sample);

        // Seed 6 TestOrders for the sample
        var toCodes = new[] { "BCC", "Salmonella", "TAMC-Water", "S. aureus", "P. aeruginosa", "E. coli" };
        var testOrders = toCodes.Select((code, idx) => new TestOrder
        {
            Id = 200 + idx,
            SampleId = 100,
            TestCode = code,
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting,
            AssignedAnalystId = 5
        }).ToList();
        db.TestOrders.AddRange(testOrders);

        // Seed Machine Parts / Locations
        var locIds = new List<int>();
        for (int i = 1; i <= locationCount; i++)
        {
            var part = new MachinePart { Id = i, MachineId = 1, Name = $"OSD II - Location {i:D2}" };
            db.MachineParts.Add(part);

            var partConfig = new MachinePartConfiguration { Id = i, MachinePartId = i, MachinePart = part, TestCode = "ALL" };
            db.MachinePartConfigurations.Add(partConfig);

            // Add sample location row per test order for each physical location
            foreach (var to in testOrders)
            {
                var sloc = new SampleLocation
                {
                    SampleId = 100,
                    TestOrderId = to.Id,
                    TestOrder = to,
                    LocationType = LocationType.MachinePart,
                    MachinePartConfigurationId = partConfig.Id,
                    MachinePartConfiguration = partConfig
                };
                db.SampleLocations.Add(sloc);
            }
            locIds.Add(i);
        }

        db.SaveChanges();
        return (db, 100, locIds);
    }

    // Regression test for a real production bug: StartSharedTsbAsync's
    // step-resolution predicate used to match StepType.SelectiveBroth as
    // well as BrothEnrichment. Salmonella's own template (seeded above) has
    // both a BrothEnrichment step ("TSB Pre-enrichment") and a distinct
    // SelectiveBroth step ("RVS Selective Broth") - exactly the shape that
    // let the shared TSB lot land on the wrong step (StepName "RVS Selective
    // Broth" instead of "TSB Pre-enrichment"), corrupting real live test
    // orders. The fix restricts the match to BrothEnrichment only.
    [Fact]
    public async Task StartSharedTsbAsync_TestWithBothBrothTypes_AttachesToBrothEnrichmentNotSelectiveBroth()
    {
        var (db, sampleId, _) = SetupTestEnvironment(1);
        var service = new PathogenSessionService(db);

        await service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(
            MediaLotId: 20,
            IncubatorEquipmentId: 3,
            IncubationStartUtc: DateTime.UtcNow
        ), userId: 5);

        var salmonellaOrderId = 201; // toCodes[1] == "Salmonella", Id = 200 + 1
        var salmonellaIncubations = await db.Incubations.Where(i => i.TestOrderId == salmonellaOrderId).ToListAsync();

        Assert.Single(salmonellaIncubations);
        Assert.Equal("TSB Pre-enrichment", salmonellaIncubations[0].StepName);
        Assert.Equal(20, salmonellaIncubations[0].MediaId);
        Assert.DoesNotContain(salmonellaIncubations, i => i.StepName == "RVS Selective Broth");
    }

    [Fact]
    public async Task StartSharedTsbAsync_WhenPreparationNotConfirmed_ThrowsAndLogsRefusal()
    {
        var (db, sampleId, _) = SetupTestEnvironment(1);
        var sample = await db.Samples.FirstAsync(s => s.Id == sampleId);
        sample.PreparationStatus = SamplePreparationStatus.NeedsPreparation;
        await db.SaveChangesAsync();

        var service = new PathogenSessionService(db);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
            service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(
                MediaLotId: 20,
                IncubatorEquipmentId: 3,
                IncubationStartUtc: DateTime.UtcNow
            ), userId: 5));

        Assert.Equal(MicroLIMS.Shared.Constants.WorkflowErrorCodes.PreparationNotConfirmed, ex.ErrorCode);
        Assert.Contains("Test Preparation must be completed and confirmed", ex.Message);

        var auditLogs = await db.AuditLogs.Where(a => a.Action == "TestStartRefused").ToListAsync();
        Assert.NotEmpty(auditLogs);
        Assert.All(auditLogs, a => Assert.Equal(5, a.UserId));

        var histories = await db.WorkflowHistories.Where(h => h.Note != null && h.Note.Contains("Transition refused")).ToListAsync();
        Assert.NotEmpty(histories);
    }

    private static async Task<TestWorkflowStepMedia> TsbMediumAsync(MicroLimsDbContext db, string testCode)
    {
        var def = await db.TestDefinitions
            .Include(d => d.Steps).ThenInclude(s => s.StepMedia)
            .FirstAsync(d => d.Code == testCode);
        return def.Steps.OrderBy(s => s.StepOrder).First().StepMedia.First();
    }

    // Each test is timed by its own TSB medium from Test Master; the shared
    // start only triggers it. The step-level 18-24 h must not leak in either.
    [Fact]
    public async Task StartSharedTsbAsync_EachTestGetsItsOwnMediumWindow()
    {
        var (db, sampleId, _) = SetupTestEnvironment(1);
        var ecoliMedium = await TsbMediumAsync(db, "E. coli");
        ecoliMedium.IncubationMinHours = 20;
        ecoliMedium.IncubationMaxHours = 48;
        await db.SaveChangesAsync();

        var service = new PathogenSessionService(db);
        var start = DateTime.UtcNow;
        var tsb = await service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 3, start), 5);

        var ecoli = await db.Incubations.SingleAsync(i => i.TestOrderId == 205);
        Assert.Equal("20-48 hours", ecoli.Duration);
        Assert.Equal(start.AddHours(48), ecoli.IncubationEndUtc);

        var salmonella = await db.Incubations.SingleAsync(i => i.TestOrderId == 201);
        Assert.Equal("18-24 hours", salmonella.Duration);
        Assert.Equal(start.AddHours(24), salmonella.IncubationEndUtc);

        Assert.Contains("E. coli: 20 – 48 h", tsb.RequiredDurationRange);
        Assert.Contains("BCC: 18 – 24 h", tsb.RequiredDurationRange);
        Assert.Equal(start.AddHours(20), tsb.MinReadyAt);
        Assert.False(tsb.WindowNotConfigured);
    }

    [Fact]
    public async Task StartSharedTsbAsync_TestWithDifferentTsbMedium_DoesNotJoin()
    {
        var (db, sampleId, _) = SetupTestEnvironment(1);
        db.Materials.Add(new Material { Id = 14, MaterialName = "Buffered Peptone Water", MaterialType = MaterialType.DehydratedMedia, BatchNumber = "BPW-MAT-01" });
        (await TsbMediumAsync(db, "S. aureus")).MaterialId = 14;
        await db.SaveChangesAsync();

        var service = new PathogenSessionService(db);
        var tsb = await service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 3, DateTime.UtcNow), 5);

        Assert.False(await db.Incubations.AnyAsync(i => i.TestOrderId == 203));
        Assert.False(await db.WorkflowStepResults.AnyAsync(r => r.TestOrderId == 203));
        Assert.True(await db.Incubations.AnyAsync(i => i.TestOrderId == 200));
        Assert.DoesNotContain("S. aureus", tsb.ApplicableTestCodes);

        var session = await service.GetSessionAsync(sampleId);
        Assert.Equal("PENDING", session!.AssignedTests.First(t => t.TestCode == "S. aureus").TestSessionState);
    }

    [Fact]
    public async Task StartSharedTsbAsync_JoiningTestWindowNotConfigured_RefusesWholeStart()
    {
        var (db, sampleId, _) = SetupTestEnvironment(1);
        (await TsbMediumAsync(db, "P. aeruginosa")).IncubationMinHours = 0;
        await db.SaveChangesAsync();

        var service = new PathogenSessionService(db);
        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
            service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 3, DateTime.UtcNow), 5));

        Assert.Equal(MicroLIMS.Shared.Constants.WorkflowErrorCodes.IncubationWindowNotConfigured, ex.ErrorCode);
        Assert.Contains("P. aeruginosa", ex.Message);
        Assert.False(await db.Incubations.AnyAsync());
    }

    [Fact]
    public async Task StartSharedTsbAsync_IncubatorOutsideAJoiningTestRange_RefusesWholeStart()
    {
        var (db, sampleId, _) = SetupTestEnvironment(1);
        (await TsbMediumAsync(db, "BCC")).TempMin = 33; // INC-03 is set to 32.5 °C
        await db.SaveChangesAsync();

        var service = new PathogenSessionService(db);
        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
            service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 3, DateTime.UtcNow), 5));

        Assert.Equal(MicroLIMS.Shared.Constants.WorkflowErrorCodes.IncubatorTempOutOfRange, ex.ErrorCode);
        Assert.Contains("BCC", ex.Message);
        Assert.False(await db.Incubations.AnyAsync());
    }

    // The session panel sends Laboratory Configuration equipment ids; an id
    // that exists only in the asset register is not an incubator.
    [Fact]
    public async Task StartSharedTsbAsync_IncubatorOnlyInAssetRegister_NotFound()
    {
        var (db, sampleId, _) = SetupTestEnvironment(1);
        db.EquipmentInventories.Add(new EquipmentInventory { Id = 50, Code = "INC-50", InstrumentType = "Incubator", Status = EquipmentOperationalStatus.InService });
        await db.SaveChangesAsync();

        var service = new PathogenSessionService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 50, DateTime.UtcNow), 5));
        Assert.False(await db.Incubations.AnyAsync());
    }

    [Fact]
    public async Task PropagateSharedTsb_CompletionNotCopiedBeforeSiblingsOwnMinimum()
    {
        var (db, sampleId, _) = SetupTestEnvironment(1);
        var ecoliMedium = await TsbMediumAsync(db, "E. coli");
        ecoliMedium.IncubationMinHours = 30;
        ecoliMedium.IncubationMaxHours = 48;
        await db.SaveChangesAsync();

        var start = DateTime.UtcNow.AddHours(-20);
        var inc = new Incubation
        {
            TestOrderId = 200, StepNumber = 1, StepName = "TSB Enrichment", MediaId = 20, IncubatorEquipmentId = 3,
            StartedAt = start, IncubationStartUtc = start, IncubationEndUtc = start.AddHours(24),
            CompletedAt = DateTime.UtcNow, CompletedByUserId = 5, Outcome = "Turbid"
        };
        db.Incubations.Add(inc);
        await db.SaveChangesAsync();

        await TestServiceFactory.TestWorkflow(db).PropagateSharedTsbToSiblingOrdersAsync(200, inc.Id, 5);

        var ecoli = await db.Incubations.SingleAsync(i => i.TestOrderId == 205);
        Assert.Null(ecoli.CompletedAt);
        Assert.Equal("30-48 hours", ecoli.Duration);
        Assert.Equal(start.AddHours(48), ecoli.IncubationEndUtc);

        var salmonella = await db.Incubations.SingleAsync(i => i.TestOrderId == 201);
        Assert.Equal(inc.CompletedAt, salmonella.CompletedAt);
    }

    [Fact]
    public async Task PropagateSharedTsb_SiblingWithDifferentTsbMedium_NotLinked()
    {
        var (db, sampleId, _) = SetupTestEnvironment(1);
        db.Materials.Add(new Material { Id = 14, MaterialName = "Buffered Peptone Water", MaterialType = MaterialType.DehydratedMedia, BatchNumber = "BPW-MAT-01" });
        (await TsbMediumAsync(db, "S. aureus")).MaterialId = 14;
        var start = DateTime.UtcNow.AddHours(-2);
        var inc = new Incubation
        {
            TestOrderId = 200, StepNumber = 1, StepName = "TSB Enrichment", MediaId = 20, IncubatorEquipmentId = 3,
            StartedAt = start, IncubationStartUtc = start, IncubationEndUtc = start.AddHours(24)
        };
        db.Incubations.Add(inc);
        await db.SaveChangesAsync();

        await TestServiceFactory.TestWorkflow(db).PropagateSharedTsbToSiblingOrdersAsync(200, inc.Id, 5);

        Assert.False(await db.Incubations.AnyAsync(i => i.TestOrderId == 203));
        Assert.False(await db.WorkflowStepResults.AnyAsync(r => r.TestOrderId == 203));
        Assert.True(await db.Incubations.AnyAsync(i => i.TestOrderId == 201));
    }

    // Validated before the source's own incubation is saved, so a refused
    // shared start never leaves one test started alone.
    [Fact]
    public async Task SelectMediaAsync_SharedTsbSiblingNotConfigured_BlocksBeforeSavingSource()
    {
        var (db, _, _) = SetupTestEnvironment(1);
        (await TsbMediumAsync(db, "E. coli")).IncubationMaxHours = 0;
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);
        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
            engine.SelectMediaAsync(200, "TSB Enrichment", 20, 3, 5));

        Assert.Equal(MicroLIMS.Shared.Constants.WorkflowErrorCodes.IncubationWindowNotConfigured, ex.ErrorCode);
        Assert.Contains("E. coli", ex.Message);
        Assert.False(await db.Incubations.AnyAsync());
    }

    [Fact]
    public async Task Scenario_3Locations_6AssignedTests_TsbIncubation_Gating_And_Counters()
    {
        var (db, sampleId, _) = SetupTestEnvironment(3);
        var service = new PathogenSessionService(db);

        // 1. Start Shared TSB
        var tsbResult = await service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(
            MediaLotId: 20,
            IncubatorEquipmentId: 3,
            IncubationStartUtc: DateTime.UtcNow
        ), userId: 5);

        Assert.True(tsbResult.IsStarted);
        Assert.True(tsbResult.IsIncubating);
        Assert.True(tsbResult.IsLocked);

        // 2. Fetch Session
        var session = await service.GetSessionAsync(sampleId);
        Assert.NotNull(session);
        Assert.Equal("TSB_INCUBATING", session.OverallSessionStatus);
        Assert.Equal(3, session.TotalLocations);
        Assert.Equal(6, session.TotalAssignedTests);
        Assert.Equal(18, session.RequiredResultCount); // 3 locations * 6 tests

        // Check assigned tests
        var bcc = session.AssignedTests.First(t => t.TestCode == "BCC");
        Assert.True(bcc.RequiresTsb);
        Assert.Equal("TSB_INCUBATING", bcc.TestSessionState);
        Assert.Equal("TSB Incubating", bcc.TestSessionStateDisplay);
        Assert.False(bcc.IsResultEntryAllowed);
        Assert.True(bcc.IsWorkflowLocked);

        var tamc = session.AssignedTests.First(t => t.TestCode == "TAMC-Water");
        Assert.False(tamc.RequiresTsb);
        Assert.Equal("PENDING", tamc.TestSessionState);
        Assert.Equal("Pending", tamc.TestSessionStateDisplay);
        Assert.True(tamc.IsResultEntryAllowed);
        Assert.False(tamc.IsWorkflowLocked);

        // Check accurate tri-state matrix counters
        Assert.Equal(0, session.CompletedResultCount);
        Assert.Equal(3, session.AvailableResultCount); // 3 TAMC-Water cells are independent and available
        Assert.Equal(15, session.LockedResultCount);   // 5 pathogen tests * 3 locations = 15 locked cells
        Assert.Equal(3, session.PendingResultCount);  // Pending matches available empty cells (3)

        // Check individual cell states
        var bccCell = session.ResultMatrix.First(c => c.TestCode == "BCC");
        Assert.Equal("LOCKED_PREREQUISITE", bccCell.CellState);
        Assert.False(bccCell.IsEditable);

        var tamcCell = session.ResultMatrix.First(c => c.TestCode == "TAMC-Water");
        Assert.Equal("AVAILABLE", tamcCell.CellState);
        Assert.True(tamcCell.IsEditable);

        // Also verify TestingWorkspaceService.ToDto generates the exact same state for test cards
        var sampleDto = await new TestingWorkspaceService(db, new UserSectionScopeService(db)).GetSampleAsync(sampleId);
        Assert.NotNull(sampleDto);
        var bccCard = sampleDto.AssignedTests.First(t => t.TestCode == "BCC");
        Assert.Equal("TSB_INCUBATING", bccCard.WorkflowState);
        Assert.Equal("TSB Incubating", bccCard.WorkflowStateDisplay);
        Assert.True(bccCard.IsWorkflowLocked);

        var tamcCard = sampleDto.AssignedTests.First(t => t.TestCode == "TAMC-Water");
        Assert.Equal("PENDING", tamcCard.WorkflowState);
        Assert.Equal("Pending", tamcCard.WorkflowStateDisplay);
        Assert.False(tamcCard.IsWorkflowLocked);
    }

    [Fact]
    public async Task Scenario_PartialWorkflowCompletion_BccAndEcoliComplete_SalmonellaLocked()
    {
        var (db, sampleId, _) = SetupTestEnvironment(3);
        var service = new PathogenSessionService(db);

        // 1. Complete TSB incubation (25h ago)
        await service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 3, DateTime.UtcNow.AddHours(-25)), 5);

        // E. coli has only 1 step (TSB) -> fully complete once TSB finishes
        // Complete Step 2 for BCC (BCA Selective Medium)
        var bccOrder = await db.TestOrders.FirstAsync(t => t.TestCode == "BCC");
        db.WorkflowStepResults.Add(new WorkflowStepResult
        {
            TestOrderId = bccOrder.Id,
            StepName = "BCA Selective Medium",
            SubmittedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Salmonella step 2 (RVS Selective Broth) is left incomplete
        var session = await service.GetSessionAsync(sampleId);
        Assert.NotNull(session);

        var bcc = session.AssignedTests.First(t => t.TestCode == "BCC");
        Assert.True(bcc.IsResultEntryAllowed);
        Assert.Equal("AWAITING_RESULTS", bcc.TestSessionState);

        var ec = session.AssignedTests.First(t => t.TestCode == "E. coli");
        Assert.True(ec.IsResultEntryAllowed);
        Assert.Equal("AWAITING_RESULTS", ec.TestSessionState);

        var salm = session.AssignedTests.First(t => t.TestCode == "Salmonella");
        Assert.False(salm.IsResultEntryAllowed);
        Assert.Equal("READY_FOR_DOWNSTREAM", salm.TestSessionState);

        // Result Matrix check
        var bccCells = session.ResultMatrix.Where(c => c.TestCode == "BCC").ToList();
        Assert.All(bccCells, c => Assert.Equal("AVAILABLE", c.CellState));

        var ecCells = session.ResultMatrix.Where(c => c.TestCode == "E. coli").ToList();
        Assert.All(ecCells, c => Assert.Equal("AVAILABLE", c.CellState));

        var salmCells = session.ResultMatrix.Where(c => c.TestCode == "Salmonella").ToList();
        Assert.All(salmCells, c => Assert.Equal("LOCKED_PREREQUISITE", c.CellState));
    }

    [Fact]
    public async Task Scenario_20Locations_6AssignedTests_FullLifecycle()
    {
        var (db, sampleId, _) = SetupTestEnvironment(20);
        var service = new PathogenSessionService(db);

        // 1. Initial State
        var session = await service.GetSessionAsync(sampleId);
        Assert.NotNull(session);
        Assert.Equal(20, session.TotalLocations);
        Assert.Equal(6, session.TotalAssignedTests);
        Assert.Equal(120, session.RequiredResultCount);

        // 2. Start TSB (complete timestamp)
        await service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 3, DateTime.UtcNow.AddHours(-25)), 5);

        // Complete Step 2 for BCC and Salmonella so all 6 tests are ready for results
        var bccOrder = await db.TestOrders.FirstAsync(t => t.TestCode == "BCC");
        var salmOrder = await db.TestOrders.FirstAsync(t => t.TestCode == "Salmonella");
        db.WorkflowStepResults.AddRange(
            new WorkflowStepResult { TestOrderId = bccOrder.Id, StepName = "BCA Selective Medium", SubmittedAtUtc = DateTime.UtcNow },
            new WorkflowStepResult { TestOrderId = salmOrder.Id, StepName = "RVS Selective Broth", SubmittedAtUtc = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var sessionAfterSteps = await service.GetSessionAsync(sampleId);
        Assert.NotNull(sessionAfterSteps);
        Assert.True(sessionAfterSteps.SharedTsb.IsCompleted);

        // 3. Enter all 120 result cells
        var cells = new List<MatrixCellInput>();
        foreach (var cell in sessionAfterSteps.ResultMatrix)
        {
            if (cell.ResultType == "Quantitative")
            {
                cells.Add(new MatrixCellInput(cell.SampleLocationId, cell.TestCode, "5", "5 CFU", 5, "Quantitative"));
            }
            else
            {
                cells.Add(new MatrixCellInput(cell.SampleLocationId, cell.TestCode, "NOT_DETECTED", "Not Detected (-)", null, "Qualitative"));
            }
        }

        var savedSession = await service.SaveResultMatrixAsync(sampleId, new SaveResultMatrixRequest(cells), 5);
        Assert.Equal(120, savedSession.CompletedResultCount);
        Assert.Equal(0, savedSession.AvailableResultCount);
        Assert.Equal(0, savedSession.LockedResultCount);
        Assert.Equal(0, savedSession.PendingResultCount);

        // 4. Complete Session
        var completedSession = await service.CompleteSessionAsync(sampleId, 5);
        Assert.Equal("READY_FOR_REVIEW", completedSession.OverallSessionStatus);

        var orders = await db.TestOrders.Where(t => t.SampleId == sampleId).ToListAsync();
        Assert.NotEmpty(orders);
        Assert.All(orders, o =>
        {
            Assert.Equal(ApprovalStatus.ResultEntered, o.Status);
            Assert.Equal(WorkflowStep.Ready, o.CurrentStep);
        });

        var sampleEvents = await db.ReviewWorkflowEvents
            .Where(e => e.EntityType == ReviewEntityTypes.Sample && e.EntityId == sampleId)
            .ToListAsync();
        var submitEvent = Assert.Single(sampleEvents);
        Assert.Equal(ReviewWorkflowEventType.SubmittedForReview, submitEvent.EventType);
        Assert.Equal(5, submitEvent.PerformedByUserId);
        Assert.Contains("submitted for review", submitEvent.Comment, StringComparison.OrdinalIgnoreCase);
    }

    // Builds one input cell per matrix cell. The prerequisite-locked tests
    // (BCC, Salmonella) go last, so a rejection happens only after cells
    // that are allowed on their own have been processed.
    private static List<MatrixCellInput> AllCellsLockedTestsLast(PathogenTestingSessionDto session) =>
        session.ResultMatrix
            .OrderBy(c => c.TestCode is "BCC" or "Salmonella" ? 1 : 0)
            .Select(c => c.ResultType == "Quantitative"
                ? new MatrixCellInput(c.SampleLocationId, c.TestCode, "5", "5 CFU", 5, "Quantitative")
                : new MatrixCellInput(c.SampleLocationId, c.TestCode, "NOT_DETECTED", "Not Detected (-)", null, "Qualitative"))
            .ToList();

    private static async Task CompleteStep2ForBccAndSalmonellaAsync(MicroLimsDbContext db)
    {
        var bccOrder = await db.TestOrders.FirstAsync(t => t.TestCode == "BCC");
        var salmOrder = await db.TestOrders.FirstAsync(t => t.TestCode == "Salmonella");
        db.WorkflowStepResults.AddRange(
            new WorkflowStepResult { TestOrderId = bccOrder.Id, StepName = "BCA Selective Medium", SubmittedAtUtc = DateTime.UtcNow },
            new WorkflowStepResult { TestOrderId = salmOrder.Id, StepName = "RVS Selective Broth", SubmittedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    // A rejected matrix save must record nothing. It used to save each
    // qualitative cell as it went and only then reach the locked test, so a
    // request answered with an error still left part of the grid persisted.
    [Fact]
    public async Task SaveResultMatrix_RejectedForALockedTest_PersistsNothing()
    {
        var (db, sampleId, _) = SetupTestEnvironment(20);
        var service = new PathogenSessionService(db);
        await service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 3, DateTime.UtcNow.AddHours(-25)), 5);

        // BCC and Salmonella have not completed step 2, so they are locked.
        var session = await service.GetSessionAsync(sampleId);
        var cells = AllCellsLockedTestsLast(session!);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
            service.SaveResultMatrixAsync(sampleId, new SaveResultMatrixRequest(cells), 5));
        Assert.Contains("BCC", ex.Message);

        Assert.Equal(0, await db.LocationPathogenObservations.AsNoTracking().CountAsync());
        Assert.Equal(0, await db.SampleLocations.AsNoTracking()
            .CountAsync(l => l.ReportedResult != null || l.CFUResult != null || l.EnteredByUserId != null));
        Assert.False(db.ChangeTracker.HasChanges());
    }

    // The whole matrix is one unit of work: one SaveChanges (one transaction
    // on PostgreSQL), not one per qualitative cell.
    [Fact]
    public async Task SaveResultMatrix_120Cells_SavesOnce()
    {
        var (db, sampleId, _) = SetupTestEnvironment(20);
        var service = new PathogenSessionService(db);
        await service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 3, DateTime.UtcNow.AddHours(-25)), 5);
        await CompleteStep2ForBccAndSalmonellaAsync(db);

        var cells = AllCellsLockedTestsLast((await service.GetSessionAsync(sampleId))!);
        Assert.Equal(120, cells.Count);

        var saves = 0;
        db.SavingChanges += (_, _) => saves++;

        var saved = await service.SaveResultMatrixAsync(sampleId, new SaveResultMatrixRequest(cells), 5);

        Assert.Equal(1, saves);
        Assert.Equal(120, saved.CompletedResultCount);
        Assert.Equal(cells.Count(c => c.ResultType == "Qualitative"), await db.LocationPathogenObservations.CountAsync());
    }

    // Re-saving a matrix updates each primary observation in place - same
    // upsert behaviour as LocationPathogenObservationService - rather than
    // adding a duplicate per (location, test order).
    [Fact]
    public async Task SaveResultMatrix_SavedTwice_UpdatesObservationsInsteadOfDuplicating()
    {
        var (db, sampleId, _) = SetupTestEnvironment(3);
        var service = new PathogenSessionService(db);
        await service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 3, DateTime.UtcNow.AddHours(-25)), 5);
        await CompleteStep2ForBccAndSalmonellaAsync(db);

        var cells = AllCellsLockedTestsLast((await service.GetSessionAsync(sampleId))!);
        await service.SaveResultMatrixAsync(sampleId, new SaveResultMatrixRequest(cells), 5);
        var countAfterFirstSave = await db.LocationPathogenObservations.CountAsync();

        var detected = cells
            .Select(c => c.ResultType == "Qualitative"
                ? c with { ResultCode = "DETECTED", ResultDisplay = "Detected (+)" }
                : c)
            .ToList();
        await service.SaveResultMatrixAsync(sampleId, new SaveResultMatrixRequest(detected), 5);

        var observations = await db.LocationPathogenObservations.AsNoTracking().ToListAsync();
        Assert.Equal(countAfterFirstSave, observations.Count);
        Assert.All(observations, o => Assert.Equal(GrowthObservation.GrowthConforming, o.GrowthObservation));
    }

    private static List<PrimaryObservationInput> NoGrowthForAllQualitativeCellsLockedTestsLast(PathogenTestingSessionDto session) =>
        session.ResultMatrix
            .Where(c => c.ResultType == "Qualitative")
            .OrderBy(c => c.TestCode is "BCC" or "Salmonella" ? 1 : 0)
            .Select(c => new PrimaryObservationInput(c.SampleLocationId, c.TestCode, GrowthObservation.NoGrowth))
            .ToList();

    // Same all-or-nothing rule as the result matrix: a request rejected for
    // a locked test must not leave earlier observations - and the location
    // statuses that route them to confirmation - half-recorded.
    [Fact]
    public async Task SavePrimaryObservations_RejectedForALockedTest_PersistsNothing()
    {
        var (db, sampleId, _) = SetupTestEnvironment(20);
        var service = new PathogenSessionService(db);
        await service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 3, DateTime.UtcNow.AddHours(-25)), 5);

        var inputs = NoGrowthForAllQualitativeCellsLockedTestsLast((await service.GetSessionAsync(sampleId))!);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
            service.SavePrimaryObservationsAsync(sampleId, new SavePrimaryObservationsRequest(inputs), 5));
        Assert.Contains("BCC", ex.Message);

        Assert.Equal(0, await db.LocationPathogenObservations.AsNoTracking().CountAsync());
        Assert.Equal(0, await db.SampleLocations.AsNoTracking()
            .CountAsync(l => l.ReportedResult != null || l.Status != null || l.EnteredByUserId != null));
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task SavePrimaryObservations_100Observations_SavesOnce()
    {
        var (db, sampleId, _) = SetupTestEnvironment(20);
        var service = new PathogenSessionService(db);
        await service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 3, DateTime.UtcNow.AddHours(-25)), 5);
        await CompleteStep2ForBccAndSalmonellaAsync(db);

        var inputs = NoGrowthForAllQualitativeCellsLockedTestsLast((await service.GetSessionAsync(sampleId))!);
        Assert.Equal(100, inputs.Count);

        var saves = 0;
        db.SavingChanges += (_, _) => saves++;

        await service.SavePrimaryObservationsAsync(sampleId, new SavePrimaryObservationsRequest(inputs), 5);

        Assert.Equal(1, saves);
        Assert.Equal(100, await db.LocationPathogenObservations.CountAsync());
    }

    // Re-recording an observation without a media snapshot keeps the one
    // already on file (RecordPrimaryObservationAsync's ALCOA+ behaviour).
    [Fact]
    public async Task SavePrimaryObservations_ResavedWithoutSnapshot_KeepsExistingSnapshot()
    {
        var (db, sampleId, _) = SetupTestEnvironment(3);
        var service = new PathogenSessionService(db);
        await service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 3, DateTime.UtcNow.AddHours(-25)), 5);
        await CompleteStep2ForBccAndSalmonellaAsync(db);

        var cell = (await service.GetSessionAsync(sampleId))!.ResultMatrix.First(c => c.TestCode == "BCC");
        const string snapshot = "{\"Media\":\"BCA\"}";

        await service.SavePrimaryObservationsAsync(sampleId, new SavePrimaryObservationsRequest(new List<PrimaryObservationInput>
        {
            new(cell.SampleLocationId, cell.TestCode, GrowthObservation.GrowthConforming, snapshot)
        }), 5);
        await service.SavePrimaryObservationsAsync(sampleId, new SavePrimaryObservationsRequest(new List<PrimaryObservationInput>
        {
            new(cell.SampleLocationId, cell.TestCode, GrowthObservation.NoGrowth)
        }), 5);

        var stored = Assert.Single(await db.LocationPathogenObservations.AsNoTracking().ToListAsync());
        Assert.Equal(GrowthObservation.NoGrowth, stored.GrowthObservation);
        Assert.Equal(snapshot, stored.SelectiveMediaSnapshot);
    }

    [Fact]
    public async Task GetSession_CountTestIncubating_BeforeMinHours_IsLocked()
    {
        var (db, sampleId, _) = SetupTestEnvironment(3);
        var service = new PathogenSessionService(db);

        var tamcOrder = await db.TestOrders.FirstAsync(t => t.TestCode == "TAMC-Water");
        var start = DateTime.UtcNow.AddHours(-1); // 1 hour ago, minHours is 48
        db.Incubations.Add(new Incubation
        {
            TestOrderId = tamcOrder.Id,
            StepName = "Plate Incubation",
            StepNumber = 1,
            MediaId = 23,
            StartedAt = start,
            IncubationStartUtc = start,
            IncubationEndUtc = start.AddHours(72),
            CompletedAt = null
        });
        tamcOrder.CurrentStep = WorkflowStep.Incubating;
        await db.SaveChangesAsync();

        var session = await service.GetSessionAsync(sampleId);
        var tamcTest = session.AssignedTests.First(t => t.TestCode == "TAMC-Water");

        Assert.False(tamcTest.IsResultEntryAllowed);
        Assert.True(tamcTest.IsWorkflowLocked);
        Assert.Equal("COUNT_INCUBATING", tamcTest.TestSessionState);
        Assert.Contains("Available from", tamcTest.LockReason);
    }

    [Fact]
    public async Task GetSession_CountTestIncubating_AfterMinHours_IsUnlocked()
    {
        var (db, sampleId, _) = SetupTestEnvironment(3);
        var service = new PathogenSessionService(db);

        var tamcOrder = await db.TestOrders.FirstAsync(t => t.TestCode == "TAMC-Water");
        var start = DateTime.UtcNow.AddHours(-50); // 50 hours ago, minHours is 48
        db.Incubations.Add(new Incubation
        {
            TestOrderId = tamcOrder.Id,
            StepName = "Plate Incubation",
            StepNumber = 1,
            MediaId = 23,
            StartedAt = start,
            IncubationStartUtc = start,
            IncubationEndUtc = start.AddHours(72),
            CompletedAt = null
        });
        tamcOrder.CurrentStep = WorkflowStep.Incubating;
        await db.SaveChangesAsync();

        var session = await service.GetSessionAsync(sampleId);
        var tamcTest = session.AssignedTests.First(t => t.TestCode == "TAMC-Water");

        Assert.True(tamcTest.IsResultEntryAllowed);
        Assert.False(tamcTest.IsWorkflowLocked);
        Assert.Equal("AWAITING_RESULTS", tamcTest.TestSessionState);
        Assert.Equal("EnterResult", tamcTest.WorkflowStatus);
    }

    [Fact]
    public async Task GetSession_PathogenTest_UnaffectedByCountIncubationFix()
    {
        var (db, sampleId, _) = SetupTestEnvironment(3);
        var service = new PathogenSessionService(db);

        // TAMC is incubating before minHours
        var tamcOrder = await db.TestOrders.FirstAsync(t => t.TestCode == "TAMC-Water");
        var start = DateTime.UtcNow.AddHours(-1);
        db.Incubations.Add(new Incubation
        {
            TestOrderId = tamcOrder.Id,
            StepName = "Plate Incubation",
            StepNumber = 1,
            MediaId = 23,
            StartedAt = start,
            IncubationStartUtc = start,
            IncubationEndUtc = start.AddHours(72),
            CompletedAt = null
        });
        tamcOrder.CurrentStep = WorkflowStep.Incubating;
        await db.SaveChangesAsync();

        // Start TSB for sample
        await service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 3, DateTime.UtcNow.AddHours(-1)), 5);

        var session = await service.GetSessionAsync(sampleId);
        var bcc = session.AssignedTests.First(t => t.TestCode == "BCC");
        var tamc = session.AssignedTests.First(t => t.TestCode == "TAMC-Water");

        // TAMC is locked by count incubation
        Assert.False(tamc.IsResultEntryAllowed);
        Assert.True(tamc.IsWorkflowLocked);
        Assert.Equal("COUNT_INCUBATING", tamc.TestSessionState);

        // BCC is locked by TSB incubation
        Assert.False(bcc.IsResultEntryAllowed);
        Assert.True(bcc.IsWorkflowLocked);
        Assert.Equal("TSB_INCUBATING", bcc.TestSessionState);
    }

    [Fact]
    public async Task StartSharedTsb_CreatesWorkflowStepResult_ForAllPathogenOrders()
    {
        var (db, sampleId, _) = SetupTestEnvironment(3);
        var service = new PathogenSessionService(db);

        var res = await service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 3, DateTime.UtcNow), 5);
        Assert.NotNull(res);

        var pathogenOrders = await db.TestOrders
            .Where(t => t.SampleId == sampleId && t.TestCode != "TAMC-Water")
            .ToListAsync();

        Assert.NotEmpty(pathogenOrders);

        foreach (var order in pathogenOrders)
        {
            var wsr = await db.WorkflowStepResults
                .FirstOrDefaultAsync(r => r.TestOrderId == order.Id && r.StepName == "Broth Enrichment");

            Assert.NotNull(wsr);
            Assert.True(wsr.IsSharedSessionStep);
            Assert.Equal(StepType.BrothEnrichment, wsr.StepType);
            Assert.Equal(5, wsr.SubmittedByUserId);
        }
    }

    [Fact]
    public async Task GetSiblingPathogenOrders_ReturnsAllSiblingPathogenTestsOnSample()
    {
        var (db, sampleId, _) = SetupTestEnvironment(3);
        var bccOrder = await db.TestOrders.FirstAsync(t => t.SampleId == sampleId && t.TestCode == "BCC");

        var engine = TestServiceFactory.TestWorkflow(db);
        var siblings = await engine.GetSiblingPathogenOrdersAsync(bccOrder.Id);

        Assert.NotEmpty(siblings);
        Assert.DoesNotContain(siblings, s => s.TestOrderId == bccOrder.Id);
        Assert.DoesNotContain(siblings, s => s.TestCode == "TAMC-Water");
        Assert.Contains(siblings, s => s.TestCode == "Salmonella");
    }

    [Fact]
    public async Task SubmitBroth_PropagatesSharedTsb_ToAllSiblingPathogenOrders()
    {
        var (db, sampleId, _) = SetupTestEnvironment(3);
        var bccOrder = await db.TestOrders.FirstAsync(t => t.SampleId == sampleId && t.TestCode == "BCC");

        // Create an incubation for BCC order
        var start = DateTime.UtcNow.AddHours(-20);
        var inc = new Incubation
        {
            TestOrderId = bccOrder.Id,
            StepNumber = 1,
            StepName = "TSB Enrichment",
            MediaId = 20,
            IncubatorEquipmentId = 3,
            StartedAt = start,
            IncubationStartUtc = start,
            IncubationEndUtc = start.AddHours(24),
            CompletedAt = null
        };
        db.Incubations.Add(inc);
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.PropagateSharedTsbToSiblingOrdersAsync(bccOrder.Id, inc.Id, 5);

        var siblingOrders = await db.TestOrders
            .Where(t => t.SampleId == sampleId && t.Id != bccOrder.Id && t.TestCode != "TAMC-Water")
            .ToListAsync();

        foreach (var sib in siblingOrders)
        {
            var wsr = await db.WorkflowStepResults
                .FirstOrDefaultAsync(r => r.TestOrderId == sib.Id);

            Assert.NotNull(wsr);
            Assert.True(wsr.IsSharedSessionStep);
            Assert.Equal(StepType.BrothEnrichment, wsr.StepType);

            var hist = await db.WorkflowHistories
                .FirstOrDefaultAsync(h => h.TestOrderId == sib.Id && h.Note!.Contains("linked to shared TSB"));
            Assert.NotNull(hist);
        }
    }

    [Fact]
    public async Task PropagateSharedTsb_Idempotent_NoDuplicatesCreated()
    {
        var (db, sampleId, _) = SetupTestEnvironment(3);
        var bccOrder = await db.TestOrders.FirstAsync(t => t.SampleId == sampleId && t.TestCode == "BCC");

        var start = DateTime.UtcNow.AddHours(-20);
        var inc = new Incubation
        {
            TestOrderId = bccOrder.Id,
            StepNumber = 1,
            StepName = "TSB Enrichment",
            MediaId = 20,
            IncubatorEquipmentId = 3,
            StartedAt = start,
            IncubationStartUtc = start,
            IncubationEndUtc = start.AddHours(24),
            CompletedAt = null
        };
        db.Incubations.Add(inc);
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.PropagateSharedTsbToSiblingOrdersAsync(bccOrder.Id, inc.Id, 5);
        var countFirst = await db.WorkflowStepResults.CountAsync();

        // Second call
        await engine.PropagateSharedTsbToSiblingOrdersAsync(bccOrder.Id, inc.Id, 5);
        var countSecond = await db.WorkflowStepResults.CountAsync();

        Assert.Equal(countFirst, countSecond);
    }

    [Fact]
    public async Task ResetSessionAsync_CleansUpAllStepResultsAndResetsOrdersToWaiting()
    {
        var (db, sampleId, _) = SetupTestEnvironment(3);
        var service = new PathogenSessionService(db);

        // Start TSB
        await service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 3, DateTime.UtcNow), 5);

        var preResetSession = await service.GetSessionAsync(sampleId);
        Assert.NotNull(preResetSession);
        Assert.True(preResetSession.SharedTsb.IsStarted);

        // Reset
        var postResetSession = await service.ResetSessionAsync(sampleId, "Testing reset functionality", 1);
        Assert.NotNull(postResetSession);
        Assert.False(postResetSession.SharedTsb.IsStarted);
        Assert.Equal("NOT_STARTED", postResetSession.OverallSessionStatus);

        var orders = await db.TestOrders.Where(t => t.SampleId == sampleId).ToListAsync();
        Assert.All(orders, o => Assert.Equal(WorkflowStep.Waiting, o.CurrentStep));
        Assert.All(orders, o => Assert.Equal(ApprovalStatus.Pending, o.Status));

        var wsrCount = await db.WorkflowStepResults.CountAsync(w => orders.Select(o => o.Id).Contains(w.TestOrderId));
        Assert.Equal(0, wsrCount);

        var incCount = await db.Incubations.CountAsync(i => i.TestOrderId.HasValue && orders.Select(o => o.Id).Contains(i.TestOrderId.Value));
        Assert.Equal(0, incCount);
    }

    // Rejecting takes a Section Head; this endpoint takes an Analyst. If a reset
    // could return a Rejected sample to Received, the lower privilege would be
    // undoing the higher one's decision about the material.
    [Fact]
    public async Task ResetSessionAsync_RefusesToResetARejectedSample()
    {
        var (db, sampleId, _) = SetupTestEnvironment(3);
        var service = new PathogenSessionService(db);

        await service.StartSharedTsbAsync(sampleId, new StartSharedTsbRequest(20, 3, DateTime.UtcNow), 5);

        var sample = await db.Samples.FirstAsync(s => s.Id == sampleId);
        sample.Status = SampleStatus.Rejected;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(
            () => service.ResetSessionAsync(sampleId, "Analyst tries to undo a rejection", 1));
        Assert.Equal("SampleRejected", ex.ErrorCode);

        // The refusal must leave the session untouched - a partially applied
        // reset would be worse than either outcome.
        var afterSample = await db.Samples.FirstAsync(s => s.Id == sampleId);
        Assert.Equal(SampleStatus.Rejected, afterSample.Status);

        var session = await service.GetSessionAsync(sampleId);
        Assert.NotNull(session);
        Assert.True(session.SharedTsb.IsStarted);
    }

    [Fact]
    public async Task ResetSessionAsync_StillResetsASampleUnderReview()
    {
        var (db, sampleId, _) = SetupTestEnvironment(3);
        var service = new PathogenSessionService(db);

        var sample = await db.Samples.FirstAsync(s => s.Id == sampleId);
        sample.Status = SampleStatus.UnderReview;
        await db.SaveChangesAsync();

        await service.ResetSessionAsync(sampleId, "Reviewer sends it back", 1);

        var afterSample = await db.Samples.FirstAsync(s => s.Id == sampleId);
        Assert.Equal(SampleStatus.Received, afterSample.Status);
    }
}
