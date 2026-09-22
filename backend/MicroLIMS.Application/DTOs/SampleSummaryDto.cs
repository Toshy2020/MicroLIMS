using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

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

    public int? AssignedAnalystId { get; set; }
    public string? AssignedAnalystName { get; set; }

    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovalDecision { get; set; }
    public string? CertificateRemarks { get; set; }

    // After Cleaning Previous Product Traceability (historical free text)
    public string? PreviousProductName { get; set; }
    public string? PreviousProductBatchNumber { get; set; }

    public SamplePreparationSummaryDto? Preparation { get; set; }
    public List<TestOrderSummaryDetailDto> TestOrders { get; set; } = new();
    public List<SampleWorkflowEventDto> Timeline { get; set; } = new();
    public List<SignatureDto> Signatures { get; set; } = new();

    // Every laboratory section with tests on this sample and its own
    // review/approval state. TestOrders only lists the tests of sections the
    // viewer can see (CanView); AllSectionsVisible says whether this summary
    // covers the whole sample (a combined Certificate of Analysis) or only
    // some of its sections.
    public List<SampleSectionSummaryDto> Sections { get; set; } = new();
    public bool AllSectionsVisible { get; set; } = true;
}

public class SampleSectionSummaryDto
{
    public int SectionId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovalDecision { get; set; }
    public string? CertificateRemarks { get; set; }
}

public class SamplePreparationSummaryDto
{
    public decimal Amount { get; set; }
    public string Technique { get; set; } = string.Empty;
    public decimal? FiltrationVolume { get; set; }
    public decimal? WashingVolume { get; set; }
    public string Diluent { get; set; } = string.Empty;
    public string Neutralizer { get; set; } = string.Empty;
    public string PreparedByName { get; set; } = string.Empty;
    public DateTime PreparedAt { get; set; }
}

public class TestOrderSummaryDetailDto
{
    public int TestOrderId { get; set; }
    public int SectionId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string TestCode { get; set; } = string.Empty;
    public string TestDisplayName { get; set; } = string.Empty;
    // Set only when this row was pulled in from a different Sample - the
    // reference number of a retest sample whose results were pulled
    // through onto an OOS origin/intermediate sample whose own TestOrders
    // were fully superseded (see SampleSummaryService.ResolveEffectiveTestOrdersAsync).
    // Null for every ordinarily-owned row.
    public string? SourceSampleReferenceNumber { get; set; }
    // Specification pass/fail text for this TestOrder's TestCode, resolved
    // from Test Master's per-Item Specifications (Item+TestCode keyed) -
    // covers both quantitative and qualitative tests generically. Null
    // when nobody has configured one for this Item/TestCode pair yet.
    public string? SpecificationText { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
    public string WorkflowState { get; set; } = string.Empty;
    public string WorkflowStateDisplay { get; set; } = string.Empty;
    public string WorkflowStatus { get; set; } = "Pending";
    public bool UsesSharedTsb { get; set; }
    public bool IsWorkflowLocked { get; set; }
    public bool IsResultEntryAllowed { get; set; }
    public string? ResultLockReason { get; set; }
    public bool IsSuperseded { get; set; }
    public List<IncubationDetailDto> Incubations { get; set; } = new();
    public List<ResultDetailDto> Results { get; set; } = new();
    public List<CountTestReadingDetailDto> CountTestReadings { get; set; } = new();
    // Elemental Assay only - the active (not returned) entry and results, null otherwise.
    public ElementalAssayDetailDto? ElementalAssay { get; set; }
    // Shared Result Foundation - generic test analysis, null if not a TestAnalysis workflow
    public AnalysisDetailDto? Analysis { get; set; }
    public List<PathogenObservationDetailDto> PathogenObservations { get; set; } = new();
    public List<BiochemicalResultDetailDto> BiochemicalResults { get; set; } = new();
    public List<WorkflowHistoryDetailDto> WorkflowHistory { get; set; } = new();

    // EM/After Cleaning batch results - one row per location, populated
    // instead of CountTestReadings for these TestOrders.
    public List<SampleLocationDetailDto> Locations { get; set; } = new();
}

public class SampleLocationDetailDto
{
    // The underlying physical location's stable identity (Room/MachinePart/
    // WaterSamplingPoint id, or the raw SampleLocation id as a last
    // resort) - NOT the display name. LocationName is free text and not
    // guaranteed unique (e.g. multiple water sampling points can share
    // the same "WTU" label); a frontend matrix that groups rows across
    // test orders must key on this instead, or it silently merges
    // distinct locations the same way PathogenSessionService's
    // display-name grouping once did.
    public string LocationKey { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string? GradeClassification { get; set; }
    public string? AlertLimit { get; set; }
    public string? ActionLimit { get; set; }
    public string? SpecLimit { get; set; }
    public decimal? CFUResult { get; set; }
    public decimal? CalculatedResult { get; set; }
    public string? ReportedResult { get; set; }
    public string? Status { get; set; }

    // Populated at result-entry time (TestWorkflowEngine.DeriveBatchLocationUnit)
    // - null for qualitative (pathogen Detected/Absent) locations. Never
    // assume "CFU/Plate" - EM/After Cleaning mix CFU/plate/4 hours (passive
    // air), CFU/25 cm2 (surface air / swab), and CFU/mL (rinse / water).
    public string? Unit { get; set; }
    public string? EnteredByName { get; set; }
    public DateTime? EnteredAt { get; set; }
}

public class IncubationDetailDto
{
    public string StepName { get; set; } = string.Empty;
    public int StageNumber { get; set; } = 1;
    public string? MediaLotNumber { get; set; }
    public string? MediaMaterialName { get; set; }
    public string? IncubatorName { get; set; }
    public string? Temperature { get; set; }
    public string? Duration { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? ExpectedReadingAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Outcome { get; set; }
    public string? StartedByName { get; set; }
    public DateTime? TransferredAt { get; set; }
    public string? TransferredByName { get; set; }
    public string? CompletedByName { get; set; }

    // Set only on the StageNumber == 2 row of a two-stage transfer step:
    // true if the same analyst started both stages, false if different
    // analysts did, null everywhere else (including every
    // RequiresIncubationTransfer = false step's single row) - explicit
    // rather than making the frontend compare two user names itself.
    public bool? SameAnalystBothStages { get; set; }
}

public class ResultDetailDto
{
    public string RawValue { get; set; } = string.Empty;
    public string? InterpretedValue { get; set; }
    public string Type { get; set; } = string.Empty;
    public string EnteredByName { get; set; } = string.Empty;
    public DateTime EnteredAt { get; set; }
}

public class ElementalAssayDetailDto
{
    public SampleMatrix SampleMatrix { get; set; }
    public decimal UnitAmount { get; set; }
    public string UnitAmountUnit { get; set; } = string.Empty;
    public DateTime AnalysedAt { get; set; }
    public string? EnteredByName { get; set; }
    public DateTime EnteredAt { get; set; }
    public List<ElementalAssayElementDetailDto> Elements { get; set; } = new();
}

public class ElementalAssayElementDetailDto
{
    public string ParameterName { get; set; } = string.Empty;
    public string Element { get; set; } = string.Empty;
    public string RunCode { get; set; } = string.Empty;
    public bool RunAnalytePassed { get; set; }
    public decimal ReportedPpm { get; set; }
    public bool OverRange { get; set; }
    public bool BelowLoq { get; set; }
    public decimal? MgPerUnit { get; set; }
    public decimal? ResultClaim { get; set; }
    public decimal? PercentLabelClaim { get; set; }
    public string ReportedDisplay { get; set; } = string.Empty;
    public string? SpecLimit { get; set; }
    public string? Unit { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class AnalysisDetailDto
{
    public int Id { get; set; }
    public int TestOrderId { get; set; }
    public WorkflowType AnalysisType { get; set; }
    public int? EquipmentId { get; set; }
    public string? EquipmentCode { get; set; }
    public string? EquipmentName { get; set; }
    public DateTime AnalysedAt { get; set; }
    public decimal? UnitAmount { get; set; }
    public SampleMatrix? SampleMatrix { get; set; }
    public string? ConditionsJson { get; set; }
    public string? ValidityRecordType { get; set; }
    public int? ValidityRecordId { get; set; }
    public string? EnteredByName { get; set; }
    public DateTime EnteredAt { get; set; }
    public string? Comment { get; set; }
    public List<ParameterResultDetailDto> ParameterResults { get; set; } = new();
}

public class ParameterResultDetailDto
{
    public int Id { get; set; }
    public int SpecificationId { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public decimal? ReportedValue { get; set; }
    public string ReportedDisplay { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string? SpecLimit { get; set; }
    public ResultBasis? ResultBasis { get; set; }
    public string ComparisonStatus { get; set; } = string.Empty;
    public bool OverRange { get; set; }
    public bool BelowLoq { get; set; }
    public int? ValidityRecordItemId { get; set; }
    public string? CalculationJson { get; set; }
    public int? StageReached { get; set; }
    public List<ResultReadingDetailDto> Readings { get; set; } = new();
}

public class ResultReadingDetailDto
{
    public int Id { get; set; }
    public ReadingKind Kind { get; set; }
    public int Index { get; set; }
    public int? Stage { get; set; }
    public decimal? TimePointMinutes { get; set; }
    public decimal? Value1 { get; set; }
    public decimal? Value2 { get; set; }
    public decimal? Value3 { get; set; }
    public string? Text { get; set; }
    public decimal? ComputedValue { get; set; }
    public bool? Passed { get; set; }
}

public class CountTestReadingDetailDto
{
    public string? StepName { get; set; }
    public string PlateReadings { get; set; } = string.Empty;
    public decimal DilutionFactor { get; set; }
    public decimal? Average { get; set; }
    public decimal? CalculatedResult { get; set; }
    public string ReportedResult { get; set; } = string.Empty;
    public string? AlertLimit { get; set; }
    public string? ActionLimit { get; set; }
    public string? SpecLimit { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasNonNumericReading { get; set; }
    public string? NonNumericValue { get; set; }
    public bool RequiresReview { get; set; }
    public string EnteredByName { get; set; } = string.Empty;
    public DateTime EnteredAt { get; set; }
}

public class BiochemicalResultDetailDto
{
    public string StepName { get; set; } = string.Empty;

    public string BiochemicalResultText { get; set; } = string.Empty;

    // The analyst's explicit interpretation - never inferred from the free
    // text. Null only for pre-this-field historical rows the backfill
    // migration couldn't confidently assign (see the migration for how it
    // was actually backfilled).
    public bool? OrganismDetected { get; set; }

    public string SubmittedByName { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}

public class PathogenObservationDetailDto
{
    public string StepName { get; set; } = string.Empty;
    public int StepOrder { get; set; }

    // The GrowthObservation enum's own name (NoGrowth /
    // GrowthNonConforming / GrowthConforming). Deliberately not a
    // boolean: collapsing it to "growth yes/no" merges the two negative
    // states with the positive one, and a GrowthNonConforming chain (the
    // target organism is absent, something else grew) then reports as
    // Detected on the GMP summary and its archived PDF.
    public string Observation { get; set; } = string.Empty;

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
