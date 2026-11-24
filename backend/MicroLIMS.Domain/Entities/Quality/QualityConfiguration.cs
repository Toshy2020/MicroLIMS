using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities.Quality;

public class QualityChecklistTemplate
{
    public int Id { get; set; }
    public QualityRecordType Type { get; set; }
    public int? SectionId { get; set; }
    public List<QualityChecklistItem> Items { get; set; } = new();
}

public class QualityChecklistItem
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public string Key { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class QualityClassification
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsActive { get; set; }
}

public class QualityDueRule
{
    public int Id { get; set; }
    public QualityRecordType Type { get; set; }
    public int? ClassificationId { get; set; }
    public int? WorkingDays { get; set; }
}

public class QualitySectionSetting : IVersionedEntity
{
    public int Id { get; set; }
    public int SectionId { get; set; }
    public bool ChangeControlRequired { get; set; }
    public uint Version { get; set; }
}
