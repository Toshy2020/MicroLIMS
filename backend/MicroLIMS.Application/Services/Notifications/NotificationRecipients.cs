using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.Notifications;

/// <summary>
/// Resolves notification recipient user IDs for a laboratory section while honoring
/// Segregation of Duties (excluding users who performed any of the target tests).
/// </summary>
public sealed class NotificationRecipients
{
    private readonly IMicroLimsDbContext _db;
    private readonly SegregationOfDutiesGuard _sod;

    public NotificationRecipients(IMicroLimsDbContext db, SegregationOfDutiesGuard sod)
    {
        _db = db;
        _sod = sod;
    }

    public Task<IReadOnlyCollection<int>> ReviewersAsync(
        int sectionId,
        IReadOnlyCollection<int> testOrderIds,
        CancellationToken ct = default) =>
        PermissionHoldersExcludingPerformersAsync(PermissionConstants.SamplesReview, sectionId, testOrderIds, ct);

    public Task<IReadOnlyCollection<int>> ApproversAsync(
        int sectionId,
        IReadOnlyCollection<int> testOrderIds,
        CancellationToken ct = default) =>
        PermissionHoldersExcludingPerformersAsync(PermissionConstants.SamplesApprove, sectionId, testOrderIds, ct);

    public async Task<IReadOnlyCollection<int>> RoleInSectionAsync(
        RoleType role,
        int sectionId,
        CancellationToken ct = default)
    {
        var section = await _db.DocumentSections.AsNoTracking()
            .Where(s => s.Id == sectionId && s.IsActive)
            .Select(s => new { s.Id, s.DepartmentId, DepartmentActive = s.Department.IsActive })
            .FirstOrDefaultAsync(ct);

        if (section is null)
            return Array.Empty<int>();

        return await _db.Users.AsNoTracking()
            .Where(u => u.IsActive
                && u.Role != null
                && u.Role.IsActive
                && u.Role.Type == role
                && _db.UserOrgMemberships.Any(m =>
                    m.UserId == u.Id
                    && (m.SectionId == section.Id
                        || (m.SectionId == null && m.DepartmentId == section.DepartmentId && section.DepartmentActive))))
            .Select(u => u.Id)
            .Distinct()
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyCollection<int>> PermissionHoldersExcludingPerformersAsync(
        string permissionCode,
        int sectionId,
        IReadOnlyCollection<int> testOrderIds,
        CancellationToken ct)
    {
        var section = await _db.DocumentSections.AsNoTracking()
            .Where(s => s.Id == sectionId && s.IsActive)
            .Select(s => new { s.Id, s.DepartmentId, DepartmentActive = s.Department.IsActive })
            .FirstOrDefaultAsync(ct);

        if (section is null)
            return Array.Empty<int>();

        var candidates = await (
            from u in _db.Users.AsNoTracking()
            join rp in _db.RolePermissions.AsNoTracking() on u.RoleId equals rp.RoleId
            join p in _db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
            where u.IsActive
                && u.Role != null
                && u.Role.IsActive
                && p.Code == permissionCode
                && _db.UserOrgMemberships.Any(m =>
                    m.UserId == u.Id
                    && (m.SectionId == section.Id
                        || (m.SectionId == null && m.DepartmentId == section.DepartmentId && section.DepartmentActive)))
            select u.Id
        ).Distinct().ToListAsync(ct);

        if (candidates.Count == 0 || testOrderIds.Count == 0)
            return candidates;

        var sampleIds = _db.TestOrders.AsNoTracking().Where(t => testOrderIds.Contains(t.Id)).Select(t => t.SampleId);
        var granters = await _db.CompetencyOverrides.AsNoTracking()
            .Where(row => sampleIds.Contains(row.SampleId) && candidates.Contains(row.GrantedByUserId))
            .Select(row => row.GrantedByUserId).ToHashSetAsync(ct);

        var eligible = new List<int>(candidates.Count);
        foreach (var candidateId in candidates.Where(id => !granters.Contains(id)))
        {
            var performed = await _sod.GetTestOrderIdsPerformedByAsync(candidateId, testOrderIds, ct);
            if (performed.Count == 0)
                eligible.Add(candidateId);
        }

        return eligible;
    }

    public async Task<IReadOnlyCollection<int>> PermissionHoldersAsync(
        string permissionCode,
        CancellationToken ct = default)
    {
        return await (
            from u in _db.Users.AsNoTracking()
            join rp in _db.RolePermissions.AsNoTracking() on u.RoleId equals rp.RoleId
            join p in _db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
            where u.IsActive
                && u.Role != null
                && u.Role.IsActive
                && p.Code == permissionCode
            select u.Id
        ).Distinct().ToListAsync(ct);
    }

    public async Task<IReadOnlyCollection<int>> PermissionHoldersInSectionAsync(
        string permissionCode,
        int? sectionId,
        CancellationToken ct = default)
    {
        var section = sectionId is int secId
            ? await _db.DocumentSections.AsNoTracking()
                .Where(s => s.Id == secId && s.IsActive)
                .Select(s => new { s.Id, s.DepartmentId, DepartmentActive = s.Department.IsActive })
                .FirstOrDefaultAsync(ct)
            : null;

        if (sectionId is not null && section is null)
            return Array.Empty<int>();

        return await (
            from u in _db.Users.AsNoTracking()
            join rp in _db.RolePermissions.AsNoTracking() on u.RoleId equals rp.RoleId
            join p in _db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
            where u.IsActive
                && u.Role != null
                && u.Role.IsActive
                && p.Code == permissionCode
                && (_db.RolePermissions.Any(vrp =>
                        vrp.RoleId == u.RoleId
                        && vrp.Permission!.Code == PermissionConstants.SectionsViewAll)
                    || (section != null && _db.UserOrgMemberships.Any(m =>
                        m.UserId == u.Id
                        && (m.SectionId == section.Id
                            || (m.SectionId == null && m.DepartmentId == section.DepartmentId && section.DepartmentActive)))))
            select u.Id
        ).Distinct().ToListAsync(ct);
    }

    public async Task<IReadOnlyCollection<int>> SectionHeadOrPermissionHoldersAsync(
        string permissionCode,
        CancellationToken ct = default)
    {
        var sectionHeads = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Role != null && u.Role.IsActive && u.Role.Type == RoleType.SectionHead)
            .Select(u => u.Id)
            .ToListAsync(ct);

        var permissionHolders = await PermissionHoldersAsync(permissionCode, ct);

        return sectionHeads.Concat(permissionHolders).Distinct().ToList();
    }
}
