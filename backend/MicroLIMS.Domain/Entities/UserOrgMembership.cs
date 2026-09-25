namespace MicroLIMS.Domain.Entities;

// SectionId null = member of the whole department (every section under it); SectionId set = member of that one section only, and the section must belong to DepartmentId.
public class UserOrgMembership
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int DepartmentId { get; set; }
    public DocumentDepartment Department { get; set; } = null!;
    public int? SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
