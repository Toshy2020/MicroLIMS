using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using MicroLIMS.Tests.UnitTests;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Dried / anhydrous titration assays read the sample's own LOD / Karl Fischer result; returning that
// moisture result returns the assays that used it.
[Collection("PostgresDatabaseCollection")]
public class SampleMoisturePostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public SampleMoisturePostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private TitrationScenario Seed(MicroLimsDbContext db, ResultBasis basis = ResultBasis.PercentDriedBasis) =>
        TitrationScenario.Seed(db, new() { ExistingUserId = _fixture.SeededUserId, TweakSpec = sp => sp.ResultBasis = basis });

    private static Task<TestWorkflowResult> Record(MicroLimsDbContext db, TitrationScenario s, TitrationPayload p) =>
        TestServiceFactory.TestWorkflow(db).RecordTitrationResultAsync(s.Order.Id, p, s.UserId, "127.0.0.1");

    // ---- resolver ----

    [PostgresFact]
    public async Task Resolver_ReturnsActiveResult_EvenWhenOos_AndIgnoresReturnedOnes()
    {
        await using var db = _fixture.CreateDbContext();
        var s = Seed(db);
        var resolver = new SampleMoistureResolver(db);
        var sampleId = s.Order.SampleId;

        Assert.Null(await resolver.ResolveAsync(sampleId, MoistureRole.LossOnDrying));

        s.AddMoistureResult(db, MoistureRole.LossOnDrying, 7m, active: false); // returned earlier
        Assert.Null(await resolver.ResolveAsync(sampleId, MoistureRole.LossOnDrying));

        var (order, pr) = s.AddMoistureResult(db, MoistureRole.LossOnDrying, 2.5m); // NMT 1 % -> OOS, still used
        var m = await resolver.ResolveAsync(sampleId, MoistureRole.LossOnDrying);
        Assert.NotNull(m);
        Assert.Equal(2.5m, m!.Percent);
        Assert.Equal(order.Id, m.TestOrderId);
        Assert.Equal(pr.Id, m.ParameterResultId);
        Assert.Equal(order.TestCode, m.TestCode);
        Assert.Equal(MoistureRole.LossOnDrying, m.Role);

        // the water role does not see the LOD result
        Assert.Null(await resolver.ResolveAsync(sampleId, MoistureRole.Water));
    }

    [PostgresFact]
    public async Task Resolver_RefusesMalformedMoistureResults()
    {
        foreach (var (role, text) in new[]
        {
            (MoistureRole.LossOnDrying, "The loss on drying test must report one numeric result."),
            (MoistureRole.Water, "The water test must report one numeric result."),
        })
        {
            foreach (var (value, count) in new (decimal?, int)[] { (null, 1), (-0.1m, 1), (100m, 1), (2m, 2) })
            {
                await using var db = _fixture.CreateDbContext();
                var s = Seed(db);
                s.AddMoistureResult(db, role, value, parameterCount: count);
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new SampleMoistureResolver(db).ResolveAsync(s.Order.SampleId, role));
                Assert.Equal(text, ex.Message);
            }
        }
    }

    // ---- titration recording ----

    [PostgresFact]
    public async Task DriedBasis_UsesRecordedLod_AndSnapshotsTheSource()
    {
        await using var db = _fixture.CreateDbContext();
        var s = Seed(db);
        var (lodOrder, lod) = s.AddMoistureResult(db, MoistureRole.LossOnDrying, 2.5m);

        var result = await Record(db, s, TitrationRecorderTests.Payload(s));

        // mean 99.07469891 x 100 / 97.5 = 101.615 -> 101.62
        Assert.Contains("101.62 %", result.OutcomeSummary);
        await using var v = _fixture.CreateDbContext();
        var a = await v.TestAnalyses.SingleAsync(x => x.TestOrderId == s.Order.Id);
        using var doc = JsonDocument.Parse(a.ConditionsJson!);
        var moisture = doc.RootElement.GetProperty("moisture");
        Assert.Equal("LossOnDrying", moisture.GetProperty("role").GetString());
        Assert.Equal(2.5m, moisture.GetProperty("percent").GetDecimal());
        Assert.Equal(lodOrder.Id, moisture.GetProperty("testOrderId").GetInt32());
        Assert.Equal(lod.Id, moisture.GetProperty("parameterResultId").GetInt32());
        Assert.Equal(lodOrder.TestCode, moisture.GetProperty("testCode").GetString());
    }

    [PostgresFact]
    public async Task AnhydrousBasis_ReadsTheWaterTest_NotTheLodTest()
    {
        await using var db = _fixture.CreateDbContext();
        var s = Seed(db, ResultBasis.PercentAnhydrousBasis);
        s.AddMoistureResult(db, MoistureRole.LossOnDrying, 9m);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, TitrationRecorderTests.Payload(s)));
        Assert.Equal("Record the sample's water content (Karl Fischer) result before this assay.", ex.Message);

        var (_, kf) = s.AddMoistureResult(db, MoistureRole.Water, 5m);
        var result = await Record(db, s, TitrationRecorderTests.Payload(s));

        // 99.07469891 x 100 / 95 = 104.29
        Assert.Contains("104.29 %", result.OutcomeSummary);
        await using var v = _fixture.CreateDbContext();
        var a = await v.TestAnalyses.SingleAsync(x => x.TestOrderId == s.Order.Id);
        using var doc = JsonDocument.Parse(a.ConditionsJson!);
        Assert.Equal(kf.Id, doc.RootElement.GetProperty("moisture").GetProperty("parameterResultId").GetInt32());
    }

    [PostgresFact]
    public async Task NoLodResult_Refused_NothingWritten()
    {
        await using var db = _fixture.CreateDbContext();
        var s = Seed(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, TitrationRecorderTests.Payload(s)));
        Assert.Equal("Record the sample's loss on drying result before this assay.", ex.Message);

        await using var v = _fixture.CreateDbContext();
        Assert.False(await v.TestAnalyses.AnyAsync(x => x.TestOrderId == s.Order.Id));
        Assert.Equal(WorkflowStep.Running, (await v.TestOrders.FindAsync(s.Order.Id))!.CurrentStep);
    }

    [PostgresFact]
    public async Task TypedMoistureValue_IsRefused_OnAnyBasis()
    {
        foreach (var basis in new[] { ResultBasis.PercentDriedBasis, ResultBasis.PercentAsIs })
        {
            await using var db = _fixture.CreateDbContext();
            var s = Seed(db, basis);
            s.AddMoistureResult(db, MoistureRole.LossOnDrying, 2.5m);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, TitrationRecorderTests.Payload(s, lod: 2.5m)));
            Assert.Equal("The loss on drying / water value is read from the sample's own result.", ex.Message);
        }
    }

    [PostgresFact]
    public async Task MixedDriedAndAnhydrousSpecs_AreRefused()
    {
        await using var db = _fixture.CreateDbContext();
        var s = Seed(db);
        s.AddMoistureResult(db, MoistureRole.LossOnDrying, 2.5m);
        s.AddMoistureResult(db, MoistureRole.Water, 5m);
        var second = new Specification
        {
            ItemId = s.Spec.ItemId, TestCode = s.Test.Code, ParameterName = "Assay 2", LimitType = LimitType.Range,
            LowerLimit = 98m, UpperLimit = 102m, Unit = "%", SpecLimit = "98-102%", DisplayOrder = 2, ResultBasis = ResultBasis.PercentAnhydrousBasis,
        };
        db.Specifications.Add(second);
        db.SaveChanges();

        var p = TitrationRecorderTests.Payload(s) with { SpecificationIds = new List<int> { s.Spec.Id, second.Id } };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, p));
        Assert.Equal("One titration cannot report on both dried and anhydrous basis.", ex.Message);
    }

    // ---- return path ----

    [PostgresFact]
    public async Task ReturningTheLodOrder_AlsoReturnsTheAssaysThatUsedIt()
    {
        await using var db = _fixture.CreateDbContext();
        var s = Seed(db);
        var (lodOrder, lod) = s.AddMoistureResult(db, MoistureRole.LossOnDrying, 2.5m);
        await Record(db, s, TitrationRecorderTests.Payload(s));
        Assert.Equal(ApprovalStatus.ResultEntered, (await db.TestOrders.FindAsync(s.Order.Id))!.Status);

        // an assay of the same sample that used another LOD result, and one past ResultEntered that used this one
        var (otherLodOrder, otherLod) = s.AddMoistureResult(db, MoistureRole.LossOnDrying, 3m, active: false);
        var unrelated = AddAssay(db, s, otherLod.Id, ApprovalStatus.ResultEntered);
        var reviewed = AddAssay(db, s, lod.Id, ApprovalStatus.Reviewed);

        var ret = await TestServiceFactory.SampleReview(db).ReturnToAnalystAsync(lodOrder.Id, s.UserId, null);
        Assert.Equal(lodOrder.Id, ret.TestOrderId);

        await using var v = _fixture.CreateDbContext();
        Assert.Equal(WorkflowStep.Running, (await v.TestOrders.FindAsync(lodOrder.Id))!.CurrentStep);

        var dependent = await v.TestOrders.FindAsync(s.Order.Id);
        Assert.Equal(WorkflowStep.Running, dependent!.CurrentStep);
        Assert.False((await v.TestAnalyses.SingleAsync(x => x.TestOrderId == s.Order.Id)).IsActive);
        Assert.All(await v.ParameterResults.Where(r => r.TestOrderId == s.Order.Id).ToListAsync(), r => Assert.False(r.IsActive));
        var events = await v.TestReturnEvents.Where(e => e.TestOrderId == s.Order.Id).ToListAsync();
        Assert.Equal("Recalculate: the loss on drying result was returned.", Assert.Single(events).Reason);

        // dependents in another state, or on another result, are untouched
        Assert.Equal(ApprovalStatus.ResultEntered, (await v.TestOrders.FindAsync(unrelated.Id))!.Status);
        Assert.True((await v.TestAnalyses.SingleAsync(x => x.TestOrderId == unrelated.Id)).IsActive);
        Assert.Equal(ApprovalStatus.Reviewed, (await v.TestOrders.FindAsync(reviewed.Id))!.Status);
        Assert.True((await v.TestAnalyses.SingleAsync(x => x.TestOrderId == reviewed.Id)).IsActive);
        Assert.Empty(await v.TestReturnEvents.Where(e => e.TestOrderId == unrelated.Id || e.TestOrderId == reviewed.Id).ToListAsync());
    }

    [PostgresFact]
    public async Task ReturningTheWaterOrder_UsesTheWaterReason()
    {
        await using var db = _fixture.CreateDbContext();
        var s = Seed(db, ResultBasis.PercentAnhydrousBasis);
        var (kfOrder, _) = s.AddMoistureResult(db, MoistureRole.Water, 5m);
        await Record(db, s, TitrationRecorderTests.Payload(s));

        await TestServiceFactory.SampleReview(db).ReturnToAnalystAsync(kfOrder.Id, s.UserId, "Re-run");

        await using var v = _fixture.CreateDbContext();
        var ev = await v.TestReturnEvents.SingleAsync(e => e.TestOrderId == s.Order.Id);
        Assert.Equal("Recalculate: the water content result was returned.", ev.Reason);
    }

    [PostgresFact]
    public async Task ReturningTheAssay_LeavesTheMoistureResultAlone()
    {
        await using var db = _fixture.CreateDbContext();
        var s = Seed(db);
        var (lodOrder, _) = s.AddMoistureResult(db, MoistureRole.LossOnDrying, 2.5m);
        await Record(db, s, TitrationRecorderTests.Payload(s));

        await TestServiceFactory.SampleReview(db).ReturnToAnalystAsync(s.Order.Id, s.UserId, "Recheck");

        await using var v = _fixture.CreateDbContext();
        Assert.Equal(ApprovalStatus.ResultEntered, (await v.TestOrders.FindAsync(lodOrder.Id))!.Status);
        Assert.True((await v.TestAnalyses.SingleAsync(x => x.TestOrderId == lodOrder.Id)).IsActive);
    }

    // A second assay order on the sample whose active analysis snapshot points at the given moisture result.
    private static TestOrder AddAssay(MicroLimsDbContext db, TitrationScenario s, int parameterResultId, ApprovalStatus status)
    {
        var order = new TestOrder
        {
            SampleId = s.Order.SampleId, TestCode = s.Test.Code, SectionId = s.Section.Id,
            CurrentStep = status == ApprovalStatus.Reviewed ? WorkflowStep.Reviewed : WorkflowStep.Ready, Status = status,
        };
        db.TestOrders.Add(order);
        var user = db.Users.First(u => u.Id == s.UserId);
        db.TestAnalyses.Add(new TestAnalysis
        {
            TestOrder = order, AnalysisType = WorkflowType.Titration, AnalysedAt = DateTime.UtcNow, IsActive = true,
            EnteredByUserId = s.UserId, EnteredAt = DateTime.UtcNow,
            ConditionsJson = JsonSerializer.Serialize(new { moisture = new { role = "LossOnDrying", percent = 2.5m, parameterResultId } }),
            Signature = new ElectronicSignature
            {
                UserId = s.UserId, UserFullNameSnapshot = user.FullName, UsernameSnapshot = user.Username, RoleSnapshot = "Analyst",
                MeaningOfSignature = SignatureMeaning.ResultRecorded, EntityType = "TestOrder", EntityId = 0, SignedAt = DateTime.UtcNow,
            },
        });
        db.SaveChanges();
        return order;
    }
}
