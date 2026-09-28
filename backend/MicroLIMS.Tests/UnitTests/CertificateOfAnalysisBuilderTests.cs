using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The Certificate of Analysis verdicts, decided on the server. The
// browser used to decide them with its own rule, which certified a
// location with no result as conforming and judged a biochemical-only
// test by the order's approval state - those two cases are pinned here.
public class CertificateOfAnalysisBuilderTests
{
    private static TestOrderSummaryDetailDto Located(int id, string code, params (string Key, string? Status, decimal? Cfu)[] locations) => new()
    {
        TestOrderId = id,
        SectionId = 1,
        TestCode = code,
        TestDisplayName = code,
        Locations = locations.Select(l => new SampleLocationDetailDto
        {
            LocationKey = l.Key,
            LocationName = "Room " + l.Key,
            Status = l.Status,
            CFUResult = l.Cfu,
            SpecLimit = "10",
            Unit = l.Cfu is null ? null : "CFU/plate"
        }).ToList()
    };

    private static SampleSummaryDto Summary(params TestOrderSummaryDetailDto[] orders) => new()
    {
        Status = "Approved",
        OverallStatus = "Approved",
        TestOrders = orders.ToList(),
        Sections = new() { new SampleSectionSummaryDto { SectionId = 1, SectionName = "Microbiology", Status = "Approved" } }
    };

    // ---- The shared rule ----

    [Theory]
    [InlineData("WithinLimits", ResultConformance.Conforms)]
    [InlineData("Absent", ResultConformance.Conforms)]
    [InlineData("LimitsNotConfigured", ResultConformance.LimitsNotConfigured)]
    [InlineData(null, ResultConformance.NoResult)]
    [InlineData("", ResultConformance.NoResult)]
    [InlineData("OutOfSpecification", ResultConformance.DoesNotConform)]
    [InlineData("PendingConfirmation", ResultConformance.DoesNotConform)]
    [InlineData("RequiresReview", ResultConformance.DoesNotConform)]
    public void FromStatus_ClassifiesEveryStatusTheBackendWrites(string? status, ResultConformance expected) =>
        Assert.Equal(expected, ResultConformanceRules.FromStatus(status));

    // ---- Location matrix ----

    [Fact]
    public void AllLocationsConform_Complies()
    {
        var coa = CertificateOfAnalysisBuilder.Build(Summary(
            Located(1, "TAMC", ("1", "WithinLimits", 2m), ("2", "WithinLimits", 0m)),
            Located(2, "PSEUDO", ("1", "Absent", null), ("2", "Absent", null))));

        Assert.True(coa.Sample.Complies);
        Assert.Equal("This sample complies with the specified requirements. All 2 tests conform across all 2 sampling locations.", coa.Sample.ConclusionText);
    }

    // Was certified as conforming by the browser (an empty status counted
    // as a pass).
    [Fact]
    public void LocationWithNoResult_IsNotCertified()
    {
        var coa = CertificateOfAnalysisBuilder.Build(Summary(
            Located(1, "TAMC", ("1", "WithinLimits", 2m), ("2", null, null))));

        Assert.False(coa.Sample.Complies);
        Assert.Equal("Cannot certify — no result is recorded for: TAMC at Room 2.", coa.Sample.ConclusionText);
        var cell = coa.Sample.Matrix!.Rows.Single(r => r.LocationKey == "2").Cells.Single()!;
        Assert.False(cell.Conform);
        Assert.Equal("—", cell.Result);
    }

    [Fact]
    public void LimitsNotConfigured_IsNotCertified()
    {
        var coa = CertificateOfAnalysisBuilder.Build(Summary(
            Located(1, "TAMC", ("1", "LimitsNotConfigured", 5m))));

        Assert.False(coa.Sample.Complies);
        Assert.Equal("Cannot certify — limits are not configured for: TAMC at Room 1.", coa.Sample.ConclusionText);
    }

    [Fact]
    public void FailureWithMissingResult_NamesBoth()
    {
        var coa = CertificateOfAnalysisBuilder.Build(Summary(
            Located(1, "TAMC", ("1", "OutOfSpecification", 50m), ("2", null, null))));

        Assert.Equal(
            "This sample does not comply with the specified requirements. Exceptions: TAMC at Room 1. " +
            "Additionally, cannot certify — no result is recorded for: TAMC at Room 2.",
            coa.Sample.ConclusionText);
    }

    [Fact]
    public void DuplicateTestCode_ShowsTheFailingOrder_AndTamcLeads()
    {
        var coa = CertificateOfAnalysisBuilder.Build(Summary(
            Located(1, "PSEUDO", ("1", "Absent", null)),
            Located(2, "TAMC", ("1", "WithinLimits", 1m)),
            Located(3, "TAMC", ("1", "OutOfSpecification", 40m))));

        var columns = coa.Sample.Matrix!.Columns;
        Assert.Equal(new[] { "TAMC", "PSEUDO" }, columns.Select(c => c.TestCode));
        Assert.Equal(3, columns[0].TestOrderId);
    }

    [Fact]
    public void QuantitativeResult_PrintsWithoutTrailingZeros()
    {
        var coa = CertificateOfAnalysisBuilder.Build(Summary(Located(1, "TAMC", ("1", "WithinLimits", 12.0m))));

        Assert.Equal("12", coa.Sample.Matrix!.Rows[0].Cells[0]!.Result);
    }

    // ---- One row per test ----

    // The browser judged this by the order's approval state ("Approved"),
    // which it never counted as conforming.
    [Fact]
    public void BiochemicalOnlyTest_IsJudgedByOrganismDetection()
    {
        var order = new TestOrderSummaryDetailDto
        {
            TestOrderId = 1, SectionId = 1, TestCode = "SALM", TestDisplayName = "Salmonella", Status = "Approved",
            BiochemicalResults = { new BiochemicalResultDetailDto { OrganismDetected = false, SubmittedAt = DateTime.UtcNow } }
        };

        var coa = CertificateOfAnalysisBuilder.Build(Summary(order));

        var row = Assert.Single(coa.Sample.Simple!.Rows);
        Assert.True(row.Conform);
        Assert.Equal("Absent", row.Result);
        Assert.Equal("Test results of the sample are Conform according to specification and decision rule.", coa.Sample.ConclusionText);
    }

    [Fact]
    public void CountTestWithoutAReading_IsNotCertified()
    {
        // Quantitative, but its single reading has no status yet.
        var order = new TestOrderSummaryDetailDto
        {
            TestOrderId = 1, SectionId = 1, TestCode = "TAMC", TestDisplayName = "TAMC",
            CountTestReadings = { new CountTestReadingDetailDto { ReportedResult = "", Status = "" } }
        };

        var coa = CertificateOfAnalysisBuilder.Build(Summary(order));

        Assert.False(coa.Sample.Complies);
        Assert.True(coa.Sample.Simple!.Rows.Single().NoResult);
        Assert.Equal("Cannot certify — one or more tests has no recorded result.", coa.Sample.ConclusionText);
    }

    [Fact]
    public void NoResultsAtAll_IsNotCertified()
    {
        var coa = CertificateOfAnalysisBuilder.Build(Summary());

        Assert.False(coa.Sample.Complies);
        Assert.Equal(CertificateOfAnalysisBuilder.SampleCompliesNoResultsText, coa.Sample.ConclusionText);
    }

    // ---- Scopes ----

    [Fact]
    public void RejectedSample_NamesTheRejectingLab()
    {
        var summary = Summary(Located(1, "TAMC", ("1", "WithinLimits", 1m)));
        summary.OverallStatus = "Rejected";
        summary.Sections[0].Status = "Rejected";

        var coa = CertificateOfAnalysisBuilder.Build(summary);

        Assert.False(coa.Sample.Complies);
        Assert.Equal("Rejected — Microbiology", coa.Sample.ConclusionText);
    }

    [Fact]
    public void EachSection_OnlyJudgesItsOwnTests()
    {
        var micro = Located(1, "TAMC", ("1", "WithinLimits", 1m));
        var chem = Located(2, "ASSAY", ("1", "OutOfSpecification", 99m));
        chem.SectionId = 2;
        var summary = Summary(micro, chem);
        summary.Sections.Add(new SampleSectionSummaryDto { SectionId = 2, SectionName = "Chemistry", Status = "Approved" });

        var coa = CertificateOfAnalysisBuilder.Build(summary);

        Assert.True(coa.Sections.Single(s => s.SectionId == 1).Scope.Complies);
        Assert.False(coa.Sections.Single(s => s.SectionId == 2).Scope.Complies);
        Assert.False(coa.Sample.Complies);
    }
}
