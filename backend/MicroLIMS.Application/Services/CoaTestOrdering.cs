using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services;

// Row order of the one-row-per-test Certificate of Analysis (Product, Raw
// Material, Packaging), as the laboratory defined it:
//
//   1. Physicochemical laboratory
//      a. qualitative tests (appearance, identification, ...)
//      b. assays (HPLC method, titration, ICP method, UV)
//      c. the other physicochemical tests (measurement, gravimetric,
//         dissolution, disintegration, weight variation)
//   2. Microbiology laboratory
//      a. count tests (TAMC, TYMC, ...)
//      b. pathogen tests (E. coli, Pseudomonas, Salmonella, ...)
//   3. Any other section
//
// The laboratory comes from the test's section code and the group from its
// configured workflow type - never from the test code or name (CLAUDE.md
// Principle 1). Inside a group the Section Head's "CoA order" in Test Master
// decides; tests without one follow, by display name, then code.
public static class CoaTestOrdering
{
    public static int LaboratoryRank(string? sectionCode) =>
        string.Equals(sectionCode, LaboratoryCodes.Physicochemical, StringComparison.OrdinalIgnoreCase) ? 0
        : string.Equals(sectionCode, LaboratoryCodes.Microbiology, StringComparison.OrdinalIgnoreCase) ? 1
        : 2;

    public static int GroupRank(WorkflowType? workflowType) => workflowType switch
    {
        WorkflowType.Qualitative => 0,

        WorkflowType.HplcMethodAssay or WorkflowType.Titration or WorkflowType.IcpMethodAssay
            or WorkflowType.UvSpectrophotometry
            // Retired assay types, still stored on older tests.
            or WorkflowType.HplcAssay or WorkflowType.ElementalAssay
            or WorkflowType.HplcMultiAnalyte or WorkflowType.StandardComparison => 1,

        WorkflowType.Measurement or WorkflowType.Gravimetric or WorkflowType.Dissolution
            or WorkflowType.Disintegration or WorkflowType.WeightVariation => 2,

        WorkflowType.CountTest => 3,
        WorkflowType.Observation => 4,

        _ => 5
    };

    public static IOrderedEnumerable<TestOrderSummaryDetailDto> Order(IEnumerable<TestOrderSummaryDetailDto> tests) =>
        tests
            .OrderBy(t => LaboratoryRank(t.SectionCode))
            .ThenBy(t => GroupRank(t.WorkflowType))
            .ThenBy(t => t.CoaDisplayOrder ?? int.MaxValue)
            .ThenBy(t => t.TestDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.TestCode, StringComparer.Ordinal);
}
