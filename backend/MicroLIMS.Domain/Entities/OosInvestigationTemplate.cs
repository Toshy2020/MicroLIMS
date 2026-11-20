using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class OosInvestigationTemplate : IVersionedEntity
{
    public int Id { get; set; }
    public int SectionId { get; set; }
    public OosInvestigationKind Kind { get; set; }
    public int? PhaseOneDueWorkingDays { get; set; }
    public int? TotalDueWorkingDays { get; set; }
    public bool AlertOpensExcursionReview { get; set; }
    public uint Version { get; set; }
}

public class OosChecklistItem
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public int Phase { get; set; }
    public string Key { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class OosCauseCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsNoCauseFound { get; set; }
}
