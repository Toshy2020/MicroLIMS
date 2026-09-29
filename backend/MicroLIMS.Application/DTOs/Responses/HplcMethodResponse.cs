using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.Responses;

public record HplcMethodMobilePhaseResponse(
    int Id, string Channel, int SolutionMasterId, string SolutionMasterName, decimal? RatioPercent);

public record HplcMethodGradientStepResponse(
    int Id, decimal TimeMin, decimal PercentA, decimal PercentB, decimal PercentC, decimal PercentD);

public record HplcMethodAnalyteResponse(
    int Id, int DisplayOrder, string Name, decimal WavelengthNm,
    int StandardEntryId, string StandardEntryCode,
    decimal TheoreticalWeightStdMg, decimal TheoreticalWeightTestMg, int StandardInjections,
    decimal? SstMaxRsdPercent, decimal? SstMinResolution, decimal? SstMaxTailingFactor,
    decimal? SstMinTheoreticalPlates, decimal? SstMinRetentionFactor, decimal? SstMinSignalToNoise,
    decimal? SstMinPeakToValley);

public class HplcMethodResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public int SectionId { get; init; }
    public string? SectionName { get; init; }
    public string Name { get; init; } = null!;
    public string Abbreviation { get; init; } = null!;
    public DateTime EffectiveDate { get; init; }
    public bool IsActive { get; init; }

    public string ColumnDesignation { get; init; } = null!;
    public decimal ColumnLengthMm { get; init; }
    public decimal ColumnInternalDiameterMm { get; init; }
    public decimal ParticleSizeUm { get; init; }
    public string? ColumnBrand { get; init; }
    public string? ColumnPartNumber { get; init; }
    public decimal ColumnTemperatureC { get; init; }

    public ElutionMode ElutionMode { get; init; }
    public decimal? EquilibrationMin { get; init; }
    public decimal FlowRateMlPerMin { get; init; }

    public HplcDetectorType DetectorType { get; init; }
    public decimal InjectionVolumeUl { get; init; }
    public decimal RunTimeMin { get; init; }

    public int DiluentSolutionId { get; init; }
    public string? DiluentSolutionName { get; init; }

    public List<HplcMethodMobilePhaseResponse> MobilePhases { get; init; } = new();
    public List<HplcMethodGradientStepResponse> GradientSteps { get; init; } = new();
    public List<HplcMethodAnalyteResponse> Analytes { get; init; } = new();

    public int CreatedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public int LastModifiedByUserId { get; init; }
    public DateTime LastModifiedAt { get; init; }

    public static HplcMethodResponse From(HplcMethod m) => new()
    {
        Id = m.Id,
        Version = m.Version,
        SectionId = m.SectionId,
        SectionName = m.Section?.Name,
        Name = m.Name,
        Abbreviation = m.Abbreviation,
        EffectiveDate = m.EffectiveDate,
        IsActive = m.IsActive,
        ColumnDesignation = m.ColumnDesignation,
        ColumnLengthMm = m.ColumnLengthMm,
        ColumnInternalDiameterMm = m.ColumnInternalDiameterMm,
        ParticleSizeUm = m.ParticleSizeUm,
        ColumnBrand = m.ColumnBrand,
        ColumnPartNumber = m.ColumnPartNumber,
        ColumnTemperatureC = m.ColumnTemperatureC,
        ElutionMode = m.ElutionMode,
        EquilibrationMin = m.EquilibrationMin,
        FlowRateMlPerMin = m.FlowRateMlPerMin,
        DetectorType = m.DetectorType,
        InjectionVolumeUl = m.InjectionVolumeUl,
        RunTimeMin = m.RunTimeMin,
        DiluentSolutionId = m.DiluentSolutionId,
        DiluentSolutionName = m.DiluentSolution?.Name,
        MobilePhases = m.MobilePhases
            .OrderBy(p => p.Channel)
            .Select(p => new HplcMethodMobilePhaseResponse(
                p.Id, p.Channel, p.SolutionMasterId, p.SolutionMaster?.Name ?? string.Empty, p.RatioPercent))
            .ToList(),
        GradientSteps = m.GradientSteps
            .OrderBy(s => s.TimeMin)
            .Select(s => new HplcMethodGradientStepResponse(s.Id, s.TimeMin, s.PercentA, s.PercentB, s.PercentC, s.PercentD))
            .ToList(),
        Analytes = m.Analytes
            .OrderBy(a => a.DisplayOrder)
            .Select(a => new HplcMethodAnalyteResponse(
                a.Id, a.DisplayOrder, a.Name, a.WavelengthNm,
                a.StandardEntryId, a.StandardEntry?.Code ?? string.Empty,
                a.TheoreticalWeightStdMg, a.TheoreticalWeightTestMg, a.StandardInjections,
                a.SstMaxRsdPercent, a.SstMinResolution, a.SstMaxTailingFactor,
                a.SstMinTheoreticalPlates, a.SstMinRetentionFactor, a.SstMinSignalToNoise,
                a.SstMinPeakToValley))
            .ToList(),
        CreatedByUserId = m.CreatedByUserId,
        CreatedAt = m.CreatedAt,
        LastModifiedByUserId = m.LastModifiedByUserId,
        LastModifiedAt = m.LastModifiedAt,
    };
}
