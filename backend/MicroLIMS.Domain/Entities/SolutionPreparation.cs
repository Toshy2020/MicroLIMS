using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// A preparation of a Mobile Phase, Diluent or Titrant against real Material
// stock lots (HPLC chain S4, spec 4). Started from a Solution master, then
// completed with a signature that in one save deducts stock, allocates a
// code (MP/DL/VS) and sets an expiry. Edited only while InProgress; every
// other status is a terminal record.
public class SolutionPreparation : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string? Code { get; set; }                     // set at completion
    public int SolutionMasterId { get; set; }
    public SolutionMaster? SolutionMaster { get; set; }
    public SolutionType Type { get; set; }                // copied from the master at start
    public string RecipeSnapshotJson { get; set; } = "{}"; // SolutionMasterResponse at start (D7)
    public int? HplcMethodId { get; set; }                // Mobile Phase only
    public HplcMethod? HplcMethod { get; set; }
    public SolutionPreparationStatus Status { get; set; }
    public int StartedByUserId { get; set; }
    public DateTime StartedAt { get; set; }
    public int? PreparedByUserId { get; set; }
    public DateTime? PreparedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public decimal? FinalVolumeMl { get; set; }
    public decimal? MeasuredPh { get; set; }
    public int? SignatureId { get; set; }
    public ElectronicSignature? Signature { get; set; }
    public List<SolutionPreparationComponent> Components { get; set; } = new();
    public List<SolutionPreparationStatusHistory> StatusHistory { get; set; } = new();
}
