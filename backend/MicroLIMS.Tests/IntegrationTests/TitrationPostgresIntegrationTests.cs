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

[Collection("PostgresDatabaseCollection")]
public class TitrationPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public TitrationPostgresIntegrationTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static Task<TestWorkflowResult> Record(
        MicroLimsDbContext db, TitrationScenario s, TitrationPayload p) =>
        TestServiceFactory.TestWorkflow(db).RecordTitrationResultAsync(s.Order.Id, p, s.UserId, "127.0.0.1");

    private TitrationScenario.Options Relative() => new()
    {
        ExistingUserId = _fixture.SeededUserId,
        WithStandard = true,
        Tweak = t => { t.TitrationCalculation = TitrationCalculation.Relative; t.TitrationEquivalencyFactor = null; t.TitrationBlankRequired = true; },
    };

    [PostgresFact]
    public async Task UspFactor_Direct_PostgresEndToEnd()
    {
        await using var db = _fixture.CreateDbContext();
        var s = TitrationScenario.Seed(db, new() { ExistingUserId = _fixture.SeededUserId });

        var result = await Record(db, s, TitrationRecorderTests.Payload(s));

        // rep 1: (22.6-0.1) x 0.1 x 1.0023 x 88.06 = 198.5907105 mg / 200 = 99.29535525 %
        // rep 2: (22.5-0.1) x 0.1 x 1.0023 x 88.06 = 197.70808512 mg / 200 = 98.85404256 % ; mean 99.0747 -> 99.07
        Assert.Equal(ResultStatus.WithinLimits, result.Status);
        Assert.Contains("99.07 %", result.OutcomeSummary);

        await using var v = _fixture.CreateDbContext();
        var a = await v.TestAnalyses.Include(x => x.Signature).Include(x => x.ParameterResults).ThenInclude(r => r.Readings)
            .SingleAsync(x => x.TestOrderId == s.Order.Id);
        Assert.True(a.IsActive);
        Assert.Equal(WorkflowType.Titration, a.AnalysisType);
        Assert.NotNull(a.Signature);
        var pr = Assert.Single(a.ParameterResults);
        Assert.Equal(s.Spec.Id, pr.SpecificationId);
        Assert.Equal(99.07m, pr.ReportedValue);
        Assert.Equal(2, pr.Readings.Count);
        Assert.All(pr.Readings, r => Assert.Equal(ReadingKind.Titration, r.Kind));
        var r1 = pr.Readings.OrderBy(r => r.Index).First();
        Assert.Equal(200m, r1.Value1);
        Assert.Equal(22.6m, r1.Value2);
        Assert.Equal(22.6m, r1.Value3);

        using var doc = JsonDocument.Parse(a.ConditionsJson!);
        var titrant = doc.RootElement.GetProperty("titrant");
        Assert.Equal(s.TitrantPrep.Code, titrant.GetProperty("code").GetString());
        Assert.Equal(1.0023m, titrant.GetProperty("factorUsed").GetDecimal());

        Assert.NotNull(await v.Results.FirstOrDefaultAsync(r => r.TestOrderId == s.Order.Id));
        Assert.Equal(WorkflowStep.Ready, (await v.TestOrders.FindAsync(s.Order.Id))!.CurrentStep);
    }

    [PostgresFact]
    public async Task Relative_TwoStandards_DeductsSummedWeightAndPersists()
    {
        await using var db = _fixture.CreateDbContext();
        var s = TitrationScenario.Seed(db, Relative());
        s.Spec.LowerLimit = 0m; s.Spec.UpperLimit = 1000m; db.SaveChanges();

        // K1 = 100 x 0.995 x 0.995 / (10.0-0.1) = 10.00025 ; K2 = 200 x 0.995 x 0.995 / (20.0-0.1) = 9.94975
        var p = TitrationRecorderTests.Payload(s, standards: new() { new(s.StandardLot!.Id, 100m, 10.00m), new(s.StandardLot.Id, 200m, 20.00m) });
        await Record(db, s, p);

        // 300 mg = 0.3 g off a 5 g lot
        await using var v = _fixture.CreateDbContext();
        Assert.Equal(4.7m, (await v.Materials.FindAsync(s.StandardLot.Id))!.QuantityRemaining);
        var a = await v.TestAnalyses.SingleAsync(x => x.TestOrderId == s.Order.Id);
        using var doc = JsonDocument.Parse(a.ConditionsJson!);
        var k = doc.RootElement.GetProperty("standard").GetProperty("k").GetDecimal();
        Assert.True(Math.Abs(k - (99.0025m / 9.9m + 198.005m / 19.9m) / 2m) < 0.0001m);
    }

    [PostgresFact]
    public async Task RejectedRules_RollBackNothingPersisted()
    {
        // Due titrant on UspFactor
        await using var db = _fixture.CreateDbContext();
        var s = TitrationScenario.Seed(db, new() { ExistingUserId = _fixture.SeededUserId, ValidityDays = 1, StandardizedDaysAgo = 5 });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, TitrationRecorderTests.Payload(s)));
        Assert.Contains("due", ex.Message);

        // wrong replicate count on a relative test holding stock
        await using var db2 = _fixture.CreateDbContext();
        var s2 = TitrationScenario.Seed(db2, Relative());
        var p = TitrationRecorderTests.Payload(s2, standards: new() { new(s2.StandardLot!.Id, 100m, 10m) }) with { Replicates = new() { new(200m, 22.6m) } };
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db2, s2, p));
        Assert.Contains("Exactly 2 replicate", ex2.Message);

        await using var v = _fixture.CreateDbContext();
        Assert.False(await v.TestAnalyses.AnyAsync(x => x.TestOrderId == s.Order.Id || x.TestOrderId == s2.Order.Id));
        Assert.Equal(5m, (await v.Materials.FindAsync(s2.StandardLot!.Id))!.QuantityRemaining);
        Assert.Equal(WorkflowStep.Running, (await v.TestOrders.FindAsync(s2.Order.Id))!.CurrentStep);
    }

    [PostgresFact]
    public async Task MigrationColumns_RoundTrip()
    {
        await using var db = _fixture.CreateDbContext();
        var s = TitrationScenario.Seed(db, new()
        {
            ExistingUserId = _fixture.SeededUserId, StandardizationTemperatureC = 23.5m, WithExcess = true,
            Tweak = t =>
            {
                t.TitrationType = TitrationType.AcidBase; t.TitrationNonAqueous = true; t.TitrationMode = TitrationMode.Residual;
                t.TitrationExcessVolumeMl = 25m; t.TitrationMaxRsdPercent = 1.5m; t.TitrationEndpoint = TitrationEndpoint.Potentiometric;
                t.TitrationIndicator = "Crystal violet"; t.TitrationTempCorrection = true; t.TitrationExpansionCoefficient = 0.0011m;
                t.TitrationEquivalencyFactor = 8.25m; t.TitrationBlankRequired = true;
            },
        });

        await using var v = _fixture.CreateDbContext();
        var t = await v.TestDefinitions.SingleAsync(x => x.Id == s.Test.Id);
        Assert.Equal(TitrationType.AcidBase, t.TitrationType);
        Assert.True(t.TitrationNonAqueous);
        Assert.Equal(TitrationMode.Residual, t.TitrationMode);
        Assert.Equal(TitrationCalculation.UspFactor, t.TitrationCalculation);
        Assert.Equal(s.TitrantMaster.Id, t.TitrantSolutionMasterId);
        Assert.Equal(s.ExcessMaster!.Id, t.TitrationExcessSolutionMasterId);
        Assert.Equal(25m, t.TitrationExcessVolumeMl);
        Assert.Equal(8.25m, t.TitrationEquivalencyFactor);
        Assert.Equal(1.5m, t.TitrationMaxRsdPercent);
        Assert.Equal(TitrationEndpoint.Potentiometric, t.TitrationEndpoint);
        Assert.Equal("Crystal violet", t.TitrationIndicator);
        Assert.True(t.TitrationTempCorrection);
        Assert.Equal(0.0011m, t.TitrationExpansionCoefficient);
        Assert.Equal(23.5m, (await v.TitrantStandardizations.SingleAsync(x => x.SolutionPreparationId == s.TitrantPrep.Id)).TemperatureC);
    }
}
