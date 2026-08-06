namespace MicroLIMS.Application.DTOs;

// Everything that happened to one Cryovial batch: how it was prepared,
// the identity-confirmation panel that qualified it, who signed for it,
// and every vial thawed from it since.
public class CryovialSummaryDto
{
    public int CryovialId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string OrganismName { get; set; } = string.Empty;
    public string ManufacturerName { get; set; } = string.Empty;

    // Source material (the lyophilized microorganism consumed).
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialBatchNumber { get; set; } = string.Empty;

    public DateTime ExpiryDate { get; set; }
    public int NumberOfVialsPrepared { get; set; }
    public int VialsRemaining { get; set; }
    public string StorageCondition { get; set; } = string.Empty;
    public string PhysicalCheckText { get; set; } = string.Empty;
    public DateTime PreparedAt { get; set; }
    public string PreparedByName { get; set; } = string.Empty;

    public string ApprovalStatus { get; set; } = string.Empty;
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public bool IsDestroyed { get; set; }

    public List<IdentityConfirmationSummaryDto> IdentityConfirmations { get; set; } = new();
    public List<ThawEventSummaryDto> ThawHistory { get; set; } = new();
    public List<SampleWorkflowEventDto> Timeline { get; set; } = new();
    public List<SignatureDto> Signatures { get; set; } = new();
}

public class IdentityConfirmationSummaryDto
{
    public string? MediaLotNumber { get; set; }
    public string? IncubatorName { get; set; }
    public DateTime IncubationStart { get; set; }
    public DateTime IncubationEnd { get; set; }
    public string ObservationText { get; set; } = string.Empty;
}

public class ThawEventSummaryDto
{
    public DateTime ThawedAt { get; set; }
    public string ThawedByName { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
