namespace MicroLIMS.Application.DTOs;

// Everything that happened to one prepared Media lot: how it was made,
// how it was challenged, and who signed for its release. Same role for a
// lot that SampleSummaryDto plays for a Sample.
public class MediaSummaryDto
{
    public int MediaId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public string MediaClass { get; set; } = string.Empty;

    // Source material (the dehydrated media consumed to make this lot).
    public string MaterialName { get; set; } = string.Empty;
    public string ManufacturerName { get; set; } = string.Empty;
    public string ManufacturerLot { get; set; } = string.Empty;

    // Preparation record - the autoclave/cycle/pH grid.
    public decimal TotalWeight { get; set; }
    public string TotalVolume { get; set; } = string.Empty;
    public string? AutoclaveName { get; set; }
    public string AutoclaveProgram { get; set; } = string.Empty;
    public string LoadType { get; set; } = string.Empty;
    public decimal Temperature { get; set; }
    public int CycleTime { get; set; }
    public int CycleNumber { get; set; }
    public decimal Ph { get; set; }
    public DateTime ExpiryDate { get; set; }
    public DateTime PreparedAt { get; set; }
    public string PreparedByName { get; set; } = string.Empty;

    // Release state.
    public string Status { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public bool IsReleasedForUse { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public MediaEvaluationSummaryDto? Evaluation { get; set; }
    public List<SampleWorkflowEventDto> Timeline { get; set; } = new();
    public List<SignatureDto> Signatures { get; set; } = new();
}

public class MediaEvaluationSummaryDto
{
    public string EvaluationType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Outcome { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedByName { get; set; }
    public List<MediaChallengeSummaryDto> Challenges { get; set; } = new();
}

public class MediaChallengeSummaryDto
{
    public string OrganismName { get; set; } = string.Empty;
    public string? ChallengeRole { get; set; }
    public string? CryovialCode { get; set; }
    public string InitialInoculum { get; set; } = string.Empty;

    public string? IncubatorName { get; set; }
    public string? Temperature { get; set; }
    public string? Duration { get; set; }
    public DateTime? IncubationStartedAt { get; set; }
    public DateTime? ExpectedReadingAt { get; set; }

    // Which of these carry meaning depends on the evaluation type - a
    // GrowthPromotion challenge has counts, an Inhibition one has
    // growth observed, and so on.
    public decimal? OldMediaCount { get; set; }
    public decimal? NewMediaCount { get; set; }
    public decimal? RecoveryPercent { get; set; }
    public bool? GrowthObserved { get; set; }
    public string? ObservedDescription { get; set; }
    public string? ExpectedDescription { get; set; }
    public bool? IsTurbid { get; set; }

    public string? Outcome { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? ReadByName { get; set; }
}
