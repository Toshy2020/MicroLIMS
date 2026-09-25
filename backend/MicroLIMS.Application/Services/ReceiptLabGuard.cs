using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Which laboratories a receipt may create tests for. The main Receiving
// page (Samples.Receive) may target any active laboratory; receiving from
// inside a lab's own workspace may target only the labs the user is a
// member of, so a lab can never create another lab's work.
public static class ReceiptLabGuard
{
    public static async Task<IReadOnlyCollection<int>> ResolveTargetsAsync(
        MicroLimsDbContext db, IUserSectionScopeService scope, int userId,
        bool canReceiveForAnyLab, IReadOnlyCollection<int>? requested)
    {
        var ids = requested?.Distinct().ToList() ?? new List<int>();
        if (ids.Count == 0)
            throw new InvalidOperationException("Choose at least one laboratory to receive the sample for.");

        var known = await db.DocumentSections.Where(s => ids.Contains(s.Id) && s.IsActive).Select(s => s.Id).ToListAsync();
        if (known.Count != ids.Count)
            throw new InvalidOperationException("One or more selected laboratories do not exist.");

        if (!canReceiveForAnyLab)
        {
            var userScope = await scope.GetAccessibleSectionIdsAsync(userId);
            if (userScope is not null && ids.Any(id => !userScope.Contains(id)))
                throw new UnauthorizedAccessException("You can receive samples only for your own laboratory.");
        }
        return ids;
    }
}
