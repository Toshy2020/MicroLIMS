namespace MicroLIMS.Application.DTOs;

public class TestOrderSummaryDto
{
    public int TestOrderId { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;

    // Pathogen-Session-Aware Effective Workflow State
    public string WorkflowState { get; set; } = string.Empty;
    public string WorkflowStateDisplay { get; set; } = string.Empty;
    public string WorkflowStatus { get; set; } = "Pending";
    public bool UsesSharedTsb { get; set; }
    public bool IsWorkflowLocked { get; set; }
    public bool IsResultEntryAllowed { get; set; }
    public string? ResultLockReason { get; set; }

    // Number of SampleLocation rows under this TestOrder - 0 for
    // Product/RM/PM/Water TestOrders, which don't use SampleLocation at
    // all. EM/AfterCleaning use it to show "TAMC (3 rooms)" instead of
    // one chip per location.
    public int LocationCount { get; set; }

    // Read-only display of TestOrder.AssignedAnalystId, resolved to a
    // name here so the frontend never needs its own user-list call.
    public int? AssignedAnalystId { get; set; }
    public string? AssignedAnalystName { get; set; }
}

// Object transferred between API and Frontend - never exposes internal
// domain/persistence details (Frozen Principle #3).
public class SampleDto
{
    public int SampleId { get; set; }
    public int? ItemId { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty; // Item name, or Sampling Point/Department/Machine name
    public int? DepartmentId { get; set; }
    public int? MachineId { get; set; }
    public int? WaterDepartmentId { get; set; }
    public string? ProductionStage { get; set; }
    public string CauseOfTesting { get; set; } = string.Empty;
    public string? BatchNumber { get; set; }
    public string ControlNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PreparationStatus { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }

    public string? SampleQuantity { get; set; }
    public string SampledBy { get; set; } = string.Empty;
    public DateTime? MfgDate { get; set; }
    public DateTime? ExpDate { get; set; }
    public string? WaterSamplingPointCode { get; set; }
    public string? WaterSamplingPointLocation { get; set; }
    public string? StorageCondition { get; set; }
    public int? StorageTimeHours { get; set; }

    // True once any Incubation record exists for any of this sample's
    // TestOrders - the frontend uses this to lock the Batch/Control
    // Number correction affordance (see SampleCorrectionService).
    public bool IncubationStarted { get; set; }

    public int? AssignedAnalystId { get; set; }
    public string? AssignedAnalystName { get; set; }

    public List<TestOrderSummaryDto> AssignedTests { get; set; } = new();
}
