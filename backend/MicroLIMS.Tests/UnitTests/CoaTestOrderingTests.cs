using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Certificate of Analysis row order for Product / Raw Material / Packaging
// (CoaTestOrdering, used by CertificateOfAnalysisBuilder.BuildSimpleRows):
// physicochemical first - qualitative (appearance, identification, ...),
// assays, then the other physicochemical tests - then microbiology - counts
// (TAMC, TYMC) then pathogens (E. coli, Pseudomonas, Salmonella, ...). The
// group comes from section + workflow type; inside it the Test Master CoA
// order, then the display name. Pure projection: no database (Rule 4.3).
public class CoaTestOrderingTests
{
    private static int _nextId;

    private static TestOrderSummaryDetailDto Test(string code, string name, string sectionCode, WorkflowType workflow, int? coaOrder = null) => new()
    {
        TestOrderId = Interlocked.Increment(ref _nextId),
        SectionId = sectionCode == LaboratoryCodes.Physicochemical ? 2 : 1,
        SectionCode = sectionCode,
        TestCode = code,
        TestDisplayName = name,
        WorkflowType = workflow,
        CoaDisplayOrder = coaOrder,
    };

    private const string Fp = LaboratoryCodes.Physicochemical;
    private const string Micro = LaboratoryCodes.Microbiology;

    private static List<string> RowCodes(IEnumerable<TestOrderSummaryDetailDto> tests) =>
        CertificateOfAnalysisBuilder.BuildSimpleRows(tests.ToList())!.Rows.Select(r => r.TestCode).ToList();

    [Fact]
    public void BuildSimpleRows_OrdersPhyschemThenMicro_AsTheLaboratoryDefined()
    {
        // Deliberately shuffled, and with CoA orders that disagree with the
        // alphabetical order wherever possible, so neither input order nor
        // name order can produce the expected result by accident.
        var tests = new[]
        {
            Test("SALM", "Salmonella", Micro, WorkflowType.Observation, coaOrder: 3),
            Test("LOD", "Loss on drying", Fp, WorkflowType.Gravimetric),
            Test("TYMC", "Total yeast and mould count", Micro, WorkflowType.CountTest, coaOrder: 2),
            Test("ID", "Identification", Fp, WorkflowType.Qualitative, coaOrder: 2),
            Test("ASSAY", "Assay (HPLC)", Fp, WorkflowType.HplcMethodAssay),
            Test("PSAE", "Pseudomonas aeruginosa", Micro, WorkflowType.Observation, coaOrder: 2),
            Test("APP", "Appearance", Fp, WorkflowType.Qualitative, coaOrder: 1),
            Test("TAMC", "Total aerobic microbial count", Micro, WorkflowType.CountTest, coaOrder: 1),
            Test("PH", "pH", Fp, WorkflowType.Measurement),
            Test("ECOLI", "Escherichia coli", Micro, WorkflowType.Observation, coaOrder: 1),
            Test("TITR", "Assay (titration)", Fp, WorkflowType.Titration),
            Test("CLAR", "Clarity of solution", Fp, WorkflowType.Qualitative),
            Test("BTGN", "Bile-tolerant gram-negative bacteria", Micro, WorkflowType.Observation),
        };

        Assert.Equal(new[]
        {
            // Physicochemical - qualitative: ordered first, then unordered by name.
            "APP", "ID", "CLAR",
            // Physicochemical - assays, by name.
            "ASSAY", "TITR",
            // Physicochemical - other tests, after the assays, by name.
            "LOD", "PH",
            // Microbiology - counts.
            "TAMC", "TYMC",
            // Microbiology - pathogens: ordered first, unordered after.
            "ECOLI", "PSAE", "SALM", "BTGN",
        }, RowCodes(tests));
    }

    // The test code and name never decide the group: a test coded "TAMC" but
    // configured as a pathogen workflow sits with the pathogens, and a count
    // test in the physicochemical section stays in the physicochemical block.
    [Fact]
    public void Group_ComesFromSectionAndWorkflowType_NotFromTheCode()
    {
        var tests = new[]
        {
            Test("TAMC", "Total aerobic microbial count", Micro, WorkflowType.Observation),
            Test("ZCOUNT", "Z count", Micro, WorkflowType.CountTest),
            Test("FPCOUNT", "Particle count", Fp, WorkflowType.CountTest),
        };

        Assert.Equal(new[] { "FPCOUNT", "ZCOUNT", "TAMC" }, RowCodes(tests));
    }

    // CoA order wins over the name inside a group, so the Section Head can put
    // any pathogen first.
    [Fact]
    public void CoaOrder_OverridesAlphabeticalOrder_WithinAGroup()
    {
        var tests = new[]
        {
            Test("ECOLI", "Escherichia coli", Micro, WorkflowType.Observation, coaOrder: 2),
            Test("SALM", "Salmonella", Micro, WorkflowType.Observation, coaOrder: 1),
        };

        Assert.Equal(new[] { "SALM", "ECOLI" }, RowCodes(tests));
    }

    // A multi-parameter physicochemical test keeps its parameter rows together
    // and in their own order, at the test's position.
    [Fact]
    public void MultiParameterTest_KeepsItsParameterRowsTogether()
    {
        var assay = Test("ASSAY", "Assay", Fp, WorkflowType.HplcMethodAssay);
        assay.Analysis = new AnalysisDetailDto
        {
            ParameterResults = new List<ParameterResultDetailDto>
            {
                new() { ParameterName = "Analyte B" },
                new() { ParameterName = "Analyte A" },
            }
        };
        var tests = new[]
        {
            Test("TAMC", "Total aerobic microbial count", Micro, WorkflowType.CountTest, coaOrder: 1),
            assay,
            Test("APP", "Appearance", Fp, WorkflowType.Qualitative, coaOrder: 1),
        };

        Assert.Equal(new[] { "APP", "ASSAY:Analyte B", "ASSAY:Analyte A", "TAMC" }, RowCodes(tests));
    }

    // Laboratory blocks on the combined certificate: Physicochemical first even
    // when its section has the higher id.
    [Fact]
    public void SampleSections_ListPhysicochemicalBeforeMicrobiology()
    {
        var sample = new Sample
        {
            Id = 1,
            TestOrders = new List<TestOrder>
            {
                new() { Id = 1, SectionId = 1, TestCode = "TAMC" },
                new() { Id = 2, SectionId = 2, TestCode = "APP" },
                new() { Id = 3, SectionId = 3, TestCode = "OTHER" },
            }
        };
        var sectionRows = new Dictionary<int, SampleSectionRow>
        {
            [1] = new(Micro, "Microbiology"),
            [2] = new(Fp, "Physicochemical"),
            [3] = new("STAB", "Stability"),
        };

        var sections = SampleSummarySectionsBuilder.Build(sample, sectionIds: null, sectionRows, _ => "");

        Assert.Equal(new[] { Fp, Micro, "STAB" }, sections.Select(s => s.SectionCode));
    }
}
