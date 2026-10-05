using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services;

public record LaboratorySectionDto(int SectionId, string SectionCode, string SectionName, int DepartmentId, string DepartmentCode, string DepartmentName, IReadOnlyList<string> PhyschemAreas);

// A user's membership: the whole department (SectionId null) or one section.
// PhyschemArea: Physicochemical Laboratory memberships only; null = both areas.
public record UserOrgMembershipDto(int DepartmentId, int? SectionId, PhyschemArea? PhyschemArea = null);

// The laboratory departments/sections used for data segregation, and which
// of them each user belongs to.
public class LaboratoryOrganizationService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;

    public LaboratoryOrganizationService(IMicroLimsDbContext db, IUserSectionScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<List<LaboratorySectionDto>> GetActiveSectionsAsync() =>
        await _db.DocumentSections.AsNoTracking()
            .Where(s => s.IsActive && s.Department.IsActive)
            .OrderBy(s => s.Department.Name).ThenBy(s => s.Name)
            .Select(s => new LaboratorySectionDto(s.Id, s.Code, s.Name, s.DepartmentId, s.Department.Code, s.Department.Name, Array.Empty<string>()))
            .ToListAsync();

    // The sections a user can work in - every active section for an
    // unrestricted user (system administrator).
    public async Task<List<LaboratorySectionDto>> GetMySectionsAsync(int userId)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId);
        var sections = await GetActiveSectionsAsync();
        var mine = scope is null ? sections : sections.Where(s => scope.Contains(s.SectionId)).ToList();
        if (mine.All(s => s.SectionCode != PhyschemAreas.SectionCode)) return mine;

        var areas = (await _scope.GetPhyschemAreasAsync(userId))
            .Select(a => a == WorkspaceArea.Fp ? "fp" : "rmpm").ToList();
        return mine.Select(s => s.SectionCode == PhyschemAreas.SectionCode ? s with { PhyschemAreas = areas } : s).ToList();
    }

    public async Task<List<UserOrgMembershipDto>> GetMembershipsAsync(int userId) =>
        await _db.UserOrgMemberships.AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.DepartmentId).ThenBy(m => m.SectionId)
            .Select(m => new UserOrgMembershipDto(m.DepartmentId, m.SectionId, m.PhyschemArea))
            .ToListAsync();

    // Replaces the user's memberships. Every change is captured by the
    // DbContext's audit trail under the acting user - membership decides
    // what laboratory data a user can see and act on.
    public async Task<List<UserOrgMembershipDto>> ReplaceMembershipsAsync(int userId, IReadOnlyCollection<UserOrgMembershipDto> memberships, int actingUserId)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId))
            throw new NotFoundException($"User {userId} not found.");

        // Memberships are keyed by department + section; the last row for a key wins.
        var wanted = memberships.GroupBy(m => (m.DepartmentId, m.SectionId)).Select(g => g.Last()).ToList();
        var departmentIds = wanted.Select(m => m.DepartmentId).Distinct().ToList();
        var activeDepartments = await _db.DocumentDepartments
            .Where(d => departmentIds.Contains(d.Id) && d.IsActive).Select(d => d.Id).ToListAsync();
        if (activeDepartments.Count != departmentIds.Count)
            throw new InvalidOperationException("One or more departments were not found or are inactive.");

        var sectionIds = wanted.Where(m => m.SectionId.HasValue).Select(m => m.SectionId!.Value).Distinct().ToList();
        var sections = await _db.DocumentSections
            .Where(s => sectionIds.Contains(s.Id) && s.IsActive)
            .ToDictionaryAsync(s => s.Id, s => (s.DepartmentId, s.Code));
        foreach (var m in wanted.Where(m => m.SectionId.HasValue))
        {
            if (!sections.TryGetValue(m.SectionId!.Value, out var section))
                throw new InvalidOperationException($"Laboratory section {m.SectionId} was not found or is inactive.");
            if (section.DepartmentId != m.DepartmentId)
                throw new InvalidOperationException($"Laboratory section {m.SectionId} does not belong to department {m.DepartmentId}.");
        }
        if (wanted.Any(m => m.PhyschemArea.HasValue && (!m.SectionId.HasValue || sections[m.SectionId.Value].Code != PhyschemAreas.SectionCode)))
            throw new InvalidOperationException("An area can only be set on a Physicochemical Laboratory membership.");

        _db.CurrentUserId = actingUserId;
        var existing = await _db.UserOrgMemberships.Where(m => m.UserId == userId).ToListAsync();
        _db.UserOrgMemberships.RemoveRange(existing.Where(e => !wanted.Any(w => w.DepartmentId == e.DepartmentId && w.SectionId == e.SectionId)));
        foreach (var w in wanted)
        {
            var row = existing.FirstOrDefault(e => e.DepartmentId == w.DepartmentId && e.SectionId == w.SectionId);
            if (row is null)
                _db.UserOrgMemberships.Add(new UserOrgMembership { UserId = userId, DepartmentId = w.DepartmentId, SectionId = w.SectionId, PhyschemArea = w.PhyschemArea });
            else if (row.PhyschemArea != w.PhyschemArea)
                row.PhyschemArea = w.PhyschemArea; // a changed area is an update of the existing row
        }

        await _db.SaveChangesAsync();
        return await GetMembershipsAsync(userId);
    }
}
