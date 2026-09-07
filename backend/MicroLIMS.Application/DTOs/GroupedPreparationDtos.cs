namespace MicroLIMS.Application.DTOs;

// Grouped Test Preparation - the confirm-only path executed across many
// samples at once. Samples group by the Item preparation configuration
// they would confirm, because that configuration IS what the analyst
// signs for: one signature statement can only cover one set of steps.

public record GroupedPreparationSampleDto(
    int SampleId,
    string SampleReference,
    string DisplayName,
    string? BatchNumber,
    string Category,
    int? AssignedAnalystId,
    string? AssignedAnalystName
);

public record ExcludedPreparationSampleDto(
    int SampleId,
    string SampleReference,
    string DisplayName,
    string Reason
);

public record GroupedPreparationDto(
    string GroupKey,
    int ConfigurationId,
    int ItemId,
    string ItemName,
    string ApprovalStatus,
    decimal Amount,
    string Technique,
    decimal? FiltrationVolume,
    decimal? WashingVolume,
    string Diluent,
    string Neutralizer,
    int SampleCount,
    List<GroupedPreparationSampleDto> Samples
);

public record GroupedPreparationResponse(
    List<GroupedPreparationDto> Groups,
    int ExcludedCount,
    List<ExcludedPreparationSampleDto> Excluded
);

// ConfigurationId pins the batch to the exact protocol the analyst was
// shown: if the Section Head edits the item's configuration between the
// panel loading and the signature, the affected samples are skipped
// rather than silently signed against steps nobody read.
public record BatchConfirmPreparationRequest(
    List<int> SampleIds,
    int ConfigurationId,
    string Password
);

public record BatchPreparationSuccessItem(
    int SampleId,
    string SampleReference,
    int PreparationId,
    string Message
);

public record BatchPreparationSkippedItem(
    int SampleId,
    string SampleReference,
    string Reason
);

public record BatchConfirmPreparationResponse(
    int TotalRequested,
    int SucceededCount,
    int SkippedCount,
    List<BatchPreparationSuccessItem> Succeeded,
    List<BatchPreparationSkippedItem> Skipped
);
