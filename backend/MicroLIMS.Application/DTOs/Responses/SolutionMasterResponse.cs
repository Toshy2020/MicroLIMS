using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.Responses;

public record SolutionComponentResponse(
    int Id, int Order, int MaterialMasterEntryId, string EntryCode, string EntryName,
    decimal Quantity, SolutionComponentUnit Unit);

public class SolutionMasterResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public int SectionId { get; init; }
    public string? SectionName { get; init; }
    public string Name { get; init; } = null!;
    public SolutionType Type { get; init; }
    public int ShelfLifeValue { get; init; }
    public ShelfLifeUnit ShelfLifeUnit { get; init; }
    public string StorageCondition { get; init; } = null!;
    public decimal FinalVolumeMl { get; init; }
    public decimal? PhTarget { get; init; }
    public decimal? PhTolerance { get; init; }
    public int? PhAdjustingEntryId { get; init; }
    public string? PhAdjustingEntryCode { get; init; }
    public string Instructions { get; init; } = null!;
    public bool IsActive { get; init; }
    public List<SolutionComponentResponse> Components { get; init; } = new();

    public decimal? NominalStrength { get; init; }
    public TitrantStrengthUnit? StrengthUnit { get; init; }
    public StandardizationMode? StandardizationMode { get; init; }
    public int? StandardEntryId { get; init; }
    public string? StandardEntryCode { get; init; }
    public decimal? EquivalenceMgPerMl { get; init; }
    public int? ReferenceSolutionId { get; init; }
    public string? ReferenceSolutionName { get; init; }
    public bool BlankRequired { get; init; }
    public int? ReplicateCount { get; init; }
    public decimal? FactorMin { get; init; }
    public decimal? FactorMax { get; init; }
    public decimal? MaxRsdPercent { get; init; }
    public int? ValidityDays { get; init; }

    public int CreatedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public int LastModifiedByUserId { get; init; }
    public DateTime LastModifiedAt { get; init; }

    public static SolutionMasterResponse From(SolutionMaster s) => new()
    {
        Id = s.Id,
        Version = s.Version,
        SectionId = s.SectionId,
        SectionName = s.Section?.Name,
        Name = s.Name,
        Type = s.Type,
        ShelfLifeValue = s.ShelfLifeValue,
        ShelfLifeUnit = s.ShelfLifeUnit,
        StorageCondition = s.StorageCondition,
        FinalVolumeMl = s.FinalVolumeMl,
        PhTarget = s.PhTarget,
        PhTolerance = s.PhTolerance,
        PhAdjustingEntryId = s.PhAdjustingEntryId,
        PhAdjustingEntryCode = s.PhAdjustingEntry?.Code,
        Instructions = s.Instructions,
        IsActive = s.IsActive,
        Components = s.Components
            .OrderBy(c => c.Order)
            .Select(c => new SolutionComponentResponse(
                c.Id, c.Order, c.MaterialMasterEntryId,
                c.MaterialMasterEntry?.Code ?? string.Empty,
                c.MaterialMasterEntry?.Name ?? string.Empty,
                c.Quantity, c.Unit))
            .ToList(),
        NominalStrength = s.NominalStrength,
        StrengthUnit = s.StrengthUnit,
        StandardizationMode = s.StandardizationMode,
        StandardEntryId = s.StandardEntryId,
        StandardEntryCode = s.StandardEntry?.Code,
        EquivalenceMgPerMl = s.EquivalenceMgPerMl,
        ReferenceSolutionId = s.ReferenceSolutionId,
        ReferenceSolutionName = s.ReferenceSolution?.Name,
        BlankRequired = s.BlankRequired,
        ReplicateCount = s.ReplicateCount,
        FactorMin = s.FactorMin,
        FactorMax = s.FactorMax,
        MaxRsdPercent = s.MaxRsdPercent,
        ValidityDays = s.ValidityDays,
        CreatedByUserId = s.CreatedByUserId,
        CreatedAt = s.CreatedAt,
        LastModifiedByUserId = s.LastModifiedByUserId,
        LastModifiedAt = s.LastModifiedAt,
    };
}
