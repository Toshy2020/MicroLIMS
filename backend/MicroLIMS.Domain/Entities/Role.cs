using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class Role
{
    public int Id { get; set; }
    public RoleType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    // True for the 4 roles DbSeeder creates - protects them from deletion
    // via RoleController, since RoleType has no 5th value for them to fall
    // back to and every [Authorize(Roles=...)] attribute in the app still
    // depends on those 4 names existing.
    public bool IsSystemRole { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Permission> Permissions { get; set; } = new();
}
