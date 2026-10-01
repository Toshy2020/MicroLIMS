using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services;

// The section scope one dashboard view may draw from.
//   SectionIds: null = unrestricted (System Administrator, no lab chosen).
//   LabCode:    the laboratory the view is for, or null for the legacy
//               all-of-my-labs view.
public record DashboardLabScope(IReadOnlyList<int>? SectionIds, string? LabCode)
{
    public bool IsMicrobiology => string.Equals(LabCode, LaboratoryCodes.Microbiology, StringComparison.OrdinalIgnoreCase);
}

// Narrows a user's section scope to one laboratory, so a Microbiology
// dashboard never counts Physicochemical work and vice versa. Users who
// belong to both laboratories (and administrators) choose the laboratory per
// view; a user may only ask for a laboratory they belong to.
public class DashboardLabScopeService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;

    public DashboardLabScopeService(IMicroLimsDbContext db, IUserSectionScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<DashboardLabScope> ResolveAsync(int userId, string? labCode, CancellationToken ct = default)
    {
        var accessible = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (string.IsNullOrWhiteSpace(labCode))
        {
            return new DashboardLabScope(accessible, null);
        }

        if (!LaboratoryCodes.IsKnown(labCode))
        {
            throw new InvalidOperationException($"Unknown laboratory '{labCode}'.");
        }

        var code = labCode.Trim().ToUpperInvariant();
        var labSectionIds = await _db.DocumentSections
            .AsNoTracking()
            .Where(s => s.IsActive && s.Code == code)
            .Select(s => s.Id)
            .ToListAsync(ct);

        if (accessible is null)
        {
            return new DashboardLabScope(labSectionIds, code);
        }

        var allowed = labSectionIds.Where(accessible.Contains).ToList();
        if (allowed.Count == 0)
        {
            throw new UnauthorizedAccessException("You are not a member of this laboratory.");
        }

        return new DashboardLabScope(allowed, code);
    }
}
