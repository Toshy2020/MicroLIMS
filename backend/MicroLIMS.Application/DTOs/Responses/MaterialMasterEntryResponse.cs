using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.Responses;

public class MaterialMasterEntryResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public int SectionId { get; init; }
    public string? SectionName { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public MaterialMasterCategory Category { get; init; }
    public string? Grade { get; init; }
    public string? Source { get; init; }
    public MaterialUnit BaseUnit { get; init; }
    public bool IsActive { get; init; }

    public string? WorkingConcentration { get; init; }
    public string? Solvent { get; init; }
    public decimal? TransitionRangeFrom { get; init; }
    public decimal? TransitionRangeTo { get; init; }
    public string? ColourChange { get; init; }
    public string? IndicatorUse { get; init; }

    public int CreatedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public int LastModifiedByUserId { get; init; }
    public DateTime LastModifiedAt { get; init; }

    public static MaterialMasterEntryResponse From(MaterialMasterEntry e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        SectionId = e.SectionId,
        SectionName = e.Section?.Name,
        Code = e.Code,
        Name = e.Name,
        Category = e.Category,
        Grade = e.Grade,
        Source = e.Source,
        BaseUnit = e.BaseUnit,
        IsActive = e.IsActive,
        WorkingConcentration = e.WorkingConcentration,
        Solvent = e.Solvent,
        TransitionRangeFrom = e.TransitionRangeFrom,
        TransitionRangeTo = e.TransitionRangeTo,
        ColourChange = e.ColourChange,
        IndicatorUse = e.IndicatorUse,
        CreatedByUserId = e.CreatedByUserId,
        CreatedAt = e.CreatedAt,
        LastModifiedByUserId = e.LastModifiedByUserId,
        LastModifiedAt = e.LastModifiedAt,
    };
}
