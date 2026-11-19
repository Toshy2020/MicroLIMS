namespace MicroLIMS.Application.DTOs;

// Shared receiving request for Product / Raw Material / Packaging Material.
public record ReceiveItemBasedSampleRequest(
    int ItemId,
    int CauseOfTestingId,
    string SampleQuantity,
    string SampledBy,
    string BatchNumber,
    string ControlNumber,
    DateTime? MfgDate,
    DateTime? ExpDate,
    string? ProductionStage,
    List<int>? TargetSectionIds,
    string? Grade = null);

public record ReceiveWaterRequest(
    int WaterDepartmentId,
    int CauseOfTestingId,
    string SampleQuantity,
    string SampledBy,
    string ControlNumber,
    DateTime? SampledAt = null,
    int? LabSectionId = null);

public record ReceiveEMRequest(
    int DepartmentId,
    int CauseOfTestingId,
    string SampledBy,
    string ControlNumber,
    DateTime? SampledAt = null);

public record ReceiveAfterCleaningRequest(
    int MachineId,
    int CauseOfTestingId,
    string SampledBy,
    string ControlNumber,
    string? PreviousProductName = null,
    string? PreviousProductBatchNumber = null,
    DateTime? SampledAt = null,
    int? LabSectionId = null,
    int? PreviousProductItemId = null,
    List<string>? Techniques = null);

public sealed record BatchReceiveRequest(
    string Category,
    IReadOnlyList<ReceiveItemBasedSampleRequest>? Items,
    IReadOnlyList<ReceiveWaterRequest>? Water,
    IReadOnlyList<ReceiveEMRequest>? Em,
    IReadOnlyList<ReceiveAfterCleaningRequest>? AfterCleaning);

public sealed record BatchReceivedSample(int Row, int SampleId, string ReferenceNumber);

public sealed record BatchReceiveResult(IReadOnlyList<BatchReceivedSample> Samples);
