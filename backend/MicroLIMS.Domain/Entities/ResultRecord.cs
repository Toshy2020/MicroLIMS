using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Flattened reporting projection - one row per (SourceTable, SourceId,
// Round), written by ResultProjectionService at result-entry time so
// the Reports module can query every test result (CountTestReading /
// PathogenObservation / SampleLocation) through one shape instead of
// joining three tables with three different projections. Derived data
// only - the source tables remain the system of record; never write
// here except through ResultProjectionService.
public class ResultRecord
{
    public int Id { get; set; }

    // ---- Source traceability - drills back to origin ----
    public int SampleId { get; set; }
    public Sample? Sample { get; set; }
    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public string SourceTable { get; set; } = string.Empty; // "CountTestReading" / "PathogenObservation" / "SampleLocation"
    public int SourceId { get; set; }

    // ---- Denormalized identity (snapshot at write time) ----
    public string ReferenceNumber { get; set; } = string.Empty;
    public SampleCategory Category { get; set; }
    public string SubjectName { get; set; } = string.Empty; // Item name, Water sampling point code, Room name, or Machine part name
    public string? SubjectDetail { get; set; } // e.g. room grade classification
    public string? BatchNumber { get; set; }
    public string? ControlNumber { get; set; }

    // ---- Test identity ----
    public string TestCode { get; set; } = string.Empty;
    public string TestDisplayName { get; set; } = string.Empty;

    // ---- Result ----
    public ResultKind ResultKind { get; set; }
    public decimal? NumericValue { get; set; } // the calculated numeric result; NULL for qualitative - the ONLY field trending reads
    public string ReportedValue { get; set; } = string.Empty; // display string: "18", "<1", "Detected", "Absent"
    public string? Unit { get; set; } // "CFU/g", "CFU/mL", "CFU/Plate"; NULL for qualitative
    public bool IsBelowDetectionLimit { get; set; } // true when reported as "<n"
    public decimal? DetectionLimit { get; set; } // the n in "<n" - trending plots at half this value per convention

    // ---- Limits, snapshotted at result entry - NOT read from current config ----
    public string? AlertLimit { get; set; }
    public string? ActionLimit { get; set; }
    public string? SpecLimit { get; set; }
    public ResultLevel ResultLevel { get; set; }

    // ---- Lifecycle ----
    public DateTime ResultEnteredAt { get; set; }
    public int ResultEnteredByUserId { get; set; }
    public string ResultEnteredByName { get; set; } = string.Empty; // snapshot
    public SampleStatus SampleStatus { get; set; }
    public int? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // Incremented for each retest round (SampleApprovalService's
    // RetestRetainedSample decision) so an earlier round's rows stay
    // queryable as history instead of being overwritten by the new one.
    public int Round { get; set; } = 1;

    public DateTime UpdatedAt { get; set; }
}
