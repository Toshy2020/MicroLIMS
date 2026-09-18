using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record LaboratorySectionDto(int SectionId, string SectionCode, string SectionName, int DepartmentId, string DepartmentCode, string DepartmentName);

// A user's membership: the whole department (SectionId null) or one section.
public record UserOrgMembershipDto(int DepartmentId, int? SectionId);

// The laboratory departments/sections used for data segregation, and which
// of them each user belongs to.
public class LaboratoryOrganizationService
{
    private readonly MicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;

    public LaboratoryOrganizationService(MicroLimsDbContext db, IUserSectionScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<List<LaboratorySectionDto>> GetActiveSectionsAsync() =>
        await _db.DocumentSections.AsNoTracking()
            .Where(s => s.IsActive && s.Department.IsActive)
            .OrderBy(s => s.Department.Name).ThenBy(s => s.Name)
            .Select(s => new LaboratorySectionDto(s.Id, s.Code, s.Name, s.DepartmentId, s.Department.Code, s.Department.Name))
            .ToListAsync();

    // The sections a user can work in - every active section for an
    // unrestricted user (system administrator).
    public async Task<List<LaboratorySectionDto>> GetMySectionsAsync(int userId)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId);
        var sections = await GetActiveSectionsAsync();
        return scope is null ? sections : sections.Where(s => scope.Contains(s.SectionId)).ToList();
    }

    public async Task<List<UserOrgMembershipDto>> GetMembershipsAsync(int userId) =>
        await _db.UserOrgMemberships.AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.DepartmentId).ThenBy(m => m.SectionId)
            .Select(m => new UserOrgMembershipDto(m.DepartmentId, m.SectionId))
            .ToListAsync();

    // Replaces the user's memberships. Every change is captured by the
    // DbContext's audit trail under the acting user - membership decides
    // what laboratory data a user can see and act on.
    public async Task<List<UserOrgMembershipDto>> ReplaceMembershipsAsync(int userId, IReadOnlyCollection<UserOrgMembershipDto> memberships, int actingUserId)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId))
            throw new InvalidOperationException($"User {userId} not found.");

        var wanted = memberships.Distinct().ToList();
        var departmentIds = wanted.Select(m => m.DepartmentId).Distinct().ToList();
        var activeDepartments = await _db.DocumentDepartments
            .Where(d => departmentIds.Contains(d.Id) && d.IsActive).Select(d => d.Id).ToListAsync();
        if (activeDepartments.Count != departmentIds.Count)
            throw new InvalidOperationException("One or more departments were not found or are inactive.");

        var sectionIds = wanted.Where(m => m.SectionId.HasValue).Select(m => m.SectionId!.Value).Distinct().ToList();
        var sections = await _db.DocumentSections
            .Where(s => sectionIds.Contains(s.Id) && s.IsActive)
            .ToDictionaryAsync(s => s.Id, s => s.DepartmentId);
        foreach (var m in wanted.Where(m => m.SectionId.HasValue))
        {
            if (!sections.TryGetValue(m.SectionId!.Value, out var departmentId))
                throw new InvalidOperationException($"Laboratory section {m.SectionId} was not found or is inactive.");
            if (departmentId != m.DepartmentId)
                throw new InvalidOperationException($"Laboratory section {m.SectionId} does not belong to department {m.DepartmentId}.");
        }

        _db.CurrentUserId = actingUserId;
        var existing = await _db.UserOrgMemberships.Where(m => m.UserId == userId).ToListAsync();
        _db.UserOrgMemberships.RemoveRange(existing.Where(e => !wanted.Contains(new UserOrgMembershipDto(e.DepartmentId, e.SectionId))));
        foreach (var m in wanted.Where(w => !existing.Any(e => e.DepartmentId == w.DepartmentId && e.SectionId == w.SectionId)))
            _db.UserOrgMemberships.Add(new UserOrgMembership { UserId = userId, DepartmentId = m.DepartmentId, SectionId = m.SectionId });

        await _db.SaveChangesAsync();
        return await GetMembershipsAsync(userId);
    }
}
