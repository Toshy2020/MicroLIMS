using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Tests.Fixtures;

// What the built-in System Administrator holds for lab scope: Sections.ViewAll,
// which reads every section and writes none. Tests that want an administrator
// who sees across sections grant it explicitly instead of relying on the role.
internal static class AdministratorScope
{
    public static void GrantViewAll(MicroLimsDbContext db, int roleId)
    {
        var permission = db.Permissions.FirstOrDefault(p => p.Code == PermissionConstants.SectionsViewAll);
        if (permission is null)
        {
            permission = new Permission { Code = PermissionConstants.SectionsViewAll, Description = "View every section read-only." };
            db.Permissions.Add(permission);
            db.SaveChanges();
        }

        if (!db.RolePermissions.Any(rp => rp.RoleId == roleId && rp.PermissionId == permission.Id))
        {
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permission.Id });
            db.SaveChanges();
        }
    }
}
