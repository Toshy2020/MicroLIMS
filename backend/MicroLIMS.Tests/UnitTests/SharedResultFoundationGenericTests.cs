using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SharedResultFoundationGenericTests
{
    private const string Password = "ValidPassword123!";

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (DocumentSection sec, User analyst, User reviewer, User head, TestDefinition def, Sample sample, TestOrder order, Specification spec)
        SeedGenericFixture(MicroLimsDbContext db, WorkflowType workflowType)
    {
        var microSec = TestServiceFactory.EnsureMicroSection(db);

        var sec = db.DocumentSections.FirstOrDefault(s => s.Code == "FP")
            ?? db.DocumentSections.Add(new DocumentSection
            {
                Name = "Finished Product Laboratory",
                Code = "FP",
                DepartmentId = microSec.DepartmentId,
                IsActive = true
            }).Entity;
        db.SaveChanges();

        var analystRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.Analyst)
            ?? db.Roles.Add(new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true }).Entity;
        var reviewerRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.Reviewer)
            ?? db.Roles.Add(new Role { Name = "Reviewer", Type = RoleType.Reviewer, IsActive = true }).Entity;
        var headRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.SectionHead)
            ?? db.Roles.Add(new Role { Name = "Section Head", Type = RoleType.SectionHead, IsActive = true }).Entity;
        db.SaveChanges();

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(Password);

        var analyst = new User
        {
            Username = "gen_analyst_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Generic Analyst",
            RoleId = analystRole.Id,
            Role = analystRole,
            PasswordHash = passwordHash,
            IsActive = true
        };
        var reviewer = new User
        {
            Username = "gen_reviewer_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Generic Reviewer",
            RoleId = reviewerRole.Id,
            Role = reviewerRole,
            PasswordHash = passwordHash,
            IsActive = true
        };
        var head = new User
        {
            Username = "gen_head_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Generic Section Head",
            RoleId = headRole.Id,
            Role = headRole,
            PasswordHash = passwordHash,
            IsActive = true
        };
        db.Users.AddRange(analyst, reviewer, head);
        db.SaveChanges();

        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = analyst.Id, DepartmentId = sec.DepartmentId, SectionId = sec.Id });
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = reviewer.Id, DepartmentId = sec.DepartmentId, SectionId = sec.Id });
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = head.Id, DepartmentId = sec.DepartmentId, SectionId = sec.Id });
        db.SaveChanges();

        var def = new TestDefinition
        {
            Code = "GEN_TEST_" + Guid.NewGuid().ToString("N")[..6],
            DisplayName = "Generic Analysis Test",
            SectionId = sec.Id,
            WorkflowType = workflowType,
            IsActive = true
        };
        db.TestDefinitions.Add(def);

        var item = new Item { Code = "GEN_ITEM_" + Guid.NewGuid().ToString("N")[..6], Name = "Generic Product", IsActive = true };
        db.Items.Add(item);
        db.SaveChanges();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = def.Code,
            ParameterName = "GenericParameter",
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m,
            Unit = "%",
            ResultBasis = ResultBasis.PercentLabelClaim
        };
        db.Specifications.Add(spec);

        var cause = db.CausesOfTesting.FirstOrDefault() ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine", IsActive = true }).Entity;
        db.SaveChanges();

        var sample = new Sample
        {
            ReferenceNumber = "REF-GEN-" + Guid.NewGuid().ToString("N")[..6],
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.InTesting,
            CauseOfTesting = cause,
            ItemId = item.Id,
            ReceivedByUserId = analyst.Id,
            ReceivedAt = DateTime.UtcNow
        };
        db.Samples.Add(sample);
        db.SaveChanges();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = def.Code,
            SectionId = sec.Id,
            AssignedAnalystId = analyst.Id,
            CurrentStep = WorkflowStep.Running,
            Status = ApprovalStatus.InProgress
        };
        db.TestOrders.Add(order);
        db.SaveChanges();

        return (sec, analyst, reviewer, head, def, sample, order, spec);
    }

    [Fact]
    public async Task ApprovalGate_BlocksApproval_WhenGenericAnalysisLacksActiveEntry()
    {
        using var db = NewDb();
        var (sec, analyst, reviewer, head, def, sample, order, _) = SeedGenericFixture(db, WorkflowType.ElementalAssay);
        order.CurrentStep = WorkflowStep.Reviewed;
        order.Status = ApprovalStatus.Approved;
        sample.Status = SampleStatus.UnderReview;

        db.SampleSectionSignoffs.Add(new SampleSectionSignoff
        {
            SampleId = sample.Id,
            SectionId = sec.Id,
            Status = SectionSignoffStatus.UnderApproval,
            ReviewedByUserId = reviewer.Id
        });
        await db.SaveChangesAsync();

        var approvalService = TestServiceFactory.SampleApproval(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.DecideAsync(sample.Id, head.Id, Password, ApprovalDecision.Approve, "Approving without analysis", "127.0.0.1", sectionId: sec.Id));

        Assert.Contains("lacks an active elemental assay entry", ex.Message);
    }

    [Fact]
    public async Task ApprovalGate_BlocksApproval_WhenSectionHeadEnteredAnalysis()
    {
        using var db = NewDb();
        var (sec, analyst, reviewer, head, def, sample, order, spec) = SeedGenericFixture(db, WorkflowType.ElementalAssay);

        var sig = new ElectronicSignature
        {
            UserId = head.Id,
            MeaningOfSignature = SignatureMeaning.ResultRecorded,
            EntityType = "TestOrder",
            EntityId = order.Id,
            UserFullNameSnapshot = head.FullName,
            UsernameSnapshot = head.Username,
            RoleSnapshot = "SectionHead",
            SignedAt = DateTime.UtcNow
        };
        db.ElectronicSignatures.Add(sig);
        await db.SaveChangesAsync();

        var analysis = new TestAnalysis
        {
            TestOrderId = order.Id,
            AnalysisType = WorkflowType.ElementalAssay,
            AnalysedAt = DateTime.UtcNow,
            IsActive = true,
            EnteredByUserId = head.Id, // Head entered it!
            SignatureId = sig.Id
        };
        db.TestAnalyses.Add(analysis);

        order.CurrentStep = WorkflowStep.Reviewed;
        order.Status = ApprovalStatus.Approved;
        sample.Status = SampleStatus.UnderReview;

        db.SampleSectionSignoffs.Add(new SampleSectionSignoff
        {
            SampleId = sample.Id,
            SectionId = sec.Id,
            Status = SectionSignoffStatus.UnderApproval,
            ReviewedByUserId = reviewer.Id
        });
        await db.SaveChangesAsync();

        var approvalService = TestServiceFactory.SampleApproval(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.DecideAsync(sample.Id, head.Id, Password, ApprovalDecision.Approve, "Self approval", "127.0.0.1", sectionId: sec.Id));

        Assert.Contains("You cannot approve a sample you tested", ex.Message);
    }

    [Fact]
    public async Task ApprovalGate_Succeeds_WhenActiveAnalysisExists_EnteredByAnalyst()
    {
        using var db = NewDb();
        var (sec, analyst, reviewer, head, def, sample, order, spec) = SeedGenericFixture(db, WorkflowType.ElementalAssay);

        var sig = new ElectronicSignature
        {
            UserId = analyst.Id,
            MeaningOfSignature = SignatureMeaning.ResultRecorded,
            EntityType = "TestOrder",
            EntityId = order.Id,
            UserFullNameSnapshot = analyst.FullName,
            UsernameSnapshot = analyst.Username,
            RoleSnapshot = "Analyst",
            SignedAt = DateTime.UtcNow
        };
        db.ElectronicSignatures.Add(sig);
        await db.SaveChangesAsync();

        var analysis = new TestAnalysis
        {
            TestOrderId = order.Id,
            AnalysisType = WorkflowType.ElementalAssay,
            AnalysedAt = DateTime.UtcNow,
            IsActive = true,
            EnteredByUserId = analyst.Id,
            SignatureId = sig.Id
        };
        db.TestAnalyses.Add(analysis);

        order.CurrentStep = WorkflowStep.Reviewed;
        order.Status = ApprovalStatus.Approved;
        sample.Status = SampleStatus.UnderReview;

        db.SampleSectionSignoffs.Add(new SampleSectionSignoff
        {
            SampleId = sample.Id,
            SectionId = sec.Id,
            Status = SectionSignoffStatus.UnderApproval,
            ReviewedByUserId = reviewer.Id
        });
        await db.SaveChangesAsync();

        var approvalService = TestServiceFactory.SampleApproval(db);

        await approvalService.DecideAsync(sample.Id, head.Id, Password, ApprovalDecision.Approve, "All good", "127.0.0.1", sectionId: sec.Id);

        var reloadedSample = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(SampleStatus.Approved, reloadedSample.Status);
    }

    [Fact]
    public async Task ReturnToAnalyst_SoftSupersedesGenericAnalysisAndParameterResults()
    {
        using var db = NewDb();
        var (sec, analyst, reviewer, head, def, sample, order, spec) = SeedGenericFixture(db, WorkflowType.ElementalAssay);
        order.CurrentStep = WorkflowStep.Ready;
        order.Status = ApprovalStatus.ResultEntered;

        var sig = new ElectronicSignature
        {
            UserId = analyst.Id,
            MeaningOfSignature = SignatureMeaning.ResultRecorded,
            EntityType = "TestOrder",
            EntityId = order.Id,
            UserFullNameSnapshot = analyst.FullName,
            UsernameSnapshot = analyst.Username,
            RoleSnapshot = "Analyst",
            SignedAt = DateTime.UtcNow
        };
        db.ElectronicSignatures.Add(sig);
        await db.SaveChangesAsync();

        var analysis = new TestAnalysis
        {
            TestOrderId = order.Id,
            AnalysisType = WorkflowType.ElementalAssay,
            AnalysedAt = DateTime.UtcNow,
            IsActive = true,
            EnteredByUserId = analyst.Id,
            SignatureId = sig.Id
        };
        var param = new ParameterResult
        {
            TestOrderId = order.Id,
            SpecificationId = spec.Id,
            ParameterName = spec.ParameterName,
            ReportedValue = 100m,
            ReportedDisplay = "100 %",
            ComparisonStatus = "WithinLimits",
            IsActive = true
        };
        analysis.ParameterResults.Add(param);
        db.TestAnalyses.Add(analysis);
        await db.SaveChangesAsync();

        var reviewService = TestServiceFactory.Review(db);
        var ret = await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, "Re-measure needed");

        Assert.NotNull(ret);

        var reloadedAnalysis = await db.TestAnalyses.Include(a => a.ParameterResults).FirstAsync(a => a.Id == analysis.Id);
        Assert.False(reloadedAnalysis.IsActive);
        Assert.All(reloadedAnalysis.ParameterResults, pr => Assert.False(pr.IsActive));

        var reloadedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.Equal(WorkflowStep.Running, reloadedOrder!.CurrentStep);
    }

    [Fact]
    public async Task ResultProjection_UpsertFromParameterResult_CreatesResultRecordWithSourceTableParameterResult()
    {
        using var db = NewDb();
        var (sec, analyst, reviewer, head, def, sample, order, spec) = SeedGenericFixture(db, WorkflowType.ElementalAssay);

        var analysis = new TestAnalysis
        {
            TestOrderId = order.Id,
            AnalysisType = WorkflowType.ElementalAssay,
            AnalysedAt = DateTime.UtcNow,
            IsActive = true,
            EnteredByUserId = analyst.Id,
            EnteredAt = DateTime.UtcNow,
            SignatureId = 1
        };
        var paramResult = new ParameterResult
        {
            TestOrderId = order.Id,
            SpecificationId = spec.Id,
            ParameterName = "GenericParam",
            ReportedValue = 98.5m,
            ReportedDisplay = "98.5 %",
            Unit = "%",
            SpecLimit = "90 - 110 %",
            ComparisonStatus = "WithinLimits",
            IsActive = true
        };
        analysis.ParameterResults.Add(paramResult);
        db.TestAnalyses.Add(analysis);
        await db.SaveChangesAsync();

        var projectionService = TestServiceFactory.ResultProjection(db);
        await projectionService.UpsertFromParameterResultAsync(paramResult.Id);
        await db.SaveChangesAsync();

        var record = await db.ResultRecords.FirstOrDefaultAsync(r => r.SourceTable == "ParameterResult" && r.SourceId == paramResult.Id);
        Assert.NotNull(record);
        Assert.Equal(sample.Id, record.SampleId);
        Assert.Equal(order.Id, record.TestOrderId);
        Assert.Equal(98.5m, record.NumericValue);
        Assert.Equal("98.5 %", record.ReportedValue);
        Assert.Equal("%", record.Unit);
        Assert.Equal(ResultLevel.WithinLimit, record.ResultLevel);
        Assert.Equal(ResultKind.Quantitative, record.ResultKind);
    }

    [Fact]
    public async Task ResultProjection_Backfill_ProjectsParameterResults()
    {
        using var db = NewDb();
        var (sec, analyst, reviewer, head, def, sample, order, spec) = SeedGenericFixture(db, WorkflowType.ElementalAssay);

        var analysis = new TestAnalysis
        {
            TestOrderId = order.Id,
            AnalysisType = WorkflowType.ElementalAssay,
            AnalysedAt = DateTime.UtcNow,
            IsActive = true,
            EnteredByUserId = analyst.Id,
            EnteredAt = DateTime.UtcNow,
            SignatureId = 1
        };
        var paramResult = new ParameterResult
        {
            TestOrderId = order.Id,
            SpecificationId = spec.Id,
            ParameterName = "GenericParam",
            ReportedValue = 101.2m,
            ReportedDisplay = "101.2 %",
            Unit = "%",
            SpecLimit = "90 - 110 %",
            ComparisonStatus = "WithinLimits",
            IsActive = true
        };
        analysis.ParameterResults.Add(paramResult);
        db.TestAnalyses.Add(analysis);
        await db.SaveChangesAsync();

        var projectionService = TestServiceFactory.ResultProjection(db);
        var backfillResult = await projectionService.BackfillAsync();

        Assert.True(backfillResult.Created >= 1);
        var record = await db.ResultRecords.FirstOrDefaultAsync(r => r.SourceTable == "ParameterResult" && r.SourceId == paramResult.Id);
        Assert.NotNull(record);
        Assert.Equal(101.2m, record.NumericValue);
    }
}
