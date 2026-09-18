using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public class UserSectionScopeService : IUserSectionScopeService
{
    private readonly MicroLimsDbContext _db;

    public UserSectionScopeService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<int>?> GetAccessibleSectionIdsAsync(int userId, CancellationToken ct = default)
    {
        var userRole = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Role != null ? (RoleType?)u.Role.Type : null)
            .FirstOrDefaultAsync(ct);

        if (userRole == RoleType.SystemAdministrator)
        {
            return null;
        }

        var memberships = await _db.UserOrgMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new { m.DepartmentId, m.SectionId })
            .ToListAsync(ct);

        if (memberships.Count == 0)
        {
            return Array.Empty<int>();
        }

        var explicitSectionIds = memberships
            .Where(m => m.SectionId.HasValue)
            .Select(m => m.SectionId!.Value)
            .Distinct()
            .ToList();

        var deptIds = memberships
            .Where(m => !m.SectionId.HasValue)
            .Select(m => m.DepartmentId)
            .Distinct()
            .ToList();

        var query = _db.DocumentSections.AsNoTracking().Where(s => s.IsActive);

        if (explicitSectionIds.Count > 0 && deptIds.Count > 0)
        {
            query = query.Where(s => explicitSectionIds.Contains(s.Id) || (deptIds.Contains(s.DepartmentId) && s.Department.IsActive));
        }
        else if (explicitSectionIds.Count > 0)
        {
            query = query.Where(s => explicitSectionIds.Contains(s.Id));
        }
        else if (deptIds.Count > 0)
        {
            query = query.Where(s => deptIds.Contains(s.DepartmentId) && s.Department.IsActive);
        }
        else
        {
            return Array.Empty<int>();
        }

        var sectionIds = await query
            .Select(s => s.Id)
            .Distinct()
            .ToListAsync(ct);

        return sectionIds;
    }

    public async Task<int> ResolveSectionForCreateAsync(int userId, int? requestedSectionId, CancellationToken ct = default)
    {
        var scope = await GetAccessibleSectionIdsAsync(userId, ct);

        if (requestedSectionId.HasValue)
        {
            var sectionId = requestedSectionId.Value;
            var existsAndActive = await _db.DocumentSections
                .AnyAsync(s => s.Id == sectionId && s.IsActive, ct);

            if (!existsAndActive)
            {
                throw new InvalidOperationException("Selected laboratory section was not found or is inactive.");
            }

            if (scope != null && !scope.Contains(sectionId))
            {
                throw new InvalidOperationException("You are not a member of the selected laboratory section.");
            }

            return sectionId;
        }

        if (scope is null)
        {
            throw new InvalidOperationException("Choose a laboratory section.");
        }

        if (scope.Count == 0)
        {
            throw new InvalidOperationException("Your account is not assigned to any laboratory section. Ask an administrator to assign one.");
        }

        if (scope.Count == 1)
        {
            return scope[0];
        }

        throw new InvalidOperationException("Choose a laboratory section - you belong to more than one.");
    }
}
