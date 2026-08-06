namespace MicroLIMS.Application.DTOs;

// Everything the floating Sample Summary page (reviewer/Section Head)
// needs in one call: receiving data, preparation, every TestOrder's full
// step/result history (including superseded retest rounds), the
// sample-level lifecycle timeline, and its signature trail.
public class SampleSummaryDto
{
    public int SampleId { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ProductionStage { get; set; }
    public string CauseOfTesting { get; set; } = string.Empty;
    public string? BatchNumber { get; set; }
    public string ControlNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ReceivedByName { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public string SampledBy { get; set; } = string.Empty;
    public string? SampleQuantity { get; set; }
    public DateTime? MfgDate { get; set; }
    public DateTime? ExpDate { get; set; }
    public string? WaterSamplingPointCode { get; set; }
    public string? WaterSamplingPointLocation { get; set; }
    public string? StorageCondition { get; set; }
    public int? StorageTimeHours { get; set; }

    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovalDecision { get; set; }

    public SamplePreparationSummaryDto? Preparation { get; set; }
    public List<TestOrderSummaryDetailDto> TestOrders { get; set; } = new();
    public List<SampleWorkflowEventDto> Timeline { get; set; } = new();
    public List<SignatureDto> Signatures { get; set; } = new();
}

public class SamplePreparationSummaryDto
{
    public decimal Amount { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Technique { get; set; } = string.Empty;
    public decimal? FiltrationVolume { get; set; }
    public decimal? WashingVolume { get; set; }
    public string NeutralizerName { get; set; } = string.Empty;
    public string PreparedByName { get; set; } = string.Empty;
    public DateTime PreparedAt { get; set; }
}

public class TestOrderSummaryDetailDto
{
    public int TestOrderId { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string TestDisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
    public bool IsSuperseded { get; set; }
    public List<IncubationDetailDto> Incubations { get; set; } = new();
    public List<ResultDetailDto> Results { get; set; } = new();
    public List<CountTestReadingDetailDto> CountTestReadings { get; set; } = new();
    public List<PathogenObservationDetailDto> PathogenObservations { get; set; } = new();
    public List<WorkflowHistoryDetailDto> WorkflowHistory { get; set; } = new();

    // EM/After Cleaning batch results - one row per location, populated
    // instead of CountTestReadings for these TestOrders.
    public List<SampleLocationDetailDto> Locations { get; set; } = new();
}

public class SampleLocationDetailDto
{
    public string LocationName { get; set; } = string.Empty;
    public string? GradeClassification { get; set; }
    public string? AlertLimit { get; set; }
    public string? ActionLimit { get; set; }
    public string? SpecLimit { get; set; }
    public decimal? CFUResult { get; set; }
    public decimal? CalculatedResult { get; set; }
    public string? ReportedResult { get; set; }
    public string? Status { get; set; }
    public string? EnteredByName { get; set; }
    public DateTime? EnteredAt { get; set; }
}

public class IncubationDetailDto
{
    public string StepName { get; set; } = string.Empty;
    public string? MediaLotNumber { get; set; }
    public string? MediaMaterialName { get; set; }
    public string? IncubatorName { get; set; }
    public string? Temperature { get; set; }
    public string? Duration { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? ExpectedReadingAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Outcome { get; set; }
}

public class ResultDetailDto
{
    public string RawValue { get; set; } = string.Empty;
    public string? InterpretedValue { get; set; }
    public string Type { get; set; } = string.Empty;
    public string EnteredByName { get; set; } = string.Empty;
    public DateTime EnteredAt { get; set; }
}

public class CountTestReadingDetailDto
{
    public string? StepName { get; set; }
    public string PlateReadings { get; set; } = string.Empty;
    public decimal DilutionFactor { get; set; }
    public decimal Average { get; set; }
    public decimal CalculatedResult { get; set; }
    public string ReportedResult { get; set; } = string.Empty;
    public string? AlertLimit { get; set; }
    public string? ActionLimit { get; set; }
    public string? SpecLimit { get; set; }
    public string Status { get; set; } = string.Empty;
    public string EnteredByName { get; set; } = string.Empty;
    public DateTime EnteredAt { get; set; }
}

public class PathogenObservationDetailDto
{
    public string StepName { get; set; } = string.Empty;
    public int StepOrder { get; set; }
    public bool GrowthObserved { get; set; }
    public string ObservedByName { get; set; } = string.Empty;
    public DateTime ObservedAt { get; set; }
}

public class WorkflowHistoryDetailDto
{
    public string FromStep { get; set; } = string.Empty;
    public string ToStep { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string PerformedByName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class SampleWorkflowEventDto
{
    public string EventType { get; set; } = string.Empty;
    public string PerformedByName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Comment { get; set; }
    public string? Decision { get; set; }
}
