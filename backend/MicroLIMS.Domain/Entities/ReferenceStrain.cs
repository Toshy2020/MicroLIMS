using System.Text.Json.Serialization;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// A microorganism reference strain received from a supplier - tracks
// cryovial inventory, passage history, and ATCC traceability. Approval
// (Section Head/Unit Head) is a hard gate: an unapproved RS cannot be
// used to prepare Cryovials.
public class ReferenceStrain
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // RS + receiving seq + MM/YY
    public string OrganismName { get; set; } = string.Empty;
    public string AtccNumber { get; set; } = string.Empty;
    public int PassageNumber { get; set; } // hard-capped at 2
    public int NumberOfDiscs { get; set; }
    public int DiscsRemaining { get; set; } // decremented by ReferenceStrainService.PrepareCryovialsAsync
    public string ManufacturerName { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public string StorageCondition { get; set; } = string.Empty; // e.g. "Freezer -15 to -25"
    public string PhysicalCheckText { get; set; } = string.Empty; // Gram stain / microscopy description

    public ApprovalGateStatus ApprovalStatus { get; set; } = ApprovalGateStatus.PendingReview;
    public int ReceivedByUserId { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public List<IdentityConfirmationEntry> IdentityConfirmations { get; set; } = new();
    public List<Cryovial> Cryovials { get; set; } = new();
}

// One media row of the free identity-confirmation panel (e.g. TSA/MAR/TBX
// for E. coli) - no fixed minimum, analyst adds as many as judged necessary.
public class IdentityConfirmationEntry
{
    public int Id { get; set; }
    public int? ReferenceStrainId { get; set; }
    [JsonIgnore]
    public ReferenceStrain? ReferenceStrain { get; set; }
    public int? CryovialId { get; set; } // set when this panel belongs to a Cryovial prep instead
    [JsonIgnore]
    public Cryovial? Cryovial { get; set; }

    public int MediaId { get; set; } // must be GPT-released
    public Media? Media { get; set; }
    public int IncubatorEquipmentId { get; set; }
    public Equipment? IncubatorEquipment { get; set; }
    public DateTime IncubationStart { get; set; }
    public DateTime IncubationEnd { get; set; }
    public string ObservationText { get; set; } = string.Empty;
}

// One physical cryovial prepared from an approved reference strain.
// Approval is a hard gate: an unapproved cryovial cannot be used in GPT.
public class Cryovial
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // RS code + per-session vial sequence
    public int ReferenceStrainId { get; set; }
    [JsonIgnore]
    public ReferenceStrain? ReferenceStrain { get; set; }
    public int PassageNumber { get; set; } // RS passage + 1, hard-capped at 3
    public string ManufacturerName { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public int NumberOfVialsPrepared { get; set; }
    public string StorageCondition { get; set; } = string.Empty;
    public string PhysicalCheckText { get; set; } = string.Empty;

    public ApprovalGateStatus ApprovalStatus { get; set; } = ApprovalGateStatus.PendingReview;
    public DateTime? ThawedAt { get; set; } // tracked, not enforced
    public bool IsDestroyed { get; set; }

    public List<IdentityConfirmationEntry> IdentityConfirmations { get; set; } = new();
    public List<PassageEvent> PassageHistory { get; set; } = new();
}

// One subculture/passage event for a cryovial - passage number
// increments the generation count away from the ATCC original.
public class PassageEvent
{
    public int Id { get; set; }
    public int CryovialId { get; set; }
    [JsonIgnore]
    public Cryovial? Cryovial { get; set; }
    public int PassageNumber { get; set; }
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    public int PerformedByUserId { get; set; }
    public string? Notes { get; set; }
}
