using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Named golden values for the titration engine (spec 2026-10-05, B2.3). These pin
// current behaviour: a failure here means the engine changed, not the test.
public class TitrationRegressionTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static TitrationPayload SingleReplicate(TitrationScenario s, decimal weightMg, decimal volumeMl,
        decimal? blank = null, int? excessPrepId = null) =>
        new(DateTime.UtcNow, s.Equipment.Id, s.TitrantPrep.Id, excessPrepId, blank, null, null, null, null,
            new List<int> { s.Spec.Id }, new List<TitrationReplicateInput> { new(weightMg, volumeMl) },
            TitrationScenario.Password, "regression");

    private static Task<TestWorkflowResult> Record(MicroLimsDbContext db, TitrationScenario s, TitrationPayload p) =>
        TestServiceFactory.TestWorkflow(db).RecordTitrationResultAsync(s.Order.Id, p, s.UserId, "127.0.0.1");

    private static TitrationScenario SeedCitric(MicroLimsDbContext db) => TitrationScenario.Seed(db, new()
    {
        Nominal = 1m, Factor = 0.9990m,
        Tweak = t =>
        {
            t.TitrationType = TitrationType.AcidBase; t.TitrationCalculation = TitrationCalculation.UspFactor;
            t.TitrationMode = TitrationMode.Direct; t.TitrationEquivalencyFactor = 64.03m;
            t.TitrationBlankRequired = false; t.ReplicateCount = 1;
        },
        TweakSpec = sp => { sp.LowerLimit = 99.5m; sp.UpperLimit = 100.5m; sp.ResultBasis = ResultBasis.PercentAsIs; },
    });

    [Fact]
    public async Task CitricAcid_Direct_AcidBase_Gives_99_57()
    {
        using var db = NewDb();
        var s = SeedCitric(db);
        // 8.58 x 1 x 0.9990 x 64.03 / 551.2 x 100 = 99.57 %
        var result = await Record(db, s, SingleReplicate(s, 551.2m, 8.58m));
        Assert.Equal(ResultStatus.WithinLimits, result.Status);
        var pr = await db.ParameterResults.SingleAsync(x => x.TestOrderId == s.Order.Id);
        Assert.Equal(99.57m, pr.ReportedValue);
        Assert.Equal(ResultStatus.WithinLimits, pr.ComparisonStatus);
    }

    [Fact]
    public async Task Aspirin_Residual_WithBlank_Gives_99_86()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, new()
        {
            Nominal = 0.5m, Factor = 1.002m, WithExcess = true,
            Tweak = t =>
            {
                t.TitrationType = TitrationType.AcidBase; t.TitrationCalculation = TitrationCalculation.UspFactor;
                t.TitrationMode = TitrationMode.Residual; t.TitrationEquivalencyFactor = 90.08m;
                t.TitrationBlankRequired = true; t.TitrationExcessVolumeMl = 25m; t.ReplicateCount = 1;
            },
        });
        // (50.10 - 16.80) x 0.5 x 1.002 x 90.08 / 1505.0 x 100 = 99.86 %
        await Record(db, s, SingleReplicate(s, 1505.0m, 16.80m, blank: 50.10m, excessPrepId: s.ExcessPrep!.Id));
        var pr = await db.ParameterResults.SingleAsync(x => x.TestOrderId == s.Order.Id);
        Assert.Equal(99.86m, pr.ReportedValue);
    }

    [Fact]
    public async Task FailedStandardizationOnly_IsBlocked_AndRecordingThrows()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, new() { Standardized = false });
        var user = db.Users.First(u => u.Id == s.UserId);
        db.TitrantStandardizations.Add(new TitrantStandardization
        {
            SolutionPreparationId = s.TitrantPrep.Id, Mode = StandardizationMode.PrimaryStandard, MeanFactor = 1.2m, Passed = false,
            StandardizedByUserId = s.UserId, StandardizedAt = DateTime.UtcNow, ValidUntil = DateTime.UtcNow.AddDays(30), TemperatureC = 25m,
            Signature = new ElectronicSignature
            {
                UserId = s.UserId, UserFullNameSnapshot = user.FullName, UsernameSnapshot = user.Username, RoleSnapshot = "Analyst",
                MeaningOfSignature = SignatureMeaning.TitrantStandardized, EntityType = "TitrantStandardization",
                EntityId = s.TitrantPrep.Id, SignedAt = DateTime.UtcNow,
            },
        });
        db.SaveChanges();

        var ctx = await new TitrationContextService(db, new UserSectionScopeService(db)).GetContextAsync(s.Order.Id, s.UserId);
        var opt = Assert.Single(ctx.TitrantPreparations);
        Assert.False(opt.Usable);
        Assert.Equal("This titrant preparation has not been standardized.", opt.BlockReason);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Record(db, s, TitrationRecorderTests.Payload(s)));
    }

    [Fact]
    public async Task RestandardizeAfterRecording_DoesNotChangeStoredResult()
    {
        using var db = NewDb();
        var s = SeedCitric(db);
        await Record(db, s, SingleReplicate(s, 551.2m, 8.58m));

        async Task<(decimal? Reported, string Readings, string Conditions)> Snapshot()
        {
            var a = await db.TestAnalyses.AsNoTracking().Include(x => x.ParameterResults).ThenInclude(r => r.Readings)
                .SingleAsync(x => x.TestOrderId == s.Order.Id);
            var pr = a.ParameterResults.Single();
            var readings = JsonSerializer.Serialize(pr.Readings.OrderBy(r => r.Index)
                .Select(r => new { r.Index, r.Value1, r.Value2, r.Value3, r.ComputedValue }));
            return (pr.ReportedValue, readings, a.ConditionsJson!);
        }

        var before = await Snapshot();
        Assert.Equal(0.9990m, JsonDocument.Parse(before.Conditions).RootElement.GetProperty("titrant").GetProperty("factorUsed").GetDecimal());

        var user = db.Users.First(u => u.Id == s.UserId);
        db.TitrantStandardizations.Add(new TitrantStandardization
        {
            SolutionPreparationId = s.TitrantPrep.Id, Mode = StandardizationMode.PrimaryStandard, MeanFactor = 1.0100m, Passed = true,
            StandardizedByUserId = s.UserId, StandardizedAt = DateTime.UtcNow.AddMinutes(5), ValidUntil = DateTime.UtcNow.AddDays(30), TemperatureC = 25m,
            Signature = new ElectronicSignature
            {
                UserId = s.UserId, UserFullNameSnapshot = user.FullName, UsernameSnapshot = user.Username, RoleSnapshot = "Analyst",
                MeaningOfSignature = SignatureMeaning.TitrantStandardized, EntityType = "TitrantStandardization",
                EntityId = s.TitrantPrep.Id, SignedAt = DateTime.UtcNow.AddMinutes(5),
            },
        });
        db.SaveChanges();

        var after = await Snapshot();
        Assert.Equal(before.Reported, after.Reported);
        Assert.Equal(before.Readings, after.Readings);
        Assert.Equal(before.Conditions, after.Conditions);
        Assert.Equal(0.9990m, JsonDocument.Parse(after.Conditions).RootElement.GetProperty("titrant").GetProperty("factorUsed").GetDecimal());
    }
}
