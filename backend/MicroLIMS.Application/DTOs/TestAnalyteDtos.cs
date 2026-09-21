using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public record CreateTestAnalyteRequest(
    string Element,
    decimal WavelengthNm,
    AnalyteView? View = null,
    decimal? LoqMgPerL = null,
    int DisplayOrder = 0,
    decimal? SstMaxRsdPercent = null,
    decimal? SstMinResolution = null,
    decimal? SstMaxTailingFactor = null,
    decimal? SstMinTheoreticalPlates = null);

public record UpdateTestAnalyteRequest(
    string? Element = null,
    decimal? WavelengthNm = null,
    AnalyteView? View = null,
    decimal? LoqMgPerL = null,
    int? DisplayOrder = null,
    bool? IsActive = null,
    decimal? SstMaxRsdPercent = null,
    decimal? SstMinResolution = null,
    decimal? SstMaxTailingFactor = null,
    decimal? SstMinTheoreticalPlates = null);

public record TestAnalyteDto(
    int Id,
    int TestDefinitionId,
    string Element,
    decimal WavelengthNm,
    AnalyteView? View,
    decimal? LoqMgPerL,
    int DisplayOrder,
    bool IsActive,
    decimal? SstMaxRsdPercent = null,
    decimal? SstMinResolution = null,
    decimal? SstMaxTailingFactor = null,
    decimal? SstMinTheoreticalPlates = null)
{
    public static TestAnalyteDto From(TestAnalyte a) => new(
        a.Id,
        a.TestDefinitionId,
        a.Element,
        a.WavelengthNm,
        a.View,
        a.LoqMgPerL,
        a.DisplayOrder,
        a.IsActive,
        a.SstMaxRsdPercent,
        a.SstMinResolution,
        a.SstMaxTailingFactor,
        a.SstMinTheoreticalPlates);
}
