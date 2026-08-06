using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// EM/After Cleaning batch model: one TestOrder per TestCode per batch
// sample, with SampleLocation rows carrying the per-room result. Covers
// the redesign's 5 spec'd test cases.
public class EMBatchLocationTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    // Seeds a single-step CountTest workflow template for the given test
    // code, plus a released media lot and incubator for it. minHours
    // defaults to 0 so most tests can record results immediately without
    // needing to fake elapsed time - tests specifically covering the
    // minimum-duration gate pass a real value (e.g. 72, matching Test
    // Master's actual EM/After Cleaning configuration).
    private static async Task<(Media media, Equipment equipment)> SeedCountTestWorkflowAsync(MicroLimsDbContext db, string testCode, int minHours = 0)
    {
        var mediaType = new MediaType
        {
            Class = MediaClass.GeneralAgar,
            IncubationMinHours = 24, IncubationMaxHours = 48, RequiredTemperatureMin = 30, RequiredTemperatureMax = 35
        };
        var testDefinition = new TestDefinition { Code = testCode, DisplayName = testCode, WorkflowType = WorkflowType.CountTest };
        db.MediaTypes.Add(mediaType);
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        db.TestWorkflowSteps.Add(new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation", MediaTypeId = mediaType.Id,
            IncubationMinHours = minHours, IncubationMaxHours = minHours + 24, TemperatureMin = 30, TemperatureMax = 35, IsFinalStep = true
        });

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-001", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = "TSA-" + testCode,
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var media = new Media
        {
            MediaTypeId = mediaType.Id, MaterialId = material.Id, LotNumber = "TSA/" + testCode, IsReleasedForUse = true,
            Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30)
        };
        var equipment = new Equipment { Name = "Incubator " + testCode, Code = "INC-" + testCode, Type = EquipmentType.Incubator };
        db.Media.Add(media);
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();

        return (media, equipment);
    }

    // Seeds the real EM two-window chain: window 1 at ~32.5°C for 3 days
    // (72h min), window 2 at ~22.5°C for 2 days (48h min) - matching Test
    // Master's actual "TAMC Passive air sample" configuration.
    private static async Task<(Media window1Media, Media window2Media, Equipment equipment1, Equipment equipment2)> SeedTwoWindowCountTestWorkflowAsync(MicroLimsDbContext db, string testCode)
    {
        var window1Media = new MediaType { Class = MediaClass.GeneralAgar, IncubationMinHours = 24, IncubationMaxHours = 48, RequiredTemperatureMin = 30, RequiredTemperatureMax = 35 };
        var window2Media = new MediaType { Class = MediaClass.GeneralAgar, IncubationMinHours = 24, IncubationMaxHours = 48, RequiredTemperatureMin = 20, RequiredTemperatureMax = 25 };
        var testDefinition = new TestDefinition { Code = testCode, DisplayName = testCode, WorkflowType = WorkflowType.CountTest };
        db.MediaTypes.AddRange(window1Media, window2Media);
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        db.TestWorkflowSteps.AddRange(
            new TestWorkflowStep { TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation", MediaTypeId = window1Media.Id, IncubationMinHours = 72, IncubationMaxHours = 96, TemperatureMin = 30, TemperatureMax = 35, IsFinalStep = false },
            new TestWorkflowStep { TestDefinitionId = testDefinition.Id, StepOrder = 2, StepName = "transfer", MediaTypeId = window2Media.Id, IncubationMinHours = 48, IncubationMaxHours = 72, TemperatureMin = 20, TemperatureMax = 25, IsFinalStep = true });

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-001", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = "TSA-2W-" + testCode,
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var media = new Media
        {
            MediaTypeId = window1Media.Id, MaterialId = material.Id, LotNumber = "TSA/2W/" + testCode, IsReleasedForUse = true,
            Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30)
        };
        // A second Media row sharing window2's MediaType, so the "same
        // plate, new incubator" window 2 selection has a matching lot to
        // pick from too (SelectMediaAsync enforces media type == step type).
        var media2 = new Media
        {
            MediaTypeId = window2Media.Id, MaterialId = material.Id, LotNumber = "TSA/2W-B/" + testCode, IsReleasedForUse = true,
            Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30)
        };
        var equipment1 = new Equipment { Name = "Incubator 32.5 " + testCode, Code = "INC-A-" + testCode, Type = EquipmentType.Incubator };
        var equipment2 = new Equipment { Name = "Incubator 22.5 " + testCode, Code = "INC-B-" + testCode, Type = EquipmentType.Incubator };
        db.Media.AddRange(media, media2);
        db.Equipment.AddRange(equipment1, equipment2);
        await db.SaveChangesAsync();

        return (media, media2, equipment1, equipment2);
    }

    private static TestWorkflowEngine NewTestWorkflowEngine(MicroLimsDbContext db) =>
        TestServiceFactory.TestWorkflow(db);

    // Seeds a single-step pathogen workflow template (Observation or
    // DualPlate) for the given test code, plus a released media lot and
    // incubator - mirrors SeedCountTestWorkflowAsync but for the
    // per-location Detected/Absent grid instead of CFU.
    private static async Task<(Media media, Equipment equipment)> SeedPathogenWorkflowAsync(MicroLimsDbContext db, string testCode, bool isDualPlate)
    {
        var mediaType = new MediaType
        {
            Class = MediaClass.SelectiveAgar,
            IncubationMinHours = 24, IncubationMaxHours = 48, RequiredTemperatureMin = 35, RequiredTemperatureMax = 37
        };
        var testDefinition = new TestDefinition
        {
            Code = testCode, DisplayName = testCode,
            WorkflowType = isDualPlate ? WorkflowType.DualPlate : WorkflowType.Observation
        };
        db.MediaTypes.Add(mediaType);
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        db.TestWorkflowSteps.Add(new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "Detection", MediaTypeId = mediaType.Id,
            IncubationMinHours = 0, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37,
            IsFinalStep = true, IsDualPlate = isDualPlate
        });

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "Selective Agar Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-P01", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = "SEL-" + testCode,
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var media = new Media
        {
            MediaTypeId = mediaType.Id, MaterialId = material.Id, LotNumber = "SEL/" + testCode, IsReleasedForUse = true,
            Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30)
        };
        var equipment = new Equipment { Name = "Incubator " + testCode, Code = "INC-P-" + testCode, Type = EquipmentType.Incubator };
        db.Media.Add(media);
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();

        return (media, equipment);
    }

    [Fact]
    public async Task PrepareAsync_NoLocationsSelected_Throws()
    {
        await using var db = NewDb();
        var dept = new Department { Name = "Filling" };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();

        var engine = new EMWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new EMReceiveRequest(dept.Id, 0, "Analyst", "CTRL-1", 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.PrepareAsync(sample.Id, new List<int>(), 1));
    }

    [Fact]
    public async Task PrepareAsync_RoomFromWrongDepartment_Throws()
    {
        await using var db = NewDb();
        var deptA = new Department { Name = "Filling" };
        var deptB = new Department { Name = "Packing" };
        var roomInB = new Room { Name = "Grade B Packing", Department = deptB, GradeClassification = "B" };
        db.Departments.AddRange(deptA, deptB);
        db.Rooms.Add(roomInB);
        await db.SaveChangesAsync();

        var config = new RoomTestConfiguration { RoomId = roomInB.Id, TestType = "PassiveAirSample", TestCode = "TAMC", AlertLimit = "1", ActionLimit = "3", SpecLimit = "5" };
        db.RoomTestConfigurations.Add(config);
        await db.SaveChangesAsync();

        var engine = new EMWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new EMReceiveRequest(deptA.Id, 0, "Analyst", "CTRL-2", 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.PrepareAsync(sample.Id, new List<int> { config.Id }, 1));
    }

    [Fact]
    public async Task RecordBatchResultsAsync_MissingLocation_ThrowsAndListsIt()
    {
        await using var db = NewDb();
        var dept = new Department { Name = "Filling" };
        var roomA = new Room { Name = "Room A", Department = dept, GradeClassification = "A" };
        var roomB = new Room { Name = "Room B", Department = dept, GradeClassification = "A" };
        db.Departments.Add(dept);
        db.Rooms.AddRange(roomA, roomB);
        await db.SaveChangesAsync();

        var configA = new RoomTestConfiguration { RoomId = roomA.Id, TestType = "PassiveAirSample", TestCode = "TAMC", AlertLimit = "1", ActionLimit = "3", SpecLimit = "5" };
        var configB = new RoomTestConfiguration { RoomId = roomB.Id, TestType = "PassiveAirSample", TestCode = "TAMC", AlertLimit = "1", ActionLimit = "3", SpecLimit = "5" };
        db.RoomTestConfigurations.AddRange(configA, configB);
        await db.SaveChangesAsync();

        var (media, equipment) = await SeedCountTestWorkflowAsync(db, "TAMC", minHours: 0);

        var emEngine = new EMWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await emEngine.ReceiveAsync(new EMReceiveRequest(dept.Id, 0, "Analyst", "CTRL-3", 1));
        var prepared = await emEngine.PrepareAsync(sample.Id, new List<int> { configA.Id, configB.Id }, 1);
        var order = prepared.TestOrders.Single();

        var workflowEngine = NewTestWorkflowEngine(db);
        await workflowEngine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, equipment.Id, 1);

        var locations = await workflowEngine.GetLocationsAsync(order.Id);
        var onlyOne = new List<BatchLocationResult> { new(locations[0].Id, 5) };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflowEngine.RecordBatchResultsAsync(order.Id, 1, onlyOne, 1));
        Assert.Contains(locations[1].RoomTestConfiguration!.Room!.Name, ex.Message);
    }

    [Fact]
    public async Task HappyPath_ThreeRooms_AllLocationsGetResults_TestOrderReady()
    {
        await using var db = NewDb();
        var dept = new Department { Name = "Filling" };
        var rooms = new[]
        {
            new Room { Name = "Room A", Department = dept, GradeClassification = "A" },
            new Room { Name = "Room B", Department = dept, GradeClassification = "A" },
            new Room { Name = "Room C", Department = dept, GradeClassification = "A" }
        };
        db.Departments.Add(dept);
        db.Rooms.AddRange(rooms);
        await db.SaveChangesAsync();

        var configs = rooms.Select(r => new RoomTestConfiguration
        { RoomId = r.Id, TestType = "PassiveAirSample", TestCode = "TAMC", AlertLimit = "1", ActionLimit = "3", SpecLimit = "5" }).ToList();
        db.RoomTestConfigurations.AddRange(configs);
        await db.SaveChangesAsync();

        var (media, equipment) = await SeedCountTestWorkflowAsync(db, "TAMC", minHours: 0);

        var emEngine = new EMWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await emEngine.ReceiveAsync(new EMReceiveRequest(dept.Id, 0, "Analyst", "CTRL-4", 1));
        var prepared = await emEngine.PrepareAsync(sample.Id, configs.Select(c => c.Id).ToList(), 1);

        Assert.Single(prepared.TestOrders); // one TestOrder for the whole batch, not one per room
        var order = prepared.TestOrders.Single();
        Assert.Null(order.RoomId); // batch TestOrders never carry a single Room

        var workflowEngine = NewTestWorkflowEngine(db);
        await workflowEngine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, equipment.Id, 1);

        var locations = await workflowEngine.GetLocationsAsync(order.Id);
        Assert.Equal(3, locations.Count);

        var submissions = locations.Select(l => new BatchLocationResult(l.Id, 0)).ToList();
        var result = await workflowEngine.RecordBatchResultsAsync(order.Id, 1, submissions, 1);

        Assert.True(result.AllStepsComplete);

        var reloadedLocations = await db.SampleLocations.Where(l => l.TestOrderId == order.Id).ToListAsync();
        Assert.All(reloadedLocations, l =>
        {
            Assert.Equal(0m, l.CFUResult);
            Assert.Equal(0m, l.CalculatedResult);
            Assert.Equal("WithinLimits", l.Status);
        });

        var reloadedOrder = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.Equal(WorkflowStep.Ready, reloadedOrder.CurrentStep);
    }

    [Fact]
    public async Task BothTestCodesReady_SampleFlipsToUnderReview()
    {
        await using var db = NewDb();
        var dept = new Department { Name = "Filling" };
        var room = new Room { Name = "Room A", Department = dept, GradeClassification = "A" };
        db.Departments.Add(dept);
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var tamcConfig = new RoomTestConfiguration { RoomId = room.Id, TestType = "PassiveAirSample", TestCode = "TAMC", AlertLimit = "1", ActionLimit = "3", SpecLimit = "5" };
        var tymcConfig = new RoomTestConfiguration { RoomId = room.Id, TestType = "PassiveAirSample", TestCode = "TYMC", AlertLimit = "1", ActionLimit = "3", SpecLimit = "5" };
        db.RoomTestConfigurations.AddRange(tamcConfig, tymcConfig);
        await db.SaveChangesAsync();

        var (tamcMedia, tamcEquipment) = await SeedCountTestWorkflowAsync(db, "TAMC", minHours: 0);
        var (tymcMedia, tymcEquipment) = await SeedCountTestWorkflowAsync(db, "TYMC", minHours: 0);

        var emEngine = new EMWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await emEngine.ReceiveAsync(new EMReceiveRequest(dept.Id, 0, "Analyst", "CTRL-5", 1));
        var prepared = await emEngine.PrepareAsync(sample.Id, new List<int> { tamcConfig.Id, tymcConfig.Id }, 1);

        Assert.Equal(2, prepared.TestOrders.Count); // one TestOrder per distinct TestCode

        var workflowEngine = NewTestWorkflowEngine(db);
        var tamcOrder = prepared.TestOrders.Single(o => o.TestCode == "TAMC");
        var tymcOrder = prepared.TestOrders.Single(o => o.TestCode == "TYMC");

        await workflowEngine.SelectMediaAsync(tamcOrder.Id, "CountIncubation", tamcMedia.Id, tamcEquipment.Id, 1);
        var tamcLocations = await workflowEngine.GetLocationsAsync(tamcOrder.Id);
        await workflowEngine.RecordBatchResultsAsync(tamcOrder.Id, 1, tamcLocations.Select(l => new BatchLocationResult(l.Id, 2)).ToList(), 1);

        // Only one of two TestOrders done - Sample must still be in testing.
        var midway = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.NotEqual(SampleStatus.UnderReview, midway.Status);

        await workflowEngine.SelectMediaAsync(tymcOrder.Id, "CountIncubation", tymcMedia.Id, tymcEquipment.Id, 1);
        var tymcLocations = await workflowEngine.GetLocationsAsync(tymcOrder.Id);
        await workflowEngine.RecordBatchResultsAsync(tymcOrder.Id, 1, tymcLocations.Select(l => new BatchLocationResult(l.Id, 2)).ToList(), 1);

        var finalSample = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(SampleStatus.UnderReview, finalSample.Status);
    }

    // Backdates an open Incubation's StartedAt so a minimum-duration gate
    // reads as already elapsed, without needing the test to actually wait.
    private static async Task BackdateOpenIncubationAsync(MicroLimsDbContext db, int testOrderId, int hours)
    {
        var incubation = await db.Incubations.Where(i => i.TestOrderId == testOrderId && i.CompletedAt == null)
            .OrderByDescending(i => i.StartedAt).FirstAsync();
        incubation.StartedAt = DateTime.UtcNow.AddHours(-hours);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CloseCurrentIncubationWindowAsync_BeforeMinimumDuration_Throws()
    {
        await using var db = NewDb();
        var dept = new Department { Name = "Filling" };
        var room = new Room { Name = "Room A", Department = dept, GradeClassification = "A" };
        db.Departments.Add(dept);
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var config = new RoomTestConfiguration { RoomId = room.Id, TestType = "PassiveAirSample", TestCode = "TAMC", AlertLimit = "1", ActionLimit = "3", SpecLimit = "5" };
        db.RoomTestConfigurations.Add(config);
        await db.SaveChangesAsync();

        var (window1Media, _, equipment1, _) = await SeedTwoWindowCountTestWorkflowAsync(db, "TAMC");

        var emEngine = new EMWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await emEngine.ReceiveAsync(new EMReceiveRequest(dept.Id, 0, "Analyst", "CTRL-6", 1));
        var prepared = await emEngine.PrepareAsync(sample.Id, new List<int> { config.Id }, 1);
        var order = prepared.TestOrders.Single();

        var workflowEngine = NewTestWorkflowEngine(db);
        await workflowEngine.SelectMediaAsync(order.Id, "CountIncubation", window1Media.Id, equipment1.Id, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => workflowEngine.CloseCurrentIncubationWindowAsync(order.Id, 1));
    }

    [Fact]
    public async Task TwoWindowHappyPath_AdvancesThroughBothWindowsThenRecordsResults()
    {
        await using var db = NewDb();
        var dept = new Department { Name = "Filling" };
        var room = new Room { Name = "Room A", Department = dept, GradeClassification = "A" };
        db.Departments.Add(dept);
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var config = new RoomTestConfiguration { RoomId = room.Id, TestType = "PassiveAirSample", TestCode = "TAMC", AlertLimit = "1", ActionLimit = "3", SpecLimit = "5" };
        db.RoomTestConfigurations.Add(config);
        await db.SaveChangesAsync();

        var (window1Media, window2Media, equipment1, equipment2) = await SeedTwoWindowCountTestWorkflowAsync(db, "TAMC");

        var emEngine = new EMWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await emEngine.ReceiveAsync(new EMReceiveRequest(dept.Id, 0, "Analyst", "CTRL-7", 1));
        var prepared = await emEngine.PrepareAsync(sample.Id, new List<int> { config.Id }, 1);
        var order = prepared.TestOrders.Single();

        var workflowEngine = NewTestWorkflowEngine(db);

        // Window 1: 32.5°C-equivalent, minimum 72 hours.
        await workflowEngine.SelectMediaAsync(order.Id, "CountIncubation", window1Media.Id, equipment1.Id, 1);
        await BackdateOpenIncubationAsync(db, order.Id, hours: 73);
        await workflowEngine.CloseCurrentIncubationWindowAsync(order.Id, 1);

        // Window 2: 22.5°C-equivalent, minimum 48 hours - opened via the
        // same generic SelectMediaAsync used for window 1.
        await workflowEngine.SelectMediaAsync(order.Id, "transfer", window2Media.Id, equipment2.Id, 1);

        // Can't record results before window 2's own minimum has elapsed.
        var locations = await workflowEngine.GetLocationsAsync(order.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflowEngine.RecordBatchResultsAsync(order.Id, 1, locations.Select(l => new BatchLocationResult(l.Id, 0)).ToList(), 1));

        await BackdateOpenIncubationAsync(db, order.Id, hours: 49);
        var result = await workflowEngine.RecordBatchResultsAsync(order.Id, 1, locations.Select(l => new BatchLocationResult(l.Id, 0)).ToList(), 1);

        Assert.True(result.AllStepsComplete);
        var reloadedOrder = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.Equal(WorkflowStep.Ready, reloadedOrder.CurrentStep);

        var incubations = await db.Incubations.Where(i => i.TestOrderId == order.Id).ToListAsync();
        Assert.Equal(2, incubations.Count);
        Assert.All(incubations, i => Assert.NotNull(i.CompletedAt));
    }

    [Fact]
    public async Task RecordBatchResultsAsync_OnNonFinalWindow_Throws()
    {
        await using var db = NewDb();
        var dept = new Department { Name = "Filling" };
        var room = new Room { Name = "Room A", Department = dept, GradeClassification = "A" };
        db.Departments.Add(dept);
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var config = new RoomTestConfiguration { RoomId = room.Id, TestType = "PassiveAirSample", TestCode = "TAMC", AlertLimit = "1", ActionLimit = "3", SpecLimit = "5" };
        db.RoomTestConfigurations.Add(config);
        await db.SaveChangesAsync();

        var (window1Media, _, equipment1, _) = await SeedTwoWindowCountTestWorkflowAsync(db, "TAMC");

        var emEngine = new EMWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await emEngine.ReceiveAsync(new EMReceiveRequest(dept.Id, 0, "Analyst", "CTRL-8", 1));
        var prepared = await emEngine.PrepareAsync(sample.Id, new List<int> { config.Id }, 1);
        var order = prepared.TestOrders.Single();

        var workflowEngine = NewTestWorkflowEngine(db);
        await workflowEngine.SelectMediaAsync(order.Id, "CountIncubation", window1Media.Id, equipment1.Id, 1);

        var locations = await workflowEngine.GetLocationsAsync(order.Id);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflowEngine.RecordBatchResultsAsync(order.Id, 1, locations.Select(l => new BatchLocationResult(l.Id, 0)).ToList(), 1));
        Assert.Contains("not the final incubation window", ex.Message);
    }

    [Fact]
    public async Task RecordBatchPathogenResultsAsync_ObservationStep_SetsDetectedAbsentPerLocation()
    {
        await using var db = NewDb();
        var dept = new Department { Name = "Filling" };
        var roomA = new Room { Name = "Room A", Department = dept, GradeClassification = "A" };
        var roomB = new Room { Name = "Room B", Department = dept, GradeClassification = "A" };
        db.Departments.Add(dept);
        db.Rooms.AddRange(roomA, roomB);
        await db.SaveChangesAsync();

        var configA = new RoomTestConfiguration { RoomId = roomA.Id, TestType = "PassiveAirSample", TestCode = "E.coli", AlertLimit = "0", ActionLimit = "0", SpecLimit = "0" };
        var configB = new RoomTestConfiguration { RoomId = roomB.Id, TestType = "PassiveAirSample", TestCode = "E.coli", AlertLimit = "0", ActionLimit = "0", SpecLimit = "0" };
        db.RoomTestConfigurations.AddRange(configA, configB);
        await db.SaveChangesAsync();

        var (media, equipment) = await SeedPathogenWorkflowAsync(db, "E.coli", isDualPlate: false);

        var emEngine = new EMWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await emEngine.ReceiveAsync(new EMReceiveRequest(dept.Id, 0, "Analyst", "CTRL-9", 1));
        var prepared = await emEngine.PrepareAsync(sample.Id, new List<int> { configA.Id, configB.Id }, 1);
        var order = prepared.TestOrders.Single();

        var workflowEngine = NewTestWorkflowEngine(db);
        await workflowEngine.SelectMediaAsync(order.Id, "Detection", media.Id, equipment.Id, 1);

        var locations = await workflowEngine.GetLocationsAsync(order.Id);
        var observations = new List<BatchLocationObservation>
        {
            new(locations[0].Id, GrowthObserved: true),
            new(locations[1].Id, GrowthObserved: false)
        };

        var result = await workflowEngine.RecordBatchPathogenResultsAsync(order.Id, observations, null, 1);
        Assert.Equal("Detected", result.FinalResult); // overall result is Detected if ANY location is

        var reloaded = await db.SampleLocations.Where(l => l.TestOrderId == order.Id).ToListAsync();
        Assert.Contains(reloaded, l => l.Status == "Detected");
        Assert.Contains(reloaded, l => l.Status == "Absent");

        var reloadedOrder = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.Equal(WorkflowStep.Ready, reloadedOrder.CurrentStep);
    }

    [Fact]
    public async Task RecordBatchPathogenResultsAsync_DualPlateInconclusiveAtOneLocation_ThrowsAndRejectsWholeSubmission()
    {
        await using var db = NewDb();
        var dept = new Department { Name = "Filling" };
        var roomA = new Room { Name = "Room A", Department = dept, GradeClassification = "A" };
        var roomB = new Room { Name = "Room B", Department = dept, GradeClassification = "A" };
        db.Departments.Add(dept);
        db.Rooms.AddRange(roomA, roomB);
        await db.SaveChangesAsync();

        var configA = new RoomTestConfiguration { RoomId = roomA.Id, TestType = "PassiveAirSample", TestCode = "Salmonella", AlertLimit = "0", ActionLimit = "0", SpecLimit = "0" };
        var configB = new RoomTestConfiguration { RoomId = roomB.Id, TestType = "PassiveAirSample", TestCode = "Salmonella", AlertLimit = "0", ActionLimit = "0", SpecLimit = "0" };
        db.RoomTestConfigurations.AddRange(configA, configB);
        await db.SaveChangesAsync();

        var (media, equipment) = await SeedPathogenWorkflowAsync(db, "Salmonella", isDualPlate: true);

        var emEngine = new EMWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await emEngine.ReceiveAsync(new EMReceiveRequest(dept.Id, 0, "Analyst", "CTRL-10", 1));
        var prepared = await emEngine.PrepareAsync(sample.Id, new List<int> { configA.Id, configB.Id }, 1);
        var order = prepared.TestOrders.Single();

        var workflowEngine = NewTestWorkflowEngine(db);
        await workflowEngine.SelectMediaAsync(order.Id, "Detection", media.Id, equipment.Id, 1);

        var locations = await workflowEngine.GetLocationsAsync(order.Id);
        var dualPlate = new List<BatchLocationDualPlateObservation>
        {
            new(locations[0].Id, Plate1GrowthObserved: true, Plate2GrowthObserved: true), // agrees - Detected
            new(locations[1].Id, Plate1GrowthObserved: true, Plate2GrowthObserved: false)  // disagrees - inconclusive
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflowEngine.RecordBatchPathogenResultsAsync(order.Id, null, dualPlate, 1));
        Assert.Contains(locations[1].RoomTestConfiguration!.Room!.Name, ex.Message);

        // Rejected as a whole - even the agreeing location must not be saved.
        var reloaded = await db.SampleLocations.Where(l => l.TestOrderId == order.Id).ToListAsync();
        Assert.All(reloaded, l => Assert.Null(l.Status));
    }
}
