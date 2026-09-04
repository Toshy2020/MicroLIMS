namespace MicroLIMS.Domain.Entities;

public class DocumentDepartment
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<DocumentSection> Sections { get; set; } = new List<DocumentSection>();
}
