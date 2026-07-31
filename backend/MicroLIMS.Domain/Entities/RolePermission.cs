namespace MicroLIMS.Domain.Entities;

// Join entity forming the Permission Matrix: which Permissions each
// Role is granted. Read by PermissionService for fine-grained checks
// beyond what [Authorize(Roles=...)] alone can express.
public class RolePermission
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public int PermissionId { get; set; }
    public Permission? Permission { get; set; }
}
