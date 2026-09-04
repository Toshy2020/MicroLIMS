namespace MicroLIMS.Application.DTOs;

public record ActionableTestOrderSummaryDto(
    int TestOrderId,
    int SampleId,
    string SampleReference,
    string DisplayName,
    string TestCode,
    string? BatchNumber,
    int? AssignedAnalystId,
    string? AssignedAnalystName
);

public record ActionableGroupDto(
    string GroupKey,
    string ActionType,
    string StepType,
    string StepName,
    string TestCode,
    int SampleCount,
    int TestOrderCount,
    decimal TempMin,
    decimal TempMax,
    int IncubationMinHours,
    int IncubationMaxHours,
    List<int> PermittedMaterialIds,
    string PermittedMaterialNames,
    string Urgency,
    List<ActionableTestOrderSummaryDto> TestOrders
);

public record ActionableGroupsResponse(
    List<ActionableGroupDto> Groups
);

public record BatchSelectMediaRequest(
    List<int> TestOrderIds,
    string StepName,
    int MediaLotId,
    int IncubatorEquipmentId,
    DateTime? IncubationStartUtc = null
);

public record BatchActionSuccessItem(
    int TestOrderId,
    string SampleReference,
    int IncubationId,
    DateTime ExpectedReadingAt,
    string Message
);

public record BatchActionSkippedItem(
    int TestOrderId,
    string SampleReference,
    string Reason
);

public record BatchSelectMediaResponse(
    int TotalRequested,
    int SucceededCount,
    int SkippedCount,
    List<BatchActionSuccessItem> Succeeded,
    List<BatchActionSkippedItem> Skipped
);
