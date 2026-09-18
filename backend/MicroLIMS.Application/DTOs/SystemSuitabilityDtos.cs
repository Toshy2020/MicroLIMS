namespace MicroLIMS.Application.DTOs;

public record CreateSystemSuitabilityRunRequest(
    int TestDefinitionId,
    int EquipmentId,
    int ChromatographyColumnId,
    int ReferenceStandardMaterialId,
    decimal StandardWeightMg,
    decimal StandardDilution,
    decimal StandardMeanArea,
    decimal? RsdPercent,
    decimal? Resolution,
    decimal? TailingFactor,
    decimal? TheoreticalPlates,
    string Password,
    string? Comment = null);

public record SystemSuitabilityRunFilter(
    int? TestDefinitionId = null,
    string? TestCode = null,
    bool? Passed = null,
    DateTime? Date = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null);

public record BulkLinkOrdersToSuitabilityRunRequest(List<int>? TestOrderIds = null);

public record EquationTypeDto(
    string Code,
    string Name,
    string FormulaText,
    IReadOnlyList<string> RequiredInputs);
