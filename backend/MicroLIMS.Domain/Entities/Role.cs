using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class Role
{
    public int Id { get; set; }
    public RoleType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Permission> Permissions { get; set; } = new();
}
