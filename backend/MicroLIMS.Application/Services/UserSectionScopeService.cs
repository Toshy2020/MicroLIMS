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

    public async Task EnsureTestOrderAccessAsync(int userId, int testOrderId, CancellationToken ct = default)
    {
        var scope = await GetAccessibleSectionIdsAsync(userId, ct);
        if (scope is null) return;

        var sectionId = await _db.TestOrders
            .AsNoTracking()
            .Where(t => t.Id == testOrderId)
            .Select(t => (int?)t.SectionId)
            .FirstOrDefaultAsync(ct);

        if (sectionId is null) return;

        if (!scope.Contains(sectionId.Value))
        {
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");
        }
    }

    public async Task EnsureTestOrdersAccessAsync(int userId, IEnumerable<int> testOrderIds, CancellationToken ct = default)
    {
        var scope = await GetAccessibleSectionIdsAsync(userId, ct);
        if (scope is null) return;

        var idList = testOrderIds as IReadOnlyCollection<int> ?? testOrderIds.Distinct().ToList();
        if (idList.Count == 0) return;

        var sectionIds = await _db.TestOrders
            .AsNoTracking()
            .Where(t => idList.Contains(t.Id))
            .Select(t => t.SectionId)
            .Distinct()
            .ToListAsync(ct);

        if (sectionIds.Any(s => !scope.Contains(s)))
        {
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");
        }
    }

    public async Task EnsureSampleAccessAsync(int userId, int sampleId, CancellationToken ct = default)
    {
        var scope = await GetAccessibleSectionIdsAsync(userId, ct);
        if (scope is null) return;

        var sampleInfo = await _db.Samples
            .AsNoTracking()
            .Where(s => s.Id == sampleId)
            .Select(s => new
            {
                OrderSectionIds = s.TestOrders
                    .Where(t => !t.IsSuperseded)
                    .Select(t => t.SectionId)
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (sampleInfo is null) return;

        if (!sampleInfo.OrderSectionIds.Any(s => scope.Contains(s)))
        {
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");
        }
    }

    public async Task EnsureIncubationAccessAsync(int userId, int incubationId, CancellationToken ct = default)
    {
        var scope = await GetAccessibleSectionIdsAsync(userId, ct);
        if (scope is null) return;

        var incInfo = await _db.Incubations
            .AsNoTracking()
            .Where(i => i.Id == incubationId)
            .Select(i => new
            {
                i.TestOrderId,
                SectionId = i.TestOrder != null ? (int?)i.TestOrder.SectionId : null
            })
            .FirstOrDefaultAsync(ct);

        if (incInfo is null) return;
        if (!incInfo.TestOrderId.HasValue) return;

        if (!incInfo.SectionId.HasValue || !scope.Contains(incInfo.SectionId.Value))
        {
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");
        }
    }

    public async Task EnsurePathogenSampleAccessAsync(int userId, int sampleId, CancellationToken ct = default)
    {
        var scope = await GetAccessibleSectionIdsAsync(userId, ct);
        if (scope is null) return;

        var sampleExists = await _db.Samples
            .AsNoTracking()
            .AnyAsync(s => s.Id == sampleId, ct);

        if (!sampleExists) return;

        var pathogenSectionIds = await (
            from t in _db.TestOrders.AsNoTracking()
            join td in _db.TestDefinitions.AsNoTracking() on t.TestCode equals td.Code
            where t.SampleId == sampleId
                  && !t.IsSuperseded
                  && td.WorkflowType == WorkflowType.Observation
            select t.SectionId
        ).ToListAsync(ct);

        if (pathogenSectionIds.Any(s => !scope.Contains(s)))
        {
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");
        }
    }

    public async Task EnsureMaterialAccessAsync(int userId, int materialId, CancellationToken ct = default)
    {
        var scope = await GetAccessibleSectionIdsAsync(userId, ct);
        if (scope is null) return;

        var sectionId = await _db.Materials
            .AsNoTracking()
            .Where(m => m.Id == materialId)
            .Select(m => (int?)m.SectionId)
            .FirstOrDefaultAsync(ct);

        if (sectionId is null) return;

        if (!scope.Contains(sectionId.Value))
        {
            throw new UnauthorizedAccessException("This material belongs to a laboratory section you are not assigned to.");
        }
    }

    private const string OtherSectionsMaterial = "This record was made from another laboratory section's material and you are not assigned to that section.";

    private async Task EnsureSectionAsync(int userId, Func<Task<int?>> sectionOf, CancellationToken ct)
    {
        var scope = await GetAccessibleSectionIdsAsync(userId, ct);
        if (scope is null) return;

        var sectionId = await sectionOf();
        if (sectionId is null) return; // not found - left to the caller's own not-found handling

        if (!scope.Contains(sectionId.Value))
            throw new UnauthorizedAccessException(OtherSectionsMaterial);
    }

    public Task EnsureMediaAccessAsync(int userId, int mediaId, CancellationToken ct = default) =>
        EnsureSectionAsync(userId, () => _db.Media.AsNoTracking()
            .Where(m => m.Id == mediaId).Select(m => (int?)m.Material!.SectionId).FirstOrDefaultAsync(ct), ct);

    public Task EnsureCryovialAccessAsync(int userId, int cryovialId, CancellationToken ct = default) =>
        EnsureSectionAsync(userId, () => _db.Cryovials.AsNoTracking()
            .Where(c => c.Id == cryovialId).Select(c => (int?)c.Material!.SectionId).FirstOrDefaultAsync(ct), ct);

    public Task EnsureMediaEvaluationAccessAsync(int userId, int evaluationId, CancellationToken ct = default) =>
        EnsureSectionAsync(userId, () => _db.MediaEvaluations.AsNoTracking()
            .Where(e => e.Id == evaluationId).Select(e => (int?)e.Media!.Material!.SectionId).FirstOrDefaultAsync(ct), ct);

    public Task EnsureMediaEvaluationChallengeAccessAsync(int userId, int challengeId, CancellationToken ct = default) =>
        EnsureSectionAsync(userId, () => _db.MediaEvaluationChallenges.AsNoTracking()
            .Where(c => c.Id == challengeId)
            .Select(c => (int?)_db.MediaEvaluations.Where(e => e.Id == c.MediaEvaluationId).Select(e => e.Media!.Material!.SectionId).FirstOrDefault())
            .FirstOrDefaultAsync(ct), ct);

    public async Task EnsureOosGroupAccessAsync(int userId, string oosGroupCode, CancellationToken ct = default)
    {
        var scope = await GetAccessibleSectionIdsAsync(userId, ct);
        if (scope is null) return;

        var groupSections = await _db.TestOrders.AsNoTracking()
            .Where(t => t.Sample!.OosGroupCode == oosGroupCode && t.Sample.OriginSampleId != null)
            .Select(t => t.SectionId)
            .Distinct()
            .ToListAsync(ct);
        if (groupSections.Count == 0) return; // unknown group - left to not-found handling

        if (!groupSections.Any(scope.Contains))
            throw new UnauthorizedAccessException("This OOS investigation belongs to a laboratory section you are not assigned to.");
    }
}
