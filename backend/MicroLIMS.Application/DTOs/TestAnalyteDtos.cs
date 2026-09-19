using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public record CreateTestAnalyteRequest(
    string Element,
    decimal WavelengthNm,
    AnalyteView View,
    decimal LoqMgPerL,
    int DisplayOrder = 0);

public record UpdateTestAnalyteRequest(
    string? Element = null,
    decimal? WavelengthNm = null,
    AnalyteView? View = null,
    decimal? LoqMgPerL = null,
    int? DisplayOrder = null,
    bool? IsActive = null);

public record TestAnalyteDto(
    int Id,
    int TestDefinitionId,
    string Element,
    decimal WavelengthNm,
    AnalyteView View,
    decimal LoqMgPerL,
    int DisplayOrder,
    bool IsActive)
{
    public static TestAnalyteDto From(TestAnalyte a) => new(
        a.Id,
        a.TestDefinitionId,
        a.Element,
        a.WavelengthNm,
        a.View,
        a.LoqMgPerL,
        a.DisplayOrder,
        a.IsActive);
}
