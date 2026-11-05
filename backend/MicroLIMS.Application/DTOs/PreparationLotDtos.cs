using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

// Test Preparation lot picking: what the analyst may pick for the diluent and
// the neutralizer of a sample's Item, and where a given lot was used.

public record PreparationLotOptionDto(
    int Id,
    string Code,
    DateTime? ExpiresAt,
    bool Usable,
    string? Reason
);

// LotRequired is false when nothing is picked: the neutralizer is contained
// in the medium, or the configuration is legacy text with no source yet.
public record PreparationLotGroupDto(
    PreparationMaterialSource? Source,
    string Name,
    bool LotRequired,
    List<PreparationLotOptionDto> Options
);

public record PreparationLotOptionsDto(
    int ConfigurationId,
    bool NeedsMaterialMapping,
    PreparationLotGroupDto Diluent,
    PreparationLotGroupDto Neutralizer
);

public record PreparationWhereUsedDto(
    int SampleId,
    string SampleNumber,
    string ItemName,
    string Role,
    DateTime PreparedAt,
    string? PreparedByName
);
