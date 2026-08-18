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

        // Seed MediaTypes
        var brothType = new MediaType { Id = 1, Class = MediaClass.GeneralBroth, IncubationMinHours = 18, IncubationMaxHours = 24, RequiredTemperatureMin = 30, RequiredTemperatureMax = 35 };
        var agarType = new MediaType { Id = 2, Class = MediaClass.GeneralAgar, IncubationMinHours = 24, IncubationMaxHours = 48, RequiredTemperatureMin = 35, RequiredTemperatureMax = 37 };
        db.MediaTypes.AddRange(brothType, agarType);

        // Seed Incubators
        var inc3 = new EquipmentInventory { Id = 3, Code = "INC-03", InstrumentType = "Incubator", Status = EquipmentOperationalStatus.InService };
        var inc4 = new EquipmentInventory { Id = 4, Code = "INC-04", InstrumentType = "Incubator", Status = EquipmentOperationalStatus.InService };
        db.EquipmentInventories.AddRange(inc3, inc4);

        // Seed Material & Released TSB Media Lot
        var tsbMat = new Material { Id = 10, MaterialName = "Tryptic Soy Broth Powder", MaterialType = MaterialType.DehydratedMedia, BatchNumber = "TSB-MAT-01" };
        db.Materials.Add(tsbMat);

        var tsbMedia = new Media
        {
            Id = 20,
            MediaTypeId = 1,
            MediaType = brothType,
            MaterialId = 10,
            Material = tsbMat,
            LotNumber = "TSB-LOT-25113",
            Status = MediaStatus.Prepared,
            IsReleasedForUse = true,
            ExpiryDate = DateTime.UtcNow.AddMonths(2),
            PreparedAt = DateTime.UtcNow.AddDays(-5)
        };
        db.Media.Add(tsbMedia);

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
                new() { StepOrder = 1, StepName = "TSB Enrichment", StepType = StepType.BrothEnrichment, MediaTypeId = 1, MediaType = brothType, IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 30, TemperatureMax = 35 },
                new() { StepOrder = 2, StepName = "BCA Selective Medium", StepType = StepType.SelectivePlating, MediaTypeId = 2, MediaType = agarType, IncubationMinHours = 24, IncubationMaxHours = 48, TemperatureMin = 35, TemperatureMax = 37 }
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
                new() { StepOrder = 1, StepName = "TSB Pre-enrichment", StepType = StepType.BrothEnrichment, MediaTypeId = 1, MediaType = brothType, IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 30, TemperatureMax = 35 },
                new() { StepOrder = 2, StepName = "RVS Selective Broth", StepType = StepType.SelectiveBroth, MediaTypeId = 1, MediaType = brothType, IncubationMinHours = 24, IncubationMaxHours = 24, TemperatureMin = 41.5m, TemperatureMax = 42.5m }
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
                new() { StepOrder = 1, StepName = "Plate Incubation", StepType = StepType.SelectivePlating, MediaTypeId = 2, MediaType = agarType, IncubationMinHours = 48, IncubationMaxHours = 72, TemperatureMin = 30, TemperatureMax = 35 }
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
                new() { StepOrder = 1, StepName = "TSB Enrichment", StepType = StepType.BrothEnrichment, MediaTypeId = 1, MediaType = brothType, IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 30, TemperatureMax = 35 }
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
                new() { StepOrder = 1, StepName = "TSB Enrichment", StepType = StepType.BrothEnrichment, MediaTypeId = 1, MediaType = brothType, IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 30, TemperatureMax = 35 }
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
                new() { StepOrder = 1, StepName = "TSB Enrichment", StepType = StepType.BrothEnrichment, MediaTypeId = 1, MediaType = brothType, IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 30, TemperatureMax = 35 }
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
        var sampleDto = await new TestingWorkspaceService(db).GetSampleAsync(sampleId);
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
}
