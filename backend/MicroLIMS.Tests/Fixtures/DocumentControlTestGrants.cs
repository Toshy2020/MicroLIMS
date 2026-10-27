using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Tests.Fixtures;

// The five Document Control codes are a role gate on top of the assignment rules (owner decision Q13).
// Workflow tests that exercise the assignment rules, not the role gate, give every role that is not the
// System Administrator the five codes, so a reviewer / approver / author fixture is not refused for a missing grant.
// Tests of the gate itself build their own roles (DocumentControlPermissionEnforcementTests).
public static class DocumentControlTestGrants
{
    public static readonly string[] Codes =
    {
        PermissionConstants.DocumentsDraftEdit, PermissionConstants.DocumentsRevisionCreate, PermissionConstants.DocumentsReview,
        PermissionConstants.DocumentsApprove, PermissionConstants.DocumentsPeriodicReview
    };

    public static void GrantToAllNonAdministratorRoles(MicroLimsDbContext db)
    {
        foreach (var code in Codes)
        {
            if (!db.Permissions.Any(p => p.Code == code))
                db.Permissions.Add(new Permission { Code = code, Description = code, IsEnforced = true });
        }
        db.SaveChanges();

        var permissionIds = db.Permissions.Where(p => Codes.Contains(p.Code)).Select(p => p.Id).ToList();
        foreach (var role in db.Roles.Where(r => r.Type != RoleType.SystemAdministrator).ToList())
        {
            foreach (var permissionId in permissionIds)
            {
                if (!db.RolePermissions.Any(rp => rp.RoleId == role.Id && rp.PermissionId == permissionId))
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
            }
        }
        db.SaveChanges();
    }
}
