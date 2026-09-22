using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Workflow types whose results are stored as TestAnalysis + ParameterResult
// (+ ResultReading). The approval gate, return to analyst, projection and
// summary use this instead of per-type branches; each new equation type is
// added here once.
public static class AnalysisWorkflows
{
    private static readonly HashSet<WorkflowType> Workflows = new()
    {
        WorkflowType.ElementalAssay,
        WorkflowType.Measurement,
        WorkflowType.Gravimetric,
        WorkflowType.Qualitative,
        WorkflowType.Dissolution,
        WorkflowType.Disintegration,
        WorkflowType.WeightVariation,
        WorkflowType.StandardComparison
    };

    public static bool UsesTestAnalysis(WorkflowType workflowType) => Workflows.Contains(workflowType);

    public static IReadOnlySet<WorkflowType> All => Workflows;

    public static string GetDisplayName(WorkflowType workflowType) => workflowType switch
    {
        WorkflowType.ElementalAssay => "elemental assay",
        WorkflowType.Measurement => "measurement",
        WorkflowType.Gravimetric => "gravimetric",
        WorkflowType.Qualitative => "qualitative",
        WorkflowType.Dissolution => "dissolution",
        WorkflowType.Disintegration => "disintegration",
        WorkflowType.WeightVariation => "weight variation",
        WorkflowType.StandardComparison => "standard comparison",
        _ => "test analysis"
    };
}
