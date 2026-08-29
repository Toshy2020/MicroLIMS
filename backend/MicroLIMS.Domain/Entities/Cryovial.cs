using System.Text.Json.Serialization;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One media row of the free identity-confirmation panel (e.g. TSA/MAR/TBX
// for E. coli) - no fixed minimum, analyst adds as many as judged necessary.
// Always belongs to a Cryovial - CryovialId is the sole owner now that
// ReferenceStrain (which used to also own these directly) is gone.
public class IdentityConfirmationEntry
{
    public int Id { get; set; }
    public int CryovialId { get; set; }
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

// One batch of physical cryovials prepared directly from a
// LyophilizedMicroorganism Material. Approval is a hard gate: an
// unapproved batch cannot be used in GPT. OrganismId comes from the
// Material's own OrganismId at preparation time; OrganismNameSnapshot is
// a copy of Organism.ScientificName taken at the same time so the record
// stays readable even if the Organism master row is later corrected.
public class Cryovial
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // {Material name/code}/{seq:D2}/{yy}

    public int MaterialId { get; set; }
    public Material? Material { get; set; }
    public int OrganismId { get; set; }
    public Organism? Organism { get; set; }
    public string OrganismNameSnapshot { get; set; } = string.Empty;

    public string ManufacturerName { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public int NumberOfVialsPrepared { get; set; }
    public int VialsRemaining { get; set; } // set equal to NumberOfVialsPrepared at preparation time
    public string StorageCondition { get; set; } = string.Empty;
    public bool PhysicalCheckConfirmed { get; set; }
    public string PhysicalCheckText { get; set; } = string.Empty;
    public DateTime PreparedAt { get; set; } = DateTime.UtcNow; // scopes the per-MaterialId yearly Code sequence

    // Who prepared this batch - required for segregation of duties at the
    // approval gate (you cannot approve a batch you prepared).
    public int PreparedByUserId { get; set; }

    public ApprovalGateStatus ApprovalStatus { get; set; } = ApprovalGateStatus.PendingReview;
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public bool IsDestroyed { get; set; }

    public List<IdentityConfirmationEntry> IdentityConfirmations { get; set; } = new();
    public List<ThawEvent> ThawHistory { get; set; } = new();
}

// One vial thawed out of a batch - GPT itself does not consume vials
// (a single thawed vial is used across multiple media-type GPT runs);
// this only tracks the lab's own vial-level usage history.
public class ThawEvent
{
    public int Id { get; set; }
    public int CryovialId { get; set; }
    [JsonIgnore]
    public Cryovial? Cryovial { get; set; }
    public DateTime ThawedAt { get; set; } = DateTime.UtcNow;
    public int ThawedByUserId { get; set; }
    public string? Notes { get; set; }
}
