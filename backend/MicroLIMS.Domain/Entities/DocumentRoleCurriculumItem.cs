namespace MicroLIMS.Domain.Entities;

public class DocumentRoleCurriculumItem
{
    public int Id { get; set; }

    public int DocumentRoleCurriculumId { get; set; }
    public DocumentRoleCurriculum DocumentRoleCurriculum { get; set; } = null!;

    public int DocumentMasterId { get; set; }
    public DocumentMaster DocumentMaster { get; set; } = null!;

    public bool IsMandatory { get; set; } = true;
    public int? CustomGracePeriodDays { get; set; }

    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}
