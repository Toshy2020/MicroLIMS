using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class OosWorkflowAndPropagationTests
{
    private const string Password = "Correct-Horse-1!";

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<User> SeedUser(MicroLimsDbContext db, int id, RoleType roleType = RoleType.Reviewer)
    {
        var role = new Role { Type = roleType, Name = roleType.ToString() };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { Id = id, FullName = $"User {id}", Username = $"user{id}", RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task SeedRetestCauseAsync(MicroLimsDbContext db)
    {
        db.CausesOfTesting.Add(new CauseOfTesting { Name = "Retest" });
        await db.SaveChangesAsync();
    }

    private static async Task<(Sample sample, TestOrder order)> SeedSampleUnderApprovalAsync(MicroLimsDbContext db, int analystId, int reviewerId)
    {
        var testDefinition = new TestDefinition { Code = "TAMC", DisplayName = "Total Aerobic Microbial Count", WorkflowType = WorkflowType.CountTest };
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation",
            IncubationMinHours = 72, IncubationMaxHours = 120, TemperatureMin = 30, TemperatureMax = 35,
            IsFinalStep = true
        };
        db.TestWorkflowSteps.Add(step);

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-001", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = "TSA",
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia { TestWorkflowStepId = step.Id, MaterialId = material.Id, TempMin = 30, TempMax = 35 });

        var media = new Media { MaterialId = material.Id, LotNumber = "TSA/1/26", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        db.Media.Add(media);
        db.Equipment.Add(new Equipment { Name = "Incubator", Code = "INC-1", Type = EquipmentType.Incubator, SetPointTemperature = 32 });

        var point = new WaterSamplingPoint { Code = "WP-01", Location = "Utility Room", AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        db.SamplingConfigurations.Add(new SamplingConfiguration { WaterSamplingPointId = point.Id, TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" });

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync(c => c.Name == "Retest")
            ?? new CauseOfTesting { Name = "Retest" };
        if (cause.Id == 0) { db.CausesOfTesting.Add(cause); await db.SaveChangesAsync(); }

        var sample = new Sample
        {
            ReferenceNumber = "WT0826001",
            Category = SampleCategory.Water,
            WaterSamplingPointId = point.Id,
            ControlNumber = "CTRL-1",
            SampledBy = "Sampler",
            ReceivedByUserId = analystId,
            ReceivedAt = DateTime.UtcNow,
            CauseOfTestingId = cause.Id,
            Status = SampleStatus.UnderApproval,
            ReviewedByUserId = reviewerId,
            ReviewedAt = DateTime.UtcNow
        };
        var order = new TestOrder
        {
            TestCode = "TAMC",
            Status = ApprovalStatus.Reviewed,
            CurrentStep = WorkflowStep.Reviewed,
            AssignedAnalystId = analystId
        };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        return (sample, order);
    }

    [Fact]
    public async Task ReferenceNumberGenerator_GenerateOosCodeAsync_IncrementsMonthlySequenceAcrossDistinctGroups()
    {
        await using var db = NewDb();
        var gen = new ReferenceNumberGenerator(db);

        var now = DateTime.UtcNow;
        var expectedPrefix = $"OOS{now:MM}{now:yy}";

        var code1 = await gen.GenerateOosCodeAsync();
        Assert.Equal($"{expectedPrefix}001", code1);

        // Add 2 samples sharing code1
        db.Samples.Add(new Sample { ReferenceNumber = "S1", ControlNumber = "C1", SampledBy = "A", OosGroupCode = code1 });
        db.Samples.Add(new Sample { ReferenceNumber = "S2", ControlNumber = "C2", SampledBy = "A", OosGroupCode = code1 });
        await db.SaveChangesAsync();

        // Distinct count should still be 1 -> next is 002
        var code2 = await gen.GenerateOosCodeAsync();
        Assert.Equal($"{expectedPrefix}002", code2);

        db.Samples.Add(new Sample { ReferenceNumber = "S3", ControlNumber = "C3", SampledBy = "A", OosGroupCode = code2 });
        await db.SaveChangesAsync();

        var code3 = await gen.GenerateOosCodeAsync();
        Assert.Equal($"{expectedPrefix}003", code3);
    }

    [Fact]
    public async Task RetestRetainedSample_SingleChild_Approval_MirrorsOutcomeToOriginAndGeneratesCoa()
    {
        await using var db = NewDb();
        await SeedUser(db, 1, RoleType.Analyst);    // tester
        await SeedUser(db, 2, RoleType.Reviewer);  // reviewer
        await SeedUser(db, 3, RoleType.SectionHead); // section head
        await SeedRetestCauseAsync(db);

        var (origin, order) = await SeedSampleUnderApprovalAsync(db, analystId: 1, reviewerId: 2);
        var approvalService = TestServiceFactory.SampleApproval(db);

        // Section Head orders RetestRetainedSample
        await approvalService.DecideAsync(origin.Id, sectionHeadUserId: 3, Password, ApprovalDecision.RetestRetainedSample,
            null, null, selectedTestOrderIds: new List<int> { order.Id });

        var reloadedOrigin = await db.Samples.FirstAsync(s => s.Id == origin.Id);
        Assert.Equal(SampleStatus.RetestRequested, reloadedOrigin.Status);
        Assert.Equal(ApprovalDecision.RetestRetainedSample, reloadedOrigin.ApprovalDecision);
        Assert.NotNull(reloadedOrigin.OosGroupCode);

        var retestSample = await db.Samples.SingleAsync(s => s.OriginSampleId == origin.Id);
        Assert.Equal(reloadedOrigin.OosGroupCode, retestSample.OosGroupCode);
        Assert.Equal(SampleStatus.Received, retestSample.Status);

        // Put retest sample through Review -> UnderApproval
        retestSample.Status = SampleStatus.UnderApproval;
        retestSample.ReviewedByUserId = 2;
        retestSample.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Section Head decides Approve on the retest sample
        await approvalService.DecideAsync(retestSample.Id, sectionHeadUserId: 3, Password, ApprovalDecision.Approve,
            "Approved retest", null);

        // Verify retest sample is Approved
        var resolvedRetest = await db.Samples.FirstAsync(s => s.Id == retestSample.Id);
        Assert.Equal(SampleStatus.Approved, resolvedRetest.Status);

        // Verify origin sample mirrored outcome to Approved
        var resolvedOrigin = await db.Samples.FirstAsync(s => s.Id == origin.Id);
        Assert.Equal(SampleStatus.Approved, resolvedOrigin.Status);
        Assert.Equal(3, resolvedOrigin.ApprovedByUserId);
        Assert.NotNull(resolvedOrigin.ApprovedAt);
        Assert.Equal(ApprovalDecision.RetestRetainedSample, resolvedOrigin.ApprovalDecision); // decision is preserved

        // Verify archived COA created for origin
        var originArchives = await db.ArchivedRecords
            .Where(a => a.EntityType == ReviewEntityTypes.Sample && a.EntityId == origin.Id)
            .ToListAsync();
        Assert.Contains(originArchives, a => a.Reason == "Sample Approve (OOS resolved)");
    }

    [Fact]
    public async Task RetestRetainedSample_SingleChild_Rejection_MirrorsOutcomeToOrigin()
    {
        await using var db = NewDb();
        await SeedUser(db, 1, RoleType.Analyst);
        await SeedUser(db, 2, RoleType.Reviewer);
        await SeedUser(db, 3, RoleType.SectionHead);
        await SeedRetestCauseAsync(db);

        var (origin, order) = await SeedSampleUnderApprovalAsync(db, analystId: 1, reviewerId: 2);
        var approvalService = TestServiceFactory.SampleApproval(db);

        await approvalService.DecideAsync(origin.Id, sectionHeadUserId: 3, Password, ApprovalDecision.RetestRetainedSample,
            null, null, selectedTestOrderIds: new List<int> { order.Id });

        var retestSample = await db.Samples.SingleAsync(s => s.OriginSampleId == origin.Id);
        retestSample.Status = SampleStatus.UnderApproval;
        retestSample.ReviewedByUserId = 2;
        retestSample.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Section Head decides Reject on the retest sample
        await approvalService.DecideAsync(retestSample.Id, sectionHeadUserId: 3, Password, ApprovalDecision.Reject,
            "Failed retest", null);

        var resolvedOrigin = await db.Samples.FirstAsync(s => s.Id == origin.Id);
        Assert.Equal(SampleStatus.Rejected, resolvedOrigin.Status);
        // Unlike a directly-Rejected sample, the origin never gets its own
        // Rejected-meaning signature for this mirrored outcome, so
        // ApprovedByUserId/At is the only record of who/when it resolved -
        // populated here even though the outcome is Reject.
        Assert.Equal(3, resolvedOrigin.ApprovedByUserId);
        Assert.NotNull(resolvedOrigin.ApprovedAt);

        var originArchives = await db.ArchivedRecords
            .Where(a => a.EntityType == ReviewEntityTypes.Sample && a.EntityId == origin.Id)
            .ToListAsync();
        Assert.Contains(originArchives, a => a.Reason == "Sample Reject (OOS resolved)");
    }

    [Fact]
    public async Task NewSampleRequest_BothMustBeApprovedForOriginToBeApproved()
    {
        await using var db = NewDb();
        await SeedUser(db, 1, RoleType.Analyst);
        await SeedUser(db, 2, RoleType.Reviewer);
        await SeedUser(db, 3, RoleType.SectionHead);
        await SeedUser(db, 4, RoleType.Analyst);
        await SeedUser(db, 5, RoleType.Analyst);
        await SeedRetestCauseAsync(db);

        var (origin, order) = await SeedSampleUnderApprovalAsync(db, analystId: 1, reviewerId: 2);
        var approvalService = TestServiceFactory.SampleApproval(db);

        await approvalService.DecideAsync(origin.Id, sectionHeadUserId: 3, Password, ApprovalDecision.NewSampleRequest,
            null, null, selectedTestOrderIds: new List<int> { order.Id }, newSampleAnalystOneId: 4, newSampleAnalystTwoId: 5);

        var siblings = await db.Samples.Where(s => s.OriginSampleId == origin.Id).OrderBy(s => s.Id).ToListAsync();
        Assert.Equal(2, siblings.Count);
        var (siblingA, siblingB) = (siblings[0], siblings[1]);

        // Sibling A reaches UnderApproval
        siblingA.Status = SampleStatus.UnderApproval;
        siblingA.ReviewedByUserId = 2;
        siblingA.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Decide Approve on Sibling A
        await approvalService.DecideAsync(siblingA.Id, sectionHeadUserId: 3, Password, ApprovalDecision.Approve, "A approved", null);

        // Origin MUST still be RetestRequested (waiting for Sibling B)
        var midOrigin = await db.Samples.FirstAsync(s => s.Id == origin.Id);
        Assert.Equal(SampleStatus.RetestRequested, midOrigin.Status);

        // Sibling B reaches UnderApproval
        siblingB.Status = SampleStatus.UnderApproval;
        siblingB.ReviewedByUserId = 2;
        siblingB.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Decide Approve on Sibling B
        await approvalService.DecideAsync(siblingB.Id, sectionHeadUserId: 3, Password, ApprovalDecision.Approve, "B approved", null);

        // Now origin must be Approved
        var finalOrigin = await db.Samples.FirstAsync(s => s.Id == origin.Id);
        Assert.Equal(SampleStatus.Approved, finalOrigin.Status);
        Assert.Equal(3, finalOrigin.ApprovedByUserId);
    }

    [Fact]
    public async Task NewSampleRequest_IfEitherSiblingIsRejected_OriginBecomesRejected_Order1()
    {
        await using var db = NewDb();
        await SeedUser(db, 1, RoleType.Analyst);
        await SeedUser(db, 2, RoleType.Reviewer);
        await SeedUser(db, 3, RoleType.SectionHead);
        await SeedUser(db, 4, RoleType.Analyst);
        await SeedUser(db, 5, RoleType.Analyst);
        await SeedRetestCauseAsync(db);

        var (origin, order) = await SeedSampleUnderApprovalAsync(db, analystId: 1, reviewerId: 2);
        var approvalService = TestServiceFactory.SampleApproval(db);

        await approvalService.DecideAsync(origin.Id, sectionHeadUserId: 3, Password, ApprovalDecision.NewSampleRequest,
            null, null, selectedTestOrderIds: new List<int> { order.Id }, newSampleAnalystOneId: 4, newSampleAnalystTwoId: 5);

        var siblings = await db.Samples.Where(s => s.OriginSampleId == origin.Id).OrderBy(s => s.Id).ToListAsync();
        var (siblingA, siblingB) = (siblings[0], siblings[1]);

        // Sibling A is Rejected first
        siblingA.Status = SampleStatus.UnderApproval;
        siblingA.ReviewedByUserId = 2;
        await db.SaveChangesAsync();
        await approvalService.DecideAsync(siblingA.Id, sectionHeadUserId: 3, Password, ApprovalDecision.Reject, "A rejected", null);

        // Sibling B is still not decided, so origin remains RetestRequested
        var midOrigin = await db.Samples.FirstAsync(s => s.Id == origin.Id);
        Assert.Equal(SampleStatus.RetestRequested, midOrigin.Status);

        // Sibling B is later Approved
        siblingB.Status = SampleStatus.UnderApproval;
        siblingB.ReviewedByUserId = 2;
        await db.SaveChangesAsync();
        await approvalService.DecideAsync(siblingB.Id, sectionHeadUserId: 3, Password, ApprovalDecision.Approve, "B approved", null);

        // Combined outcome is Rejected because sibling A was Rejected
        var finalOrigin = await db.Samples.FirstAsync(s => s.Id == origin.Id);
        Assert.Equal(SampleStatus.Rejected, finalOrigin.Status);
    }

    [Fact]
    public async Task NewSampleRequest_IfEitherSiblingIsRejected_OriginBecomesRejected_Order2()
    {
        await using var db = NewDb();
        await SeedUser(db, 1, RoleType.Analyst);
        await SeedUser(db, 2, RoleType.Reviewer);
        await SeedUser(db, 3, RoleType.SectionHead);
        await SeedUser(db, 4, RoleType.Analyst);
        await SeedUser(db, 5, RoleType.Analyst);
        await SeedRetestCauseAsync(db);

        var (origin, order) = await SeedSampleUnderApprovalAsync(db, analystId: 1, reviewerId: 2);
        var approvalService = TestServiceFactory.SampleApproval(db);

        await approvalService.DecideAsync(origin.Id, sectionHeadUserId: 3, Password, ApprovalDecision.NewSampleRequest,
            null, null, selectedTestOrderIds: new List<int> { order.Id }, newSampleAnalystOneId: 4, newSampleAnalystTwoId: 5);

        var siblings = await db.Samples.Where(s => s.OriginSampleId == origin.Id).OrderBy(s => s.Id).ToListAsync();
        var (siblingA, siblingB) = (siblings[0], siblings[1]);

        // Sibling A is Approved first
        siblingA.Status = SampleStatus.UnderApproval;
        siblingA.ReviewedByUserId = 2;
        await db.SaveChangesAsync();
        await approvalService.DecideAsync(siblingA.Id, sectionHeadUserId: 3, Password, ApprovalDecision.Approve, "A approved", null);

        // Sibling B is later Rejected
        siblingB.Status = SampleStatus.UnderApproval;
        siblingB.ReviewedByUserId = 2;
        await db.SaveChangesAsync();
        await approvalService.DecideAsync(siblingB.Id, sectionHeadUserId: 3, Password, ApprovalDecision.Reject, "B rejected", null);

        var finalOrigin = await db.Samples.FirstAsync(s => s.Id == origin.Id);
        Assert.Equal(SampleStatus.Rejected, finalOrigin.Status);
    }

    [Fact]
    public async Task MultiLevelEscalation_PropagatesOutcomeAllTheWayUpToRoot()
    {
        await using var db = NewDb();
        await SeedUser(db, 1, RoleType.Analyst);
        await SeedUser(db, 2, RoleType.Reviewer);
        await SeedUser(db, 3, RoleType.SectionHead);
        await SeedUser(db, 4, RoleType.Analyst);
        await SeedUser(db, 5, RoleType.Analyst);
        await SeedRetestCauseAsync(db);

        var (root, rootOrder) = await SeedSampleUnderApprovalAsync(db, analystId: 1, reviewerId: 2);
        var approvalService = TestServiceFactory.SampleApproval(db);

        // 1. Root receives RetestRetainedSample -> Child 1
        await approvalService.DecideAsync(root.Id, sectionHeadUserId: 3, Password, ApprovalDecision.RetestRetainedSample,
            null, null, selectedTestOrderIds: new List<int> { rootOrder.Id });

        var reloadedRoot = await db.Samples.FirstAsync(s => s.Id == root.Id);
        var oosGroupCode = reloadedRoot.OosGroupCode;
        Assert.NotNull(oosGroupCode);

        var child1 = await db.Samples.SingleAsync(s => s.OriginSampleId == root.Id);
        Assert.Equal(oosGroupCode, child1.OosGroupCode);

        // 2. Child 1 goes to UnderApproval, then receives NewSampleRequest -> Grandchildren 2A & 2B
        child1.Status = SampleStatus.UnderApproval;
        child1.ReviewedByUserId = 2;
        var child1Order = await db.TestOrders.SingleAsync(t => t.SampleId == child1.Id);
        await db.SaveChangesAsync();

        await approvalService.DecideAsync(child1.Id, sectionHeadUserId: 3, Password, ApprovalDecision.NewSampleRequest,
            null, null, selectedTestOrderIds: new List<int> { child1Order.Id }, newSampleAnalystOneId: 4, newSampleAnalystTwoId: 5);

        var grandchildren = await db.Samples.Where(s => s.OriginSampleId == child1.Id).OrderBy(s => s.Id).ToListAsync();
        Assert.Equal(2, grandchildren.Count);
        Assert.All(grandchildren, g => Assert.Equal(oosGroupCode, g.OosGroupCode));

        var (gA, gB) = (grandchildren[0], grandchildren[1]);

        // 3. Resolve Grandchild A -> Approve
        gA.Status = SampleStatus.UnderApproval;
        gA.ReviewedByUserId = 2;
        await db.SaveChangesAsync();
        await approvalService.DecideAsync(gA.Id, sectionHeadUserId: 3, Password, ApprovalDecision.Approve, "gA approved", null);

        // Root and Child 1 must still be RetestRequested
        Assert.Equal(SampleStatus.RetestRequested, (await db.Samples.FirstAsync(s => s.Id == child1.Id)).Status);
        Assert.Equal(SampleStatus.RetestRequested, (await db.Samples.FirstAsync(s => s.Id == root.Id)).Status);

        // 4. Resolve Grandchild B -> Approve
        gB.Status = SampleStatus.UnderApproval;
        gB.ReviewedByUserId = 2;
        await db.SaveChangesAsync();
        await approvalService.DecideAsync(gB.Id, sectionHeadUserId: 3, Password, ApprovalDecision.Approve, "gB approved", null);

        // Both Child 1 and Root must now be Approved
        var resolvedChild1 = await db.Samples.FirstAsync(s => s.Id == child1.Id);
        Assert.Equal(SampleStatus.Approved, resolvedChild1.Status);
        Assert.Equal(3, resolvedChild1.ApprovedByUserId);

        var resolvedRoot = await db.Samples.FirstAsync(s => s.Id == root.Id);
        Assert.Equal(SampleStatus.Approved, resolvedRoot.Status);
        Assert.Equal(3, resolvedRoot.ApprovedByUserId);

        // Both Child 1 and Root should have fresh archived COAs
        var rootArchives = await db.ArchivedRecords
            .Where(a => a.EntityType == ReviewEntityTypes.Sample && a.EntityId == root.Id)
            .ToListAsync();
        Assert.Contains(rootArchives, a => a.Reason == "Sample Approve (OOS resolved)");
    }
}
