using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// User decision 2026-11-15: resetting a testing session keeps the recorded results,
// marked invalidated with who, when and why, instead of deleting them. Normal reads
// no longer see an invalidated result. Arrange, act and assert each use their own
// DbContext.
[Collection("PostgresDatabaseCollection")]
public class PathogenResetInvalidationPostgresTests
{
    private static readonly DateTime ResetAt = new(2026, 11, 15, 9, 30, 0, DateTimeKind.Utc);
    private const string Reason = "Wrong enrichment broth lot was used";

    private readonly PostgresTestFixture _fixture;

    public PathogenResetInvalidationPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }

    private static PathogenSessionService Service(MicroLimsDbContext db, DateTime nowUtc) =>
        TestServiceFactory.PathogenSession(db, timeProvider: new FixedClock(nowUtc));

    private async Task<(int SampleId, int OrderId, int ResultId)> SeedAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var cause = await db.CausesOfTesting.FirstOrDefaultAsync();
        if (cause is null)
        {
            cause = new CauseOfTesting { Name = "Routine Testing", IsActive = true };
            db.CausesOfTesting.Add(cause);
            await db.SaveChangesAsync();
        }
        var section = await db.DocumentSections.AsNoTracking().FirstAsync(s => s.Code == "MICRO");
        var uid = Guid.NewGuid().ToString("N")[..8];

        var sample = new Sample
        {
            ReferenceNumber = $"SMP-RST-{uid}", Category = SampleCategory.FinishedProduct, CauseOfTestingId = cause.Id,
            ReceivedAt = ResetAt.AddDays(-2), ReceivedByUserId = _fixture.SeededUserId, Status = SampleStatus.InTesting,
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var order = new TestOrder
        {
            SampleId = sample.Id, TestCode = $"PATH-{uid}", SectionId = section.Id,
            CurrentStep = WorkflowStep.Running, Status = ApprovalStatus.InProgress,
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        var result = new Result
        {
            TestOrderId = order.Id, RawValue = "Detected", InterpretedValue = "Detected", Type = ResultType.GrowthNoGrowth,
            EnteredByUserId = _fixture.SeededUserId, EnteredAt = ResetAt.AddHours(-3),
        };
        db.Results.Add(result);
        await db.SaveChangesAsync();
        return (sample.Id, order.Id, result.Id);
    }

    [PostgresFact]
    public async Task Reset_KeepsResult_MarkedInvalidated_HiddenFromNormalReads_Audited()
    {
        var (sampleId, orderId, resultId) = await SeedAsync();

        await using (var actDb = _fixture.CreateDbContext())
            await Service(actDb, ResetAt).ResetSessionAsync(sampleId, "  " + Reason + "  ", _fixture.SeededUserId);

        await using var assertDb = _fixture.CreateDbContext();

        // Normal reads (every existing query) no longer see it.
        Assert.False(await assertDb.Results.AsNoTracking().AnyAsync(r => r.TestOrderId == orderId));
        Assert.Empty((await assertDb.TestOrders.AsNoTracking().Include(o => o.Results).SingleAsync(o => o.Id == orderId)).Results);

        // The row itself is kept, values untouched, with who, when and why.
        var kept = await assertDb.Results.IgnoreQueryFilters().AsNoTracking().SingleAsync(r => r.Id == resultId);
        Assert.Equal("Detected", kept.RawValue);
        Assert.Equal(_fixture.SeededUserId, kept.EnteredByUserId);
        Assert.Equal(ResetAt, kept.InvalidatedAt);
        Assert.Equal(_fixture.SeededUserId, kept.InvalidatedByUserId);
        Assert.Equal(Reason, kept.InvalidationReason);

        var order = await assertDb.TestOrders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(WorkflowStep.Waiting, order.CurrentStep);
        Assert.Equal(ApprovalStatus.Pending, order.Status);
        Assert.Equal(SampleStatus.Received, (await assertDb.Samples.AsNoTracking().SingleAsync(s => s.Id == sampleId)).Status);

        // Audit: the invalidation is an update carrying the reason; the result was never deleted.
        var resultAudits = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(Result) && a.EntityId == resultId.ToString())
            .ToListAsync();
        var update = Assert.Single(resultAudits, a => a.Action == "Update");
        Assert.Equal(Reason, update.Reason);
        Assert.Contains(Reason, update.NewValue);
        Assert.DoesNotContain(resultAudits, a => a.Action == "Delete");
    }

    [PostgresFact]
    public async Task SecondReset_KeepsTheFirstInvalidation_AndInvalidatesTheNewResult()
    {
        var (sampleId, orderId, firstId) = await SeedAsync();
        await using (var actDb = _fixture.CreateDbContext())
            await Service(actDb, ResetAt).ResetSessionAsync(sampleId, Reason, _fixture.SeededUserId);

        int secondId;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var again = new Result
            {
                TestOrderId = orderId, RawValue = "Not detected", Type = ResultType.GrowthNoGrowth,
                EnteredByUserId = _fixture.SeededUserId, EnteredAt = ResetAt.AddHours(5),
            };
            arrange.Results.Add(again);
            (await arrange.Samples.SingleAsync(s => s.Id == sampleId)).Status = SampleStatus.InTesting;
            await arrange.SaveChangesAsync();
            secondId = again.Id;
        }

        const string secondReason = "Plate contaminated during transfer";
        await using (var actDb = _fixture.CreateDbContext())
            await Service(actDb, ResetAt.AddDays(1)).ResetSessionAsync(sampleId, secondReason, _fixture.SeededUserId);

        await using var assertDb = _fixture.CreateDbContext();
        var rows = await assertDb.Results.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.TestOrderId == orderId).ToDictionaryAsync(r => r.Id);
        Assert.Equal(2, rows.Count);
        Assert.Equal(Reason, rows[firstId].InvalidationReason);
        Assert.Equal(ResetAt, rows[firstId].InvalidatedAt);
        Assert.Equal(secondReason, rows[secondId].InvalidationReason);
        Assert.Equal(ResetAt.AddDays(1), rows[secondId].InvalidatedAt);
    }

    // [PostgresFact] looping the cases, not a multi-row [PostgresTheory]: skipped without a database a
    // theory counts as one test, and CI's verify_test_results.py then sees different totals for its two runs.
    [PostgresFact]
    public async Task Reset_WithoutAReasonOfTenCharacters_Refused_NothingChanged()
    {
        var cases = new string?[] { null, "   ", "too short" };

        await PostgresCases.RunAsync(cases, async reason =>
        {
            var (sampleId, orderId, resultId) = await SeedAsync();

            await using (var actDb = _fixture.CreateDbContext())
            {
                var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
                    Service(actDb, ResetAt).ResetSessionAsync(sampleId, reason, _fixture.SeededUserId));
                Assert.Equal(PathogenSessionService.ResetReasonRequiredMessage, ex.Message);
            }

            await using var assertDb = _fixture.CreateDbContext();
            var result = await assertDb.Results.AsNoTracking().SingleAsync(r => r.Id == resultId);
            Assert.Null(result.InvalidatedAt);
            var order = await assertDb.TestOrders.AsNoTracking().SingleAsync(o => o.Id == orderId);
            Assert.Equal(WorkflowStep.Running, order.CurrentStep);
            Assert.Equal(SampleStatus.InTesting, (await assertDb.Samples.AsNoTracking().SingleAsync(s => s.Id == sampleId)).Status);
        });
    }
}
