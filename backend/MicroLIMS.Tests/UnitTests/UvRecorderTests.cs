using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class UvRecorderTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class UvCase
    {
        public TitrationScenario S = null!;
        public TestDefinition Test = null!;
        public TestOrder Order = null!;
        public List<Specification> Specs = new();
        public Equipment Spectrophotometer = null!;
    }

    // One UV test (with its specs and order) on the scenario's sample, plus a UV-Vis instrument.
    private static UvCase ArrangeOn(MicroLimsDbContext db, TitrationScenario s, UvCalculationMode mode, int replicates,
        Action<TestDefinition>? tweakTest, Action<Specification>[] specTweaks)
    {
        var uid = Guid.NewGuid().ToString("N")[..6];
        var c = new UvCase { S = s };
        c.Spectrophotometer = db.Equipment.FirstOrDefault(e => e.Type == EquipmentType.UvVis)
            ?? db.Equipment.Add(new Equipment { Name = "UV-Vis", Code = $"EQ-UV-{uid}", Type = EquipmentType.UvVis, SectionId = s.Section.Id }).Entity;

        c.Test = new TestDefinition
        {
            Code = $"UV_{uid}", DisplayName = $"UV {uid}", SectionId = s.Section.Id, IsActive = true, PhyschemArea = PhyschemArea.Both,
            WorkflowType = WorkflowType.UvSpectrophotometry, EquationType = EquationType.UvSpectrophotometry,
            ReplicateCount = replicates, UvCalculationMode = mode,
            UvSpecificAbsorbance = mode == UvCalculationMode.SpecificAbsorbanceAssay ? 328m : null,
            UvTotalVolumeMl = mode switch
            {
                UvCalculationMode.SpecificAbsorbanceAssay => 5000m,
                UvCalculationMode.SpecificAbsorbanceValue => 250000m,
                UvCalculationMode.StandardComparison => 100m,
                _ => null,
            },
            UvStandardVolumeMl = mode == UvCalculationMode.StandardComparison ? 100m : null,
        };
        tweakTest?.Invoke(c.Test);
        db.TestDefinitions.Add(c.Test);

        var itemId = db.Samples.Where(x => x.Id == s.Order.SampleId).Select(x => x.ItemId!.Value).First();
        db.SampleTests.Add(new SampleTest { ItemId = itemId, TestCode = c.Test.Code, DisplayName = c.Test.DisplayName });

        if (specTweaks.Length == 0) specTweaks = new Action<Specification>[] { _ => { } };
        int order = 1;
        foreach (var tweak in specTweaks)
        {
            var spec = new Specification
            {
                ItemId = itemId, TestCode = c.Test.Code, ParameterName = $"Parameter {order}", DisplayOrder = order,
                LimitType = LimitType.Range, LowerLimit = 73.000000m, UpperLimit = 79.000000m, Unit = "%", ResultBasis = ResultBasis.PercentAsIs,
            };
            tweak(spec);
            db.Specifications.Add(spec);
            c.Specs.Add(spec);
            order++;
        }

        c.Order = new TestOrder { SampleId = s.Order.SampleId, TestCode = c.Test.Code, SectionId = s.Section.Id, CurrentStep = WorkflowStep.Running, Status = ApprovalStatus.InProgress };
        db.TestOrders.Add(c.Order);
        db.SaveChanges();
        return c;
    }

    private static UvCase Arrange(MicroLimsDbContext db, UvCalculationMode mode, int replicates = 1,
        Action<TestDefinition>? tweakTest = null, TitrationScenario.Options? so = null, Action<Specification>? specTweak = null) =>
        ArrangeOn(db, TitrationScenario.Seed(db, so), mode, replicates, tweakTest,
            specTweak == null ? Array.Empty<Action<Specification>>() : new[] { specTweak });

    private static UvParameterInput Param(Specification spec, decimal? absorbance, decimal? weightG = 0.1m, decimal wavelength = 444m, decimal? reference = null) =>
        new(spec.Id, wavelength, reference, new List<UvReplicateInput> { new(weightG, absorbance) });

    private static UvPayload Payload(UvCase c, params UvParameterInput[] parameters) =>
        new(DateTime.UtcNow, c.Spectrophotometer.Id, null, null, null, parameters.ToList(), TitrationScenario.Password, "uv test");

    private static Task<TestWorkflowResult> Record(MicroLimsDbContext db, UvCase c, UvPayload p) =>
        TestServiceFactory.TestWorkflow(db).RecordUvResultAsync(c.Order.Id, p, c.S.UserId, "127.0.0.1");

    private static async Task<InvalidOperationException> Refused(MicroLimsDbContext db, UvCase c, UvPayload p) =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => Record(db, c, p));

    private static Action<Specification> Dried => sp => sp.ResultBasis = ResultBasis.PercentDriedBasis;

    // ---- the STM numbers ----

    [Fact]
    public async Task Riboflavin_DriedBasis_UsesTheSampleLod()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceAssay, specTweak: Dried);
        var (lodOrder, lodResult) = c.S.AddMoistureResult(db, MoistureRole.LossOnDrying, 5m);

        var result = await Record(db, c, Payload(c, Param(c.Specs[0], 0.49m)));

        Assert.Equal(ResultStatus.WithinLimits, result.Status);
        var a = await db.TestAnalyses.Include(x => x.Signature).Include(x => x.ParameterResults).ThenInclude(r => r.Readings)
            .SingleAsync(x => x.TestOrderId == c.Order.Id);
        Assert.Equal(WorkflowType.UvSpectrophotometry, a.AnalysisType);
        Assert.NotNull(a.Signature);
        Assert.Equal(c.Spectrophotometer.Id, a.EquipmentId);
        var pr = Assert.Single(a.ParameterResults);
        Assert.Equal(78.63m, pr.ReportedValue);
        Assert.Equal("78.63 %", pr.ReportedDisplay);
        Assert.Equal(ResultBasis.PercentDriedBasis, pr.ResultBasis);
        var reading = Assert.Single(pr.Readings);
        Assert.Equal(ReadingKind.Absorbance, reading.Kind);
        Assert.Equal(0.1m, reading.Value1);
        Assert.Equal(0.49m, reading.Value2);
        Assert.Equal(444m, reading.Value3);
        Assert.True(Math.Abs(74.695m - reading.ComputedValue!.Value) < 0.001m);

        using var calc = JsonDocument.Parse(pr.CalculationJson!);
        var root = calc.RootElement;
        Assert.Equal("Uv", root.GetProperty("quantity").GetString());
        Assert.Equal("SpecificAbsorbanceAssay", root.GetProperty("mode").GetString());
        Assert.Equal(444m, root.GetProperty("wavelengthNm").GetDecimal());
        Assert.Equal(5000m, root.GetProperty("totalVolumeMl").GetDecimal());
        Assert.Equal(328m, root.GetProperty("specificAbsorbance").GetDecimal());
        Assert.Equal(5m, root.GetProperty("moisturePercent").GetDecimal());
        Assert.Equal(78.63m, root.GetProperty("reportedValue").GetDecimal());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("standard").ValueKind);

        using var cond = JsonDocument.Parse(a.ConditionsJson!);
        var moisture = cond.RootElement.GetProperty("moisture");
        Assert.Equal(lodResult.Id, moisture.GetProperty("parameterResultId").GetInt32());
        Assert.Equal(lodOrder.Id, moisture.GetProperty("testOrderId").GetInt32());
        Assert.Equal(5m, moisture.GetProperty("percent").GetDecimal());
        Assert.Equal("LossOnDrying", moisture.GetProperty("role").GetString());
    }

    [Fact]
    public async Task DriedBasis_WithoutLod_IsRefused()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceAssay, specTweak: Dried);
        var ex = await Refused(db, c, Payload(c, Param(c.Specs[0], 0.49m)));
        Assert.Equal(SampleMoistureResolver.MissingMessage(MoistureRole.LossOnDrying), ex.Message);
        Assert.Empty(db.TestAnalyses.Where(x => x.TestOrderId == c.Order.Id));
    }

    [Fact]
    public async Task Pyridoxine_ThreeRows_EachJudgedAlone()
    {
        using var db = NewDb();
        var c = ArrangeOn(db, TitrationScenario.Seed(db), UvCalculationMode.SpecificAbsorbanceValue, 1, null, new Action<Specification>[] {
            sp => { sp.LowerLimit = 425m; sp.UpperLimit = 445m; sp.ResultBasis = null; sp.Unit = "A (1%, 1 cm)"; },
            sp => { sp.LowerLimit = 175m; sp.UpperLimit = 195m; sp.ResultBasis = null; sp.Unit = "A (1%, 1 cm)"; },
            sp => { sp.LowerLimit = 345m; sp.UpperLimit = 365m; sp.ResultBasis = null; sp.Unit = "A (1%, 1 cm)"; } });

        var allIn = Payload(c,
            Param(c.Specs[0], 0.435m, 2.5m, 253m), Param(c.Specs[1], 0.185m, 2.5m, 280m), Param(c.Specs[2], 0.355m, 2.5m, 332m));
        // the third row reads outside its range: 0.380 x 250000 / 250 = 380
        var oneOut = allIn with { Parameters = new() { allIn.Parameters[0], allIn.Parameters[1], Param(c.Specs[2], 0.380m, 2.5m, 332m) } };

        var result = await Record(db, c, oneOut);

        Assert.Equal(ResultStatus.OutOfSpecification, result.Status);
        var rows = await db.ParameterResults.Where(r => r.TestOrderId == c.Order.Id).OrderBy(r => r.SpecificationId).ToListAsync();
        Assert.Equal(new[] { 435m, 185m, 380m }, rows.Select(r => r.ReportedValue!.Value).ToArray());
        Assert.Equal(new[] { ResultStatus.WithinLimits, ResultStatus.WithinLimits, ResultStatus.OutOfSpecification }, rows.Select(r => r.ComparisonStatus).ToArray());
        Assert.Equal("435.00 A (1%, 1 cm)", rows[0].ReportedDisplay);
        Assert.Null(rows[0].ResultBasis);
        Assert.Equal(new[] { 253m, 280m, 332m }, rows.Select(r => r.Readings.Single().Value3!.Value).ToArray());
        Assert.Null((await db.TestAnalyses.SingleAsync(x => x.TestOrderId == c.Order.Id)).ConditionsJson);
    }

    [Fact]
    public async Task AbsorbanceLimit_EqualConforms_AboveFails()
    {
        void Limit(Specification sp)
        {
            sp.LimitType = LimitType.Qualitative; sp.ExpectedResultText = "Not more than the reference";
            sp.LowerLimit = null; sp.UpperLimit = null; sp.ResultBasis = null; sp.Unit = "";
        }

        foreach (var (absorbance, expected) in new[] { (0.120m, ResultStatus.WithinLimits), (0.150m, ResultStatus.WithinLimits), (0.151m, ResultStatus.OutOfSpecification) })
        {
            using var db = NewDb();
            var c = Arrange(db, UvCalculationMode.AbsorbanceLimit, specTweak: Limit);
            var result = await Record(db, c, Payload(c, Param(c.Specs[0], absorbance, null, 700m, 0.150m)));
            Assert.Equal(expected, result.Status);

            var pr = await db.ParameterResults.Include(r => r.Readings).SingleAsync(r => r.TestOrderId == c.Order.Id);
            Assert.Equal(absorbance, pr.ReportedValue);
            Assert.Equal($"{absorbance:0.000} AU", pr.ReportedDisplay);
            Assert.Null(pr.Readings.Single().Value1);
            Assert.Equal(700m, pr.Readings.Single().Value3);
            using var calc = JsonDocument.Parse(pr.CalculationJson!);
            Assert.Equal(0.150m, calc.RootElement.GetProperty("referenceAbsorbance").GetDecimal());
            Assert.Equal("AbsorbanceLimit", calc.RootElement.GetProperty("mode").GetString());
        }
    }

    // ---- standard comparison ----

    private static TitrationScenario.Options WithStandard(Action<TitrationScenario.Options>? tweak = null)
    {
        var o = new TitrationScenario.Options { WithStandard = true, StandardPurity = 99.8m, StandardMoisture = 0.1m };
        tweak?.Invoke(o);
        return o;
    }

    private static UvPayload StandardPayload(UvCase c, decimal absorbance = 0.50m) =>
        Payload(c, Param(c.Specs[0], absorbance, 0.1m)) with
        {
            StandardMaterialId = c.S.StandardLot!.Id, StandardWeightG = 0.1m, StandardAbsorbance = 0.50m,
        };

    private static void Wide(Specification sp) { sp.LowerLimit = 90m; sp.UpperLimit = 110m; }

    [Fact]
    public async Task StandardComparison_MatchesTheStandardComparisonCalculator()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.StandardComparison, 1, null, WithStandard(), Wide);

        var result = await Record(db, c, StandardPayload(c));

        var expected = StandardComparisonCalculator.CalculatePreparationAssay(0.50m, 0.50m, 0.1m, 100m, 100m, 0.1m, 0.1m, 99.8m);
        Assert.Equal(ResultStatus.WithinLimits, result.Status);
        var pr = await db.ParameterResults.SingleAsync(r => r.TestOrderId == c.Order.Id);
        Assert.Equal(Math.Round(expected, 2), pr.ReportedValue);
        using var calc = JsonDocument.Parse(pr.CalculationJson!);
        var std = calc.RootElement.GetProperty("standard");
        Assert.Equal(c.S.StandardLot!.Id, std.GetProperty("lotId").GetInt32());
        Assert.Equal(99.8m, std.GetProperty("purityPercent").GetDecimal());
        Assert.Equal(0.1m, std.GetProperty("moisturePercent").GetDecimal());
        Assert.Equal(0.1m, std.GetProperty("weightG").GetDecimal());
        Assert.Equal(0.50m, std.GetProperty("absorbance").GetDecimal());
        Assert.Equal(100m, std.GetProperty("volumeMl").GetDecimal());
    }

    [Fact]
    public async Task StandardComparison_RefusesExpiredLot_NoPurity_OutOfStock_AndOtherSectionLot()
    {
        // expired
        using (var db = NewDb())
        {
            var c = Arrange(db, UvCalculationMode.StandardComparison, 1, null, WithStandard(o => o.StandardExpiry = DateTime.UtcNow.AddDays(-2)), Wide);
            var ex = await Refused(db, c, StandardPayload(c));
            Assert.Contains("expired", ex.Message);
        }
        // no purity
        using (var db = NewDb())
        {
            var c = Arrange(db, UvCalculationMode.StandardComparison, 1, null, WithStandard(), Wide);
            c.S.StandardLot!.Purity = null; db.SaveChanges();
            var ex = await Refused(db, c, StandardPayload(c));
            Assert.Contains("no purity recorded", ex.Message);
        }
        // out of stock
        using (var db = NewDb())
        {
            var c = Arrange(db, UvCalculationMode.StandardComparison, 1, null, WithStandard(), Wide);
            c.S.StandardLot!.QuantityRemaining = 0m; db.SaveChanges();
            var ex = await Refused(db, c, StandardPayload(c));
            Assert.Contains("no stock left", ex.Message);
        }
        // a lot of another laboratory section
        using (var db = NewDb())
        {
            var c = Arrange(db, UvCalculationMode.StandardComparison, 1, null, WithStandard(), Wide);
            var other = db.DocumentSections.Add(new DocumentSection { Name = "Other lab", Code = "OTH", DepartmentId = c.S.Section.DepartmentId, IsActive = true }).Entity;
            db.SaveChanges();
            c.S.StandardLot!.SectionId = other.Id; db.SaveChanges();
            var ex = await Refused(db, c, StandardPayload(c));
            Assert.Contains("another laboratory", ex.Message);
        }
        // a material that is not a standard
        using (var db = NewDb())
        {
            var c = Arrange(db, UvCalculationMode.StandardComparison, 1, null, WithStandard(o => o.StandardType = MaterialType.Chemical), Wide);
            var ex = await Refused(db, c, StandardPayload(c));
            Assert.Contains("reference standard", ex.Message);
        }
        // nothing is stored by any refusal
    }

    [Fact]
    public async Task StandardComparison_NeedsTheStandardFields()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.StandardComparison, 1, null, WithStandard(), Wide);
        var ex = await Refused(db, c, StandardPayload(c) with { StandardAbsorbance = null });
        Assert.Contains("Standard absorbance", ex.Message);
        var ex2 = await Refused(db, c, StandardPayload(c) with { StandardMaterialId = null });
        Assert.Contains("standard lot", ex2.Message);
        var ex3 = await Refused(db, c, StandardPayload(c) with { StandardWeightG = 0m });
        Assert.Contains("Standard weight", ex3.Message);
    }

    // ---- input rules ----

    [Theory]
    [InlineData(0, false)]
    [InlineData(-0.1, false)]
    [InlineData(5.01, false)]
    [InlineData(5, true)]
    public async Task Absorbance_OutOfRange_IsRefused(double value, bool accepted)
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceAssay);
        var p = Payload(c, Param(c.Specs[0], (decimal)value));
        if (accepted) Assert.NotNull(await Record(db, c, p));
        else Assert.Equal("Absorbance must be above 0 and at most 5.", (await Refused(db, c, p)).Message);
    }

    [Fact]
    public async Task Absorbance_ZeroIsAcceptedInAnAbsorbanceLimitTest_ButNotNegativeOrAboveFive()
    {
        static void Limit(Specification sp) { sp.LimitType = LimitType.Qualitative; sp.ExpectedResultText = "NMT reference"; sp.ResultBasis = null; }
        using (var db = NewDb())
        {
            var c = Arrange(db, UvCalculationMode.AbsorbanceLimit, specTweak: Limit);
            var result = await Record(db, c, Payload(c, Param(c.Specs[0], 0m, null, 700m, 0.150m)));
            Assert.Equal(ResultStatus.WithinLimits, result.Status);
        }
        foreach (var bad in new[] { -0.001m, 5.01m })
        {
            using var db = NewDb();
            var c = Arrange(db, UvCalculationMode.AbsorbanceLimit, specTweak: Limit);
            var ex = await Refused(db, c, Payload(c, Param(c.Specs[0], bad, null, 700m, 0.150m)));
            Assert.Contains("Absorbance must be at least 0 and at most 5.", ex.Message);
        }
    }

    [Theory]
    [InlineData(UvCalculationMode.AbsorbanceLimit)]
    [InlineData(UvCalculationMode.SpecificAbsorbanceAssay)]
    [InlineData(UvCalculationMode.SpecificAbsorbanceValue)]
    public async Task BlankAbsorbance_IsRefused_InEveryMode(UvCalculationMode mode)
    {
        using var db = NewDb();
        var limit = mode == UvCalculationMode.AbsorbanceLimit;
        var c = Arrange(db, mode, specTweak: limit
            ? sp => { sp.LimitType = LimitType.Qualitative; sp.ExpectedResultText = "NMT reference"; sp.ResultBasis = null; }
            : null);
        var ex = await Refused(db, c, Payload(c, Param(c.Specs[0], null, limit ? null : 0.1m, 700m, limit ? 0.150m : null)));
        Assert.Equal("Enter the absorbance for every replicate.", ex.Message);
    }

    [Theory]
    [InlineData(189)]
    [InlineData(1101)]
    public async Task Wavelength_OutOfRange_IsRefused(int nm)
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceAssay);
        var ex = await Refused(db, c, Payload(c, Param(c.Specs[0], 0.49m, 0.1m, nm)));
        Assert.Equal("Wavelength must be between 190 and 1100 nm.", ex.Message);
    }

    [Fact]
    public async Task MissingEquipment_IsRefused()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceAssay);
        var ex = await Refused(db, c, Payload(c, Param(c.Specs[0], 0.49m)) with { EquipmentId = null });
        Assert.Contains("spectrophotometer", ex.Message);
    }

    [Fact]
    public async Task EquipmentThatIsNotASpectrophotometer_IsRefused()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceAssay);
        var ex = await Refused(db, c, Payload(c, Param(c.Specs[0], 0.49m)) with { EquipmentId = c.S.Equipment.Id });
        Assert.Contains("not a UV spectrophotometer", ex.Message);
    }

    [Fact]
    public async Task WrongReplicateCount_IsRefused()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceAssay, replicates: 2);
        var ex = await Refused(db, c, Payload(c, Param(c.Specs[0], 0.49m)));
        Assert.Contains("exactly 2 replicate", ex.Message);
    }

    [Fact]
    public async Task TwoReplicates_AreAveraged()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceAssay, replicates: 2);
        var param = new UvParameterInput(c.Specs[0].Id, 444m, null, new() { new(0.1m, 0.49m), new(0.1m, 0.50m) });
        await Record(db, c, Payload(c, param));
        var pr = await db.ParameterResults.Include(r => r.Readings).SingleAsync(r => r.TestOrderId == c.Order.Id);
        // (74.695 + 76.2195) / 2 = 75.457 -> 75.46
        Assert.Equal(75.46m, pr.ReportedValue);
        Assert.Equal(2, pr.Readings.Count);
    }

    [Fact]
    public async Task WrongPassword_LeavesNothingBehind()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceAssay);
        await Assert.ThrowsAnyAsync<Exception>(() => Record(db, c, Payload(c, Param(c.Specs[0], 0.49m)) with { Password = "wrong" }));
        Assert.Empty(db.TestAnalyses.Where(x => x.TestOrderId == c.Order.Id));
        Assert.Empty(db.ParameterResults.Where(x => x.TestOrderId == c.Order.Id));
    }

    [Fact]
    public async Task FieldsForAnotherMode_AreRefused()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceAssay, 1, null, WithStandard());
        // a standard on a test that does not compare against one
        var withStandard = Payload(c, Param(c.Specs[0], 0.49m)) with { StandardMaterialId = c.S.StandardLot!.Id, StandardWeightG = 0.1m, StandardAbsorbance = 0.5m };
        Assert.Contains("only used when the test compares against a standard", (await Refused(db, c, withStandard)).Message);
        // a reference on an assay
        var withReference = Payload(c, Param(c.Specs[0], 0.49m, 0.1m, 444m, 0.150m));
        Assert.Contains("only used in an absorbance limit test", (await Refused(db, c, withReference)).Message);
        // no weight on an assay
        var noWeight = Payload(c, Param(c.Specs[0], 0.49m, null));
        Assert.Contains("sample weight must be above 0", (await Refused(db, c, noWeight)).Message);

        // and for an absorbance limit test: a weight, or no reference
        using var db2 = NewDb();
        var limit = Arrange(db2, UvCalculationMode.AbsorbanceLimit, specTweak: sp => { sp.LimitType = LimitType.Qualitative; sp.ExpectedResultText = "NMT reference"; sp.ResultBasis = null; });
        Assert.Contains("weight is not used", (await Refused(db2, limit, Payload(limit, Param(limit.Specs[0], 0.12m, 0.1m, 700m, 0.15m)))).Message);
        Assert.Contains("reference absorbance", (await Refused(db2, limit, Payload(limit, Param(limit.Specs[0], 0.12m, null, 700m, null)))).Message);
    }

    [Fact]
    public async Task SpecificationsNotMadeForUv_AreRefused()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceAssay, specTweak: sp => sp.ResultBasis = ResultBasis.PercentLabelClaim);
        Assert.Contains("no valid UV result basis", (await Refused(db, c, Payload(c, Param(c.Specs[0], 0.49m)))).Message);

        using var db2 = NewDb();
        var c2 = Arrange(db2, UvCalculationMode.SpecificAbsorbanceAssay, specTweak: sp => sp.RelatedSubstanceRow = RelatedSubstanceRow.Total);
        Assert.Contains("cannot be used by a UV test", (await Refused(db2, c2, Payload(c2, Param(c2.Specs[0], 0.49m)))).Message);

        using var db3 = NewDb();
        var c3 = Arrange(db3, UvCalculationMode.SpecificAbsorbanceAssay, specTweak: sp => { sp.LimitType = LimitType.TargetWithTolerance; sp.Target = 76m; sp.Tolerance = 3m; sp.ToleranceMode = ToleranceMode.Absolute; });
        Assert.Contains("Range, NotMoreThan or NotLessThan", (await Refused(db3, c3, Payload(c3, Param(c3.Specs[0], 0.49m)))).Message);
    }

    [Fact]
    public async Task OldRowWithoutItsConstants_IsRefusedAtRecordTime()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceAssay, tweakTest: t => t.UvSpecificAbsorbance = 0m);
        Assert.Equal("This UV mode needs a specific absorbance above 0.", (await Refused(db, c, Payload(c, Param(c.Specs[0], 0.49m)))).Message);

        using var db2 = NewDb();
        var c2 = Arrange(db2, UvCalculationMode.SpecificAbsorbanceValue, tweakTest: t => t.UvTotalVolumeMl = null);
        Assert.Equal("This UV mode needs a total volume above 0.", (await Refused(db2, c2, Payload(c2, Param(c2.Specs[0], 0.435m, 2.5m)))).Message);

        using var db3 = NewDb();
        var c3 = Arrange(db3, UvCalculationMode.StandardComparison, 1, t => t.UvStandardVolumeMl = null, WithStandard(), Wide);
        Assert.Equal("A UV test against a standard needs a standard volume above 0.", (await Refused(db3, c3, StandardPayload(c3))).Message);
    }

    // ---- signed results keep what they used ----

    [Fact]
    public async Task SignedResult_KeepsItsConstants_WhenTheTestIsEdited()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceAssay);
        await Record(db, c, Payload(c, Param(c.Specs[0], 0.49m)));
        var before = await db.ParameterResults.SingleAsync(r => r.TestOrderId == c.Order.Id);
        var storedJson = before.CalculationJson;
        Assert.Equal(74.70m, before.ReportedValue);

        c.Test.UvSpecificAbsorbance = 400m;
        c.Test.UvTotalVolumeMl = 1000m;
        db.SaveChanges();

        var after = await db.ParameterResults.AsNoTracking().SingleAsync(r => r.TestOrderId == c.Order.Id);
        Assert.Equal(74.70m, after.ReportedValue);
        Assert.Equal(storedJson, after.CalculationJson);
        using var calc = JsonDocument.Parse(after.CalculationJson!);
        Assert.Equal(328m, calc.RootElement.GetProperty("specificAbsorbance").GetDecimal());
        Assert.Equal(5000m, calc.RootElement.GetProperty("totalVolumeMl").GetDecimal());
    }

    // ---- fix round 1 ----

    private static void LimitSpec(Specification sp)
    {
        sp.LimitType = LimitType.Qualitative; sp.ExpectedResultText = "Not more than the reference";
        sp.LowerLimit = null; sp.UpperLimit = null; sp.ResultBasis = null; sp.Unit = "";
    }

    [Fact]
    public async Task AbsorbanceLimit_AFailingReplicateIsNotMaskedByTheMean()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.AbsorbanceLimit, replicates: 2, specTweak: LimitSpec);
        var param = new UvParameterInput(c.Specs[0].Id, 700m, 0.150m, new() { new(null, 0.140m), new(null, 0.160m) });

        var result = await Record(db, c, Payload(c, param));

        Assert.Equal(ResultStatus.OutOfSpecification, result.Status);
        var pr = await db.ParameterResults.SingleAsync(r => r.TestOrderId == c.Order.Id);
        Assert.Equal(0.150m, pr.ReportedValue); // the reported value stays the mean
        Assert.Equal("0.150 AU", pr.ReportedDisplay);
    }

    [Fact]
    public async Task AbsorbanceLimit_IsJudgedUnrounded()
    {
        using (var db = NewDb())
        {
            var c = Arrange(db, UvCalculationMode.AbsorbanceLimit, specTweak: LimitSpec);
            var result = await Record(db, c, Payload(c, Param(c.Specs[0], 0.1504m, null, 700m, 0.1501m)));
            Assert.Equal(ResultStatus.OutOfSpecification, result.Status);
        }
        using (var db = NewDb())
        {
            var c = Arrange(db, UvCalculationMode.AbsorbanceLimit, specTweak: LimitSpec);
            var result = await Record(db, c, Payload(c, Param(c.Specs[0], 0.150m, null, 700m, 0.150m)));
            Assert.Equal(ResultStatus.WithinLimits, result.Status);
        }
    }

    [Fact]
    public async Task StandardComparison_TakesOneParameter()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.StandardComparison, 1, null, WithStandard(), Wide);
        var second = new Specification
        {
            ItemId = c.Specs[0].ItemId, TestCode = c.Test.Code, ParameterName = "Second", DisplayOrder = 2,
            LimitType = LimitType.Range, LowerLimit = 90m, UpperLimit = 110m, Unit = "%", ResultBasis = ResultBasis.PercentAsIs,
        };
        db.Specifications.Add(second); db.SaveChanges();
        var p = StandardPayload(c);
        p = p with { Parameters = new() { p.Parameters[0], Param(second, 0.50m, 0.1m) } };

        var ex = await Refused(db, c, p);
        Assert.Equal("A UV test against a standard takes one parameter.", ex.Message);
    }

    [Fact]
    public async Task SpecificAbsorbanceValue_WithAnEmptyUnit_HasNoPercentSign()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceValue, specTweak: sp =>
        { sp.LowerLimit = 425m; sp.UpperLimit = 445m; sp.ResultBasis = null; sp.Unit = ""; });
        await Record(db, c, Payload(c, Param(c.Specs[0], 0.435m, 2.5m, 253m)));
        var pr = await db.ParameterResults.SingleAsync(r => r.TestOrderId == c.Order.Id);
        Assert.Equal("435.00", pr.ReportedDisplay);
    }

    [Fact]
    public async Task SpecificAbsorbanceAssay_WithAnEmptyUnit_StillReadsAsPercent()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceAssay, specTweak: sp => sp.Unit = "");
        await Record(db, c, Payload(c, Param(c.Specs[0], 0.49m)));
        Assert.Equal("74.70 %", (await db.ParameterResults.SingleAsync(r => r.TestOrderId == c.Order.Id)).ReportedDisplay);
    }

    [Fact]
    public async Task EquipmentIdZero_IsRefusedLikeMissingEquipment()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceAssay);
        var ex = await Refused(db, c, Payload(c, Param(c.Specs[0], 0.49m)) with { EquipmentId = 0 });
        Assert.Contains("spectrophotometer", ex.Message);
    }

    [Fact]
    public async Task StandardLot_WithNoMoistureValue_IsRefused()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.StandardComparison, 1, null, WithStandard(), Wide);
        c.S.StandardLot!.MoisturePercent = null; db.SaveChanges();
        var ex = await Refused(db, c, StandardPayload(c));
        Assert.Contains("standard values are incomplete", ex.Message);
        Assert.Empty(db.TestAnalyses.Where(x => x.TestOrderId == c.Order.Id));
    }

    [Fact]
    public async Task AnhydrousBasis_UsesTheSampleWaterResult()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.SpecificAbsorbanceAssay, specTweak: sp => sp.ResultBasis = ResultBasis.PercentAnhydrousBasis);
        var refused = await Refused(db, c, Payload(c, Param(c.Specs[0], 0.49m)));
        Assert.Equal(SampleMoistureResolver.MissingMessage(MoistureRole.Water), refused.Message);

        c.S.AddMoistureResult(db, MoistureRole.Water, 5m);
        var result = await Record(db, c, Payload(c, Param(c.Specs[0], 0.49m)));

        Assert.Equal(ResultStatus.WithinLimits, result.Status);
        var a = await db.TestAnalyses.Include(x => x.ParameterResults).SingleAsync(x => x.TestOrderId == c.Order.Id);
        Assert.Equal(78.63m, Assert.Single(a.ParameterResults).ReportedValue);
        using var cond = JsonDocument.Parse(a.ConditionsJson!);
        Assert.Equal("Water", cond.RootElement.GetProperty("moisture").GetProperty("role").GetString());
    }

    [Fact]
    public async Task DriedBasis_WithStandardComparison_CorrectsTheMean()
    {
        using var db = NewDb();
        var c = Arrange(db, UvCalculationMode.StandardComparison, 1, null, WithStandard(), sp => { Wide(sp); sp.ResultBasis = ResultBasis.PercentDriedBasis; });
        c.S.AddMoistureResult(db, MoistureRole.LossOnDrying, 5m);

        await Record(db, c, StandardPayload(c));

        // as-is 99.7002 (see the calculator test) x 100 / 95 = 104.9476 -> 104.95, inside 90-110
        var expected = Math.Round(Math.Round(
            StandardComparisonCalculator.CalculatePreparationAssay(0.50m, 0.50m, 0.1m, 100m, 100m, 0.1m, 0.1m, 99.8m) * 100m / 95m, 2), 2);
        var pr = await db.ParameterResults.SingleAsync(r => r.TestOrderId == c.Order.Id);
        Assert.Equal(expected, pr.ReportedValue);
        Assert.Equal(104.95m, pr.ReportedValue);
        Assert.Equal(ResultBasis.PercentDriedBasis, pr.ResultBasis);
    }

    [Fact]
    public async Task DriedAndAnhydrousSpecs_OnOneTest_AreRefusedAtRecordTime()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db);
        var c = ArrangeOn(db, s, UvCalculationMode.SpecificAbsorbanceValue, 1, null, new Action<Specification>[]
        {
            sp => { sp.ResultBasis = ResultBasis.PercentDriedBasis; },
            sp => { sp.ResultBasis = ResultBasis.PercentAnhydrousBasis; },
        });
        s.AddMoistureResult(db, MoistureRole.LossOnDrying, 5m);
        s.AddMoistureResult(db, MoistureRole.Water, 5m);

        var ex = await Refused(db, c, Payload(c, Param(c.Specs[0], 0.435m, 2.5m), Param(c.Specs[1], 0.435m, 2.5m)));
        Assert.Equal("One UV test cannot report on both dried and anhydrous basis.", ex.Message);
    }

    // ---- the return cascade ----

    [Fact]
    public async Task ReturningTheLod_ReturnsTheDriedUvAssay_AndAnAsIsUvResultStays()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db);
        var dried = ArrangeOn(db, s, UvCalculationMode.SpecificAbsorbanceAssay, 1, null, new[] { Dried });
        var asIs = ArrangeOn(db, s, UvCalculationMode.SpecificAbsorbanceAssay, 1, null, new Action<Specification>[] { sp => sp.ResultBasis = ResultBasis.PercentAsIs });
        var (lodOrder, _) = s.AddMoistureResult(db, MoistureRole.LossOnDrying, 5m);

        await Record(db, dried, Payload(dried, Param(dried.Specs[0], 0.49m)));
        await Record(db, asIs, Payload(asIs, Param(asIs.Specs[0], 0.49m)));
        Assert.Equal(ApprovalStatus.ResultEntered, (await db.TestOrders.FindAsync(dried.Order.Id))!.Status);

        await TestServiceFactory.SampleReview(db).ReturnToAnalystAsync(lodOrder.Id, s.UserId, null);

        var driedOrder = await db.TestOrders.AsNoTracking().SingleAsync(o => o.Id == dried.Order.Id);
        Assert.Equal(WorkflowStep.Running, driedOrder.CurrentStep);
        Assert.False((await db.TestAnalyses.SingleAsync(x => x.TestOrderId == dried.Order.Id)).IsActive);
        Assert.All(await db.ParameterResults.Where(r => r.TestOrderId == dried.Order.Id).ToListAsync(), r => Assert.False(r.IsActive));
        var events = await db.TestReturnEvents.Where(e => e.TestOrderId == dried.Order.Id).ToListAsync();
        Assert.Equal("Recalculate: the loss on drying result was returned.", Assert.Single(events).Reason);

        Assert.Equal(ApprovalStatus.ResultEntered, (await db.TestOrders.AsNoTracking().SingleAsync(o => o.Id == asIs.Order.Id)).Status);
        Assert.True((await db.TestAnalyses.SingleAsync(x => x.TestOrderId == asIs.Order.Id)).IsActive);
        Assert.Empty(await db.TestReturnEvents.Where(e => e.TestOrderId == asIs.Order.Id).ToListAsync());
    }
}

// The rules a UV specification row has to meet.
public class UvSpecificationRuleTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(MicroLimsDbContext db, Item item)> SetupAsync(UvCalculationMode mode, bool withLod = false)
    {
        var db = NewDb();
        var item = new Item { Code = "RM-UV", Name = "Riboflavin 5-phosphate", Category = SampleCategory.RawMaterial, IsActive = true };
        db.Items.Add(item);
        db.TestDefinitions.Add(new TestDefinition
        {
            Code = "UV-1", DisplayName = "UV", WorkflowType = WorkflowType.UvSpectrophotometry, EquationType = EquationType.UvSpectrophotometry,
            UvCalculationMode = mode, IsActive = true,
        });
        db.TestDefinitions.Add(new TestDefinition { Code = "LOD", DisplayName = "LOD", WorkflowType = WorkflowType.Gravimetric, MoistureRole = MoistureRole.LossOnDrying, IsActive = true });
        await db.SaveChangesAsync();
        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = "UV-1" });
        if (withLod) db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = "LOD" });
        await db.SaveChangesAsync();
        return (db, item);
    }

    private static Specification Spec(Item item, LimitType limit = LimitType.Range, ResultBasis? basis = ResultBasis.PercentAsIs) => new()
    {
        ItemId = item.Id, TestCode = "UV-1", ParameterName = "Assay", LimitType = limit, Unit = "%", ResultBasis = basis,
        LowerLimit = limit == LimitType.Range ? 73m : null, UpperLimit = limit == LimitType.Range ? 79m : null,
        ExpectedResultText = limit == LimitType.Qualitative ? "NMT reference" : null,
    };

    [Fact]
    public async Task AssayRow_SavesAsIs_AndDriedOnlyWithAnAssignedLod()
    {
        var (db, item) = await SetupAsync(UvCalculationMode.SpecificAbsorbanceAssay);
        using var _ = db;
        await new SpecificationService(db).ValidateAsync(Spec(item));
        await new SpecificationService(db).ValidateAsync(Spec(item, basis: null));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new SpecificationService(db).ValidateAsync(Spec(item, basis: ResultBasis.PercentDriedBasis)));
        Assert.Contains("Assign a loss on drying test", ex.Message);

        var (db2, item2) = await SetupAsync(UvCalculationMode.SpecificAbsorbanceAssay, withLod: true);
        using var _2 = db2;
        await new SpecificationService(db2).ValidateAsync(Spec(item2, basis: ResultBasis.PercentDriedBasis));
    }

    [Fact]
    public async Task AbsorbanceLimitRow_IsQualitativeWithoutBasis()
    {
        var (db, item) = await SetupAsync(UvCalculationMode.AbsorbanceLimit);
        using var _ = db;
        await new SpecificationService(db).ValidateAsync(Spec(item, LimitType.Qualitative, null));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new SpecificationService(db).ValidateAsync(Spec(item)));
        Assert.Contains("Qualitative", ex.Message);
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new SpecificationService(db).ValidateAsync(Spec(item, LimitType.Qualitative, ResultBasis.PercentAsIs)));
        Assert.Contains("Result basis is not used", ex2.Message);
    }

    [Fact]
    public async Task OtherModes_RefuseQualitative_LabelClaimAndAnalytes()
    {
        var (db, item) = await SetupAsync(UvCalculationMode.SpecificAbsorbanceValue);
        using var _ = db;
        var svc = new SpecificationService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ValidateAsync(Spec(item, LimitType.Qualitative, null)));
        var claim = Spec(item); claim.LabelClaim = 100m; claim.LabelClaimUnit = "mg";
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ValidateAsync(claim));
        var analyte = Spec(item); analyte.HplcMethodAnalyteId = 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ValidateAsync(analyte));
        var labelClaimBasis = Spec(item, basis: ResultBasis.PercentLabelClaim);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ValidateAsync(labelClaimBasis));
    }
}
