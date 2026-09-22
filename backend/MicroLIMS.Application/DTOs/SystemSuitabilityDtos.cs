using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.DTOs;

public record SystemSuitabilityStandardResponseDto(
    int Id,
    int Index,
    decimal Response);

public record CreateSystemSuitabilityRunAnalyteRequest(
    int TestAnalyteId,
    int ReferenceStandardMaterialId,
    decimal StandardWeightMg,
    decimal StandardDilution,
    decimal StandardMeanArea,
    decimal? RsdPercent = null,
    decimal? Resolution = null,
    decimal? TailingFactor = null,
    decimal? TheoreticalPlates = null,
    decimal? TheoreticalWeightMg = null,
    decimal? MoisturePercent = null,
    string? WeighInJustification = null,
    List<decimal>? Responses = null,
    decimal? BlankTitreMl = null);

public record CreateSystemSuitabilityRunRequest(
    int TestDefinitionId,
    int EquipmentId,
    int? ChromatographyColumnId,
    int ReferenceStandardMaterialId = 0,
    decimal StandardWeightMg = 0,
    decimal StandardDilution = 0,
    decimal StandardMeanArea = 0,
    decimal? RsdPercent = null,
    decimal? Resolution = null,
    decimal? TailingFactor = null,
    decimal? TheoreticalPlates = null,
    string Password = "",
    string? Comment = null,
    List<CreateSystemSuitabilityRunAnalyteRequest>? Analytes = null,
    decimal? TheoreticalWeightMg = null,
    decimal? MoisturePercent = null,
    string? WeighInJustification = null,
    List<decimal>? Responses = null);

public record SystemSuitabilityRunAnalyteView(
    int Id,
    int SystemSuitabilityRunId,
    int TestAnalyteId,
    string AnalyteName,
    decimal WavelengthNm,
    int ReferenceStandardMaterialId,
    string? ReferenceStandardName,
    string? ReferenceStandardBatch,
    decimal StandardPurityPercent,
    decimal StandardWeightMg,
    decimal StandardDilution,
    decimal StandardMeanArea,
    decimal? RsdPercent,
    decimal? Resolution,
    decimal? TailingFactor,
    decimal? TheoreticalPlates,
    bool Passed,
    string? FailureReasons,
    decimal? TheoreticalWeightMg = null,
    decimal? MoisturePercent = null,
    decimal? StandardWeighInDeviationPercent = null,
    bool StandardWeighInOutOfWindow = false,
    string? WeighInJustification = null,
    decimal? ComputedRsdPercent = null,
    List<SystemSuitabilityStandardResponseDto>? Responses = null,
    decimal? BlankTitreMl = null)
{
    public static SystemSuitabilityRunAnalyteView From(SystemSuitabilityRunAnalyte a) => new(
        a.Id,
        a.SystemSuitabilityRunId,
        a.TestAnalyteId,
        a.AnalyteName,
        a.WavelengthNm,
        a.ReferenceStandardMaterialId,
        a.ReferenceStandardMaterial?.MaterialName,
        a.ReferenceStandardMaterial?.BatchNumber,
        a.StandardPurityPercent,
        a.StandardWeightMg,
        a.StandardDilution,
        a.StandardMeanArea,
        a.RsdPercent,
        a.Resolution,
        a.TailingFactor,
        a.TheoreticalPlates,
        a.Passed,
        a.FailureReasons,
        a.TheoreticalWeightMg,
        a.MoisturePercent,
        a.StandardWeighInDeviationPercent,
        a.StandardWeighInOutOfWindow,
        a.WeighInJustification,
        a.ComputedRsdPercent,
        a.Responses?.OrderBy(r => r.Index).Select(r => new SystemSuitabilityStandardResponseDto(r.Id, r.Index, r.Response)).ToList(),
        a.BlankTitreMl);
}

public record SuitabilityRunReportAnalyteDto(
    int TestAnalyteId,
    string AnalyteName,
    decimal WavelengthNm,
    string? ReferenceStandardName,
    string? ReferenceStandardBatch,
    decimal StandardPurityPercent,
    decimal StandardWeightMg,
    decimal StandardDilution,
    decimal StandardMeanArea,
    decimal? RsdPercent,
    decimal? Resolution,
    decimal? TailingFactor,
    decimal? TheoreticalPlates,
    decimal? SstMaxRsdPercent,
    decimal? SstMinResolution,
    decimal? SstMaxTailingFactor,
    decimal? SstMinTheoreticalPlates,
    bool Passed,
    string? FailureReasons,
    decimal? TheoreticalWeightMg = null,
    decimal? MoisturePercent = null,
    decimal? StandardWeighInDeviationPercent = null,
    bool StandardWeighInOutOfWindow = false,
    string? WeighInJustification = null,
    decimal? ComputedRsdPercent = null,
    List<SystemSuitabilityStandardResponseDto>? Responses = null,
    decimal? BlankTitreMl = null);

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
