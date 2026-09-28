namespace MicroLIMS.Domain.Entities;

public class DocumentSection : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public DocumentDepartment Department { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
