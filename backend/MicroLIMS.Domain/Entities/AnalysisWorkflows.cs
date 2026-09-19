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
        WorkflowType.ElementalAssay
    };

    public static bool UsesTestAnalysis(WorkflowType workflowType) => Workflows.Contains(workflowType);

    public static IReadOnlySet<WorkflowType> All => Workflows;

    public static string GetDisplayName(WorkflowType workflowType) => workflowType switch
    {
        WorkflowType.ElementalAssay => "elemental assay",
        _ => "test analysis"
    };
}
