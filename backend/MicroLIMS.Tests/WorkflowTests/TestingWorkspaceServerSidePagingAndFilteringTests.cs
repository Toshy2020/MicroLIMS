using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class TestingWorkspaceServerSidePagingAndFilteringTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static CauseOfTesting SeedCause(MicroLimsDbContext db, int id = 1, string name = "Routine Release")
    {
        var existing = db.CausesOfTesting.Find(id);
        if (existing != null) return existing;
        var cause = new CauseOfTesting { Id = id, Name = name };
        db.CausesOfTesting.Add(cause);
        db.SaveChanges();
        return cause;
    }

    private static Sample CreateSample(
        int id,
        string controlNumber,
        CauseOfTesting cause,
        SampleCategory category = SampleCategory.FinishedProduct,
        SampleStatus status = SampleStatus.Received,
        DateTime? receivedAt = null,
        string? referenceNumber = null)
    {
        return new Sample
        {
            Id = id,
            ReferenceNumber = referenceNumber ?? $"REF-{id:D3}",
            ControlNumber = controlNumber,
            Category = category,
            Status = status,
            CauseOfTestingId = cause.Id,
            CauseOfTesting = cause,
            ReceivedAt = receivedAt ?? DateTime.UtcNow,
            SampledBy = "Sampler"
        };
    }

    [Fact]
    public async Task Paging_EnforcesServerSideClampingAndDefaultSize()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);

        var baseTime = DateTime.UtcNow;
        for (int i = 1; i <= 60; i++)
        {
            db.Samples.Add(CreateSample(i, $"CTRL-{i:D3}", cause, receivedAt: baseTime.AddMinutes(-i)));
        }
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db);

        // Default paging: page = 1, pageSize = 50
        var paged1 = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto());
        Assert.Equal(50, paged1.Items.Count);
        Assert.Equal(60, paged1.TotalCount);
        Assert.Equal(1, paged1.Page);
        Assert.Equal(50, paged1.PageSize);
        Assert.Equal(2, paged1.TotalPages);

        // Page 2
        var paged2 = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Page = 2, PageSize = 50 });
        Assert.Equal(10, paged2.Items.Count);
        Assert.Equal(60, paged2.TotalCount);
        Assert.Equal(2, paged2.Page);

        // Clamping to max 200: even if client asks for 500, pageSize is clamped to 200
        var pagedLarge = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { PageSize = 500 });
        Assert.Equal(200, pagedLarge.PageSize);
        Assert.Equal(60, pagedLarge.Items.Count);
    }

    [Fact]
    public async Task FreeTextSearch_MatchesExpectedFieldsAndCategoryDisplayName()
    {
        await using var db = NewDb();

        var cause1 = SeedCause(db, 1, "Stability Study");
        var cause2 = SeedCause(db, 2, "Environmental Monitoring");
        var item = new Item { Id = 1, Name = "Paracetamol 500mg", Code = "PARA" };
        var machine = new Machine { Id = 1, Name = "Blister Line 4" };
        var waterPoint = new WaterSamplingPoint { Id = 1, Code = "WSP-RO-01", Location = "Purified Water Loop" };
        var dept = new Department { Id = 1, Name = "Aseptic Filling Room" };

        db.Items.Add(item);
        db.Machines.Add(machine);
        db.WaterSamplingPoints.Add(waterPoint);
        db.Departments.Add(dept);

        var s1 = CreateSample(10, "CTRL-10", cause1, SampleCategory.FinishedProduct, referenceNumber: "REF-SPECIAL-1");
        s1.BatchNumber = "BATCH-ALPHA";
        s1.Item = item;
        s1.ItemId = item.Id;
        s1.SampledBy = "John Doe";

        var s2 = CreateSample(20, "CTRL-20", cause2, SampleCategory.AfterCleaning, referenceNumber: "REF-AC-2");
        s2.Machine = machine;
        s2.MachineId = machine.Id;
        s2.SampledBy = "Jane Smith";

        var s3 = CreateSample(30, "CTRL-30", cause2, SampleCategory.Water, referenceNumber: "REF-WATER-3");
        s3.WaterSamplingPoint = waterPoint;
        s3.WaterSamplingPointId = waterPoint.Id;
        s3.SampledBy = "Alice Brown";

        var s4 = CreateSample(40, "CTRL-40", cause2, SampleCategory.EnvironmentalMonitoring, referenceNumber: "REF-EM-4");
        s4.Department = dept;
        s4.DepartmentId = dept.Id;
        s4.SampledBy = "Bob White";

        db.Samples.AddRange(s1, s2, s3, s4);
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db);

        // Search by sampleId
        var resId = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Search = "10" });
        Assert.Contains(resId.Items, x => x.SampleId == 10);

        // Search by referenceNumber
        var resRef = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Search = "SPECIAL" });
        Assert.Single(resRef.Items);
        Assert.Equal(10, resRef.Items[0].SampleId);

        // Search by batchNumber
        var resBatch = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Search = "ALPHA" });
        Assert.Single(resBatch.Items);
        Assert.Equal(10, resBatch.Items[0].SampleId);

        // Search by controlNumber
        var resCtrl = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Search = "CTRL-20" });
        Assert.Single(resCtrl.Items);
        Assert.Equal(20, resCtrl.Items[0].SampleId);

        // Search by sampledBy
        var resSampled = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Search = "Jane Smith" });
        Assert.Single(resSampled.Items);
        Assert.Equal(20, resSampled.Items[0].SampleId);

        // Search by causeOfTesting
        var resCause = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Search = "Stability" });
        Assert.Single(resCause.Items);
        Assert.Equal(10, resCause.Items[0].SampleId);

        // Search by displayName - Item name
        var resItem = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Search = "Paracetamol" });
        Assert.Single(resItem.Items);
        Assert.Equal(10, resItem.Items[0].SampleId);

        // Search by displayName - Machine name (AfterCleaning)
        var resMachine = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Search = "Blister Line" });
        Assert.Single(resMachine.Items);
        Assert.Equal(20, resMachine.Items[0].SampleId);

        // Search by displayName - Water sampling point (Water)
        var resWater = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Search = "WSP-RO" });
        Assert.Single(resWater.Items);
        Assert.Equal(30, resWater.Items[0].SampleId);

        // Search by displayName - Department name (EM)
        var resEm = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Search = "Aseptic" });
        Assert.Single(resEm.Items);
        Assert.Equal(40, resEm.Items[0].SampleId);
    }

    [Fact]
    public async Task CategoryFilter_FiltersCorrectly()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);

        db.Samples.Add(CreateSample(1, "C1", cause, SampleCategory.FinishedProduct));
        db.Samples.Add(CreateSample(2, "C2", cause, SampleCategory.Water));
        db.Samples.Add(CreateSample(3, "C3", cause, SampleCategory.AfterCleaning));
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db);

        var resProduct = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Category = "FinishedProduct" });
        Assert.Single(resProduct.Items);
        Assert.Equal(1, resProduct.Items[0].SampleId);

        var resWater = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Category = "Water" });
        Assert.Single(resWater.Items);
        Assert.Equal(2, resWater.Items[0].SampleId);

        var resAll = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Category = "ALL" });
        Assert.Equal(3, resAll.Items.Count);
    }

    [Fact]
    public async Task SampleStatusFilter_PendingReview_ExpandsToUnderReviewAndUnderApproval()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);

        db.Samples.Add(CreateSample(1, "C1", cause, status: SampleStatus.Received));
        db.Samples.Add(CreateSample(2, "C2", cause, status: SampleStatus.UnderReview));
        db.Samples.Add(CreateSample(3, "C3", cause, status: SampleStatus.UnderApproval));
        db.Samples.Add(CreateSample(4, "C4", cause, status: SampleStatus.Approved));
        db.Samples.Add(CreateSample(5, "C5", cause, status: SampleStatus.RetestRequested));
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db);

        // PendingReview expands to UnderReview and UnderApproval
        var resPending = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { SampleStatus = "PendingReview" });
        Assert.Equal(2, resPending.Items.Count);
        Assert.Contains(resPending.Items, s => s.SampleId == 2);
        Assert.Contains(resPending.Items, s => s.SampleId == 3);

        // RetestRequested matches RetestRequested
        var resRetest = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Status = "RetestRequested" });
        Assert.Single(resRetest.Items);
        Assert.Equal(5, resRetest.Items[0].SampleId);

        // Direct status match
        var resReceived = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { SampleStatus = "Received" });
        Assert.Single(resReceived.Items);
        Assert.Equal(1, resReceived.Items[0].SampleId);
    }

    [Fact]
    public async Task TestStatusFilter_FiltersAppropriately()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);

        var now = DateTime.UtcNow;

        // Sample 1: Waiting / Pending test order
        var s1 = CreateSample(1, "C1", cause, status: SampleStatus.InTesting, receivedAt: now);
        s1.TestOrders.Add(new TestOrder { Id = 1, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });

        // Sample 2: InProgress test order
        var s2 = CreateSample(2, "C2", cause, status: SampleStatus.InTesting, receivedAt: now);
        s2.TestOrders.Add(new TestOrder { Id = 2, TestCode = "TYMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running });

        // Sample 3: ReadyToRead test order (open incubation where ExpectedReadingAt <= now)
        var s3 = CreateSample(3, "C3", cause, status: SampleStatus.InTesting, receivedAt: now);
        var to3 = new TestOrder { Id = 3, TestCode = "TAMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Incubating };
        to3.Incubations.Add(new Incubation { Id = 1, TestOrderId = 3, CompletedAt = null, ExpectedReadingAt = now.AddMinutes(-10) });
        s3.TestOrders.Add(to3);

        // Sample 4: ResultEntered test order
        var s4 = CreateSample(4, "C4", cause, status: SampleStatus.InTesting, receivedAt: now);
        s4.TestOrders.Add(new TestOrder { Id = 4, TestCode = "EC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready });

        // Sample 5: Approved test order
        var s5 = CreateSample(5, "C5", cause, status: SampleStatus.Approved, receivedAt: now);
        s5.TestOrders.Add(new TestOrder { Id = 5, TestCode = "EC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved });

        db.Samples.AddRange(s1, s2, s3, s4, s5);
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db);

        // Waiting / Pending
        var resWaiting = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { TestStatus = "Waiting" });
        Assert.Single(resWaiting.Items);
        Assert.Equal(1, resWaiting.Items[0].SampleId);

        // InProgress
        var resInProgress = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { TestStatus = "InProgress" });
        Assert.Contains(resInProgress.Items, s => s.SampleId == 2);

        // ReadyToRead
        var resReadyToRead = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { TestStatus = "ReadyToRead" });
        Assert.Single(resReadyToRead.Items);
        Assert.Equal(3, resReadyToRead.Items[0].SampleId);

        // ResultEntered / UnderReview
        var resResultEntered = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { TestStatus = "ResultEntered" });
        Assert.Single(resResultEntered.Items);
        Assert.Equal(4, resResultEntered.Items[0].SampleId);

        // Approved
        var resApproved = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { TestStatus = "Approved" });
        Assert.Single(resApproved.Items);
        Assert.Equal(5, resApproved.Items[0].SampleId);
    }

    [Fact]
    public async Task AnalystIdFilter_MatchesAssignedAnalystOnTestOrders()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);

        var s1 = CreateSample(1, "C1", cause, status: SampleStatus.InTesting);
        s1.TestOrders.Add(new TestOrder { Id = 1, TestCode = "TAMC", AssignedAnalystId = 42 });

        var s2 = CreateSample(2, "C2", cause, status: SampleStatus.InTesting);
        s2.TestOrders.Add(new TestOrder { Id = 2, TestCode = "TYMC", AssignedAnalystId = 99 });

        db.Samples.AddRange(s1, s2);
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db);

        var res = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { AnalystId = 42 });
        Assert.Single(res.Items);
        Assert.Equal(1, res.Items[0].SampleId);
    }

    [Fact]
    public async Task UrgencyFilter_Overdue_MatchesNonClosedReceivedPast24Hours()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);

        var now = DateTime.UtcNow;

        // Open sample received 2 days ago -> overdue
        var s1 = CreateSample(1, "C1", cause, status: SampleStatus.InTesting, receivedAt: now.AddDays(-2));
        // Open sample received 2 hours ago -> NOT overdue
        var s2 = CreateSample(2, "C2", cause, status: SampleStatus.InTesting, receivedAt: now.AddHours(-2));
        // Closed sample (Approved) received 5 days ago -> NOT overdue
        var s3 = CreateSample(3, "C3", cause, status: SampleStatus.Approved, receivedAt: now.AddDays(-5));
        // Closed sample (Rejected) received 3 days ago -> NOT overdue
        var s4 = CreateSample(4, "C4", cause, status: SampleStatus.Rejected, receivedAt: now.AddDays(-3));
        // Closed sample (RetestRequested) received 3 days ago -> NOT overdue
        var s5 = CreateSample(5, "C5", cause, status: SampleStatus.RetestRequested, receivedAt: now.AddDays(-3));

        db.Samples.AddRange(s1, s2, s3, s4, s5);
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db);

        var res = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Urgency = "overdue" });
        Assert.Single(res.Items);
        Assert.Equal(1, res.Items[0].SampleId);
    }

    [Fact]
    public async Task DateRangeFilter_IncludesSamplesWithinFromAndToDate()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);

        var s1 = CreateSample(1, "C1", cause, receivedAt: new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));
        var s2 = CreateSample(2, "C2", cause, receivedAt: new DateTime(2026, 9, 5, 14, 0, 0, DateTimeKind.Utc));
        var s3 = CreateSample(3, "C3", cause, receivedAt: new DateTime(2026, 9, 10, 18, 0, 0, DateTimeKind.Utc));
        var s4 = CreateSample(4, "C4", cause, receivedAt: new DateTime(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc));

        db.Samples.AddRange(s1, s2, s3, s4);
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db);

        var res = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto
        {
            FromDate = "2026-09-05",
            ToDate = "2026-09-10"
        });

        Assert.Equal(2, res.Items.Count);
        Assert.Contains(res.Items, s => s.SampleId == 2);
        Assert.Contains(res.Items, s => s.SampleId == 3);
    }

    [Fact]
    public async Task GetWorkloadCountsAsync_ComputesTileCountsInSqlCorrectly()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);

        var now = DateTime.UtcNow;
        var myUserId = 77;

        // 1. Needs preparation (PreparationStatus = NeedsPreparation, not closed)
        var s1 = CreateSample(1, "C1", cause, status: SampleStatus.Received, receivedAt: now);
        s1.PreparationStatus = SamplePreparationStatus.NeedsPreparation;

        // 2. Ready to read (open incubation with ExpectedReadingAt <= now)
        var s2 = CreateSample(2, "C2", cause, status: SampleStatus.InTesting, receivedAt: now);
        s2.PreparationStatus = SamplePreparationStatus.Ready;
        var to2 = new TestOrder { Id = 20, TestCode = "TAMC", Status = ApprovalStatus.InProgress, AssignedAnalystId = myUserId };
        to2.Incubations.Add(new Incubation { Id = 200, TestOrderId = 20, CompletedAt = null, ExpectedReadingAt = now.AddHours(-1) });
        s2.TestOrders.Add(to2);

        // 3. Awaiting review (UnderReview or test ResultEntered)
        var s3 = CreateSample(3, "C3", cause, status: SampleStatus.UnderReview, receivedAt: now);
        s3.PreparationStatus = SamplePreparationStatus.Ready;

        // 4. Overdue (open sample received > 24h ago)
        var s4 = CreateSample(4, "C4", cause, status: SampleStatus.InTesting, receivedAt: now.AddHours(-30));
        s4.PreparationStatus = SamplePreparationStatus.Ready;

        // 5. Unassigned (not closed, no analyst assigned to sample or any test)
        var s5 = CreateSample(5, "C5", cause, status: SampleStatus.Received, receivedAt: now);
        s5.PreparationStatus = SamplePreparationStatus.Ready;
        s5.TestOrders.Add(new TestOrder { Id = 50, TestCode = "EC", Status = ApprovalStatus.Pending, AssignedAnalystId = null });

        db.Samples.AddRange(s1, s2, s3, s4, s5);
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db);

        var counts = await service.GetWorkloadCountsAsync(myUserId);

        Assert.Equal(1, counts.NeedsPreparation);
        Assert.Equal(1, counts.ReadyToRead);
        Assert.Equal(1, counts.AwaitingReview);
        Assert.Equal(1, counts.Overdue);
        Assert.Equal(1, counts.Mine); // s2 has test assigned to myUserId
        // s1, s3, s4 (no test orders, open) and s5 (test order with null analyst, open) are unassigned
        Assert.Equal(4, counts.Unassigned);
    }

    [Fact]
    public async Task Parameterless_GetActiveSamplesAsync_ReturnsAllActiveSamplesUnpaged()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);

        for (int i = 1; i <= 10; i++)
        {
            db.Samples.Add(CreateSample(i, $"C{i}", cause, receivedAt: DateTime.UtcNow.AddMinutes(-i)));
        }
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db);

        var list = await service.GetActiveSamplesAsync();
        Assert.Equal(10, list.Count);
        Assert.IsType<List<SampleDto>>(list);
    }

    [Fact]
    public async Task Controller_GetActive_ReturnsPagedResultInApiResponseEnvelope()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);
        db.Samples.Add(CreateSample(1, "C1", cause));
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db);
        var controller = new MicroLIMS.API.Controllers.TestingWorkspaceController(service);

        var user = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "42")
        }, "TestAuth"));

        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = user }
        };

        var actionResult = await controller.GetActivePaged(new TestingWorkspaceFilterDto());
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(actionResult);
        var apiRes = Assert.IsType<ApiResponse<PagedResult<SampleDto>>>(okResult.Value);
        Assert.True(apiRes.Success);
        Assert.NotNull(apiRes.Data);
        Assert.Equal(1, apiRes.Data.TotalCount);
        Assert.Single(apiRes.Data.Items);
    }

    [Fact]
    public async Task Controller_GetWorkloadCounts_ReturnsCountsInApiResponseEnvelope()
    {
        await using var db = NewDb();
        var cause = SeedCause(db);
        var s = CreateSample(1, "C1", cause);
        s.PreparationStatus = SamplePreparationStatus.NeedsPreparation;
        db.Samples.Add(s);
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db);
        var controller = new MicroLIMS.API.Controllers.TestingWorkspaceController(service);

        var actionResult = await controller.GetWorkloadCounts();
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(actionResult);
        var apiRes = Assert.IsType<ApiResponse<WorkspaceTileCountsDto>>(okResult.Value);
        Assert.True(apiRes.Success);
        Assert.NotNull(apiRes.Data);
        Assert.Equal(1, apiRes.Data.NeedsPreparation);
    }
}
