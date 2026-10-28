using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class Role : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public RoleType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    // True for the 4 roles DbSeeder creates - protects them from deletion
    // via RoleController, since RoleType has no 5th value for them to fall
    // back to and every [Authorize(Roles=...)] attribute in the app still
    // depends on those 4 names existing.
    public bool IsSystemRole { get; set; }
    public bool IsActive { get; set; } = true;
    // Bumped every time this role's permission grants change. It is signed
    // into each access token (the "pv" claim) and compared with this value
    // on every request, so editing a role's permissions ends the sessions
    // that were issued with the old grants. Starts at 1 so a token can never
    // match a role through a missing or zero value.
    public int PermissionVersion { get; set; } = 1;
    public List<Permission> Permissions { get; set; } = new();
}
