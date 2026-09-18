using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SpecLimitParserTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Theory]
    [InlineData("100", null, 100, null)]
    [InlineData("100.5", null, 100.5, null)]
    [InlineData("  100  ", null, 100, null)]
    [InlineData("100 CFU/g", null, 100, "CFU/g")]
    [InlineData("100 %", null, 100, "%")]
    [InlineData("100%", null, 100, "%")]
    [InlineData("0", null, 0, null)]
    [InlineData("0.0", null, 0, null)]
    public void Parse_PlainNumber_ReturnsUpperBound(string input, object? expectedMin, object? expectedMax, string? expectedUnit)
    {
        var result = SpecLimitParser.Parse(input);
        Assert.True(result.HasLimit);
        Assert.False(result.IsRange);
        Assert.Equal(expectedMin is null ? null : Convert.ToDecimal(expectedMin, System.Globalization.CultureInfo.InvariantCulture), result.Min);
        Assert.Equal(expectedMax is null ? null : Convert.ToDecimal(expectedMax, System.Globalization.CultureInfo.InvariantCulture), result.Max);
        Assert.Equal(expectedUnit, result.Unit);
    }

    [Theory]
    [InlineData("NMT 100", null, 100, null)]
    [InlineData("nmt 100", null, 100, null)]
    [InlineData("Nmt 100.5", null, 100.5, null)]
    [InlineData("NMT 100 CFU/g", null, 100, "CFU/g")]
    [InlineData("NMT 100 %", null, 100, "%")]
    [InlineData("NMT 100%", null, 100, "%")]
    [InlineData("NMT: 100", null, 100, null)]
    [InlineData("NMT100", null, 100, null)]
    public void Parse_NotMoreThan_ReturnsMax(string input, object? expectedMin, object? expectedMax, string? expectedUnit)
    {
        var result = SpecLimitParser.Parse(input);
        Assert.True(result.HasLimit);
        Assert.False(result.IsRange);
        Assert.Equal(expectedMin is null ? null : Convert.ToDecimal(expectedMin, System.Globalization.CultureInfo.InvariantCulture), result.Min);
        Assert.Equal(expectedMax is null ? null : Convert.ToDecimal(expectedMax, System.Globalization.CultureInfo.InvariantCulture), result.Max);
        Assert.Equal(expectedUnit, result.Unit);
    }

    [Theory]
    [InlineData("NLT 90", 90, null, null)]
    [InlineData("nlt 90", 90, null, null)]
    [InlineData("Nlt 90.0", 90.0, null, null)]
    [InlineData("NLT 90.0 %", 90.0, null, "%")]
    [InlineData("NLT 90%", 90, null, "%")]
    [InlineData("NLT: 90", 90, null, null)]
    [InlineData("NLT90", 90, null, null)]
    public void Parse_NotLessThan_ReturnsMin(string input, object? expectedMin, object? expectedMax, string? expectedUnit)
    {
        var result = SpecLimitParser.Parse(input);
        Assert.True(result.HasLimit);
        Assert.False(result.IsRange);
        Assert.Equal(expectedMin is null ? null : Convert.ToDecimal(expectedMin, System.Globalization.CultureInfo.InvariantCulture), result.Min);
        Assert.Equal(expectedMax is null ? null : Convert.ToDecimal(expectedMax, System.Globalization.CultureInfo.InvariantCulture), result.Max);
        Assert.Equal(expectedUnit, result.Unit);
    }

    [Theory]
    [InlineData("90-110", 90, 110, null)]
    [InlineData("90 - 110", 90, 110, null)]
    [InlineData("90 – 110", 90, 110, null)]          // en dash (\u2013)
    [InlineData("90 — 110", 90, 110, null)]          // em dash (\u2014)
    [InlineData("90.0 - 110.0", 90.0, 110.0, null)]
    [InlineData("90.0 – 110.0", 90.0, 110.0, null)]  // en dash
    [InlineData("90.0 - 110.0 %", 90.0, 110.0, "%")]
    [InlineData("90.0-110.0%", 90.0, 110.0, "%")]
    [InlineData("90.0 – 110.0 %", 90.0, 110.0, "%")] // en dash
    [InlineData("90.0 - 110.0 mg/mL", 90.0, 110.0, "mg/mL")]
    [InlineData("0.5 - 2.5", 0.5, 2.5, null)]
    public void Parse_Ranges_ReturnsMinAndMax(string input, object expectedMin, object expectedMax, string? expectedUnit)
    {
        var result = SpecLimitParser.Parse(input);
        Assert.True(result.HasLimit);
        Assert.True(result.IsRange);
        Assert.Equal(expectedMin is null ? null : Convert.ToDecimal(expectedMin, System.Globalization.CultureInfo.InvariantCulture), result.Min);
        Assert.Equal(expectedMax is null ? null : Convert.ToDecimal(expectedMax, System.Globalization.CultureInfo.InvariantCulture), result.Max);
        Assert.Equal(expectedUnit, result.Unit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("Absent")]
    [InlineData("Negative")]
    [InlineData("Pass")]
    [InlineData("<10")]
    [InlineData(">5")]
    [InlineData("110-90")]         // min > max
    [InlineData("10 - 20 - 30")]   // multiple dashes
    [InlineData("10 -")]
    [InlineData("- 20")]
    [InlineData("NMT")]
    [InlineData("NLT")]
    public void Parse_UnparseableOrEmpty_ReturnsNoLimitAndNeverThrows(string? input)
    {
        var result = SpecLimitParser.Parse(input);
        Assert.False(result.HasLimit);
        Assert.False(result.IsRange);
        Assert.Null(result.Min);
        Assert.Null(result.Max);
        Assert.Null(result.Unit);
    }

    [Fact]
    public void ParsedSpecLimit_BoundaryEquality_IsInSpec()
    {
        var range = SpecLimitParser.Parse("90.0 - 110.0 %");
        Assert.False(range.IsExceededBy(90.0m));  // Lower boundary = in spec
        Assert.False(range.IsExceededBy(110.0m)); // Upper boundary = in spec
        Assert.False(range.IsExceededBy(100.0m)); // Within = in spec
        Assert.True(range.IsExceededBy(89.99m));  // Below min = out of spec
        Assert.True(range.IsExceededBy(110.01m)); // Above max = out of spec

        var nmt = SpecLimitParser.Parse("NMT 100");
        Assert.False(nmt.IsExceededBy(100m));     // Boundary = in spec
        Assert.False(nmt.IsExceededBy(99.9m));    // Below = in spec
        Assert.True(nmt.IsExceededBy(100.01m));   // Above = out of spec

        var nlt = SpecLimitParser.Parse("NLT 90");
        Assert.False(nlt.IsExceededBy(90m));      // Boundary = in spec
        Assert.False(nlt.IsExceededBy(90.1m));    // Above = in spec
        Assert.True(nlt.IsExceededBy(89.99m));    // Below = out of spec

        var plain = SpecLimitParser.Parse("100");
        Assert.False(plain.IsExceededBy(100m));   // Boundary = in spec
        Assert.False(plain.IsExceededBy(50m));    // Below = in spec
        Assert.True(plain.IsExceededBy(100.01m)); // Above = out of spec

        var none = SpecLimitParser.Parse("Invalid");
        Assert.False(none.IsExceededBy(10000m));  // Unparseable = never exceeded
    }

    [Fact]
    public void TestWorkflowEngine_Compare_EvaluatesRangePrecedenceAndBoundaries()
    {
        const string spec = "90.0 - 110.0 %";
        const string action = "92.0 - 108.0 %";
        const string alert = "95.0 - 105.0 %";

        // Within all limits
        Assert.Equal(("WithinLimits", null), TestWorkflowEngine.Compare(100.0m, alert, action, spec));

        // Boundary equality on alert
        Assert.Equal(("WithinLimits", null), TestWorkflowEngine.Compare(95.0m, alert, action, spec));
        Assert.Equal(("WithinLimits", null), TestWorkflowEngine.Compare(105.0m, alert, action, spec));

        // Alert limit exceeded (below or above)
        Assert.Equal(("AlertLimitExceeded", "Alert"), TestWorkflowEngine.Compare(94.9m, alert, action, spec));
        Assert.Equal(("AlertLimitExceeded", "Alert"), TestWorkflowEngine.Compare(105.1m, alert, action, spec));

        // Action boundary equality is within action, but exceeds alert
        Assert.Equal(("AlertLimitExceeded", "Alert"), TestWorkflowEngine.Compare(92.0m, alert, action, spec));
        Assert.Equal(("AlertLimitExceeded", "Alert"), TestWorkflowEngine.Compare(108.0m, alert, action, spec));

        // Action limit exceeded
        Assert.Equal(("ActionLimitExceeded", "Action"), TestWorkflowEngine.Compare(91.9m, alert, action, spec));
        Assert.Equal(("ActionLimitExceeded", "Action"), TestWorkflowEngine.Compare(108.1m, alert, action, spec));

        // Spec boundary equality is within spec, but exceeds action
        Assert.Equal(("ActionLimitExceeded", "Action"), TestWorkflowEngine.Compare(90.0m, alert, action, spec));
        Assert.Equal(("ActionLimitExceeded", "Action"), TestWorkflowEngine.Compare(110.0m, alert, action, spec));

        // Spec limit exceeded (OutOfSpecification)
        Assert.Equal(("OutOfSpecification", "Specification"), TestWorkflowEngine.Compare(89.9m, alert, action, spec));
        Assert.Equal(("OutOfSpecification", "Specification"), TestWorkflowEngine.Compare(110.1m, alert, action, spec));
    }

    [Fact]
    public void TestWorkflowEngine_Compare_PreservesExistingCountTestBehavior()
    {
        const string alert = "10";
        const string action = "50";
        const string spec = "100";

        Assert.Equal(("WithinLimits", null), TestWorkflowEngine.Compare(5m, alert, action, spec));
        Assert.Equal(("WithinLimits", null), TestWorkflowEngine.Compare(10m, alert, action, spec));
        Assert.Equal(("AlertLimitExceeded", "Alert"), TestWorkflowEngine.Compare(15m, alert, action, spec));
        Assert.Equal(("AlertLimitExceeded", "Alert"), TestWorkflowEngine.Compare(50m, alert, action, spec));
        Assert.Equal(("ActionLimitExceeded", "Action"), TestWorkflowEngine.Compare(60m, alert, action, spec));
        Assert.Equal(("ActionLimitExceeded", "Action"), TestWorkflowEngine.Compare(100m, alert, action, spec));
        Assert.Equal(("OutOfSpecification", "Specification"), TestWorkflowEngine.Compare(150m, alert, action, spec));
    }

    [Fact]
    public void TestWorkflowEngine_Compare_HandlesNmtAndNlt()
    {
        // NMT
        Assert.Equal(("WithinLimits", null), TestWorkflowEngine.Compare(10m, "NMT 10", "NMT 50", "NMT 100"));
        Assert.Equal(("AlertLimitExceeded", "Alert"), TestWorkflowEngine.Compare(15m, "NMT 10", "NMT 50", "NMT 100"));
        Assert.Equal(("ActionLimitExceeded", "Action"), TestWorkflowEngine.Compare(60m, "NMT 10", "NMT 50", "NMT 100"));
        Assert.Equal(("OutOfSpecification", "Specification"), TestWorkflowEngine.Compare(150m, "NMT 10", "NMT 50", "NMT 100"));

        // NLT (spec = NLT 10, action = NLT 20, alert = NLT 50)
        Assert.Equal(("WithinLimits", null), TestWorkflowEngine.Compare(60m, "NLT 50", "NLT 20", "NLT 10"));
        Assert.Equal(("WithinLimits", null), TestWorkflowEngine.Compare(50m, "NLT 50", "NLT 20", "NLT 10"));
        Assert.Equal(("AlertLimitExceeded", "Alert"), TestWorkflowEngine.Compare(45m, "NLT 50", "NLT 20", "NLT 10"));
        Assert.Equal(("ActionLimitExceeded", "Action"), TestWorkflowEngine.Compare(15m, "NLT 50", "NLT 20", "NLT 10"));
        Assert.Equal(("OutOfSpecification", "Specification"), TestWorkflowEngine.Compare(5m, "NLT 50", "NLT 20", "NLT 10"));
    }

    [Fact]
    public void TestWorkflowEngine_Compare_HandlesUnparseableLimits()
    {
        // All unparseable -> LimitsNotConfigured
        Assert.Equal(("LimitsNotConfigured", null), TestWorkflowEngine.Compare(50m, "", "   ", null));
        Assert.Equal(("LimitsNotConfigured", null), TestWorkflowEngine.Compare(50m, "Invalid", "N/A", "Pass"));

        // Partial unparseable: valid alert limit is evaluated
        Assert.Equal(("WithinLimits", null), TestWorkflowEngine.Compare(5m, "10", "N/A", "Pass"));
        Assert.Equal(("AlertLimitExceeded", "Alert"), TestWorkflowEngine.Compare(15m, "10", "N/A", "Pass"));
    }

    [Fact]
    public void SpecificationService_CompareAgainstLimits_EvaluatesRangesCorrectly()
    {
        using var db = NewDb();
        var service = new SpecificationService(db);
        var spec = new Specification
        {
            TestCode = "VITC_ASSAY",
            AlertLimit = "95.0 - 105.0 %",
            ActionLimit = "92.0 - 108.0 %",
            SpecLimit = "90.0 - 110.0 %"
        };

        Assert.Equal("WithinLimits", service.CompareAgainstLimits(100.0m, spec));
        Assert.Equal("WithinLimits", service.CompareAgainstLimits(95.0m, spec));
        Assert.Equal("WithinLimits", service.CompareAgainstLimits(105.0m, spec));
        Assert.Equal("AlertLimitExceeded", service.CompareAgainstLimits(94.9m, spec));
        Assert.Equal("AlertLimitExceeded", service.CompareAgainstLimits(105.1m, spec));
        Assert.Equal("ActionLimitExceeded", service.CompareAgainstLimits(91.9m, spec));
        Assert.Equal("ActionLimitExceeded", service.CompareAgainstLimits(108.1m, spec));
        Assert.Equal("ActionLimitExceeded", service.CompareAgainstLimits(90.0m, spec)); // within spec boundary, exceeds action
        Assert.Equal("ActionLimitExceeded", service.CompareAgainstLimits(110.0m, spec)); // within spec boundary, exceeds action
        Assert.Equal("OutOfSpecification", service.CompareAgainstLimits(89.9m, spec));
        Assert.Equal("OutOfSpecification", service.CompareAgainstLimits(110.1m, spec));
    }

    [Fact]
    public void SpecificationService_CompareAgainstLimits_HandlesUnparseableAndNmt()
    {
        using var db = NewDb();
        var service = new SpecificationService(db);

        var unparseableSpec = new Specification
        {
            TestCode = "ECOLI",
            AlertLimit = "Absent",
            ActionLimit = "Negative",
            SpecLimit = "Pass"
        };
        Assert.Equal("LimitsNotConfigured", service.CompareAgainstLimits(50, unparseableSpec));

        var nmtSpec = new Specification
        {
            TestCode = "TAMC",
            AlertLimit = "NMT 10",
            ActionLimit = "NMT 50",
            SpecLimit = "NMT 100"
        };
        Assert.Equal("WithinLimits", service.CompareAgainstLimits(10, nmtSpec));
        Assert.Equal("AlertLimitExceeded", service.CompareAgainstLimits(15, nmtSpec));
        Assert.Equal("ActionLimitExceeded", service.CompareAgainstLimits(60, nmtSpec));
        Assert.Equal("OutOfSpecification", service.CompareAgainstLimits(150, nmtSpec));
    }

    [Fact]
    public void WaterWorkflowEngine_And_PathogenSessionService_DelegateComparison()
    {
        // WaterWorkflowEngine.Compare
        Assert.Equal(("WithinLimits", null), WaterWorkflowEngine.Compare(100m, "95-105", "92-108", "90-110"));
        Assert.Equal(("OutOfSpecification", "Specification"), WaterWorkflowEngine.Compare(89m, "95-105", "92-108", "90-110"));
        Assert.Equal(("OutOfSpecification", "Specification"), WaterWorkflowEngine.Compare(111m, "95-105", "92-108", "90-110"));
    }

    [Theory]
    [InlineData("90.0 - 110.0", "%", "90.0 – 110.0 %")]
    [InlineData("90.0-110.0", "%", "90.0 – 110.0 %")]
    [InlineData("90.0 – 110.0 %", null, "90.0 – 110.0 %")]
    [InlineData("90.0 – 110.0 %", "%", "90.0 – 110.0 %")]
    [InlineData("90.0 - 110.0", null, "90.0 – 110.0")]
    [InlineData("90 - 110", "%", "90 – 110 %")]
    [InlineData("100", "g", "NMT 100/g")]
    [InlineData("100", "", "100")]
    [InlineData("100", null, "100")]
    [InlineData("NMT 100", null, "NMT 100")]
    [InlineData("NMT 100", "g", "NMT 100")]
    [InlineData("NLT 90", null, "NLT 90")]
    [InlineData("NLT 90", "%", "NLT 90")]
    [InlineData("Absent in 10g", null, "Absent in 10g")]
    public void SampleSummaryService_FormatSpecificationText_FormatsCorrectly(string? specLimit, string? unit, string? expected)
    {
        var spec = new Specification { SpecLimit = specLimit, Unit = unit };
        var formatted = SampleSummaryService.FormatSpecificationText(spec);
        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void SampleSummaryService_FormatSpecificationText_NullOrEmptySpec_ReturnsNull()
    {
        Assert.Null(SampleSummaryService.FormatSpecificationText(null));
        Assert.Null(SampleSummaryService.FormatSpecificationText(new Specification { SpecLimit = null }));
        Assert.Null(SampleSummaryService.FormatSpecificationText(new Specification { SpecLimit = "" }));
        Assert.Null(SampleSummaryService.FormatSpecificationText(new Specification { SpecLimit = "   " }));
    }
}
