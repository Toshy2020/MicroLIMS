using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services;

// One proposed change that could take administration capability away:
// a user's role changing, a user being disabled or deleted, or a role's
// permission set being replaced. Build one with the factories below.
public sealed record AdministrationChange
{
    public int? UserId { get; private init; }
    public int? NewRoleId { get; private init; }
    public bool UserRemoved { get; private init; }
    public int? RoleId { get; private init; }
    public IReadOnlyCollection<string>? NewPermissionCodes { get; private init; }

    public static AdministrationChange UserRoleChanged(int userId, int newRoleId) =>
        new() { UserId = userId, NewRoleId = newRoleId };

    // Disabling and hard-deleting look the same from here: the user stops
    // being an active holder of whatever their role grants.
    public static AdministrationChange UserDisabledOrDeleted(int userId) =>
        new() { UserId = userId, UserRemoved = true };

    public static AdministrationChange RolePermissionsReplaced(int roleId, IReadOnlyCollection<string> permissionCodes) =>
        new() { RoleId = roleId, NewPermissionCodes = permissionCodes };
}

// Keeps the system administrable: after any change that could remove
// administration capability, at least one ACTIVE user must still hold
// Users.Manage and at least one must still hold Roles.Manage. Counts by
// permission through the user's role - never by RoleType, so a custom role
// holding the permission counts and a System Administrator-typed role
// stripped of it does not. Temporarily locked users still count: a lockout
// expires by itself and must not block administration, so only IsActive is
// considered. Evaluated against the database plus the proposal, never the
// change tracker, so it can run before anything is modified or saved.
public class AdministrationContinuityGuard
{
    private static readonly string[] RequiredPermissions =
    {
        PermissionConstants.UsersManage,
        PermissionConstants.RolesManage
    };

    private readonly IMicroLimsDbContext _db;

    public AdministrationContinuityGuard(IMicroLimsDbContext db)
    {
        _db = db;
    }

    // Throws InvalidOperationException naming the first required permission
    // that no active user would hold after the change.
    public async Task EnsureAdministrableAfterAsync(AdministrationChange change)
    {
        var missing = await FindMissingPermissionAfterAsync(change);
        if (missing is not null)
            throw new InvalidOperationException(
                $"This change is not allowed: no active user would be left with the '{missing}' permission. " +
                "Make sure another active user holds a role with that permission first.");
    }

    // Returns the first required permission that no active user would hold
    // after the change, or null when administration stays possible. For
    // callers that need their own user-facing wording.
    public async Task<string?> FindMissingPermissionAfterAsync(AdministrationChange change)
    {
        var activeUsers = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new { u.Id, u.RoleId })
            .ToListAsync();

        foreach (var code in RequiredPermissions)
        {
            var rolesHolding = (await _db.RolePermissions.AsNoTracking()
                .Where(rp => rp.Permission!.Code == code)
                .Select(rp => rp.RoleId)
                .ToListAsync()).ToHashSet();

            if (change.RoleId is int replacedRoleId)
            {
                if (change.NewPermissionCodes!.Contains(code))
                    rolesHolding.Add(replacedRoleId);
                else
                    rolesHolding.Remove(replacedRoleId);
            }

            var anyHolder = activeUsers.Any(u =>
            {
                if (u.Id == change.UserId)
                {
                    if (change.UserRemoved) return false;
                    if (change.NewRoleId is int newRoleId) return rolesHolding.Contains(newRoleId);
                }
                return rolesHolding.Contains(u.RoleId);
            });

            if (!anyHolder)
                return code;
        }

        return null;
    }
}
