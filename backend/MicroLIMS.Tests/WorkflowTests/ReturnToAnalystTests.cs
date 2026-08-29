using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class ReturnToAnalystTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<(TestOrder order, User analyst, User reviewer, Media media)> SeedCompletedTamcOrderAsync(MicroLimsDbContext db)
    {
        var analyst = new User { Username = "analyst1", FullName = "Analyst One", Email = "analyst1@microlims.local" };
        var reviewer = new User { Username = "reviewer1", FullName = "Reviewer One", Email = "reviewer1@microlims.local" };
        db.Users.AddRange(analyst, reviewer);
        await db.SaveChangesAsync();

        var testDefinition = new TestDefinition
        {
            Code = "TAMC",
            DisplayName = "Total Aerobic Microbial Count",
            WorkflowType = WorkflowType.CountTest
        };
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        var countStep = new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id,
            StepOrder = 1,
            StepName = "CountIncubation",
            IncubationMinHours = 72,
            IncubationMaxHours = 120,
            TemperatureMin = 30,
            TemperatureMax = 35,
            IsFinalStep = true,
            StepType = StepType.PlateCount
        };
        db.TestWorkflowSteps.Add(countStep);

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = "TSA Powder",
            ManufacturerName = "Himedia",
            BatchNumber = "LOT-001",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            Code = "TSA",
            Location = "Micro Lab",
            QuantityReceived = 500,
            QuantityRemaining = 500,
            Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia
        {
            TestWorkflowStepId = countStep.Id,
            MaterialId = material.Id,
            TempMin = 30,
            TempMax = 35,
            IncubationMinHours = 72,
            IncubationMaxHours = 120
        });

        var media = new Media
        {
            MaterialId = material.Id,
            LotNumber = "TSA/1/26",
            IsReleasedForUse = true,
            Status = MediaStatus.Active,
            ExpiryDate = DateTime.UtcNow.AddDays(30)
        };
        db.Media.Add(media);

        db.Equipment.Add(new Equipment
        {
            Name = "Incubator",
            Code = "INC-1",
            Type = EquipmentType.Incubator,
            SetPointTemperature = 32
        });

        var point = new WaterSamplingPoint
        {
            Code = "WP-01",
            Location = "Utility Room",
            AssignedTestCodes = new() { "TAMC" }
        };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        db.SamplingConfigurations.Add(new SamplingConfiguration
        {
            WaterSamplingPointId = point.Id,
            TestCode = "TAMC",
            AlertLimit = "10",
            ActionLimit = "50",
            SpecLimit = "100"
        });

        var cause = new CauseOfTesting { Name = "Routine" };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            Category = SampleCategory.Water,
            WaterSamplingPointId = point.Id,
            ControlNumber = "CTRL-1",
            ReferenceNumber = "WP2608001",
            SampledBy = "Sampler",
            CauseOfTestingId = cause.Id,
            Status = SampleStatus.Received
        };
        var order = new TestOrder
        {
            TestCode = "TAMC",
            AssignedAnalystId = analyst.Id,
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        };
        sample.TestOrders.Add(order);

        var prep = new SamplePreparation
        {
            Sample = sample,
            Amount = 1m,
            Unit = "mL",
            Technique = "PourPlate",
            PreparedByUserId = analyst.Id,
            NeutralizerId = 1,
            DiluentTypeId = 1
        };
        db.SamplePreparations.Add(prep);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);

        // 1. Select media (starts incubation)
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: analyst.Id);

        // 2. Record initial result
        var payload = new CountTestPayload(new List<string> { "25", "30" }, 1m);
        await engine.RecordResultAsync(order.Id, "CountIncubation", payload, analyst.Id);

        return (order, analyst, reviewer, media);
    }

    [Fact]
    public async Task ReturnToAnalyst_RevertsCountTestAndSupersedesReading()
    {
        await using var db = NewDb();
        var (order, analyst, reviewer, _) = await SeedCompletedTamcOrderAsync(db);

        // Pre-conditions
        Assert.Equal(WorkflowStep.Ready, order.CurrentStep);
        Assert.Equal(ApprovalStatus.ResultEntered, order.Status);
        var initialReading = await db.CountTestReadings.SingleAsync(r => r.TestOrderId == order.Id);
        Assert.True(initialReading.IsActive);
        var initialIncubation = await db.Incubations.SingleAsync(i => i.TestOrderId == order.Id);
        Assert.NotNull(initialIncubation.CompletedAt);
        Assert.NotNull(initialIncubation.Outcome);

        var engine = TestServiceFactory.TestWorkflow(db);
        var currentBefore = await engine.GetCurrentStepAsync(order.Id);
        Assert.True(currentBefore.AllStepsComplete);

        var reviewService = TestServiceFactory.Review(db);
        var returnEvent = await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, "Plate count recount requested due to bubble artifact");

        // Assertions on returned state
        Assert.NotNull(returnEvent);
        Assert.Equal(order.Id, returnEvent.TestOrderId);
        Assert.Equal(reviewer.Id, returnEvent.ReviewerUserId);
        Assert.Equal(analyst.Id, returnEvent.AssignedAnalystId);
        Assert.Equal("Plate count recount requested due to bubble artifact", returnEvent.Reason);

        // Old reading is soft-superseded (IsActive = false)
        await db.Entry(initialReading).ReloadAsync();
        Assert.False(initialReading.IsActive);

        // TestOrder state reverted to Incubating and InProgress
        await db.Entry(order).ReloadAsync();
        Assert.Equal(WorkflowStep.Incubating, order.CurrentStep);
        Assert.Equal(ApprovalStatus.InProgress, order.Status);
        Assert.Equal(analyst.Id, order.AssignedAnalystId); // Analyst unchanged

        // Incubation row reopened (CompletedAt cleared)
        await db.Entry(initialIncubation).ReloadAsync();
        Assert.Null(initialIncubation.CompletedAt);
        Assert.Null(initialIncubation.Outcome);

        // GetCurrentStepAsync no longer reports "all complete" and points to the step
        var currentAfter = await engine.GetCurrentStepAsync(order.Id);
        Assert.False(currentAfter.AllStepsComplete);
        Assert.NotNull(currentAfter.Step);
        Assert.Equal("CountIncubation", currentAfter.Step.StepName);
        Assert.NotNull(currentAfter.OpenIncubation);
        Assert.Equal(initialIncubation.Id, currentAfter.OpenIncubation.Id);
    }

    [Fact]
    public async Task ReturnToAnalyst_AllowsAnalystToRecordNewResultAndCompleteWorkflow()
    {
        await using var db = NewDb();
        var (order, analyst, reviewer, _) = await SeedCompletedTamcOrderAsync(db);

        var reviewService = TestServiceFactory.Review(db);
        await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, "Recount plates");

        var engine = TestServiceFactory.TestWorkflow(db);

        // Analyst records new corrected result for the reopened step
        var correctedPayload = new CountTestPayload(new List<string> { "20", "22" }, 1m);
        var recordResult = await engine.RecordResultAsync(order.Id, "CountIncubation", correctedPayload, analyst.Id);

        Assert.True(recordResult.AllStepsComplete);

        // Verify two CountTestReading rows exist: one inactive, one active
        var allReadings = await db.CountTestReadings.Where(r => r.TestOrderId == order.Id).OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(2, allReadings.Count);
        Assert.False(allReadings[0].IsActive); // Original reading superseded
        Assert.True(allReadings[1].IsActive);  // New reading active
        Assert.Equal("21 CFU/mL", allReadings[1].ReportedResult);

        // Workflow state back to Ready / ResultEntered
        await db.Entry(order).ReloadAsync();
        Assert.Equal(WorkflowStep.Ready, order.CurrentStep);
        Assert.Equal(ApprovalStatus.ResultEntered, order.Status);

        // Incubation is closed again
        var incubation = await db.Incubations.SingleAsync(i => i.TestOrderId == order.Id);
        Assert.NotNull(incubation.CompletedAt);
        Assert.Equal("21 CFU/mL", incubation.Outcome);

        // GetCurrentStepAsync reports all complete again with the corrected result
        var currentAfter = await engine.GetCurrentStepAsync(order.Id);
        Assert.True(currentAfter.AllStepsComplete);
    }

    [Fact]
    public async Task ReturnToAnalyst_PathogenWorkflow_Rejected()
    {
        await using var db = NewDb();

        var pathogenDef = new TestDefinition
        {
            Code = "SALM",
            DisplayName = "Salmonella Test",
            WorkflowType = WorkflowType.Observation
        };
        db.TestDefinitions.Add(pathogenDef);

        var sample = new Sample { Category = SampleCategory.RawMaterial, Status = SampleStatus.InTesting };
        var order = new TestOrder
        {
            TestCode = "SALM",
            Status = ApprovalStatus.ResultEntered,
            CurrentStep = WorkflowStep.Ready
        };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var reviewService = TestServiceFactory.Review(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reviewService.ReturnToAnalystAsync(order.Id, reviewerId: 99, "Try again"));

        Assert.Contains("only supported for Count Test workflows", ex.Message);
    }

    [Fact]
    public async Task ReturnToAnalyst_NonResultEnteredStatus_Rejected()
    {
        await using var db = NewDb();

        var testDef = new TestDefinition
        {
            Code = "TAMC",
            DisplayName = "Total Aerobic Microbial Count",
            WorkflowType = WorkflowType.CountTest
        };
        db.TestDefinitions.Add(testDef);

        var sample = new Sample { Category = SampleCategory.Water, Status = SampleStatus.InTesting };
        var order = new TestOrder
        {
            TestCode = "TAMC",
            Status = ApprovalStatus.InProgress,
            CurrentStep = WorkflowStep.Incubating
        };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var reviewService = TestServiceFactory.Review(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reviewService.ReturnToAnalystAsync(order.Id, reviewerId: 99, "Premature return"));

        Assert.Contains("Only test orders in ResultEntered status can be returned", ex.Message);
    }

    [Fact]
    public async Task ReturnToAnalyst_ReturnEventIsQueryableWithAttribution()
    {
        await using var db = NewDb();
        var (order, analyst, reviewer, _) = await SeedCompletedTamcOrderAsync(db);

        var reviewService = TestServiceFactory.Review(db);
        var beforeTime = DateTime.UtcNow.AddSeconds(-1);
        await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, "Re-verify plates");
        var afterTime = DateTime.UtcNow.AddSeconds(1);

        // Query return events for this analyst in the date range
        var count = await db.TestReturnEvents
            .Where(e => e.AssignedAnalystId == analyst.Id && e.ReturnedAt >= beforeTime && e.ReturnedAt <= afterTime)
            .CountAsync();

        Assert.Equal(1, count);

        var returnRecord = await db.TestReturnEvents
            .Include(e => e.TestOrder)
            .SingleAsync(e => e.TestOrderId == order.Id);

        Assert.Equal(order.Id, returnRecord.TestOrderId);
        Assert.Equal(reviewer.Id, returnRecord.ReviewerUserId);
        Assert.Equal(analyst.Id, returnRecord.AssignedAnalystId);
        Assert.Equal("Re-verify plates", returnRecord.Reason);
        Assert.InRange(returnRecord.ReturnedAt, beforeTime, afterTime);
    }

    [Fact]
    public async Task ReturnToAnalyst_WithoutReason_SucceedsWithNullReason()
    {
        await using var db = NewDb();
        var (order, analyst, reviewer, _) = await SeedCompletedTamcOrderAsync(db);

        var reviewService = TestServiceFactory.Review(db);
        var returnEvent = await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, null);

        Assert.Null(returnEvent.Reason);
        Assert.Equal(analyst.Id, returnEvent.AssignedAnalystId);
        Assert.Equal(reviewer.Id, returnEvent.ReviewerUserId);
    }

    [Fact]
    public async Task ReturnToAnalyst_ParentSampleUnderReview_RevertsToInTesting()
    {
        await using var db = NewDb();
        var (order, _, reviewer, _) = await SeedCompletedTamcOrderAsync(db);

        var sample = await db.Samples.FirstAsync(s => s.Id == order.SampleId);
        sample.Status = SampleStatus.UnderReview;
        await db.SaveChangesAsync();

        var reviewService = TestServiceFactory.Review(db);
        await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, "Need recount");

        await db.Entry(sample).ReloadAsync();
        Assert.Equal(SampleStatus.InTesting, sample.Status);
    }

    [Fact]
    public async Task Notification_TestReturnedForRevision_FiresWithReasonAndSampleDetails()
    {
        await using var db = NewDb();
        var (order, analyst, reviewer, _) = await SeedCompletedTamcOrderAsync(db);

        var sample = await db.Samples.FirstAsync(s => s.Id == order.SampleId);
        sample.ReferenceNumber = "FP0107026";
        await db.SaveChangesAsync();

        var reviewService = TestServiceFactory.Review(db);
        await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, "Plate count recount requested due to bubble artifact");

        var notificationService = TestServiceFactory.DashboardNotification(db);

        // Fetch notifications for the assigned analyst
        var notifications = await notificationService.GetNotificationsAsync(RoleType.Analyst, analyst.Id);

        var returnNotification = notifications.FirstOrDefault(n => n.Type == "TestReturnedForRevision");
        Assert.NotNull(returnNotification);
        Assert.Equal("warning", returnNotification.Severity);
        Assert.Equal("Test TAMC for sample FP0107026 was returned for revision: Plate count recount requested due to bubble artifact", returnNotification.Message);

        // Other analyst should not receive the notification
        var otherNotifications = await notificationService.GetNotificationsAsync(RoleType.Analyst, 999);
        Assert.DoesNotContain(otherNotifications, n => n.Type == "TestReturnedForRevision");
    }

    [Fact]
    public async Task Notification_TestReturnedForRevision_WithoutReason_GeneratesGenericMessage()
    {
        await using var db = NewDb();
        var (order, analyst, reviewer, _) = await SeedCompletedTamcOrderAsync(db);

        var sample = await db.Samples.FirstAsync(s => s.Id == order.SampleId);
        sample.ReferenceNumber = "WP2608001";
        await db.SaveChangesAsync();

        var reviewService = TestServiceFactory.Review(db);
        await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, null);

        var notificationService = TestServiceFactory.DashboardNotification(db);

        var notifications = await notificationService.GetNotificationsAsync(RoleType.Analyst, analyst.Id);
        var returnNotification = notifications.FirstOrDefault(n => n.Type == "TestReturnedForRevision");
        Assert.NotNull(returnNotification);
        Assert.Equal("warning", returnNotification.Severity);
        Assert.Equal("Test TAMC for sample WP2608001 was returned for revision.", returnNotification.Message);
    }

    [Fact]
    public async Task Notification_TestReturnedForRevision_DeduplicatesAcrossMultiplePolls()
    {
        await using var db = NewDb();
        var (order, analyst, reviewer, _) = await SeedCompletedTamcOrderAsync(db);

        var sample = await db.Samples.FirstAsync(s => s.Id == order.SampleId);
        sample.ReferenceNumber = "FP0107026";
        await db.SaveChangesAsync();

        var reviewService = TestServiceFactory.Review(db);
        await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, "Recount required");

        var spyPush = new SpyNotificationService();
        var notificationService = TestServiceFactory.DashboardNotification(db, spyPush);

        // First poll
        var poll1 = await notificationService.GetNotificationsAsync(RoleType.Analyst, analyst.Id);
        Assert.Contains(poll1, n => n.Type == "TestReturnedForRevision");
        Assert.Single(spyPush.Sent.Where(s => s.UserId == analyst.Id));

        // Second poll immediately after (simulating periodic 60s polling)
        var poll2 = await notificationService.GetNotificationsAsync(RoleType.Analyst, analyst.Id);
        Assert.Contains(poll2, n => n.Type == "TestReturnedForRevision");

        // Assert no duplicate push notification was sent
        Assert.Single(spyPush.Sent.Where(s => s.UserId == analyst.Id));

        // Assert exactly one NotificationLog row exists in DB for this user and event
        var persistedLogs = await db.NotificationLogs
            .Where(n => n.UserId == analyst.Id && n.Type == "TestReturnedForRevision")
            .ToListAsync();
        Assert.Single(persistedLogs);
    }

    [Fact]
    public async Task TestReturnHelper_DerivesPendingReturnCorrectly_AcrossLifecycle()
    {
        await using var db = NewDb();
        var (order, analyst, reviewer, _) = await SeedCompletedTamcOrderAsync(db);

        // 1. Before return: No return events exist -> GetPendingReturnAsync is null
        var pendingBefore = await TestReturnHelper.GetPendingReturnAsync(db, order.Id);
        Assert.Null(pendingBefore);

        // 2. Immediately after return: Return event exists & zero active readings -> pending is true
        var reviewService = TestServiceFactory.Review(db);
        await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, "Recount plates due to artifact");

        var pendingAfterReturn = await TestReturnHelper.GetPendingReturnAsync(db, order.Id);
        Assert.NotNull(pendingAfterReturn);
        Assert.Equal("Recount plates due to artifact", pendingAfterReturn.Reason);
        Assert.True(pendingAfterReturn.ReturnedAt <= DateTime.UtcNow);

        // Batch helper also returns this order
        var batchReturns = await TestReturnHelper.GetPendingReturnsForOrdersAsync(db, new[] { order.Id, 999 });
        Assert.True(batchReturns.ContainsKey(order.Id));
        Assert.False(batchReturns.ContainsKey(999));
        Assert.Equal("Recount plates due to artifact", batchReturns[order.Id].Reason);

        // 3. After analyst submits a new result: New active CountTestReading exists -> pending is false
        var engine = TestServiceFactory.TestWorkflow(db);
        var correctedPayload = new CountTestPayload(new List<string> { "22", "24" }, 1m);
        await engine.RecordResultAsync(order.Id, "CountIncubation", correctedPayload, analyst.Id);

        var pendingAfterResubmit = await TestReturnHelper.GetPendingReturnAsync(db, order.Id);
        Assert.Null(pendingAfterResubmit);

        var batchReturnsAfterResubmit = await TestReturnHelper.GetPendingReturnsForOrdersAsync(db, new[] { order.Id });
        Assert.Empty(batchReturnsAfterResubmit);

        // 4. If returned a SECOND time with a different reason, latest reason is derived
        await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, "Second return: check dilution factor");
        var pendingSecondReturn = await TestReturnHelper.GetPendingReturnAsync(db, order.Id);
        Assert.NotNull(pendingSecondReturn);
        Assert.Equal("Second return: check dilution factor", pendingSecondReturn.Reason);
    }

    [Fact]
    public async Task GetCurrentStep_PopulatesReturnInfoWhenPending_AndNullOtherwise()
    {
        await using var db = NewDb();
        var (order, analyst, reviewer, _) = await SeedCompletedTamcOrderAsync(db);

        var engine = TestServiceFactory.TestWorkflow(db);
        var eligibility = TestServiceFactory.IncubatorEligibility(db);
        var snapshot = TestServiceFactory.AppearanceSnapshot(db);
        var controller = new TestWorkflowController(engine, db, eligibility, snapshot);

        // Before return: returnInfo is null
        var actionResultBefore = await controller.GetCurrentStep(order.Id);
        var okBefore = Assert.IsType<OkObjectResult>(actionResultBefore);
        var responseBefore = Assert.IsType<ApiResponse<object>>(okBefore.Value);
        var returnInfoBefore = responseBefore.Data!.GetType().GetProperty("returnInfo")?.GetValue(responseBefore.Data);
        Assert.Null(returnInfoBefore);

        // Reviewer returns test order
        var reviewService = TestServiceFactory.Review(db);
        await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, "Recount plates 1 and 2");

        // After return: returnInfo is present with reason and returnedAt
        var actionResultAfterReturn = await controller.GetCurrentStep(order.Id);
        var okAfterReturn = Assert.IsType<OkObjectResult>(actionResultAfterReturn);
        var responseAfterReturn = Assert.IsType<ApiResponse<object>>(okAfterReturn.Value);
        var returnInfoAfterReturn = responseAfterReturn.Data!.GetType().GetProperty("returnInfo")?.GetValue(responseAfterReturn.Data);
        Assert.NotNull(returnInfoAfterReturn);

        var reason = returnInfoAfterReturn.GetType().GetProperty("reason")?.GetValue(returnInfoAfterReturn) as string;
        var returnedAt = (DateTime?)returnInfoAfterReturn.GetType().GetProperty("returnedAt")?.GetValue(returnInfoAfterReturn);
        Assert.Equal("Recount plates 1 and 2", reason);
        Assert.NotNull(returnedAt);

        // Analyst resubmits result
        var correctedPayload = new CountTestPayload(new List<string> { "25", "25" }, 1m);
        await engine.RecordResultAsync(order.Id, "CountIncubation", correctedPayload, analyst.Id);

        // After resubmission: returnInfo is null again
        var actionResultAfterResubmit = await controller.GetCurrentStep(order.Id);
        var okAfterResubmit = Assert.IsType<OkObjectResult>(actionResultAfterResubmit);
        var responseAfterResubmit = Assert.IsType<ApiResponse<object>>(okAfterResubmit.Value);
        var returnInfoAfterResubmit = responseAfterResubmit.Data!.GetType().GetProperty("returnInfo")?.GetValue(responseAfterResubmit.Data);
        Assert.Null(returnInfoAfterResubmit);
    }

    [Fact]
    public async Task Dashboard_GetTodaysWork_FlagsReturnedCountTest()
    {
        await using var db = NewDb();
        var (order, analyst, reviewer, _) = await SeedCompletedTamcOrderAsync(db);

        // Ensure sample was received today
        var sample = await db.Samples.FirstAsync(s => s.Id == order.SampleId);
        sample.ReceivedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var dashboardService = TestServiceFactory.Dashboard(db);

        // Before return
        var workBefore = await dashboardService.GetTodaysWorkAsync(RoleType.Analyst, analyst.Id);
        var itemBefore = Assert.Single(workBefore);
        var testBefore = Assert.Single(itemBefore.Tests);
        Assert.False(testBefore.IsReturned);
        Assert.Null(testBefore.ReturnReason);

        // Return order
        var reviewService = TestServiceFactory.Review(db);
        await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, "TNTC threshold check required");

        // After return
        var workAfter = await dashboardService.GetTodaysWorkAsync(RoleType.Analyst, analyst.Id);
        var itemAfter = Assert.Single(workAfter);
        var testAfter = Assert.Single(itemAfter.Tests);
        Assert.True(testAfter.IsReturned);
        Assert.Equal("TNTC threshold check required", testAfter.ReturnReason);

        // After resubmitting
        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new CountTestPayload(new List<string> { "30", "32" }, 1m);
        await engine.RecordResultAsync(order.Id, "CountIncubation", payload, analyst.Id);

        var workAfterResubmit = await dashboardService.GetTodaysWorkAsync(RoleType.Analyst, analyst.Id);
        var itemAfterResubmit = Assert.Single(workAfterResubmit);
        var testAfterResubmit = Assert.Single(itemAfterResubmit.Tests);
        Assert.False(testAfterResubmit.IsReturned);
    }

    [Fact]
    public async Task MyTasksService_GeneratesUrgentReviseTaskForReturnedOrder()
    {
        await using var db = NewDb();
        var (order, analyst, reviewer, _) = await SeedCompletedTamcOrderAsync(db);

        var myTasksService = TestServiceFactory.MyTasks(db);

        // Before return: initial incubation was completed, so 0 open incubation tasks
        var tasksBefore = await myTasksService.GetMyTasksAsync(analyst.Id);
        Assert.Empty(tasksBefore);

        // Return order
        var reviewService = TestServiceFactory.Review(db);
        await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, "Colony count verification");

        // After return: My Tasks contains an urgent task to revise the test
        var tasksAfter = await myTasksService.GetMyTasksAsync(analyst.Id);
        var task = Assert.Single(tasksAfter);
        Assert.Equal("Revise Test", task.TaskType);
        Assert.Equal(TaskUrgency.Overdue, task.Urgency);
        Assert.True(task.IsReturned);
        Assert.Equal("Colony count verification", task.ReturnReason);
        Assert.Equal(order.Id, task.TestOrderId);
        Assert.Contains("Revise TAMC", task.Title);
        Assert.Contains("Returned: Colony count verification", task.Subtitle);

        // After resubmission: task is cleared
        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new CountTestPayload(new List<string> { "28", "29" }, 1m);
        await engine.RecordResultAsync(order.Id, "CountIncubation", payload, analyst.Id);

        var tasksAfterResubmit = await myTasksService.GetMyTasksAsync(analyst.Id);
        Assert.Empty(tasksAfterResubmit);
    }

    [Fact]
    public async Task KpiService_GetReturnToAnalystCountAsync_IncrementsWhenReturnToAnalystExecuted()
    {
        await using var db = NewDb();
        var (order, analyst, reviewer, _) = await SeedCompletedTamcOrderAsync(db);

        var fromDate = DateTime.UtcNow.AddDays(-7);
        var toDate = DateTime.UtcNow.AddDays(1);

        var kpiService = TestServiceFactory.Kpi(db);

        // Initial count for analyst is 0
        var initialCounts = await kpiService.GetReturnToAnalystCountAsync(analyst.Id, fromDate, toDate);
        Assert.Equal(0, initialCounts[analyst.Id]);

        // Reviewer returns count test order
        var reviewService = TestServiceFactory.Review(db);
        await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, "Recount required");

        // Count for analyst increments to 1
        var countsAfterReturn = await kpiService.GetReturnToAnalystCountAsync(analyst.Id, fromDate, toDate);
        Assert.Equal(1, countsAfterReturn[analyst.Id]);

        // Other analyst ID returns 0
        var otherAnalystCounts = await kpiService.GetReturnToAnalystCountAsync(999, fromDate, toDate);
        Assert.Equal(0, otherAnalystCounts[999]);
    }
}
