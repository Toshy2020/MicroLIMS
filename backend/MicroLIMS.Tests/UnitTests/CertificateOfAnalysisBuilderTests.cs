using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
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
    private static TestOrderSummaryDetailDto Located(int id, string code, params (string Key, ResultStatus? Status, decimal? Cfu)[] locations) => new()
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
    [InlineData(ResultStatus.WithinLimits, ResultConformance.Conforms)]
    [InlineData(ResultStatus.Absent, ResultConformance.Conforms)]
    [InlineData(ResultStatus.LimitsNotConfigured, ResultConformance.LimitsNotConfigured)]
    [InlineData(null, ResultConformance.NoResult)]
    [InlineData(ResultStatus.OutOfSpecification, ResultConformance.DoesNotConform)]
    [InlineData(ResultStatus.PendingConfirmation, ResultConformance.DoesNotConform)]
    [InlineData(ResultStatus.RequiresReview, ResultConformance.DoesNotConform)]
    public void FromStatus_ClassifiesEveryStatusTheBackendWrites(ResultStatus? status, ResultConformance expected) =>
        Assert.Equal(expected, ResultConformanceRules.FromStatus(status));

    // ---- Location matrix ----

    [Fact]
    public void AllLocationsConform_Complies()
    {
        var coa = CertificateOfAnalysisBuilder.Build(Summary(
            Located(1, "TAMC", ("1", ResultStatus.WithinLimits, 2m), ("2", ResultStatus.WithinLimits, 0m)),
            Located(2, "PSEUDO", ("1", ResultStatus.Absent, null), ("2", ResultStatus.Absent, null))));

        Assert.True(coa.Sample.Complies);
        Assert.Equal("This sample complies with the specified requirements. All 2 tests conform across all 2 sampling locations.", coa.Sample.ConclusionText);
    }

    // Was certified as conforming by the browser (an empty status counted
    // as a pass).
    [Fact]
    public void LocationWithNoResult_IsNotCertified()
    {
        var coa = CertificateOfAnalysisBuilder.Build(Summary(
            Located(1, "TAMC", ("1", ResultStatus.WithinLimits, 2m), ("2", null, null))));

        Assert.False(coa.Sample.Complies);
        Assert.Equal("Cannot certify because no result is recorded for: TAMC at Room 2.", coa.Sample.ConclusionText);
        var cell = coa.Sample.Matrix!.Rows.Single(r => r.LocationKey == "2").Cells.Single()!;
        Assert.False(cell.Conform);
        Assert.Equal("—", cell.Result);
    }

    [Fact]
    public void LimitsNotConfigured_IsNotCertified()
    {
        var coa = CertificateOfAnalysisBuilder.Build(Summary(
            Located(1, "TAMC", ("1", ResultStatus.LimitsNotConfigured, 5m))));

        Assert.False(coa.Sample.Complies);
        Assert.Equal("Cannot certify because limits are not configured for: TAMC at Room 1.", coa.Sample.ConclusionText);
    }

    [Fact]
    public void FailureWithMissingResult_NamesBoth()
    {
        var coa = CertificateOfAnalysisBuilder.Build(Summary(
            Located(1, "TAMC", ("1", ResultStatus.OutOfSpecification, 50m), ("2", null, null))));

        Assert.Equal(
            "This sample does not comply with the specified requirements. Exceptions: TAMC at Room 1. " +
            "Additionally, cannot certify because no result is recorded for: TAMC at Room 2.",
            coa.Sample.ConclusionText);
    }

    [Fact]
    public void DuplicateTestCode_ShowsTheFailingOrder_AndTamcLeads()
    {
        var coa = CertificateOfAnalysisBuilder.Build(Summary(
            Located(1, "PSEUDO", ("1", ResultStatus.Absent, null)),
            Located(2, "TAMC", ("1", ResultStatus.WithinLimits, 1m)),
            Located(3, "TAMC", ("1", ResultStatus.OutOfSpecification, 40m))));

        var columns = coa.Sample.Matrix!.Columns;
        Assert.Equal(new[] { "TAMC", "PSEUDO" }, columns.Select(c => c.TestCode));
        Assert.Equal(3, columns[0].TestOrderId);
    }

    [Fact]
    public void QuantitativeResult_PrintsWithoutTrailingZeros()
    {
        var coa = CertificateOfAnalysisBuilder.Build(Summary(Located(1, "TAMC", ("1", ResultStatus.WithinLimits, 12.0m))));

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

    // ---- Supporting rows: not-applicable tests and supplier-stated attributes ----

    private static TestOrderSummaryDetailDto AbsentBiochemical(int id = 1) => new()
    {
        TestOrderId = id, SectionId = 1, TestCode = "SALM", TestDisplayName = "Salmonella", Status = "Approved",
        BiochemicalResults = { new BiochemicalResultDetailDto { OrganismDetected = false, SubmittedAt = DateTime.UtcNow } }
    };

    private static NotApplicableTest Na(string code, string? grade, NotApplicableStatus status, string? reference) => new()
    {
        ItemId = 7, TestCode = code, Grade = grade, Status = status, Reference = reference
    };

    private static readonly IReadOnlyDictionary<string, (string Name, int? SectionId)> TestNames =
        new Dictionary<string, (string, int?)> { ["HM"] = ("Heavy metals", 1), ["PS"] = ("Particle size", 1) };

    private static List<CoaSimpleRowDto> Supporting(string? grade, NotApplicableTest[] na, params SupplierStatedAttributeDto[] supplier) =>
        CertificateOfAnalysisBuilder.BuildSupportingRows(grade, na, TestNames, supplier);

    [Fact]
    public void NotApplicableRow_PrintsWithReference_AndDoesNotChangeTheVerdict()
    {
        var supporting = Supporting(null, new[]
        {
            Na("HM", null, NotApplicableStatus.Approved, "QC form NA-01"),
            Na("PS", null, NotApplicableStatus.Approved, null)
        });

        var coa = CertificateOfAnalysisBuilder.Build(Summary(AbsentBiochemical()), supporting);

        Assert.True(coa.Sample.Complies);
        Assert.Equal("Test results of the sample are Conform according to specification and decision rule.", coa.Sample.ConclusionText);
        Assert.True(coa.Sample.Simple!.OverallComplies);
        Assert.Single(coa.Sample.Simple.Rows);
        Assert.Equal(2, coa.Sample.SupportingRows.Count);
        var row = coa.Sample.SupportingRows[0];
        Assert.Equal("NotApplicable", row.Kind);
        Assert.Equal("Heavy metals", row.TestDisplayName);
        Assert.Null(row.Specification);
        Assert.Equal("Not applicable (QC form NA-01)", row.Result);
        Assert.Null(row.AnalystName);
        Assert.Equal("Not applicable", coa.Sample.SupportingRows[1].Result);
        Assert.Equal("Tested", coa.Sample.Simple.Rows[0].Kind);
    }

    [Fact]
    public void NotApplicableRow_IsDropped_WhenTheSampleActuallyOrderedThatTest()
    {
        var ordered = AbsentBiochemical();
        ordered.TestCode = "HM";
        var supporting = Supporting(null, new[]
        {
            Na("HM", null, NotApplicableStatus.Approved, "QC form NA-01"),
            Na("PS", null, NotApplicableStatus.Approved, null)
        });

        var coa = CertificateOfAnalysisBuilder.Build(Summary(ordered), supporting);

        Assert.Equal("Particle size", Assert.Single(coa.Sample.SupportingRows).TestDisplayName);

        // A superseded order does not count as ordered.
        ordered.IsSuperseded = true;
        var coa2 = CertificateOfAnalysisBuilder.Build(Summary(ordered), supporting);
        Assert.Equal(2, coa2.Sample.SupportingRows.Count);
    }

    [Fact]
    public void SupplierRow_PrintsStatedBySupplier()
    {
        var supporting = Supporting(null, Array.Empty<NotApplicableTest>(),
            new SupplierStatedAttributeDto(1, 7, null, "Particle size", "20 mesh", 0));

        var coa = CertificateOfAnalysisBuilder.Build(Summary(AbsentBiochemical()), supporting);

        var row = Assert.Single(coa.Sample.SupportingRows);
        Assert.Equal("SupplierStated", row.Kind);
        Assert.Equal("Particle size", row.TestDisplayName);
        Assert.Null(row.Specification);
        Assert.Equal("20 mesh (stated by supplier)", row.Result);
        Assert.Null(row.AnalystName);
        Assert.True(coa.Sample.Complies);
    }

    [Fact]
    public void FailingTestedRow_StillFails_WithSupportingRowsPresent()
    {
        var failing = new TestOrderSummaryDetailDto
        {
            TestOrderId = 2, SectionId = 1, TestCode = "SALM", TestDisplayName = "Salmonella", Status = "Approved",
            BiochemicalResults = { new BiochemicalResultDetailDto { OrganismDetected = true, SubmittedAt = DateTime.UtcNow } }
        };
        var supporting = Supporting(null, new[] { Na("HM", null, NotApplicableStatus.Approved, "QC form NA-01") },
            new SupplierStatedAttributeDto(1, 7, null, "Particle size", "20 mesh", 0));

        var coa = CertificateOfAnalysisBuilder.Build(Summary(failing), supporting);

        Assert.False(coa.Sample.Complies);
        Assert.Equal("Test results of the sample are Non-Conform according to specification and decision rule.", coa.Sample.ConclusionText);
        Assert.Equal(2, coa.Sample.SupportingRows.Count);
    }

    [Fact]
    public void OnlyApprovedNotApplicableRecordsOfTheSampleGrade_Print()
    {
        var supporting = Supporting("USP", new[]
        {
            Na("HM", "USP", NotApplicableStatus.Approved, "usp-grade"),
            Na("HM", "usp", NotApplicableStatus.Approved, "case-insensitive"),
            Na("HM", null, NotApplicableStatus.Approved, "all-grades"),
            Na("HM", "BP", NotApplicableStatus.Approved, "other-grade"),
            Na("HM", "USP", NotApplicableStatus.Draft, "draft"),
            Na("HM", "USP", NotApplicableStatus.HeadApproved, "head-approved"),
            Na("HM", "USP", NotApplicableStatus.Withdrawn, "withdrawn")
        });

        Assert.Equal(
            new[] { "Not applicable (usp-grade)", "Not applicable (case-insensitive)", "Not applicable (all-grades)" },
            supporting.Select(s => s.Result).ToArray());

        // An ungraded sample only takes the all-grade record.
        var ungraded = Supporting(null, new[]
        {
            Na("HM", "USP", NotApplicableStatus.Approved, "usp-grade"),
            Na("HM", null, NotApplicableStatus.Approved, "all-grades")
        });
        Assert.Equal("Not applicable (all-grades)", Assert.Single(ungraded).Result);
    }

    private static SampleSummaryDto TwoLabSummary()
    {
        var summary = Summary(AbsentBiochemical());
        summary.Sections.Add(new SampleSectionSummaryDto { SectionId = 2, SectionName = "Chemistry", Status = "Approved" });
        return summary;
    }

    private static readonly IReadOnlyDictionary<string, (string Name, int? SectionId)> TwoLabTestNames =
        new Dictionary<string, (string, int?)> { ["HM"] = ("Heavy metals", 2), ["MICRO_NA"] = ("Mould", 1) };

    private static List<CoaSimpleRowDto> TwoLabSupporting() => CertificateOfAnalysisBuilder.BuildSupportingRows(
        null,
        new[]
        {
            Na("HM", null, NotApplicableStatus.Approved, "QC form NA-01"),      // Chemistry
            Na("MICRO_NA", null, NotApplicableStatus.Approved, "QC form NA-02"), // Microbiology
            Na("UNKNOWN", null, NotApplicableStatus.Approved, "QC form NA-03")   // no known section
        },
        TwoLabTestNames,
        new[] { new SupplierStatedAttributeDto(1, 7, null, "Particle size", "20 mesh", 0) });

    [Fact]
    public void SupportingRows_CarryTheirSection_SoACombinedCertificateCanPrintUnscopedOnesOnce()
    {
        var rows = TwoLabSupporting();

        Assert.Equal(2, rows.Single(r => r.TestDisplayName == "Heavy metals").SectionId);
        Assert.Equal(1, rows.Single(r => r.TestDisplayName == "Mould").SectionId);
        Assert.Null(rows.Single(r => r.TestCode == "UNKNOWN").SectionId);
        Assert.Null(rows.Single(r => r.Kind == "SupplierStated").SectionId);
    }

    [Fact]
    public void SectionScope_TakesItsOwnAndUnscopedRows_EvenWithNoTestedRows()
    {
        var coa = CertificateOfAnalysisBuilder.Build(TwoLabSummary(), TwoLabSupporting());

        // Chemistry has no tested rows at all, yet keeps its own N/A row.
        var chem = coa.Sections.Single(s => s.SectionId == 2).Scope;
        Assert.Null(chem.Simple);
        Assert.Equal(new[] { "Heavy metals", "Particle size", "UNKNOWN" }, chem.SupportingRows.Select(r => r.TestDisplayName).OrderBy(x => x).ToArray());
        Assert.DoesNotContain(chem.SupportingRows, r => r.TestDisplayName == "Mould");
        Assert.Contains(chem.SupportingRows, r => r.TestDisplayName == "Heavy metals");
        // A lab with only supporting rows still has no verdict of its own.
        Assert.False(chem.Complies);

        var micro = coa.Sections.Single(s => s.SectionId == 1).Scope;
        Assert.DoesNotContain(micro.SupportingRows, r => r.TestDisplayName == "Heavy metals");
        Assert.Contains(micro.SupportingRows, r => r.TestDisplayName == "Mould");

        Assert.Equal(4, coa.Sample.SupportingRows.Count);
    }

    [Fact]
    public void RestrictedSections_ShowOnlyUnscopedAndTheirOwnSupportingRows()
    {
        var coa = CertificateOfAnalysisBuilder.Build(TwoLabSummary(), TwoLabSupporting(), sectionIds: new[] { 1 });

        var names = coa.Sample.SupportingRows.Select(r => r.TestDisplayName).ToList();
        Assert.DoesNotContain("Heavy metals", names);
        Assert.Contains("Mould", names);
        Assert.Contains("Particle size", names);
        Assert.Equal(3, names.Count);
    }

    [Fact]
    public void RestrictedSections_ASectionTheViewerCannotSeeCarriesNoOtherLabRows()
    {
        var coa = CertificateOfAnalysisBuilder.Build(TwoLabSummary(), TwoLabSupporting(), sectionIds: new[] { 1 });

        var hidden = coa.Sections.Single(s => s.SectionId == 2).Scope;
        Assert.DoesNotContain(hidden.SupportingRows, r => r.SectionId == 2);
        Assert.DoesNotContain(hidden.SupportingRows, r => r.TestDisplayName == "Heavy metals");

        var visible = coa.Sections.Single(s => s.SectionId == 1).Scope;
        Assert.Contains(visible.SupportingRows, r => r.TestDisplayName == "Mould");
    }

    [Fact]
    public void CountTestWithoutAReading_IsNotCertified()
    {
        // A saved reading always carries a status, so the unread count
        // test is one with no reading at all.
        var order = new TestOrderSummaryDetailDto
        {
            TestOrderId = 1, SectionId = 1, TestCode = "TAMC", TestDisplayName = "TAMC"
        };

        var coa = CertificateOfAnalysisBuilder.Build(Summary(order));

        Assert.False(coa.Sample.Complies);
        Assert.True(coa.Sample.Simple!.Rows.Single().NoResult);
        Assert.Equal("Cannot certify because one or more tests has no recorded result.", coa.Sample.ConclusionText);
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
        var summary = Summary(Located(1, "TAMC", ("1", ResultStatus.WithinLimits, 1m)));
        summary.OverallStatus = "Rejected";
        summary.Sections[0].Status = "Rejected";

        var coa = CertificateOfAnalysisBuilder.Build(summary);

        Assert.False(coa.Sample.Complies);
        Assert.Equal("Rejected: Microbiology", coa.Sample.ConclusionText);
    }

    [Fact]
    public void EachSection_OnlyJudgesItsOwnTests()
    {
        var micro = Located(1, "TAMC", ("1", ResultStatus.WithinLimits, 1m));
        var chem = Located(2, "ASSAY", ("1", ResultStatus.OutOfSpecification, 99m));
        chem.SectionId = 2;
        var summary = Summary(micro, chem);
        summary.Sections.Add(new SampleSectionSummaryDto { SectionId = 2, SectionName = "Chemistry", Status = "Approved" });

        var coa = CertificateOfAnalysisBuilder.Build(summary);

        Assert.True(coa.Sections.Single(s => s.SectionId == 1).Scope.Complies);
        Assert.False(coa.Sections.Single(s => s.SectionId == 2).Scope.Complies);
        Assert.False(coa.Sample.Complies);
    }
}
