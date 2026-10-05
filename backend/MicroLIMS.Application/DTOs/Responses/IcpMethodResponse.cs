using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.Responses;

public record IcpMethodElementResponse(int Id, int DisplayOrder, string Symbol, decimal WavelengthNm, AnalyteView View, decimal ConversionFactor);

public class IcpMethodResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public int SectionId { get; init; }
    public string? SectionName { get; init; }
    public string Name { get; init; } = null!;
    public string Abbreviation { get; init; } = null!;
    public DateTime EffectiveDate { get; init; }
    public bool IsActive { get; init; }
    public IcpMethodMode Mode { get; init; }

    public string StandardLevelsMgPerL { get; init; } = null!;
    public int CalibrationStandardEntryId { get; init; }
    public string? CalibrationStandardEntryCode { get; init; }
    public decimal MinCorrelation { get; init; }
    public int MaxCalibrationAgeHours { get; init; }

    public bool RequireBlank { get; init; }
    public decimal? BlankMaxMgPerL { get; init; }
    public bool RequireIcv { get; init; }
    public int? IcvStandardEntryId { get; init; }
    public string? IcvStandardEntryCode { get; init; }
    public decimal? IcvNominalMgPerL { get; init; }
    public decimal? IcvRecoveryLowPercent { get; init; }
    public decimal? IcvRecoveryHighPercent { get; init; }
    public bool RequireCcv { get; init; }
    public decimal? CcvNominalMgPerL { get; init; }
    public decimal? CcvRecoveryLowPercent { get; init; }
    public decimal? CcvRecoveryHighPercent { get; init; }

    public decimal SampleVolumeMl { get; init; }
    public decimal DilutionFactor { get; init; }

    public List<IcpMethodElementResponse> Elements { get; init; } = new();

    public int CreatedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public int LastModifiedByUserId { get; init; }
    public DateTime LastModifiedAt { get; init; }

    public static IcpMethodResponse From(IcpMethod m) => new()
    {
        Id = m.Id,
        Version = m.Version,
        SectionId = m.SectionId,
        SectionName = m.Section?.Name,
        Name = m.Name,
        Abbreviation = m.Abbreviation,
        EffectiveDate = m.EffectiveDate,
        IsActive = m.IsActive,
        Mode = m.Mode,
        StandardLevelsMgPerL = m.StandardLevelsMgPerL,
        CalibrationStandardEntryId = m.CalibrationStandardEntryId,
        CalibrationStandardEntryCode = m.CalibrationStandardEntry?.Code,
        MinCorrelation = m.MinCorrelation,
        MaxCalibrationAgeHours = m.MaxCalibrationAgeHours,
        RequireBlank = m.RequireBlank,
        BlankMaxMgPerL = m.BlankMaxMgPerL,
        RequireIcv = m.RequireIcv,
        IcvStandardEntryId = m.IcvStandardEntryId,
        IcvStandardEntryCode = m.IcvStandardEntry?.Code,
        IcvNominalMgPerL = m.IcvNominalMgPerL,
        IcvRecoveryLowPercent = m.IcvRecoveryLowPercent,
        IcvRecoveryHighPercent = m.IcvRecoveryHighPercent,
        RequireCcv = m.RequireCcv,
        CcvNominalMgPerL = m.CcvNominalMgPerL,
        CcvRecoveryLowPercent = m.CcvRecoveryLowPercent,
        CcvRecoveryHighPercent = m.CcvRecoveryHighPercent,
        SampleVolumeMl = m.SampleVolumeMl,
        DilutionFactor = m.DilutionFactor,
        Elements = m.Elements
            .OrderBy(e => e.DisplayOrder)
            .Select(e => new IcpMethodElementResponse(e.Id, e.DisplayOrder, e.Symbol, e.WavelengthNm, e.View, e.ConversionFactor))
            .ToList(),
        CreatedByUserId = m.CreatedByUserId,
        CreatedAt = m.CreatedAt,
        LastModifiedByUserId = m.LastModifiedByUserId,
        LastModifiedAt = m.LastModifiedAt,
    };
}
