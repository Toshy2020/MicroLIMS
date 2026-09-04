namespace MicroLIMS.Domain.Entities;

public class DocumentRoleCurriculum
{
    public int Id { get; set; }

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public int? DepartmentId { get; set; }
    public DocumentDepartment? Department { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<DocumentRoleCurriculumItem> Items { get; set; } = new List<DocumentRoleCurriculumItem>();
}
