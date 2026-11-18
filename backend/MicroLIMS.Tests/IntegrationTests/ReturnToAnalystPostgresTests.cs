using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class ReturnToAnalystPostgresTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
    private readonly PostgresTestFixture _fixture;

    public ReturnToAnalystPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed record Seed(
        int SampleId,
        int TestOrderId,
        int ReadingId,
        int IncubationId,
        int HeadWhoTestedId,
        int OtherReviewerId,
        long BaselineMaxAuditId);

    private async Task<Seed> SeedCountTestUnderReviewAsync()
    {
        await using var arrangeDb = _fixture.CreateDbContext();
        var section = await arrangeDb.DocumentSections.AsNoTracking().SingleAsync(s => s.Code == "MICRO");
        var headRole = await arrangeDb.Roles.AsNoTracking().Where(r => r.Type == RoleType.SectionHead).OrderBy(r => r.Id).FirstAsync();
        var reviewerRole = await arrangeDb.Roles.AsNoTracking().Where(r => r.Type == RoleType.Reviewer).OrderBy(r => r.Id).FirstAsync();

        var uid = Guid.NewGuid().ToString("N")[..8];
        var headWhoTested = new User
        {
            Username = $"rta_head_{uid}",
            FullName = $"RTA Head {uid}",
            PasswordHash = "x",
            RoleId = headRole.Id,
            IsActive = true
        };
        var otherReviewer = new User
        {
            Username = $"rta_rev_{uid}",
            FullName = $"RTA Reviewer {uid}",
            PasswordHash = "x",
            RoleId = reviewerRole.Id,
            IsActive = true
        };
        arrangeDb.Users.AddRange(headWhoTested, otherReviewer);
        await arrangeDb.SaveChangesAsync();

        arrangeDb.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = headWhoTested.Id, DepartmentId = section.DepartmentId, SectionId = section.Id },
            new UserOrgMembership { UserId = otherReviewer.Id, DepartmentId = section.DepartmentId, SectionId = section.Id });

        var cause = await arrangeDb.CausesOfTesting.FirstOrDefaultAsync();
        if (cause is null)
        {
            cause = new CauseOfTesting { Name = "Routine Testing", IsActive = true };
            arrangeDb.CausesOfTesting.Add(cause);
            await arrangeDb.SaveChangesAsync();
        }

        var testCode = $"RTA{uid}";
        arrangeDb.TestDefinitions.Add(new TestDefinition
        {
            Code = testCode,
            DisplayName = $"RTA Count {uid}",
            SectionId = section.Id,
            WorkflowType = WorkflowType.CountTest,
            IsActive = true
        });

        var sample = new Sample
        {
            ReferenceNumber = $"RTA-{uid}",
            Category = SampleCategory.FinishedProduct,
            CauseOfTestingId = cause.Id,
            ReceivedAt = Now.AddHours(-4),
            ReceivedByUserId = _fixture.SeededUserId,
            Status = SampleStatus.UnderReview
        };
        arrangeDb.Samples.Add(sample);
        await arrangeDb.SaveChangesAsync();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = testCode,
            SectionId = section.Id,
            Status = ApprovalStatus.ResultEntered,
            CurrentStep = WorkflowStep.Ready,
            AssignedAnalystId = headWhoTested.Id
        };
        arrangeDb.TestOrders.Add(order);
        await arrangeDb.SaveChangesAsync();

        arrangeDb.SampleSectionSignoffs.Add(new SampleSectionSignoff
        {
            SampleId = sample.Id,
            SectionId = section.Id,
            Status = SectionSignoffStatus.UnderReview,
            SubmittedForReviewAt = Now.AddMinutes(-30)
        });

        var reading = new CountTestReading
        {
            TestOrderId = order.Id,
            StepName = "CountIncubation",
            PlateReadings = "10,12",
            DilutionFactor = 1m,
            Average = 11m,
            CalculatedResult = 11m,
            ReportedResult = "11",
            Status = ResultStatus.WithinLimits,
            IsActive = true,
            EnteredByUserId = headWhoTested.Id,
            EnteredAt = Now.AddMinutes(-30)
        };
        arrangeDb.CountTestReadings.Add(reading);

        var incubation = new Incubation
        {
            TestOrderId = order.Id,
            StepNumber = 1,
            StepName = "CountIncubation",
            StartedAt = Now.AddHours(-3),
            CompletedAt = Now.AddMinutes(-30),
            CompletedByUserId = headWhoTested.Id,
            Outcome = "11 CFU/g",
            StartedByUserId = headWhoTested.Id
        };
        arrangeDb.Incubations.Add(incubation);
        await arrangeDb.SaveChangesAsync();

        var maxAuditId = await arrangeDb.AuditLogs.AsNoTracking().MaxAsync(a => (long?)a.Id) ?? 0L;
        return new Seed(sample.Id, order.Id, reading.Id, incubation.Id, headWhoTested.Id, otherReviewer.Id, maxAuditId);
    }

    [PostgresFact]
    public async Task ReturnToAnalyst_BySectionHeadWhoPerformedTest_Refused_NothingChangesNoAudit()
    {
        var seed = await SeedCountTestUnderReviewAsync();

        await using (var actDb = _fixture.CreateDbContext())
        {
            var reviewSvc = TestServiceFactory.SampleReview(actDb);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => reviewSvc.ReturnToAnalystAsync(seed.TestOrderId, seed.HeadWhoTestedId, "Self return"));
            Assert.Equal(SignoffRefusalSentences.CannotReviewTested, ex.Message);
            Assert.Equal(MessageCodes.SampleCannotReviewTested, ex.Code);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var order = await assertDb.TestOrders.AsNoTracking().SingleAsync(o => o.Id == seed.TestOrderId);
        Assert.Equal(ApprovalStatus.ResultEntered, order.Status);
        Assert.Equal(WorkflowStep.Ready, order.CurrentStep);

        var sample = await assertDb.Samples.AsNoTracking().SingleAsync(s => s.Id == seed.SampleId);
        Assert.Equal(SampleStatus.UnderReview, sample.Status);

        var reading = await assertDb.CountTestReadings.AsNoTracking().SingleAsync(r => r.Id == seed.ReadingId);
        Assert.True(reading.IsActive);

        var incubation = await assertDb.Incubations.AsNoTracking().SingleAsync(i => i.Id == seed.IncubationId);
        Assert.NotNull(incubation.CompletedAt);

        Assert.Empty(await assertDb.TestReturnEvents.AsNoTracking().Where(e => e.TestOrderId == seed.TestOrderId).ToListAsync());
        Assert.Empty(await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.Id > seed.BaselineMaxAuditId && (a.TestOrderId == seed.TestOrderId || a.EntityId == seed.TestOrderId.ToString()))
            .ToListAsync());
    }

    [PostgresFact]
    public async Task ReturnToAnalyst_ByDifferentReviewer_Succeeds_WithReturnAndAuditRows()
    {
        var seed = await SeedCountTestUnderReviewAsync();

        await using (var actDb = _fixture.CreateDbContext())
        {
            var reviewSvc = TestServiceFactory.SampleReview(actDb);
            var ret = await reviewSvc.ReturnToAnalystAsync(seed.TestOrderId, seed.OtherReviewerId, "Verify plate count");
            Assert.Equal(seed.TestOrderId, ret.TestOrderId);
            Assert.Equal(seed.OtherReviewerId, ret.ReviewerUserId);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var order = await assertDb.TestOrders.AsNoTracking().SingleAsync(o => o.Id == seed.TestOrderId);
        Assert.Equal(ApprovalStatus.InProgress, order.Status);
        Assert.Equal(WorkflowStep.Incubating, order.CurrentStep);

        var sample = await assertDb.Samples.AsNoTracking().SingleAsync(s => s.Id == seed.SampleId);
        Assert.Equal(SampleStatus.InTesting, sample.Status);

        var reading = await assertDb.CountTestReadings.AsNoTracking().SingleAsync(r => r.Id == seed.ReadingId);
        Assert.False(reading.IsActive);

        var incubation = await assertDb.Incubations.AsNoTracking().SingleAsync(i => i.Id == seed.IncubationId);
        Assert.Null(incubation.CompletedAt);

        var retEvent = await assertDb.TestReturnEvents.AsNoTracking().SingleAsync(e => e.TestOrderId == seed.TestOrderId);
        Assert.Equal(seed.OtherReviewerId, retEvent.ReviewerUserId);
        Assert.Equal(seed.HeadWhoTestedId, retEvent.AssignedAnalystId);
        Assert.Equal("Verify plate count", retEvent.Reason);

        var audits = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.Id > seed.BaselineMaxAuditId && a.EntityName == "TestOrder" && a.EntityId == seed.TestOrderId.ToString())
            .ToListAsync();
        Assert.NotEmpty(audits);
    }
}
