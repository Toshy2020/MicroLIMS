using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.Responses;

public class MaterialCategoryResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Name { get; init; } = null!;
    public string CodePrefix { get; init; } = null!;
    public MaterialUnit DefaultUnit { get; init; }
    public bool CoaRequired { get; init; }
    public bool ExpiryRequired { get; init; }
    public bool QuantityTracked { get; init; }
    public string? DefaultStorageCondition { get; init; }
    public bool ApprovalBeforeUse { get; init; }
    public bool LabelRequired { get; init; }
    public bool IsActive { get; init; }
    // Empty = available to every section.
    public List<int> SectionIds { get; init; } = new();
    // Any master entry uses the category: its code prefix is frozen.
    public bool HasEntries { get; init; }
    public int CreatedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public int LastModifiedByUserId { get; init; }
    public DateTime LastModifiedAt { get; init; }

    public static MaterialCategoryResponse From(MaterialCategory c, bool hasEntries = false) => new()
    {
        Id = c.Id,
        Version = c.Version,
        Name = c.Name,
        CodePrefix = c.CodePrefix,
        DefaultUnit = c.DefaultUnit,
        CoaRequired = c.CoaRequired,
        ExpiryRequired = c.ExpiryRequired,
        QuantityTracked = c.QuantityTracked,
        DefaultStorageCondition = c.DefaultStorageCondition,
        ApprovalBeforeUse = c.ApprovalBeforeUse,
        LabelRequired = c.LabelRequired,
        IsActive = c.IsActive,
        SectionIds = c.Sections.Select(s => s.SectionId).OrderBy(i => i).ToList(),
        HasEntries = hasEntries,
        CreatedByUserId = c.CreatedByUserId,
        CreatedAt = c.CreatedAt,
        LastModifiedByUserId = c.LastModifiedByUserId,
        LastModifiedAt = c.LastModifiedAt,
    };
}
