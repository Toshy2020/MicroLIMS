using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// ICP workspace (I2): a run on an ICP instrument, snapshotting the method at start.
// One calibration record per run (Pending until confirmed), CCV readings, assigned
// samples with replicates, and instrument-report evidence.
public class IcpRun : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string Code { get; set; } = string.Empty; // "{ABBR} RUN {nn}/{MM}{yyyy}"
    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }
    public int IcpMethodId { get; set; }
    public IcpMethod? IcpMethod { get; set; }
    public string MethodSnapshotJson { get; set; } = "{}"; // IcpMethodResponse at start
    public int AnalystUserId { get; set; }
    public DateTime StartedAt { get; set; }
    public IcpRunStatus Status { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? CloseReason { get; set; } // required for Abandoned
    public IcpCalibration? Calibration { get; set; }
    public List<IcpCcvReading> CcvReadings { get; set; } = new();
    public List<IcpRunSample> Samples { get; set; } = new();
    public List<IcpEvidence> Evidence { get; set; } = new();
}

// 1:1 with the run, created Pending at start.
public class IcpCalibration
{
    public int Id { get; set; }
    public int IcpRunId { get; set; }
    public IcpRun? IcpRun { get; set; }
    public string Code { get; set; } = string.Empty; // "{ABBR} CAL {nn}/{MM}{yyyy}"
    public IcpCalibrationStatus Status { get; set; }
    public int? CalibrationStandardMaterialId { get; set; }
    public Material? CalibrationStandardMaterial { get; set; }
    public int? IcvStandardMaterialId { get; set; }
    public Material? IcvStandardMaterial { get; set; }
    public int? ConfirmedByUserId { get; set; }
    public DateTime? ConfirmedAt { get; set; } // starts the MaxCalibrationAgeHours clock
    public int? SignatureId { get; set; }
    public ElectronicSignature? Signature { get; set; }
    public List<IcpCalibrationElement> Elements { get; set; } = new();
}

// One per method element, created at start.
public class IcpCalibrationElement
{
    public int Id { get; set; }
    public int IcpCalibrationId { get; set; }
    public IcpCalibration? IcpCalibration { get; set; }
    public int IcpMethodElementId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal? CorrelationR { get; set; }
    public decimal? BlankMgPerL { get; set; }
    public decimal? IcvMeasuredMgPerL { get; set; }
    public decimal? IcvRecoveryPercent { get; set; } // server-computed
    public bool Passed { get; set; }
    public string? FailureReasons { get; set; }
}

public class IcpCcvReading
{
    public int Id { get; set; }
    public int IcpRunId { get; set; }
    public IcpRun? IcpRun { get; set; }
    public int IcpMethodElementId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal MeasuredMgPerL { get; set; }
    public decimal RecoveryPercent { get; set; } // server-computed
    public bool Passed { get; set; }
    public int EnteredByUserId { get; set; }
    public DateTime EnteredAt { get; set; }
}

public class IcpRunSample
{
    public int Id { get; set; }
    public int IcpRunId { get; set; }
    public IcpRun? IcpRun { get; set; }
    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public IcpRunSampleStatus Status { get; set; }
    public IcpAmountUnit AmountUnit { get; set; } // g or mL, for sample amount and unit amount
    public decimal? UnitAmount { get; set; } // average unit weight (g) or dose (mL); mineral mg/unit only
    public DateTime AssignedAt { get; set; }
    public int AssignedByUserId { get; set; }
    public string? RemovedReason { get; set; }
    public DateTime? RemovedAt { get; set; }
    public int? RemovedByUserId { get; set; }
    public List<IcpSampleReplicate> Replicates { get; set; } = new();
}

public class IcpSampleReplicate
{
    public int Id { get; set; }
    public int IcpRunSampleId { get; set; }
    public IcpRunSample? IcpRunSample { get; set; }
    public int ReplicateNo { get; set; }
    public decimal SampleAmount { get; set; } // g or mL (AmountUnit)
    public decimal VolumeMl { get; set; }
    public decimal DilutionFactor { get; set; }
    public List<IcpReplicateConcentration> Concentrations { get; set; } = new();
}

public class IcpReplicateConcentration
{
    public int Id { get; set; }
    public int IcpSampleReplicateId { get; set; }
    public IcpSampleReplicate? IcpSampleReplicate { get; set; }
    public int IcpMethodElementId { get; set; }
    public decimal SolutionMgPerL { get; set; } // "Conc. in Calib. Units, mg/L"
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
