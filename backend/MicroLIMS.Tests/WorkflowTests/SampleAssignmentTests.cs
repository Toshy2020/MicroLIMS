using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class SampleAssignmentTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    // Micro: Section Heads 1 and 2, Analyst 3, Reviewer 4. FP: Analyst 5.
    // System Administrator 6 has no membership and reaches every section.
    private sealed record World(Sample Sample, TestOrder Tamc, TestOrder Tymc, TestOrder Assay, TestOrder ApprovedEc, DocumentSection Micro, DocumentSection Fp);

    private const int HeadMicro = 1, OtherHeadMicro = 2, AnalystMicro = 3, ReviewerMicro = 4, AnalystFp = 5, Admin = 6;

    private static async Task<World> SeedAsync(MicroLimsDbContext db)
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Finished Product Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(fp);
        await db.SaveChangesAsync();

        async Task User(int id, RoleType type, DocumentSection? section)
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Type == type);
            if (role is null)
            {
                role = new Role { Type = type, Name = type.ToString() };
                db.Roles.Add(role);
                await db.SaveChangesAsync();
            }
            db.Users.Add(new User { Id = id, Username = $"user{id}", FullName = $"{type} {id}", PasswordHash = "hash", RoleId = role.Id, IsActive = true });
            if (section is not null)
                db.UserOrgMemberships.Add(new UserOrgMembership { UserId = id, DepartmentId = section.DepartmentId, SectionId = section.Id });
            await db.SaveChangesAsync();
        }

        await User(HeadMicro, RoleType.SectionHead, micro);
        await User(OtherHeadMicro, RoleType.SectionHead, micro);
        await User(AnalystMicro, RoleType.Analyst, micro);
        await User(ReviewerMicro, RoleType.Reviewer, micro);
        await User(AnalystFp, RoleType.Analyst, fp);
        await User(Admin, RoleType.SystemAdministrator, null);

        var sample = new Sample { ReferenceNumber = "FP1026001", Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-10", Status = SampleStatus.InTesting };
        var tamc = new TestOrder { TestCode = "TAMC", SectionId = micro.Id, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        var tymc = new TestOrder { TestCode = "TYMC", SectionId = micro.Id, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        var assay = new TestOrder { TestCode = "ASSAY", SectionId = fp.Id, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        var approvedEc = new TestOrder { TestCode = "EC", SectionId = micro.Id, Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved, AssignedAnalystId = 99 };
        sample.TestOrders.AddRange(new[] { tamc, tymc, assay, approvedEc });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        return new World(sample, tamc, tymc, assay, approvedEc, micro, fp);
    }

    private static SampleAssignmentService NewService(MicroLimsDbContext db)
    {
        var scope = new UserSectionScopeService(db);
        return new SampleAssignmentService(db, new TestingWorkspaceService(db, scope), scope);
    }

    [Fact]
    public async Task AssignAnalystAsync_AssignsAnalystToOpenTestsInTheirOwnSectionOnly()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);

        await NewService(db).AssignAnalystAsync(w.Sample.Id, AnalystMicro, actingUserId: Admin);

        Assert.Equal(AnalystMicro, w.Tamc.AssignedAnalystId);
        Assert.Equal(AnalystMicro, w.Tymc.AssignedAnalystId);
        Assert.Null(w.Assay.AssignedAnalystId);           // FP test - not their laboratory
        Assert.Equal(99, w.ApprovedEc.AssignedAnalystId); // closed test unchanged
    }

    [Fact]
    public async Task AssignAnalystAsync_AllowsUnassigningSample()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);
        w.Tamc.AssignedAnalystId = AnalystMicro;
        w.Assay.AssignedAnalystId = AnalystFp;
        await db.SaveChangesAsync();

        await NewService(db).AssignAnalystAsync(w.Sample.Id, null, actingUserId: Admin);

        Assert.Null(w.Tamc.AssignedAnalystId);
        Assert.Null(w.Assay.AssignedAnalystId);
        Assert.Equal(99, w.ApprovedEc.AssignedAnalystId);
    }

    [Fact]
    public async Task AssignAnalystAsync_AssigneeInNoneOfTheSamplesSections_Throws()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);
        w.Assay.Status = ApprovalStatus.Approved;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService(db).AssignAnalystAsync(w.Sample.Id, AnalystFp, actingUserId: Admin));

        Assert.Contains("not a member of the Microbiology Laboratory laboratory section", ex.Message);
        Assert.Null(w.Tamc.AssignedAnalystId);
    }

    [Fact]
    public async Task AssignTestsAsync_GivesEachTestItsOwnAnalyst_IncludingReviewers_AndAuditsEachTest()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);

        await NewService(db).AssignTestsAsync(w.Sample.Id, new[]
        {
            new TestAnalystAssignment(w.Tamc.Id, AnalystMicro),
            new TestAnalystAssignment(w.Tymc.Id, ReviewerMicro)
        }, actingUserId: HeadMicro, reason: "Workload balancing");

        Assert.Equal(AnalystMicro, w.Tamc.AssignedAnalystId);
        Assert.Equal(ReviewerMicro, w.Tymc.AssignedAnalystId);
        Assert.Null(w.Assay.AssignedAnalystId);

        var audits = await db.AuditLogs.Where(a => a.SampleId == w.Sample.Id && a.Action == "AssignedAnalyst").ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.Contains(audits, a => a.TestOrderId == w.Tamc.Id && a.PreviousValue == "TAMC: Unassigned"
            && a.NewValue == "TAMC: Analyst 3 | Reason: Workload balancing" && a.UserId == HeadMicro);
        Assert.Contains(audits, a => a.TestOrderId == w.Tymc.Id && a.NewValue!.StartsWith("TYMC: Reviewer 4"));
    }

    [Fact]
    public async Task AssignTestsAsync_ReassignOneTest_LeavesTheOthersAndAuditsThePreviousAnalyst()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);
        w.Tamc.AssignedAnalystId = AnalystMicro;
        w.Tymc.AssignedAnalystId = AnalystMicro;
        await db.SaveChangesAsync();

        await NewService(db).AssignTestsAsync(w.Sample.Id, new[] { new TestAnalystAssignment(w.Tymc.Id, ReviewerMicro) }, actingUserId: HeadMicro);

        Assert.Equal(AnalystMicro, w.Tamc.AssignedAnalystId);
        Assert.Equal(ReviewerMicro, w.Tymc.AssignedAnalystId);
        var audit = await db.AuditLogs.SingleAsync(a => a.TestOrderId == w.Tymc.Id && a.EntityName == "TestOrder" && a.Action.EndsWith("Analyst"));
        Assert.Equal("ReassignedAnalyst", audit.Action);
        Assert.Equal("TYMC: Analyst 3", audit.PreviousValue);
    }

    [Fact]
    public async Task AssignTestsAsync_SectionHeadMayAssignThemselves()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);

        await NewService(db).AssignTestsAsync(w.Sample.Id, new[] { new TestAnalystAssignment(w.Tamc.Id, HeadMicro) }, actingUserId: HeadMicro);
        await NewService(db).AssignAnalystAsync(w.Sample.Id, HeadMicro, actingUserId: HeadMicro);

        Assert.Equal(HeadMicro, w.Tamc.AssignedAnalystId);
        Assert.Equal(HeadMicro, w.Tymc.AssignedAnalystId);
    }

    [Theory]
    [InlineData(OtherHeadMicro)]
    [InlineData(Admin)]
    [InlineData(AnalystMicro)]
    public async Task SectionHead_CannotBeAssignedByAnyoneElse(int actingUserId)
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);
        var service = NewService(db);

        var perTest = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignTestsAsync(w.Sample.Id, new[] { new TestAnalystAssignment(w.Tamc.Id, HeadMicro) }, actingUserId));
        var wholeSample = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignAnalystAsync(w.Sample.Id, HeadMicro, actingUserId));

        Assert.Contains("a Section Head can only assign themselves", perTest.Message);
        Assert.Contains("a Section Head can only assign themselves", wholeSample.Message);
        Assert.Null(w.Tamc.AssignedAnalystId);
    }

    [Fact]
    public async Task AssignTestsAsync_AssigneeNotInTheTestsSection_Throws()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService(db).AssignTestsAsync(w.Sample.Id, new[] { new TestAnalystAssignment(w.Tamc.Id, AnalystFp) }, actingUserId: Admin));

        Assert.Contains("not a member of the Microbiology Laboratory laboratory section", ex.Message);
    }

    [Fact]
    public async Task AssignTestsAsync_SystemAdministratorCannotBeAssigned()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService(db).AssignTestsAsync(w.Sample.Id, new[] { new TestAnalystAssignment(w.Tamc.Id, Admin) }, actingUserId: Admin));

        Assert.Contains("only Analysts, Reviewers and Section Heads", ex.Message);
    }

    [Fact]
    public async Task AssignTestsAsync_ClosedTest_Throws()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService(db).AssignTestsAsync(w.Sample.Id, new[] { new TestAnalystAssignment(w.ApprovedEc.Id, AnalystMicro) }, actingUserId: HeadMicro));

        Assert.Contains("closed", ex.Message);
        Assert.Equal(99, w.ApprovedEc.AssignedAnalystId);
    }

    [Fact]
    public async Task AssignTestsAsync_TestInASectionTheActingUserIsNotIn_IsForbidden()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            NewService(db).AssignTestsAsync(w.Sample.Id, new[] { new TestAnalystAssignment(w.Assay.Id, AnalystFp) }, actingUserId: HeadMicro));
        Assert.Null(w.Assay.AssignedAnalystId);
    }

    [Fact]
    public async Task AssignTestsAsync_OneInvalidAssignment_SavesNone()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService(db).AssignTestsAsync(w.Sample.Id, new[]
            {
                new TestAnalystAssignment(w.Tamc.Id, AnalystMicro),
                new TestAnalystAssignment(w.Tymc.Id, OtherHeadMicro)
            }, actingUserId: HeadMicro));

        Assert.Null((await db.TestOrders.AsNoTracking().SingleAsync(t => t.Id == w.Tamc.Id)).AssignedAnalystId);
        Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "AssignedAnalyst"));
    }

    [Fact]
    public async Task GetAssignmentOptionsAsync_SectionHead_SeesOnlyThemselvesAmongSectionHeads_AndOnlyTheirSectionsTests()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);

        var options = await NewService(db).GetAssignmentOptionsAsync(w.Sample.Id, actingUserId: HeadMicro);

        Assert.Equal(new[] { HeadMicro, AnalystMicro, ReviewerMicro }.OrderBy(i => i), options.Users.Select(u => u.Id).OrderBy(i => i));
        var tamc = options.Tests.Single(t => t.TestOrderId == w.Tamc.Id);
        Assert.True(tamc.CanAssign);
        Assert.Equal(new[] { HeadMicro, AnalystMicro, ReviewerMicro }.OrderBy(i => i), tamc.EligibleUserIds.OrderBy(i => i));
        Assert.Equal("Microbiology Laboratory", tamc.SectionName);

        var assay = options.Tests.Single(t => t.TestOrderId == w.Assay.Id);
        Assert.False(assay.CanAssign);
        Assert.Empty(assay.EligibleUserIds);

        var ec = options.Tests.Single(t => t.TestOrderId == w.ApprovedEc.Id);
        Assert.False(ec.CanAssign);
    }

    [Fact]
    public async Task GetAssignmentOptionsAsync_Administrator_SeesNoSectionHeads_AndEachTestsOwnSectionMembers()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);

        var options = await NewService(db).GetAssignmentOptionsAsync(w.Sample.Id, actingUserId: Admin);

        Assert.DoesNotContain(options.Users, u => u.RoleType == nameof(RoleType.SectionHead));
        Assert.DoesNotContain(options.Users, u => u.Id == Admin);
        Assert.Equal(new[] { AnalystFp }, options.Tests.Single(t => t.TestOrderId == w.Assay.Id).EligibleUserIds);
        Assert.Equal(new[] { AnalystMicro, ReviewerMicro }.OrderBy(i => i),
            options.Tests.Single(t => t.TestOrderId == w.Tamc.Id).EligibleUserIds.OrderBy(i => i));
    }

    [Fact]
    public async Task SegregationOfDutiesGuard_BlocksUserWhoPreparedSampleFromReviewing()
    {
        await using var db = NewDb();
        var preparer = new User { Id = 50, Username = "prepUser", FullName = "Prep User", PasswordHash = "hash", IsActive = true };
        db.Users.Add(preparer);

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-12", Status = SampleStatus.UnderReview };
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Ready, AssignedAnalystId = 99 };
        sample.TestOrders.Add(order);

        var prep = new SamplePreparation
        {
            Sample = sample,
            Amount = 10m,
            Technique = "PourPlate",
            PreparedByUserId = 50,
            Neutralizer = "Tween",
            Diluent = "Buffer"
        };
        db.SamplePreparations.Add(prep);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var guard = new SegregationOfDutiesGuard(db);
        var performedByPreparer = await guard.DidUserPerformTestAsync(order.Id, userId: 50);
        var performedByOther = await guard.DidUserPerformTestAsync(order.Id, userId: 70);

        Assert.True(performedByPreparer);
        Assert.False(performedByOther);
    }
}
