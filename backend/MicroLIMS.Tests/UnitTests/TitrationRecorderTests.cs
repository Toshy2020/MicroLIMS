using MicroLIMS.Application.DTOs;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class TitrationRecorderTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    internal static TitrationPayload Payload(TitrationScenario s,
        decimal w1 = 200m, decimal v1 = 22.6m, decimal w2 = 200m, decimal v2 = 22.5m,
        decimal? blank = 0.1m, int? excessPrepId = null, decimal? temp = null, decimal? lod = null, decimal? avgWt = null,
        List<TitrationStandardInput>? standards = null, int? titrantPrepId = null) =>
        new(DateTime.UtcNow, s.Equipment.Id, titrantPrepId ?? s.TitrantPrep.Id, excessPrepId, blank, temp, lod, avgWt, standards,
            new List<int> { s.Spec.Id },
            new List<TitrationReplicateInput> { new(w1, v1), new(w2, v2) },
            TitrationScenario.Password, "titration test");

    private static Task<TestWorkflowResult> Record(MicroLimsDbContext db, TitrationScenario s, TitrationPayload p) =>
        TestServiceFactory.TestWorkflow(db).RecordTitrationResultAsync(s.Order.Id, p, s.UserId, "127.0.0.1");

    [Fact]
    public async Task UspFactor_Direct_RecordsSignedResultWithSnapshotAndReadings()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db);

        var result = await Record(db, s, Payload(s));

        // replicate 1: (22.6-0.1) x 0.1 x 1.0023 x 88.06 = 198.5907105 mg / 200 = 99.29535525 %
        // replicate 2: (22.5-0.1) x 0.1 x 1.0023 x 88.06 = 197.70808512 mg / 200 = 98.85404256 % ; mean 99.07469891 -> 99.07
        Assert.Equal(ResultStatus.WithinLimits, result.Status);
        Assert.Contains("99.07 %", result.OutcomeSummary);

        var a = await db.TestAnalyses.Include(x => x.Signature).Include(x => x.ParameterResults).ThenInclude(r => r.Readings)
            .SingleAsync(x => x.TestOrderId == s.Order.Id);
        Assert.Equal(WorkflowType.Titration, a.AnalysisType);
        Assert.NotNull(a.Signature);
        var pr = Assert.Single(a.ParameterResults);
        Assert.Equal(99.07m, pr.ReportedValue);
        Assert.Equal("99.07 %", pr.ReportedDisplay); // the mean, rounded - this is what the CoA prints
        Assert.Equal(ResultBasis.PercentAsIs, pr.ResultBasis);
        Assert.Equal(2, pr.Readings.Count);
        Assert.All(pr.Readings, r => Assert.Equal(ReadingKind.Titration, r.Kind));
        var r1 = pr.Readings.OrderBy(r => r.Index).First();
        Assert.Equal(200m, r1.Value1);
        Assert.Equal(22.6m, r1.Value2);
        Assert.Equal(22.6m, r1.Value3);
        Assert.True(Math.Abs(99.29535525m - r1.ComputedValue!.Value) < 0.0001m);

        using var doc = JsonDocument.Parse(a.ConditionsJson!);
        var titrant = doc.RootElement.GetProperty("titrant");
        Assert.Equal(s.TitrantPrep.Id, titrant.GetProperty("preparationId").GetInt32());
        Assert.Equal(1.0023m, titrant.GetProperty("factorUsed").GetDecimal());
        Assert.Equal(s.Standardization!.Id, titrant.GetProperty("standardizationId").GetInt32());
        Assert.Equal("Redox", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(0.1m, doc.RootElement.GetProperty("blankVolumeMl").GetDecimal());
    }

    [Fact]
    public async Task RsdAboveMaximum_FlagsReview()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, new() { Tweak = t => t.TitrationMaxRsdPercent = 0.1m });
        // values 99.2954 and 98.8540: RSD ~0.315 % > 0.1 %
        var result = await Record(db, s, Payload(s));
        Assert.Equal(ResultStatus.RequiresReview, result.Status);
    }

    [Fact]
    public async Task OutOfSpecificationMean_IsOos()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db);
        // W 400 mg -> ~49.6 %
        var result = await Record(db, s, Payload(s, w1: 400m, w2: 400m));
        Assert.Equal(ResultStatus.OutOfSpecification, result.Status);
    }

    [Fact]
    public async Task WrongPassword_LeavesNothingBehind()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, new() { WithStandard = true, Tweak = t => { t.TitrationCalculation = TitrationCalculation.Relative; t.TitrationEquivalencyFactor = null; } });
        var p = Payload(s, standards: new() { new(s.StandardLot!.Id, 100m, 10m) }) with { Password = "wrong" };
        await Assert.ThrowsAnyAsync<Exception>(() => Record(db, s, p));
        Assert.Empty(db.TestAnalyses);
        Assert.Equal(5m, (await db.Materials.FindAsync(s.StandardLot.Id))!.QuantityRemaining);
    }

    [Fact]
    public async Task ReplicateCountMustMatch()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db);
        var p = Payload(s) with { Replicates = new() { new(200m, 22.6m) } };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, p));
        Assert.Contains("Exactly 2 replicate", ex.Message);
    }

    [Fact]
    public async Task BlankGivenIffRequired()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, Payload(s, blank: null)));
        Assert.Contains("blank", ex.Message);

        using var db2 = NewDb();
        var s2 = TitrationScenario.Seed(db2, new() { Tweak = t => t.TitrationBlankRequired = false });
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db2, s2, Payload(s2, blank: 0.1m)));
        Assert.Contains("does not use a blank", ex2.Message);
    }

    [Fact]
    public async Task UspFactor_NotStandardized_Blocked()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, new() { Standardized = false });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, Payload(s)));
        Assert.Contains("not been standardized", ex.Message);
    }

    [Fact]
    public async Task UspFactor_Due_Blocked()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, new() { ValidityDays = 1, StandardizedDaysAgo = 5 });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, Payload(s)));
        Assert.Contains("due", ex.Message);
    }

    [Fact]
    public async Task UspFactor_BeforeEachUse_NotToday_Blocked()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, new() { ValidityDays = 0, StandardizedDaysAgo = 2 });
        var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => Record(db, s, Payload(s)));
        Assert.Contains("before each use", ex.Message);
    }

    [Fact]
    public async Task TitrantOfAnotherMaster_Rejected()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, new() { WithExcess = true });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, Payload(s, titrantPrepId: s.ExcessPrep!.Id)));
        Assert.Contains("not of the solution configured", ex.Message);
    }

    [Fact]
    public async Task TemperatureCorrection_UsesStandardizationTemperature_AndStoresCorrectedVolume()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, new()
        {
            Tweak = t =>
            {
                t.TitrationType = TitrationType.AcidBase; t.TitrationNonAqueous = true; t.TitrationEndpoint = TitrationEndpoint.Potentiometric;
                t.TitrationIndicatorEntryId = null; t.TitrationIndicator = null; t.TitrationTempCorrection = true; t.TitrationExpansionCoefficient = 0.0011m;
            },
            TweakSpec = sp => { sp.LowerLimit = 0m; sp.UpperLimit = 500m; },
        });
        // Tstd 25, Tt 30: V' = V x 0.9945; blank 0.1 -> 0.09945
        var result = await Record(db, s, Payload(s, temp: 30m));
        Assert.NotEqual(ResultStatus.OutOfSpecification, result.Status);
        var reading = await db.ResultReadings.OrderBy(r => r.Index).FirstAsync();
        Assert.Equal(22.6m, reading.Value2);
        Assert.True(Math.Abs(22.6m * 0.9945m - reading.Value3!.Value) < 0.00001m);

        // temperature required / forbidden
        using var db2 = NewDb();
        var s2 = TitrationScenario.Seed(db2, new()
        {
            Tweak = t => { t.TitrationType = TitrationType.AcidBase; t.TitrationNonAqueous = true; t.TitrationTempCorrection = true; t.TitrationExpansionCoefficient = 0.0011m; },
        });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db2, s2, Payload(s2)));
        Assert.Contains("titration temperature", ex.Message);
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, TitrationScenario.Seed(db), Payload(s, temp: 30m)));
        Assert.NotNull(ex2);
    }

    [Fact]
    public async Task TemperatureCorrection_BlockedWhenStandardizationHasNoTemperature()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, new()
        {
            StandardizationTemperatureC = null,
            Tweak = t => { t.TitrationType = TitrationType.AcidBase; t.TitrationNonAqueous = true; t.TitrationTempCorrection = true; t.TitrationExpansionCoefficient = 0.0011m; },
        });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, Payload(s, temp: 30m)));
        Assert.Contains("record the temperature", ex.Message);

        var ctx = await new TitrationContextService(db, new UserSectionScopeService(db)).GetContextAsync(s.Order.Id, s.UserId);
        var opt = Assert.Single(ctx.TitrantPreparations);
        Assert.False(opt.Usable);
    }

    [Fact]
    public async Task Residual_RequiresExcessPreparation_AndUsesBlankForm()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, new()
        {
            WithExcess = true,
            Tweak = t =>
            {
                t.TitrationMode = TitrationMode.Residual; t.TitrationExcessVolumeMl = 25m; t.TitrationEquivalencyFactor = 8.0m;
                t.TitrationBlankRequired = true;
            },
            TweakSpec = sp => { sp.LowerLimit = 0m; sp.UpperLimit = 1000m; },
        });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, Payload(s, blank: 25.1m, v1: 10.3m, v2: 10.3m)));
        Assert.Contains("excess titrant preparation is required", ex.Message);

        // blank 25.1, V 10.3: net 14.8 x 0.1 x 1.0023 x 8.0 = 11.8672... mg ; W 200 -> 5.93 %
        var result = await Record(db, s, Payload(s, blank: 25.1m, v1: 10.3m, v2: 10.3m, excessPrepId: s.ExcessPrep!.Id));
        Assert.Contains("5.93 %", result.OutcomeSummary);
    }

    [Fact]
    public async Task Residual_WithoutBlank_UsesExcessMinusBack()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, new()
        {
            WithExcess = true,
            Tweak = t =>
            {
                t.TitrationMode = TitrationMode.Residual; t.TitrationExcessVolumeMl = 25m; t.TitrationEquivalencyFactor = 8.0m;
                t.TitrationBlankRequired = false;
            },
            TweakSpec = sp => { sp.LowerLimit = 0m; sp.UpperLimit = 1000m; },
        });
        // excess 25 x 0.1 x 1.0010 = 2.5025 ; back 10.3 x 0.1 x 1.0023 = 1.032369 ; diff 1.470131 x 8 = 11.761048 mg ; /200 = 5.880524 -> 5.88 %
        var result = await Record(db, s, Payload(s, blank: null, v1: 10.3m, v2: 10.3m, excessPrepId: s.ExcessPrep!.Id));
        Assert.Contains("5.88 %", result.OutcomeSummary);
    }

    [Fact]
    public async Task KarlFischer_WaterPercent()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, new()
        {
            Unit = TitrantStrengthUnit.MgWaterPerMl, Nominal = 5.0m, Factor = 1.0024m,
            Tweak = t =>
            {
                t.TitrationType = TitrationType.KarlFischer; t.TitrationEquivalencyFactor = null; t.TitrationEndpoint = TitrationEndpoint.Potentiometric;
                t.TitrationIndicatorEntryId = null; t.TitrationIndicator = null; t.TitrationBlankRequired = true;
            },
            TweakSpec = sp => { sp.LowerLimit = null; sp.LimitType = LimitType.NotMoreThan; sp.UpperLimit = 6m; },
        });
        // F_KF = 5.0 x 1.0024 = 5.012 ; (4.20 - 0.05) x 5.012 = 20.7998 mg ; / 500 mg = 4.15996 % -> 4.16
        var p = Payload(s, w1: 500m, w2: 500m, v1: 4.20m, v2: 4.20m, blank: 0.05m);
        var result = await Record(db, s, p);
        Assert.Contains("4.16 %", result.OutcomeSummary);
        Assert.Equal(ResultStatus.WithinLimits, result.Status);
    }

    [Fact]
    public async Task DriedBasis_NeedsLossOnDrying_AndAppliesIt()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, new() { TweakSpec = sp => sp.ResultBasis = ResultBasis.PercentDriedBasis });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, Payload(s)));
        Assert.Contains("Loss on drying", ex.Message);

        // 99.07469891 x 100 / 95 = 104.2891 -> OOS above 102
        var result = await Record(db, s, Payload(s, lod: 5m));
        Assert.Equal(ResultStatus.OutOfSpecification, result.Status);
        Assert.Contains("104.29 %", result.OutcomeSummary);

        using var db2 = NewDb();
        var s2 = TitrationScenario.Seed(db2);
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db2, s2, Payload(s2, lod: 5m)));
        Assert.Contains("only used with a dried", ex2.Message);
    }

    [Fact]
    public async Task LabelClaim_NeedsAverageUnitWeight()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, new() { TweakSpec = sp => { sp.ResultBasis = ResultBasis.PercentLabelClaim; sp.LabelClaim = 100m; sp.LabelClaimUnit = "mg"; } });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, Payload(s)));
        Assert.Contains("average unit weight", ex.Message);

        // mg / W x AvgWt / claim x 100 : 99.0747 % x 200 / 200... AvgWt 200 mg, claim 100 mg -> 198.15 %
        var result = await Record(db, s, Payload(s, avgWt: 200m));
        Assert.Equal(ResultStatus.OutOfSpecification, result.Status);
        Assert.Contains("198.15 %", result.OutcomeSummary);
    }

    private static TitrationScenario.Options RelativeOptions() => new()
    {
        WithStandard = true,
        Tweak = t =>
        {
            t.TitrationCalculation = TitrationCalculation.Relative; t.TitrationEquivalencyFactor = null;
            t.TitrationBlankRequired = true;
        },
    };

    [Fact]
    public async Task Relative_ComputesKFromStandards_AndDeductsStock()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, RelativeOptions());
        // K = 100 x 0.995 x 0.995 / (10.00 - 0.10) = 10.00025253 mg/mL ; replicate (22.6-0.1) x K = 225.0056818 mg ; / 250 -> 90.0 % (as is)
        s.Spec.LowerLimit = 0m; s.Spec.UpperLimit = 1000m; db.SaveChanges();
        var p = Payload(s, w1: 250m, w2: 250m, v1: 22.6m, v2: 22.6m, standards: new() { new(s.StandardLot!.Id, 100m, 10.00m) });
        var result = await Record(db, s, p);
        Assert.Contains("90.00 %", result.OutcomeSummary);

        // 100 mg of a 5 g lot -> 4.9 g left
        Assert.Equal(4.9m, (await db.Materials.FindAsync(s.StandardLot.Id))!.QuantityRemaining);

        var a = await db.TestAnalyses.SingleAsync(x => x.TestOrderId == s.Order.Id);
        using var doc = JsonDocument.Parse(a.ConditionsJson!);
        var std = doc.RootElement.GetProperty("standard");
        Assert.Equal(s.StandardLot.Id, std.GetProperty("lotId").GetInt32());
        Assert.True(Math.Abs(10.00025253m - std.GetProperty("k").GetDecimal()) < 0.0001m);
        Assert.Equal(1, std.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public async Task Relative_MultipleStandards_AverageK_DeductSum()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, RelativeOptions());
        s.Spec.LowerLimit = 0m; s.Spec.UpperLimit = 1000m; db.SaveChanges();
        // K1 = 99.0025 / 9.9 = 10.00025253 ; K2 = 200 x 0.995 x 0.995 / (20.0 - 0.1) = 198.005 / 19.9 = 9.9499... -> mean checked loosely
        var p = Payload(s, standards: new() { new(s.StandardLot!.Id, 100m, 10.00m), new(s.StandardLot.Id, 200m, 20.00m) });
        await Record(db, s, p);
        Assert.Equal(4.7m, (await db.Materials.FindAsync(s.StandardLot.Id))!.QuantityRemaining);
        var a = await db.TestAnalyses.SingleAsync(x => x.TestOrderId == s.Order.Id);
        using var doc = JsonDocument.Parse(a.ConditionsJson!);
        var k = doc.RootElement.GetProperty("standard").GetProperty("k").GetDecimal();
        Assert.True(Math.Abs(k - (99.0025m / 9.9m + 198.005m / 19.9m) / 2m) < 0.0001m);
    }

    [Fact]
    public async Task Relative_Due_IsUsableWithWarning()
    {
        using var db = NewDb();
        var o = RelativeOptions();
        o.ValidityDays = 1; o.StandardizedDaysAgo = 5;
        var s = TitrationScenario.Seed(db, o);
        s.Spec.LowerLimit = 0m; s.Spec.UpperLimit = 1000m; db.SaveChanges();

        var ctx = await new TitrationContextService(db, new UserSectionScopeService(db)).GetContextAsync(s.Order.Id, s.UserId);
        var opt = Assert.Single(ctx.TitrantPreparations);
        Assert.True(opt.Usable);
        Assert.Equal("Due", opt.FactorState);
        Assert.NotNull(opt.Warning);

        await Record(db, s, Payload(s, standards: new() { new(s.StandardLot!.Id, 100m, 10.00m) }) with
        { DueTitrantAcknowledged = true, DueTitrantJustification = Justification });
        var a = await db.TestAnalyses.SingleAsync(x => x.TestOrderId == s.Order.Id);
        Assert.Contains("standardization is due", a.ConditionsJson!);
    }

    private const string Justification = "Restandardization booked for tomorrow; factor not used in relative method.";

    private static async Task<(MicroLimsDbContext Db, TitrationScenario S)> DueRelativeAsync()
    {
        var db = NewDb();
        var o = RelativeOptions();
        o.ValidityDays = 1; o.StandardizedDaysAgo = 5;
        var s = TitrationScenario.Seed(db, o);
        s.Spec.LowerLimit = 0m; s.Spec.UpperLimit = 1000m; db.SaveChanges();
        await Task.CompletedTask;
        return (db, s);
    }

    private static TitrationPayload DuePayload(TitrationScenario s) =>
        Payload(s, standards: new() { new(s.StandardLot!.Id, 100m, 10.00m) });

    [Fact]
    public async Task Relative_Due_WithoutAcknowledgement_Refused()
    {
        var (db, s) = await DueRelativeAsync();
        using var _ = db;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, DuePayload(s)));
        Assert.Contains("acknowledge it and give a justification", ex.Message);
    }

    [Fact]
    public async Task Relative_Due_Acknowledged_RecordsTwoSignaturesAndSnapshot()
    {
        var (db, s) = await DueRelativeAsync();
        using var _ = db;
        await Record(db, s, DuePayload(s) with { DueTitrantAcknowledged = true, DueTitrantJustification = Justification });

        var sigs = await db.ElectronicSignatures.Where(x => x.EntityType == "TestOrder" && x.EntityId == s.Order.Id).ToListAsync();
        Assert.Equal(2, sigs.Count);
        Assert.Contains(sigs, x => x.MeaningOfSignature == SignatureMeaning.ResultRecorded);
        Assert.Contains(sigs, x => x.MeaningOfSignature == SignatureMeaning.TitrantDueAcknowledged);
        Assert.All(sigs, x => Assert.Equal(s.UserId, x.UserId));

        var a = await db.TestAnalyses.SingleAsync(x => x.TestOrderId == s.Order.Id);
        using var doc = JsonDocument.Parse(a.ConditionsJson!);
        Assert.Equal("titration-1", doc.RootElement.GetProperty("engineVersion").GetString());
        var ack = doc.RootElement.GetProperty("dueTitrantAcknowledgement");
        Assert.Equal(Justification, ack.GetProperty("justification").GetString());
        Assert.Equal(s.TitrantPrep.Code, ack.GetProperty("titrantCodes")[0].GetString());
    }

    [Fact]
    public async Task Relative_Due_ShortJustification_Refused()
    {
        var (db, s) = await DueRelativeAsync();
        using var _ = db;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Record(db, s, DuePayload(s) with { DueTitrantAcknowledged = true, DueTitrantJustification = "too short" }));
        Assert.Contains("10 to 500", ex.Message);
    }

    [Fact]
    public async Task ValidTitrant_WithAcknowledgement_Refused()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Record(db, s, Payload(s) with { DueTitrantAcknowledged = true, DueTitrantJustification = Justification }));
        Assert.Contains("No titrant warning to acknowledge", ex.Message);
    }

    [Fact]
    public async Task Relative_Due_WrongPassword_PersistsOnlyTheFailedAttempt()
    {
        var (db, s) = await DueRelativeAsync();
        using var _ = db;
        var p = DuePayload(s) with { Password = "wrong", DueTitrantAcknowledged = true, DueTitrantJustification = Justification };
        await Assert.ThrowsAnyAsync<Exception>(() => Record(db, s, p));
        Assert.Empty(db.TestAnalyses);
        Assert.Empty(db.Results);
        Assert.DoesNotContain(db.ElectronicSignatures, x => x.EntityType == "TestOrder");
        Assert.Equal(5m, (await db.Materials.FindAsync(s.StandardLot!.Id))!.QuantityRemaining);
        var log = Assert.Single(db.AuditLogs.Where(a => a.EntityName == "ElectronicSignature" && a.Action == "SignatureFailed"));
        Assert.Contains("TitrantDueAcknowledged", log.NewValue);
    }

    [Fact]
    public void WarningsOf_CoversMainAndExcessOptions()
    {
        static TitrantPreparationOption Opt(string? w) =>
            new(1, "VS-1", null, 1m, "Due", null, null, null, null, true, w, null);
        Assert.Equal(new[] { "a" }, TitrationRecorder.WarningsOf(Opt("a"), null));
        Assert.Equal(new[] { "b" }, TitrationRecorder.WarningsOf(Opt(null), Opt("b")));
        Assert.Empty(TitrationRecorder.WarningsOf(Opt(null), null));
    }

    [Fact]
    public async Task Relative_StandardRules()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, RelativeOptions());
        var lot = s.StandardLot!;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, Payload(s)));
        Assert.Contains("standard titrations are required", ex.Message);

        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s,
            Payload(s, standards: new() { new(lot.Id, 1m, 1m), new(lot.Id, 1m, 1m), new(lot.Id, 1m, 1m), new(lot.Id, 1m, 1m) })));
        Assert.Contains("One to three", ex.Message);

        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s,
            Payload(s, standards: new() { new(lot.Id, 6000m, 10m) })));
        Assert.Contains("left", ex.Message);

        lot.ExpiryDate = DateTime.UtcNow.AddDays(-1); db.SaveChanges();
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s,
            Payload(s, standards: new() { new(lot.Id, 100m, 10m) })));
        Assert.Contains("Expired", ex.Message);
        Assert.Equal(5m, lot.QuantityRemaining);

        // a standard on a UspFactor test is refused
        using var db2 = NewDb();
        var s2 = TitrationScenario.Seed(db2, new() { WithStandard = true });
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db2, s2,
            Payload(s2, standards: new() { new(s2.StandardLot!.Id, 100m, 10m) })));
        Assert.Contains("only used with the relative", ex.Message);
    }

    [Fact]
    public async Task Relative_ApprovedWorkingStandardLot_IsAccepted()
    {
        using var db = NewDb();
        var o = RelativeOptions();
        o.StandardType = MaterialType.WorkingStandard; o.StandardPurity = 99.0m; o.StandardMoisture = null;
        var s = TitrationScenario.Seed(db, o);
        s.Spec.LowerLimit = 0m; s.Spec.UpperLimit = 1000m; db.SaveChanges();

        var ctx = await new TitrationContextService(db, new UserSectionScopeService(db)).GetContextAsync(s.Order.Id, s.UserId);
        var lot = Assert.Single(ctx.StandardLots);
        Assert.Equal("WorkingStandard", lot.Kind);
        Assert.Equal(99.0m, lot.PurityPercent);

        await Record(db, s, Payload(s, standards: new() { new(s.StandardLot!.Id, 100m, 10.00m) }));
        Assert.Equal(4.9m, (await db.Materials.FindAsync(s.StandardLot.Id))!.QuantityRemaining);
    }

    [Fact]
    public async Task Equipment_MustBeATitrator()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db);
        s.Equipment.Type = EquipmentType.Balance; db.SaveChanges();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, Payload(s)));
        Assert.Contains("not a titrator", ex.Message);
    }

    [Fact]
    public async Task SecondRecord_ForSameOrder_IsRefused()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db);
        await Record(db, s, Payload(s));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, s, Payload(s)));
    }

    [Fact]
    public async Task Context_ListsPreparationsSpecsAndConfiguration()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db);
        var ctx = await new TitrationContextService(db, new UserSectionScopeService(db)).GetContextAsync(s.Order.Id, s.UserId);

        Assert.Equal(TitrationType.Redox, ctx.TitrationType);
        Assert.Equal(TitrationCalculation.UspFactor, ctx.Calculation);
        Assert.Equal(88.06m, ctx.EquivalencyFactor);
        Assert.Equal(2, ctx.ReplicateCount);
        Assert.Equal(0.1m, ctx.Titrant.NominalStrength);
        var opt = Assert.Single(ctx.TitrantPreparations);
        Assert.True(opt.Usable);
        Assert.Equal("Valid", opt.FactorState);
        Assert.Equal(1.0023m, opt.Factor);
        Assert.Equal(25m, opt.StandardizationTemperatureC);
        Assert.Empty(ctx.ExcessPreparations);
        Assert.Empty(ctx.StandardLots);
        var spec = Assert.Single(ctx.Specifications);
        Assert.Equal(ResultBasis.PercentAsIs, spec.ResultBasis);
    }

    [Fact]
    public async Task Context_OnlyListsPreparedUnexpiredPreparationsOfTheMaster()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db);
        var expired = new SolutionPreparation
        {
            SectionId = s.Section.Id, SolutionMasterId = s.TitrantMaster.Id, Type = SolutionType.Titrant, Code = "OLD",
            Status = SolutionPreparationStatus.Prepared, StartedByUserId = 1, StartedAt = DateTime.UtcNow.AddDays(-40),
            PreparedAt = DateTime.UtcNow.AddDays(-40), ExpiresAt = DateTime.UtcNow.AddDays(-1), RecipeSnapshotJson = "{}",
        };
        db.SolutionPreparations.Add(expired); db.SaveChanges();
        var ctx = await new TitrationContextService(db, new UserSectionScopeService(db)).GetContextAsync(s.Order.Id, s.UserId);
        Assert.Equal(s.TitrantPrep.Id, Assert.Single(ctx.TitrantPreparations).PreparationId);
    }

    // ---- test master validation ----

    private static async Task<(MicroLimsDbContext db, TitrationScenario s, TestDefinitionMasterDataService svc)> RulesSetup(TitrationScenario.Options? o = null)
    {
        var db = NewDb();
        var s = TitrationScenario.Seed(db, o);
        var svc = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));
        await Task.CompletedTask;
        return (db, s, svc);
    }

    private static CreateTestDefinitionRequest Create(TitrationScenario s, string code, Func<CreateTestDefinitionRequest, CreateTestDefinitionRequest>? tweak = null)
    {
        var r = new CreateTestDefinitionRequest(code, "Titration test", s.Section.Id, WorkflowType.Titration, EquationType.Titration,
            ReplicateCount: 3, TitrationType: TitrationType.Redox, TitrationMode: TitrationMode.Direct,
            TitrationCalculation: TitrationCalculation.UspFactor, TitrantSolutionMasterId: s.TitrantMaster.Id,
            TitrationEquivalencyFactor: 88.06m, TitrationEndpoint: TitrationEndpoint.Visual, TitrationIndicatorEntryId: s.IndicatorEntry.Id);
        return tweak == null ? r : tweak(r);
    }

    [Fact]
    public async Task Master_ValidTitrationCreates_AndClearsIrrelevantFields()
    {
        var (db, s, svc) = await RulesSetup();
        using var _ = db;
        var created = await svc.CreateTestDefinitionAsync(s.UserId, Create(s, "NEWTIT"));
        Assert.Equal(WorkflowType.Titration, created.WorkflowType);
        Assert.Equal(false, created.TitrationTempCorrection);
        Assert.Null(created.TitrationNonAqueous);   // only acid-base keeps it
        Assert.False(created.TitrationBlankRequired);
        Assert.Equal(s.IndicatorEntry.Id, created.TitrationIndicatorEntryId);
        Assert.Equal("Starch", created.TitrationIndicator);   // copied from the Indicator entry
    }

    [Theory]
    [InlineData("type")]
    [InlineData("fnull")]
    [InlineData("replicates")]
    [InlineData("indicator")]
    [InlineData("indicatorNotIndicator")]
    [InlineData("kfunit")]
    [InlineData("relativeNoStd")]
    [InlineData("relativeResidual")]
    [InlineData("residualNoExcess")]
    [InlineData("tempWrongType")]
    [InlineData("maxRsd")]
    [InlineData("kfResidual")]
    public async Task Master_RejectsInvalidConfigurations(string which)
    {
        var (db, s, svc) = await RulesSetup(new() { WithExcess = true, WithStandard = true });
        using var _ = db;
        CreateTestDefinitionRequest req = which switch
        {
            "type" => Create(s, "X1", r => r with { TitrationType = null }),
            "fnull" => Create(s, "X1", r => r with { TitrationEquivalencyFactor = null }),
            "replicates" => Create(s, "X1", r => r with { ReplicateCount = 11 }),
            "indicator" => Create(s, "X1", r => r with { TitrationIndicatorEntryId = null }),
            "indicatorNotIndicator" => Create(s, "X1", r => r with { TitrationIndicatorEntryId = s.StandardEntry!.Id }),
            "kfunit" => Create(s, "X1", r => r with { TitrationType = TitrationType.KarlFischer, TitrationEquivalencyFactor = null }),
            "relativeNoStd" => Create(s, "X1", r => r with { TitrationCalculation = TitrationCalculation.Relative }),
            "relativeResidual" => Create(s, "X1", r => r with { TitrationCalculation = TitrationCalculation.Relative, TitrationMode = TitrationMode.Residual, TitrationStandardEntryId = s.StandardEntry!.Id, TitrationExcessSolutionMasterId = s.ExcessMaster!.Id, TitrationExcessVolumeMl = 25m }),
            "residualNoExcess" => Create(s, "X1", r => r with { TitrationMode = TitrationMode.Residual }),
            "tempWrongType" => Create(s, "X1", r => r with { TitrationTempCorrection = true }),
            "maxRsd" => Create(s, "X1", r => r with { TitrationMaxRsdPercent = 0m }),
            "kfResidual" => Create(s, "X1", r => r with { TitrationType = TitrationType.KarlFischer, TitrationMode = TitrationMode.Residual }),
            _ => throw new ArgumentException(which)
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateTestDefinitionAsync(s.UserId, req));
    }

    [Fact]
    public async Task Master_ResidualAcceptsExcessWithSameUnit_AndNonTitrationClearsFields()
    {
        var (db, s, svc) = await RulesSetup(new() { WithExcess = true });
        using var _ = db;
        var created = await svc.CreateTestDefinitionAsync(s.UserId, Create(s, "RES1", r => r with
        { TitrationMode = TitrationMode.Residual, TitrationExcessSolutionMasterId = s.ExcessMaster!.Id, TitrationExcessVolumeMl = 25m }));
        Assert.Equal(25m, created.TitrationExcessVolumeMl);

        // changing the workflow away from titration clears every titration column
        var upd = await svc.UpdateTestDefinitionAsync(s.UserId, created.Id, new UpdateTestDefinitionRequest(
            "RES1", "Now a qualitative test", WorkflowType: WorkflowType.Qualitative, EquationType: EquationType.Qualitative) { ChangeReason = "Test retired from titration" });
        Assert.Null(upd.TitrationType);
        Assert.Null(upd.TitrantSolutionMasterId);
        Assert.Null(upd.TitrationExcessVolumeMl);
    }

    [Fact]
    public async Task Spec_TitrationRules()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db);
        var svc = TestServiceFactory.Specification(db);
        var item = await db.Items.FirstAsync();

        Specification NewSpec(Action<Specification> t)
        {
            var sp = new Specification
            {
                ItemId = item.Id, TestCode = s.Test.Code, ParameterName = "P" + Guid.NewGuid().ToString("N")[..4], LimitType = LimitType.Range,
                LowerLimit = 1m, UpperLimit = 2m, Unit = "%", SpecLimit = "1-2", ResultBasis = ResultBasis.PercentAsIs,
            };
            t(sp);
            return sp;
        }

        Task Run(Specification sp) => svc.ValidateAsync(sp);

        await Run(NewSpec(_ => { }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(NewSpec(sp => sp.ResultBasis = null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(NewSpec(sp => sp.ResultBasis = ResultBasis.MgPerKg)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(NewSpec(sp => sp.ResultBasis = ResultBasis.PercentLabelClaim)));
        await Run(NewSpec(sp => { sp.ResultBasis = ResultBasis.PercentLabelClaim; sp.LabelClaim = 500m; sp.LabelClaimUnit = "mg"; }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(NewSpec(sp => sp.LabelClaim = 500m)));

        // Label claim bases exist only for dosage units: a raw material may use % bases only.
        item.Category = SampleCategory.RawMaterial;
        await db.SaveChangesAsync();
        await Run(NewSpec(sp => sp.ResultBasis = ResultBasis.PercentDriedBasis));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Run(NewSpec(sp => { sp.ResultBasis = ResultBasis.MgPerUnit; sp.LabelClaim = 500m; sp.LabelClaimUnit = "mg"; })));
        Assert.Contains("Finished Product", ex.Message);
    }
}
