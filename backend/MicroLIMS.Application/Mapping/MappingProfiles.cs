using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Mapping;

// Pure mapping functions between Domain entities and DTOs. Kept separate
// from services so the mapping rules are visible and testable on their
// own (Frozen Principle #3 - Backend owns logic, frontend just displays).
// Sample -> SampleDto mapping now lives in TestingWorkspaceService.ToDto,
// since it needs Category-dependent lookups (Item vs WaterSamplingPoint
// vs Department vs Machine) that don't belong in a static mapper.
public static class ResultMappingProfile
{
    public static ResultDto ToDto(Result result) => new()
    {
        ResultId = result.Id,
        TestOrderId = result.TestOrderId,
        RawValue = result.RawValue,
        InterpretedValue = result.InterpretedValue,
        EnteredAt = result.EnteredAt
    };
}
