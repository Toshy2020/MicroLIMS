namespace MicroLIMS.Domain.Entities;

public class DocumentSection
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public DocumentDepartment Department { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
