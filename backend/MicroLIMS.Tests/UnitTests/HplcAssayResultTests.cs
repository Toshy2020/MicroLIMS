using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class HplcAssayResultTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (DocumentSection fpSec, DocumentSection microSec, User fpAnalyst, User fpReviewer, User fpHead, TestDefinition testDef, SystemSuitabilityRun passedRun) SeedPrerequisites(MicroLimsDbContext db)
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

        var analystRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.Analyst)
            ?? new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        if (analystRole.Id == 0) { db.Roles.Add(analystRole); db.SaveChanges(); }

        var reviewerRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.Reviewer)
            ?? new Role { Name = "Reviewer", Type = RoleType.Reviewer, IsActive = true };
        if (reviewerRole.Id == 0) { db.Roles.Add(reviewerRole); db.SaveChanges(); }

        var headRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.SectionHead)
            ?? new Role { Name = "Section Head", Type = RoleType.SectionHead, IsActive = true };
        if (headRole.Id == 0) { db.Roles.Add(headRole); db.SaveChanges(); }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("ValidPassword123!");

        var fpAnalyst = new User
        {
            Username = "fp_analyst_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "FP Analyst",
            RoleId = analystRole.Id,
            Role = analystRole,
            PasswordHash = passwordHash,
            IsActive = true
        };
        db.Users.Add(fpAnalyst);

        var fpReviewer = new User
        {
            Username = "fp_reviewer_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "FP Reviewer",
            RoleId = reviewerRole.Id,
            Role = reviewerRole,
            PasswordHash = passwordHash,
            IsActive = true
        };
        db.Users.Add(fpReviewer);

        var fpHead = new User
        {
            Username = "fp_head_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "FP Section Head",
            RoleId = headRole.Id,
            Role = headRole,
            PasswordHash = passwordHash,
            IsActive = true
        };
        db.Users.Add(fpHead);
        db.SaveChanges();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = fpAnalyst.Id,
            DepartmentId = fpSec.DepartmentId,
            SectionId = fpSec.Id
        });
        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = fpReviewer.Id,
            DepartmentId = fpSec.DepartmentId,
            SectionId = fpSec.Id
        });
        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = fpHead.Id,
            DepartmentId = fpSec.DepartmentId,
            SectionId = fpSec.Id
        });
        db.SaveChanges();

        var testDef = new TestDefinition
        {
            Code = "VITC_ASSAY",
            DisplayName = "Vitamin C Assay",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.HplcAssay,
            EquationType = EquationType.HplcAssay,
            RequiresSystemSuitability = true,
            MethodAbbreviation = "VIT-C",
            SstMaxRsdPercent = 2.0m,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        db.SaveChanges();

        var passedRun = new SystemSuitabilityRun
        {
            Code = "VIT-C S.S 01/092026",
            TestDefinitionId = testDef.Id,
            SectionId = fpSec.Id,
            StandardPurityPercent = 99.5m,
            StandardWeightMg = 50.0m,
            StandardDilution = 100.0m,
            StandardMeanArea = 1000m,
            Passed = true,
            PerformedByUserId = fpAnalyst.Id,
            PerformedAt = DateTime.UtcNow
        };
        db.SystemSuitabilityRuns.Add(passedRun);
        db.SaveChanges();

        return (fpSec, microSec, fpAnalyst, fpReviewer, fpHead, testDef, passedRun);
    }

    private static (Sample sample, TestOrder order) SeedSampleAndOrder(MicroLimsDbContext db, DocumentSection section, TestDefinition testDef, int? runId = null, string? specLimit = "90.0-110.0 %")
    {
        var item = new Item
        {
            Code = "VITC_TAB_" + Guid.NewGuid().ToString("N")[..6],
            Name = "Vitamin C Tablets",
            IsActive = true
        };
        db.Items.Add(item);
        db.SaveChanges();

        if (specLimit != null)
        {
            db.Specifications.Add(new Specification
            {
                ItemId = item.Id,
                TestCode = testDef.Code,
                SpecLimit = specLimit,
                Unit = "%"
            });
            db.SaveChanges();
        }

        var sample = new Sample
        {
            ReferenceNumber = "SMP-" + Guid.NewGuid().ToString("N")[..6],
            ItemId = item.Id,
            BatchNumber = "BATCH-001",
            ControlNumber = "CTL-001",
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.InTesting,
            ReceivedAt = DateTime.UtcNow
        };
        db.Samples.Add(sample);
        db.SaveChanges();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = testDef.Code,
            SectionId = section.Id,
            CurrentStep = WorkflowStep.Running,
            Status = ApprovalStatus.InProgress,
            SystemSuitabilityRunId = runId
        };
        db.TestOrders.Add(order);
        db.SaveChanges();

        return (sample, order);
    }

    [Fact]
    public async Task CalculateAssayPercent_HandComputedExample_MatchesFormulaExactly()
    {
        using var db = NewDb();
        var (fpSec, _, fpAnalyst, _, _, testDef, passedRun) = SeedPrerequisites(db);
        var (sample, order) = SeedSampleAndOrder(db, fpSec, testDef, passedRun.Id, "90.0-110.0 %");

        var engine = TestServiceFactory.TestWorkflow(db);

        // Hand-computed example:
        // area = 1000, stdArea = 1000, stdWeight = 50, sampleWeight = 50, purity = 99.5, dilutions = 100
        // Expected replicate 1 = 99.5%
        // Area replicate 2 = 1005 -> 1005/1000 * 99.5 = 99.9975%
        // Area replicate 3 = 995 -> 995/1000 * 99.5 = 99.0025%
        // Mean = (99.5 + 99.9975 + 99.0025) / 3 = 99.5%
        var payload = new HplcAssayPayload(
            SampleWeightMg: 50.0m,
            SampleDilution: 100.0m,
            SampleAreas: new List<decimal> { 1000m, 1005m, 995m },
            Password: "ValidPassword123!",
            Comment: "Hand-computed test");

        var result = await engine.RecordHplcAssayResultAsync(order.Id, payload, fpAnalyst.Id);

        Assert.Equal("99.5 %", result.OutcomeSummary);
        Assert.True(result.IsDefinitive);
        Assert.True(result.AllStepsComplete);
        Assert.Equal(99.5m, result.CalculatedResult);
        Assert.Equal("WithinLimits", result.Status);

        var savedResult = await db.HplcAssayResults.FirstOrDefaultAsync(r => r.TestOrderId == order.Id);
        Assert.NotNull(savedResult);
        Assert.Equal(99.5m, savedResult.MeanAssayPercent);
        Assert.Equal("99.5 %", savedResult.ReportedResult);
        Assert.Equal("WithinLimits", savedResult.ComparisonStatus);
        Assert.True(savedResult.IsActive);
        Assert.Equal(fpAnalyst.Id, savedResult.EnteredByUserId);
        Assert.True(savedResult.SignatureId > 0);

        // Replicates JSON contains 3 replicates
        var replicates = System.Text.Json.JsonSerializer.Deserialize<List<HplcAssayReplicate>>(savedResult.ReplicatesJson);
        Assert.NotNull(replicates);
        Assert.Equal(3, replicates.Count);
        Assert.Equal(99.5m, replicates[0].AssayPercent);
        Assert.Equal(99.9975m, replicates[1].AssayPercent);
        Assert.Equal(99.0025m, replicates[2].AssayPercent);

        // ResultRecord projection check
        var record = await db.ResultRecords.FirstOrDefaultAsync(r => r.SourceTable == "HplcAssayResult" && r.SourceId == savedResult.Id);
        Assert.NotNull(record);
        Assert.Equal(ResultKind.Quantitative, record.ResultKind);
        Assert.Equal(99.5m, record.NumericValue);
        Assert.Equal("99.5 %", record.ReportedValue);
        Assert.Equal("%", record.Unit);
        Assert.Equal(ResultLevel.WithinLimit, record.ResultLevel);

        // Order transitioned to Ready
        var updatedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.Equal(WorkflowStep.Ready, updatedOrder!.CurrentStep);
    }

    [Fact]
    public async Task RecordHplcAssayResult_GateRejection_NoSuitabilityRunLinked_Throws()
    {
        using var db = NewDb();
        var (fpSec, _, fpAnalyst, _, _, testDef, _) = SeedPrerequisites(db);
        var (_, order) = SeedSampleAndOrder(db, fpSec, testDef, runId: null);

        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new HplcAssayPayload(50.0m, 100.0m, new List<decimal> { 1000m }, "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RecordHplcAssayResultAsync(order.Id, payload, fpAnalyst.Id));

        Assert.Contains("must be linked to a system suitability run", ex.Message);
    }

    [Fact]
    public async Task RecordHplcAssayResult_GateRejection_RunFailed_Throws()
    {
        using var db = NewDb();
        var (fpSec, _, fpAnalyst, _, _, testDef, _) = SeedPrerequisites(db);

        var failedRun = new SystemSuitabilityRun
        {
            Code = "VIT-C S.S 02/092026",
            TestDefinitionId = testDef.Id,
            SectionId = fpSec.Id,
            StandardPurityPercent = 99.5m,
            StandardWeightMg = 50.0m,
            StandardDilution = 100.0m,
            StandardMeanArea = 1000m,
            Passed = false,
            FailureReasons = "RSD% exceeded",
            PerformedByUserId = fpAnalyst.Id,
            PerformedAt = DateTime.UtcNow
        };
        db.SystemSuitabilityRuns.Add(failedRun);
        db.SaveChanges();

        var (_, order) = SeedSampleAndOrder(db, fpSec, testDef, runId: failedRun.Id);

        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new HplcAssayPayload(50.0m, 100.0m, new List<decimal> { 1000m }, "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RecordHplcAssayResultAsync(order.Id, payload, fpAnalyst.Id));

        Assert.Contains("did not pass", ex.Message);
    }

    [Fact]
    public async Task RecordHplcAssayResult_GateRejection_DifferentMethod_Throws()
    {
        using var db = NewDb();
        var (fpSec, _, fpAnalyst, _, _, testDef, _) = SeedPrerequisites(db);

        var otherTest = new TestDefinition
        {
            Code = "OTHER_ASSAY",
            DisplayName = "Other Assay",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.HplcAssay,
            MethodAbbreviation = "OTH",
            IsActive = true
        };
        db.TestDefinitions.Add(otherTest);
        db.SaveChanges();

        var otherRun = new SystemSuitabilityRun
        {
            Code = "OTH S.S 01/092026",
            TestDefinitionId = otherTest.Id,
            SectionId = fpSec.Id,
            StandardPurityPercent = 99.5m,
            StandardWeightMg = 50.0m,
            StandardDilution = 100.0m,
            StandardMeanArea = 1000m,
            Passed = true,
            PerformedByUserId = fpAnalyst.Id,
            PerformedAt = DateTime.UtcNow
        };
        db.SystemSuitabilityRuns.Add(otherRun);
        db.SaveChanges();

        var (_, order) = SeedSampleAndOrder(db, fpSec, testDef, runId: otherRun.Id);

        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new HplcAssayPayload(50.0m, 100.0m, new List<decimal> { 1000m }, "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RecordHplcAssayResultAsync(order.Id, payload, fpAnalyst.Id));

        Assert.Contains("different test method", ex.Message);
    }

    [Fact]
    public async Task RecordHplcAssayResult_GateRejection_DifferentSection_Throws()
    {
        using var db = NewDb();
        var (fpSec, microSec, fpAnalyst, _, _, testDef, _) = SeedPrerequisites(db);

        var microRun = new SystemSuitabilityRun
        {
            Code = "VIT-C S.S 03/092026",
            TestDefinitionId = testDef.Id,
            SectionId = microSec.Id,
            StandardPurityPercent = 99.5m,
            StandardWeightMg = 50.0m,
            StandardDilution = 100.0m,
            StandardMeanArea = 1000m,
            Passed = true,
            PerformedByUserId = fpAnalyst.Id,
            PerformedAt = DateTime.UtcNow
        };
        db.SystemSuitabilityRuns.Add(microRun);
        db.SaveChanges();

        var (_, order) = SeedSampleAndOrder(db, fpSec, testDef, runId: microRun.Id);

        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new HplcAssayPayload(50.0m, 100.0m, new List<decimal> { 1000m }, "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RecordHplcAssayResultAsync(order.Id, payload, fpAnalyst.Id));

        Assert.Contains("different laboratory section", ex.Message);
    }

    [Fact]
    public async Task RecordHplcAssayResult_WrongPassword_ThrowsAndRecordsNoResult()
    {
        using var db = NewDb();
        var (fpSec, _, fpAnalyst, _, _, testDef, passedRun) = SeedPrerequisites(db);
        var (_, order) = SeedSampleAndOrder(db, fpSec, testDef, passedRun.Id);

        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new HplcAssayPayload(50.0m, 100.0m, new List<decimal> { 1000m }, "WrongPassword!");

        await Assert.ThrowsAsync<SignatureVerificationException>(
            () => engine.RecordHplcAssayResultAsync(order.Id, payload, fpAnalyst.Id));

        Assert.Empty(await db.HplcAssayResults.ToListAsync());
        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task RecordHplcAssayResult_RangeSpec_OutOfSpec_FlaggedCorrectly()
    {
        using var db = NewDb();
        var (fpSec, _, fpAnalyst, _, _, testDef, passedRun) = SeedPrerequisites(db);
        // Spec is 90.0 - 110.0 %
        var (_, order) = SeedSampleAndOrder(db, fpSec, testDef, passedRun.Id, "90.0-110.0 %");

        var engine = TestServiceFactory.TestWorkflow(db);

        // Area = 1200 -> 1200/1000 * 99.5 = 119.4% (Out of specification)
        var payload = new HplcAssayPayload(
            SampleWeightMg: 50.0m,
            SampleDilution: 100.0m,
            SampleAreas: new List<decimal> { 1200m },
            Password: "ValidPassword123!");

        var result = await engine.RecordHplcAssayResultAsync(order.Id, payload, fpAnalyst.Id);

        Assert.Equal("OutOfSpecification", result.Status);
        Assert.Equal("119.4 %", result.OutcomeSummary);

        var saved = await db.HplcAssayResults.FirstOrDefaultAsync(r => r.TestOrderId == order.Id);
        Assert.NotNull(saved);
        Assert.Equal("OutOfSpecification", saved.ComparisonStatus);

        var record = await db.ResultRecords.FirstOrDefaultAsync(r => r.SourceId == saved.Id);
        Assert.NotNull(record);
        Assert.Equal(ResultLevel.OutOfSpecification, record.ResultLevel);
    }

    [Fact]
    public async Task SampleApproval_ApprovalGate_RejectsIfHplcOrderLacksPassedRun()
    {
        using var db = NewDb();
        var (fpSec, _, fpAnalyst, fpReviewer, fpHead, testDef, passedRun) = SeedPrerequisites(db);

        // Create sample and order with NO run
        var (sample, order) = SeedSampleAndOrder(db, fpSec, testDef, runId: null);
        order.CurrentStep = WorkflowStep.Reviewed;
        order.Status = ApprovalStatus.Approved;
        sample.Status = SampleStatus.UnderReview;

        db.SampleSectionSignoffs.Add(new SampleSectionSignoff
        {
            SampleId = sample.Id,
            SectionId = fpSec.Id,
            Status = SectionSignoffStatus.UnderApproval,
            ReviewedByUserId = fpReviewer.Id
        });
        db.SaveChanges();

        var approvalService = TestServiceFactory.SampleApproval(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => approvalService.DecideAsync(sample.Id, fpHead.Id, "ValidPassword123!", ApprovalDecision.Approve, "approve", "127.0.0.1", sectionId: fpSec.Id));

        Assert.Contains("lacks a linked system suitability run", ex.Message);

        // Now link the passed run and ensure approval passes
        order.SystemSuitabilityRunId = passedRun.Id;
        db.SaveChanges();

        await approvalService.DecideAsync(sample.Id, fpHead.Id, "ValidPassword123!", ApprovalDecision.Approve, "approve", "127.0.0.1", sectionId: fpSec.Id);
        var reloadedSample = await db.Samples.FindAsync(sample.Id);
        Assert.Equal(SampleStatus.Approved, reloadedSample!.Status);
    }

    [Fact]
    public async Task SampleApproval_ApprovalGate_SupersededOrderDoesNotBlock()
    {
        using var db = NewDb();
        var (fpSec, _, fpAnalyst, fpReviewer, fpHead, testDef, passedRun) = SeedPrerequisites(db);

        var (sample, supersededOrder) = SeedSampleAndOrder(db, fpSec, testDef, runId: null);
        supersededOrder.IsSuperseded = true;
        supersededOrder.CurrentStep = WorkflowStep.Reviewed;

        // Active replacement order is linked to passed run
        var activeOrder = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = testDef.Code,
            SectionId = fpSec.Id,
            CurrentStep = WorkflowStep.Reviewed,
            Status = ApprovalStatus.Approved,
            SystemSuitabilityRunId = passedRun.Id,
            IsSuperseded = false
        };
        db.TestOrders.Add(activeOrder);
        sample.Status = SampleStatus.UnderReview;

        db.SampleSectionSignoffs.Add(new SampleSectionSignoff
        {
            SampleId = sample.Id,
            SectionId = fpSec.Id,
            Status = SectionSignoffStatus.UnderApproval,
            ReviewedByUserId = fpReviewer.Id
        });
        db.SaveChanges();

        var approvalService = TestServiceFactory.SampleApproval(db);
        await approvalService.DecideAsync(sample.Id, fpHead.Id, "ValidPassword123!", ApprovalDecision.Approve, "approve", "127.0.0.1", sectionId: fpSec.Id);

        var reloadedSample = await db.Samples.FindAsync(sample.Id);
        Assert.Equal(SampleStatus.Approved, reloadedSample!.Status);
    }

    [Fact]
    public async Task ReviewService_ReturnToAnalyst_SoftSupersedesActiveHplcResult_AndTransitionsToRunning()
    {
        using var db = NewDb();
        var (fpSec, _, fpAnalyst, fpReviewer, _, testDef, passedRun) = SeedPrerequisites(db);
        var (sample, order) = SeedSampleAndOrder(db, fpSec, testDef, passedRun.Id);

        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new HplcAssayPayload(50.0m, 100.0m, new List<decimal> { 1000m }, "ValidPassword123!");
        await engine.RecordHplcAssayResultAsync(order.Id, payload, fpAnalyst.Id);

        var activeResult = await db.HplcAssayResults.FirstOrDefaultAsync(r => r.TestOrderId == order.Id && r.IsActive);
        Assert.NotNull(activeResult);

        var reviewService = TestServiceFactory.Review(db);
        await reviewService.ReturnToAnalystAsync(order.Id, fpReviewer.Id, "Need re-entry");

        // Previous result is retained in db, but soft-superseded (IsActive = false)
        var deactivated = await db.HplcAssayResults.FindAsync(activeResult.Id);
        Assert.NotNull(deactivated);
        Assert.False(deactivated.IsActive);

        // Order is back to Running / InProgress
        var reloadedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.NotNull(reloadedOrder);
        Assert.Equal(WorkflowStep.Running, reloadedOrder.CurrentStep);
        Assert.Equal(ApprovalStatus.InProgress, reloadedOrder.Status);

        // Analyst can now record a new result
        var newPayload = new HplcAssayPayload(50.0m, 100.0m, new List<decimal> { 1010m }, "ValidPassword123!");
        var newResult = await engine.RecordHplcAssayResultAsync(order.Id, newPayload, fpAnalyst.Id);

        Assert.Equal("100.5 %", newResult.OutcomeSummary);
        var activeCount = await db.HplcAssayResults.CountAsync(r => r.TestOrderId == order.Id && r.IsActive);
        Assert.Equal(1, activeCount);
        var totalCount = await db.HplcAssayResults.CountAsync(r => r.TestOrderId == order.Id);
        Assert.Equal(2, totalCount);
    }

    [Fact]
    public async Task SystemSuitability_Relink_BlockedWhileActiveHplcResultExists()
    {
        using var db = NewDb();
        var (fpSec, _, fpAnalyst, _, _, testDef, passedRun) = SeedPrerequisites(db);
        var (_, order) = SeedSampleAndOrder(db, fpSec, testDef, passedRun.Id);

        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new HplcAssayPayload(50.0m, 100.0m, new List<decimal> { 1000m }, "ValidPassword123!");
        await engine.RecordHplcAssayResultAsync(order.Id, payload, fpAnalyst.Id);

        var newRun = new SystemSuitabilityRun
        {
            Code = "VIT-C S.S 02/092026",
            TestDefinitionId = testDef.Id,
            SectionId = fpSec.Id,
            StandardPurityPercent = 99.5m,
            StandardWeightMg = 50.0m,
            StandardDilution = 100.0m,
            StandardMeanArea = 1000m,
            Passed = true,
            PerformedByUserId = fpAnalyst.Id,
            PerformedAt = DateTime.UtcNow
        };
        db.SystemSuitabilityRuns.Add(newRun);
        db.SaveChanges();

        var sstService = TestServiceFactory.SystemSuitability(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sstService.LinkTestOrdersAsync(newRun.Id, new[] { order.Id }, fpAnalyst.Id));

        Assert.Contains("active HPLC assay result", ex.Message);
    }
}
