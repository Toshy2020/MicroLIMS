using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One status transition of a SolutionPreparation. ChangedByUserId is null
// for the automatic Prepared -> Expired transition written by
// SolutionPreparationExpiryWorker.
public class SolutionPreparationStatusHistory
{
    public int Id { get; set; }
    public int SolutionPreparationId { get; set; }
    public SolutionPreparation? SolutionPreparation { get; set; }
    public SolutionPreparationStatus? FromStatus { get; set; }
    public SolutionPreparationStatus ToStatus { get; set; }
    public int? ChangedByUserId { get; set; }               // null = automatic expiry
    public DateTime ChangedAt { get; set; }
    public string? Reason { get; set; }
}
