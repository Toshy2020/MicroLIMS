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

// What the API returns for a run. Never the entity itself: its navigation to
// the performing User would serialize that user's PasswordHash.
public record SystemSuitabilityRunView(
    int Id, string Code, bool Passed, string? FailureReasons,
    int TestDefinitionId, string? TestCode, string? TestName, string? MethodAbbreviation,
    int SectionId, string? SectionName,
    int EquipmentId, string? EquipmentCode, string? EquipmentName,
    int? ChromatographyColumnId, string? ColumnCode, string? ColumnName,
    int ReferenceStandardMaterialId, string? ReferenceStandardName, string? ReferenceStandardBatch,
    decimal StandardPurityPercent, decimal StandardWeightMg, decimal StandardDilution, decimal StandardMeanArea,
    decimal? RsdPercent, decimal? Resolution, decimal? TailingFactor, decimal? TheoreticalPlates,
    int PerformedByUserId, string? PerformedByName, DateTime PerformedAt, string? Comment,
    List<SystemSuitabilityRunAnalyteView>? Analytes = null,
    decimal? TheoreticalWeightMg = null,
    decimal? MoisturePercent = null,
    decimal? StandardWeighInDeviationPercent = null,
    bool StandardWeighInOutOfWindow = false,
    string? WeighInJustification = null,
    decimal? ComputedRsdPercent = null)
{
    public static SystemSuitabilityRunView From(SystemSuitabilityRun r) => new(
        r.Id, r.Code, r.Passed, r.FailureReasons,
        r.TestDefinitionId, r.TestDefinition?.Code, r.TestDefinition?.DisplayName, r.TestDefinition?.MethodAbbreviation,
        r.SectionId, r.Section?.Name,
        r.EquipmentId, r.Equipment?.Code, r.Equipment?.Name,
        r.ChromatographyColumnId, r.ChromatographyColumn?.Code, r.ChromatographyColumn?.Name,
        r.ReferenceStandardMaterialId, r.ReferenceStandardMaterial?.MaterialName, r.ReferenceStandardMaterial?.BatchNumber,
        r.StandardPurityPercent, r.StandardWeightMg, r.StandardDilution, r.StandardMeanArea,
        r.RsdPercent, r.Resolution, r.TailingFactor, r.TheoreticalPlates,
        r.PerformedByUserId, r.PerformedByUser?.FullName ?? r.Signature?.UserFullNameSnapshot, r.PerformedAt, r.Comment,
        r.Analytes != null && r.Analytes.Count > 0 ? r.Analytes.Select(SystemSuitabilityRunAnalyteView.From).ToList() : null,
        r.TheoreticalWeightMg,
        r.MoisturePercent,
        r.StandardWeighInDeviationPercent,
        r.StandardWeighInOutOfWindow,
        r.WeighInJustification,
        r.ComputedRsdPercent);
}
