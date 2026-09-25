using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// A Specification row carries no lab of its own - its lab is whichever
// section owns the Test Master row for its TestCode (TestDefinitions.SectionId).
// Item.Specifications is one shared list for both labs; only the owning
// lab's Section Head (or a System Administrator, whose scope is null/
// unrestricted) may add, edit or delete a row - the other lab sees it
// read-only (see MasterDataController specifications endpoints).
public static class SpecificationOwnership
{
    public static async Task EnsureCanEditAsync(
        MicroLimsDbContext db, IUserSectionScopeService scope, int userId, string testCode)
    {
        var scopeIds = await scope.GetAccessibleSectionIdsAsync(userId);
        if (scopeIds is null) return; // System Administrator - unrestricted

        var testDef = await db.TestDefinitions
            .AsNoTracking()
            .Include(t => t.Section)
            .FirstOrDefaultAsync(t => t.Code == testCode);

        if (testDef is null)
            throw new InvalidOperationException($"Test code '{testCode}' is not in the Test Master.");

        if (!scopeIds.Contains(testDef.SectionId))
        {
            var labName = testDef.Section?.Name ?? "another laboratory section";
            throw new UnauthorizedAccessException(
                $"This specification belongs to {labName} - only its Section Head can change it.");
        }
    }
}
