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

    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }

    public WorkflowType WorkflowType { get; set; } = WorkflowType.Observation;
    public List<TestWorkflowStep> Steps { get; set; } = new();

    // Frozen tests are hidden from TestCodePicker/TestCodePickerMulti's
    // dropdown for new selections, but stay in the table (and keep
    // rendering correctly on anything that already references their
    // Code) so existing assignments aren't disrupted.
    public bool IsActive { get; set; } = true;

    // Finished Product / HPLC fields
    public EquationType EquationType { get; set; } = EquationType.None;
    public bool RequiresSystemSuitability { get; set; }
    public string? MethodAbbreviation { get; set; }

    // Acceptance criteria, all nullable (null = not checked)
    public decimal? SstMaxRsdPercent { get; set; }
    public decimal? SstMinResolution { get; set; }
    public decimal? SstMaxTailingFactor { get; set; }
    public decimal? SstMinTheoreticalPlates { get; set; }

    // Finished Product / ICP-OES Calibration Curve criteria
    public CalibrationEntryMode? CalibrationEntryMode { get; set; }
    public decimal? CalMinCorrelation { get; set; }
    public CorrelationType? CalCorrelationType { get; set; }
    public int? CalMinStandards { get; set; }
    public decimal? CalCheckRecoveryLowPercent { get; set; }
    public decimal? CalCheckRecoveryHighPercent { get; set; }
    public decimal? CalBlankMax { get; set; }
    public decimal? CalIsRecoveryLowPercent { get; set; }
    public decimal? CalIsRecoveryHighPercent { get; set; }
    public bool? CalRequireBlank { get; set; }
    public bool? CalRequireIcv { get; set; }
    public bool? CalRequireCcv { get; set; }
    public bool? CalRequireInternalStandard { get; set; }
    public ReportedConcentrationBasis? ReportedConcentrationBasis { get; set; }
    public int? CalMaxRunAgeHours { get; set; } = 24;

    // Finished Product / Numeric Measurement criteria
    public int? ReplicateCount { get; set; }
    public MeasurementEvaluationBasis? EvaluationBasis { get; set; }

    public List<TestAnalyte> Analytes { get; set; } = new();
}

