namespace MicroLIMS.Domain.Entities;

// One challenge-organism result within a media lot's Growth Promotion
// Test. Shape varies by the media's Class (see GptWorkflowEngine):
//  - GeneralAgar: RS/Cryovial + counts + Recovery% (auto-computed)
//  - GeneralBroth: TurbidResult only
//  - SelectiveAgar/Broth: Inhibition (free text + Pass/Fail) and/or
//    Indication (free text + Pass/Fail against an expected description)
public class GptChallengeResult
{
    public int Id { get; set; }
    public int MediaId { get; set; }
    public Media? Media { get; set; }

    public string Panel { get; set; } = string.Empty; // "General" or "Inhibition" or "Indication"
    public string OrganismName { get; set; } = string.Empty;
    public int? CryovialId { get; set; }
    public Cryovial? Cryovial { get; set; }
    public string? Atcc { get; set; }
    public string? InitialInoculum { get; set; } // recorded only, no pass/fail role

    // General Agar fields
    public int? OldMediaResult { get; set; }
    public int? NewMediaResult { get; set; }
    public decimal? RecoveryPercent { get; set; } // NewMediaResult / OldMediaResult * 100, auto-computed
    public bool NegativeControlGrowth { get; set; } // true = Growth = auto-fail the whole run

    // General Broth field
    public string? TurbidResult { get; set; } // "Turbid" (pass) or "Clear" (fail)

    // Selective media fields
    public string? ObservationText { get; set; } // free text (Inhibition or Indication observation)
    public string? ExpectedDescription { get; set; } // master-data expected description, for Indication

    public bool Passed { get; set; }
    public int RecordedByUserId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
