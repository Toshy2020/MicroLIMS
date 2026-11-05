using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// ICP workspace: a run on an ICP instrument. One calibration record per run (Pending until the
// analyst confirms it with an e-signature), assigned samples with their element results, and
// instrument-report evidence. The tests define what is measured; the run carries no method.
public class IcpRun : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string Code { get; set; } = string.Empty; // "ICP RUN {nn}/{MM}{yyyy}"
    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }
    public int AnalystUserId { get; set; }
    public DateTime StartedAt { get; set; }
    public IcpRunStatus Status { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? CloseReason { get; set; } // required for Abandoned
    public IcpCalibration? Calibration { get; set; }
    public List<IcpRunSample> Samples { get; set; } = new();
    public List<IcpEvidence> Evidence { get; set; } = new();
}

// 1:1 with the run, created Pending at start. The analyst lists the elements calibrated and their r.
public class IcpCalibration
{
    public int Id { get; set; }
    public int IcpRunId { get; set; }
    public IcpRun? IcpRun { get; set; }
    public string Code { get; set; } = string.Empty; // "ICP CAL {nn}/{MM}{yyyy}"
    public IcpCalibrationStatus Status { get; set; }
    public int? CalibrationStandardMaterialId { get; set; }
    public Material? CalibrationStandardMaterial { get; set; }
    public int? ConfirmedByUserId { get; set; }
    public DateTime? ConfirmedAt { get; set; } // starts the validity clock
    public int? SignatureId { get; set; }
    public ElectronicSignature? Signature { get; set; }
    public List<IcpCalibrationElement> Elements { get; set; } = new();
}

public class IcpCalibrationElement
{
    public int Id { get; set; }
    public int IcpCalibrationId { get; set; }
    public IcpCalibration? IcpCalibration { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal? CorrelationR { get; set; }
    public bool Passed { get; set; }            // server-computed: r at or above the minimum
    public string? FailureReasons { get; set; }
}

public class IcpRunSample
{
    public int Id { get; set; }
    public int IcpRunId { get; set; }
    public IcpRun? IcpRun { get; set; }
    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public IcpRunSampleStatus Status { get; set; }
    public decimal? ActualWeightG { get; set; } // Act.Wt, assay mode
    public DateTime AssignedAt { get; set; }
    public int AssignedByUserId { get; set; }
    public string? RemovedReason { get; set; }
    public DateTime? RemovedAt { get; set; }
    public int? RemovedByUserId { get; set; }
    public List<IcpSampleConcentration> Concentrations { get; set; } = new();
}

// The instrument's reported concentration for one element of the sample.
public class IcpSampleConcentration
{
    public int Id { get; set; }
    public int IcpRunSampleId { get; set; }
    public IcpRunSample? IcpRunSample { get; set; }
    public int IcpTestElementId { get; set; }
    public decimal ResultPpm { get; set; }
}

// Same shape as HplcEvidence: a replacement supersedes the old row, nothing is overwritten.
public class IcpEvidence
{
    public int Id { get; set; }
    public int IcpRunId { get; set; }
    public IcpRun? IcpRun { get; set; }
    public int? IcpRunSampleId { get; set; }
    public IcpRunSample? IcpRunSample { get; set; }
    public IcpEvidenceContext Context { get; set; }
    public IcpEvidenceKind Kind { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }
    public int? SupersededByEvidenceId { get; set; }
    public IcpEvidence? SupersededByEvidence { get; set; }
    public string? SupersedeReason { get; set; }
}
