using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public static class SpecificationLookup
{
    public static async Task<Specification?> PrimaryAsync(MicroLimsDbContext db, int itemId, string testCode, CancellationToken cancellationToken = default)
    {
        return await db.Specifications
            .Where(s => s.ItemId == itemId && s.TestCode == testCode)
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
