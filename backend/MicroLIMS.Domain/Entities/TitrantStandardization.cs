using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// A standardization (replicate titration) of a titrant preparation against a
// primary-standard lot or another standardized VS (HPLC chain S5, spec 4).
// Re-standardization adds a new record; earlier records are kept. The
// current factor is the latest Passed record still valid - see
// TitrantStandardizationService.ComputeCurrentFactor. A failed record is
// saved for traceability but never becomes current.
public class TitrantStandardization
{
    public int Id { get; set; }
    public int SolutionPreparationId { get; set; }
    public SolutionPreparation? SolutionPreparation { get; set; }
    public StandardizationMode Mode { get; set; }
    public string SettingsSnapshotJson { get; set; } = "{}"; // strength, unit, E, range, max RSD, replicates, validity at the time
    public decimal MeanFactor { get; set; }
    public decimal? RsdPercent { get; set; }
    public bool Passed { get; set; }
    public string? FailureReasons { get; set; }
    public int StandardizedByUserId { get; set; }
    public DateTime StandardizedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public decimal? TemperatureC { get; set; } // lab temperature when standardized (needed for temperature-corrected titrations)
    public int SignatureId { get; set; }
    public ElectronicSignature? Signature { get; set; }
    public List<TitrantStandardizationReplicate> Replicates { get; set; } = new();
}
