using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record RoleDetailDto(int Id, string Name, string? Description, string Type, bool IsSystemRole, bool IsActive, List<string> PermissionCodes);
public record PermissionDto(string Code, string Description, bool IsEnforced);

// Role CRUD + permission-grant management. Plain create/update/delete
// rely on MicroLimsDbContext.SaveChanges's automatic audit capture -
// the same convention ItemService/MasterDataController's CRUD already
// uses, with no custom AuditLog code. UpdatePermissionsAsync is the one
// exception: granting/revoking spans several RolePermission rows, so a
// single consolidated old-set/new-set entry is genuinely more useful
// than N generic per-row Create/Delete entries - the same reasoning
// UserService.ChangeRoleAsync/SetStatusAsync already apply to their own
// multi-field security-sensitive changes.
public class RoleService
{
    private readonly MicroLimsDbContext _db;
    private readonly PermissionService _permissionService;

    public RoleService(MicroLimsDbContext db, PermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
    }

    public async Task<RoleDetailDto?> GetByIdAsync(int id)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return null;
        var codes = await _permissionService.GetPermissionCodesForRoleAsync(id);
        return ToDto(role, codes);
    }

    public async Task<List<PermissionDto>> GetAllPermissionsAsync() =>
        await _db.Permissions.OrderBy(p => p.Code).Select(p => new PermissionDto(p.Code, p.Description, p.IsEnforced)).ToListAsync();

    // baseType: which of the 4 RoleType values this role's holders are
    // treated as by the still-untouched [Authorize(Roles=...)] attributes
    // and the JWT Role claim - RoleType has no 5th "custom" value, and
    // widening it would ripple into DashboardController.CurrentRole and
    // every other Role.Type read-site this phase is explicitly not
    // touching. A new role is a fully distinct row (own permission
    // grants via RolePermission, enforced by any new policy-based
    // endpoint) that simply borrows an existing legacy bucket for
    // role-string compatibility. Pick the least-privileged bucket that
    // still makes sense for the role's intended use; it is not the same
    // thing as the role's actual (permission-based) access.
    public async Task<RoleDetailDto> CreateAsync(string name, string? description, RoleType baseType)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Role name is required.");

        var role = new Role { Name = name, Description = description, Type = baseType, IsSystemRole = false, IsActive = true };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(); // automatic audit capture logs Action="Create" for this row, with the now-assigned Id

        return ToDto(role, new List<string>());
    }

    public async Task<RoleDetailDto> UpdateAsync(int id, string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Role name is required.");

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new InvalidOperationException($"Role {id} not found.");

        role.Name = name;
        role.Description = description;
        await _db.SaveChangesAsync(); // automatic audit capture logs Action="Update" with the full before/after diff

        var codes = await _permissionService.GetPermissionCodesForRoleAsync(id);
        return ToDto(role, codes);
    }

    public async Task DeleteAsync(int id)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new InvalidOperationException($"Role {id} not found.");

        if (role.IsSystemRole)
            throw new InvalidOperationException("System roles cannot be deleted.");

        var inUseCount = await _db.Users.CountAsync(u => u.RoleId == id);
        if (inUseCount > 0)
            throw new InvalidOperationException($"Role '{role.Name}' is currently assigned to {inUseCount} user(s) and cannot be deleted.");

        var grantedPermissions = await _db.RolePermissions.Where(rp => rp.RoleId == id).ToListAsync();
        _db.RolePermissions.RemoveRange(grantedPermissions);
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(); // automatic audit capture logs Action="Delete" for both the Role row and each removed RolePermission row
    }

    public async Task<RoleDetailDto> UpdatePermissionsAsync(int id, List<string> permissionCodes, int actingUserId)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new InvalidOperationException($"Role {id} not found.");

        var validCodes = await _db.Permissions.Select(p => p.Code).ToListAsync();
        var unknown = permissionCodes.Except(validCodes).ToList();
        if (unknown.Count > 0)
            throw new InvalidOperationException($"Unknown permission code(s): {string.Join(", ", unknown)}.");

        var currentCodes = await _permissionService.GetPermissionCodesForRoleAsync(id);
        var toGrant = permissionCodes.Except(currentCodes).ToList();
        var toRevoke = currentCodes.Except(permissionCodes).ToList();

        var permissionIdByCode = await _db.Permissions
            .Where(p => toGrant.Contains(p.Code) || toRevoke.Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, p => p.Id);

        foreach (var code in toGrant)
            await _permissionService.GrantAsync(id, permissionIdByCode[code]);
        foreach (var code in toRevoke)
            await _permissionService.RevokeAsync(id, permissionIdByCode[code]);

        // PermissionService.GrantAsync/RevokeAsync each call SaveChangesAsync
        // internally (see PermissionService), so this audit entry is
        // written and saved separately rather than batched with the
        // grant/revoke - it still captures the full before/after diff in
        // one record, which is what actually matters for "old value ->
        // new value, who, when".
        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(Role),
            EntityId = id.ToString(),
            Action = "ROLE_PERMISSIONS_UPDATED",
            PreviousValue = JsonSerializer.Serialize(new { PermissionCodes = currentCodes }),
            NewValue = JsonSerializer.Serialize(new { PermissionCodes = permissionCodes, Granted = toGrant, Revoked = toRevoke }),
            UserId = actingUserId,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return ToDto(role, permissionCodes);
    }

    private static RoleDetailDto ToDto(Role r, List<string> permissionCodes) =>
        new(r.Id, r.Name, r.Description, r.Type.ToString(), r.IsSystemRole, r.IsActive, permissionCodes);
}
