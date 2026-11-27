using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.Oos;

public sealed class OosRecipientResolver(IMicroLimsDbContext db, TimeProvider time) : IPermissionRecipientResolver
{
    public async Task<IReadOnlyList<int>> ResolveAsync(int sectionId, string permissionCode, CancellationToken ct = default) =>
        await EligibleUsers(sectionId)
            .Where(u => db.RolePermissions.Any(rp => rp.RoleId == u.RoleId && rp.Permission!.Code == permissionCode))
            .Select(u => u.Id)
            .ToListAsync(ct);

    public Task<IReadOnlyList<int>> ResolveAsync(int? sectionId, string permissionCode, CancellationToken ct = default) =>
        sectionId is int id
            ? ResolveAsync(id, permissionCode, ct)
            : ResolveSiteWideAsync(permissionCode, ct);

    private async Task<IReadOnlyList<int>> ResolveSiteWideAsync(string permissionCode, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        return await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Role != null && u.Role.IsActive &&
                (u.LockedUntil == null || u.LockedUntil <= now) &&
                db.RolePermissions.Any(rp => rp.RoleId == u.RoleId && rp.Permission!.Code == permissionCode))
            .Select(u => u.Id)
            .ToListAsync(ct);
    }

    internal Task<List<int>> ResolvePerformingAnalystsAsync(int sectionId, int[] orderIds, SegregationOfDutiesGuard duties,
        CancellationToken ct = default) =>
        EligibleUsers(sectionId)
            .Where(u => u.Role != null && u.Role.Type == RoleType.Analyst)
            .Select(u => u.Id)
            .Intersect(duties.PerformerIds(orderIds))
            .ToListAsync(ct);

    private IQueryable<User> EligibleUsers(int sectionId)
    {
        var now = time.GetUtcNow().UtcDateTime;
        return db.Users.AsNoTracking().Where(u => u.IsActive && u.Role != null && u.Role.IsActive &&
            (u.LockedUntil == null || u.LockedUntil <= now) &&
            db.DocumentSections.Any(s => s.Id == sectionId && s.IsActive &&
                (db.RolePermissions.Any(rp => rp.RoleId == u.RoleId && rp.Permission!.Code == PermissionConstants.SectionsViewAll) ||
                 db.UserOrgMemberships.Any(m => m.UserId == u.Id && (m.SectionId == s.Id ||
                     (m.SectionId == null && m.DepartmentId == s.DepartmentId && s.Department.IsActive))))));
    }
}
