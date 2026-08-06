using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// The Test Master - the single canonical list of TestCode values used
// everywhere a test is assigned to something (Item.AssignedTests/
// Specifications, WaterSamplingPoint.AssignedTestCodes,
// RoomTestConfiguration, MachinePartConfiguration). Those places still
// store a plain TestCode string - this table exists so the UI can offer
// a picker sourced from one place instead of everyone free-typing a
// code by hand, which is exactly how a typo could silently misroute a
// sample.
//
// WorkflowType + Steps (TestWorkflowStep) are the configurable
// replacement for what used to be hardcoded per-test-code chains in
// PathogenWorkflowEngine/CountTestWorkflowEngine - TestWorkflowEngine
// reads these to know what to do for ANY test code, never comparing
// against a literal code itself.
public class TestDefinition
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // e.g. "TAMC", "PATHOGEN_SALMONELLA"
    public string DisplayName { get; set; } = string.Empty; // e.g. "Total Aerobic Microbial Count", "Pathogen - Salmonella"

    public WorkflowType WorkflowType { get; set; } = WorkflowType.Observation;
    public List<TestWorkflowStep> Steps { get; set; } = new();

    // Frozen tests are hidden from TestCodePicker/TestCodePickerMulti's
    // dropdown for new selections, but stay in the table (and keep
    // rendering correctly on anything that already references their
    // Code) so existing assignments aren't disrupted.
    public bool IsActive { get; set; } = true;
}
