using Microsoft.EntityFrameworkCore;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Reads the Role -> Permission matrix (RolePermission join table).
// [Authorize(Roles="...")] handles the coarse "which role" check;
// this handles finer-grained checks like "can this Section Head edit
// Items in a specific department" where a role alone isn't enough.
public class PermissionService
{
    private readonly MicroLimsDbContext _db;

    public PermissionService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasPermissionAsync(int roleId, string permissionCode)
    {
        return await _db.RolePermissions
            .Include(rp => rp.Permission)
            .AnyAsync(rp => rp.RoleId == roleId && rp.Permission!.Code == permissionCode);
    }

    public async Task<List<string>> GetPermissionCodesForRoleAsync(int roleId)
    {
        return await _db.RolePermissions
            .Include(rp => rp.Permission)
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission!.Code)
            .ToListAsync();
    }

    public async Task GrantAsync(int roleId, int permissionId)
    {
        var exists = await _db.RolePermissions.AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);
        if (exists) return;
        _db.RolePermissions.Add(new Domain.Entities.RolePermission { RoleId = roleId, PermissionId = permissionId });
        await _db.SaveChangesAsync();
    }

    public async Task RevokeAsync(int roleId, int permissionId)
    {
        var rp = await _db.RolePermissions.FirstOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == permissionId);
        if (rp is null) return;
        _db.RolePermissions.Remove(rp);
        await _db.SaveChangesAsync();
    }
}
