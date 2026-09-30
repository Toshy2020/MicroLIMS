using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// HPLC Workspace (HPLC chain S6, spec 5): a run on an instrument, snapshotting
// the method at start (D7). One System Suitability record per run gates sample
// assignment (D12) - passed before any sample can be assigned. Samples are
// assigned only after SST passes, entered per replicate, and submitted through
// HplcMethodAssayRecorder into the existing review/approval flow (Part B).
public class HplcRun : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string Code { get; set; } = string.Empty;
    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }
    public int ChromatographyColumnId { get; set; }
    public ChromatographyColumn? ChromatographyColumn { get; set; }
    public int HplcMethodId { get; set; }
    public HplcMethod? HplcMethod { get; set; }
    public string MethodSnapshotJson { get; set; } = "{}"; // HplcMethodResponse at start (D7)
    public int AnalystUserId { get; set; }
    public DateTime StartedAt { get; set; }
    public HplcRunStatus Status { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? CloseReason { get; set; } // required for Abandoned
    public List<HplcRunMobilePhase> MobilePhases { get; set; } = new();
    public HplcSstRecord? Sst { get; set; }
    public List<HplcRunSample> Samples { get; set; } = new();
    public List<HplcEvidence> Evidence { get; set; } = new();
}

public class HplcRunMobilePhase
{
    public int Id { get; set; }
    public int HplcRunId { get; set; }
    public HplcRun? HplcRun { get; set; }
    public string Channel { get; set; } = string.Empty;
    public int SolutionPreparationId { get; set; }
    public SolutionPreparation? SolutionPreparation { get; set; }
}

// One per run, created Pending at StartRunAsync with one HplcSstAnalyte row
// per method analyte. ConfirmSstAsync is final: Passed or Failed, never
// re-evaluated.
public class HplcSstRecord
{
    public int Id { get; set; }
    public int HplcRunId { get; set; }
    public HplcRun? HplcRun { get; set; }
    public string Code { get; set; } = string.Empty;
    public HplcSstStatus Status { get; set; }
    public string? FailureReasons { get; set; }
    public int? ConfirmedByUserId { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public int? SignatureId { get; set; }
    public ElectronicSignature? Signature { get; set; }
    public List<HplcSstAnalyte> Analytes { get; set; } = new();
}

public class HplcSstAnalyte
{
    public int Id { get; set; }
    public int HplcSstRecordId { get; set; }
    public HplcSstRecord? HplcSstRecord { get; set; }
    public int HplcMethodAnalyteId { get; set; }
    public string AnalyteName { get; set; } = string.Empty;
    public int? StandardMaterialId { get; set; } // usable ReferenceStandard lot (Material)
    public Material? StandardMaterial { get; set; }
    public decimal? StandardPurityPercent { get; set; }   // snapshot from the lot
    public decimal? StandardMoisturePercent { get; set; } // snapshot from the lot
    public decimal? StandardWeightMg { get; set; }
    public decimal? MeanResponse { get; set; }
    public decimal? ComputedRsdPercent { get; set; }
    public decimal? ReportedRsdPercent { get; set; }
    public decimal? Resolution { get; set; }
    public decimal? TailingFactor { get; set; }
    public decimal? TheoreticalPlates { get; set; }
    public decimal? RetentionFactor { get; set; }
    public decimal? SignalToNoise { get; set; }
    public decimal? PeakToValley { get; set; }
    public bool Passed { get; set; }
    public string? FailureReasons { get; set; }
    public List<HplcSstInjection> Injections { get; set; } = new();
}

public class HplcSstInjection
{
    public int Id { get; set; }
    public int HplcSstAnalyteId { get; set; }
    public HplcSstAnalyte? HplcSstAnalyte { get; set; }
    public int InjectionNo { get; set; }
    public decimal Response { get; set; }
}

// A sample assigned to a run after its SST passed. Removed = Status Removed +
// reason, never deleted (Global Constraints).
public class HplcRunSample
{
    public int Id { get; set; }
    public int HplcRunId { get; set; }
    public HplcRun? HplcRun { get; set; }
    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public HplcRunSampleStatus Status { get; set; }
    public DateTime AssignedAt { get; set; }
    public int AssignedByUserId { get; set; }
    public string? RemovedReason { get; set; }
    public DateTime? RemovedAt { get; set; }
    public int? RemovedByUserId { get; set; }
    public List<HplcSampleReplicate> Replicates { get; set; } = new();
}

public class HplcSampleReplicate
{
    public int Id { get; set; }
    public int HplcRunSampleId { get; set; }
    public HplcRunSample? HplcRunSample { get; set; }
    public int ReplicateNo { get; set; }
    public decimal ActualWeightMg { get; set; }
    public List<HplcReplicateResponse> Responses { get; set; } = new();
}

public class HplcReplicateResponse
{
    public int Id { get; set; }
    public int HplcSampleReplicateId { get; set; }
    public HplcSampleReplicate? HplcSampleReplicate { get; set; }
    public int HplcMethodAnalyteId { get; set; }
    public decimal Response { get; set; }
}

// Instrument report evidence (standard or sample chromatogram/report).
// Nothing is replaced in place - a replacement supersedes the old row via
// SupersededByEvidenceId + SupersedeReason (Global Constraints).
public class HplcEvidence
{
    public int Id { get; set; }
    public int HplcRunId { get; set; }
    public HplcRun? HplcRun { get; set; }
    public int? HplcRunSampleId { get; set; }
    public HplcRunSample? HplcRunSample { get; set; }
    public HplcEvidenceContext Context { get; set; }
    public HplcEvidenceKind Kind { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }
    public int? SupersededByEvidenceId { get; set; }
    public HplcEvidence? SupersededByEvidence { get; set; }
    public string? SupersedeReason { get; set; }
}
