namespace MicroLIMS.Application.DTOs;

// What the suitability run report shows beyond the run itself.
public record SuitabilityRunLinkedTestDto(
    int TestOrderId,
    int SampleId,
    string SampleReferenceNumber,
    string? ItemName,
    string? BatchNumber,
    string TestCode,
    string? ReportedResult,
    string? ResultStatus,
    DateTime? ResultEnteredAt);

public record SuitabilityRunReportDetailsDto(
    // Acceptance criteria as currently set on the Test Master (null = not checked).
    decimal? SstMaxRsdPercent,
    decimal? SstMinResolution,
    decimal? SstMaxTailingFactor,
    decimal? SstMinTheoreticalPlates,
    string? EquipmentVendor,
    string? CdsSoftware,
    string? ColumnSerialNumber,
    SignatureDto? Signature,
    List<SuitabilityRunLinkedTestDto> LinkedTests);
