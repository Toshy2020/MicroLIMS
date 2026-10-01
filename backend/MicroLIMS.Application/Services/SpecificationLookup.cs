using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services;

// Which Specification rows apply to a sample. An item can carry
// stage-specific rows for a test (e.g. Appearance at Bulk vs Finished,
// keyed on Specification.ProductionStageRole) alongside rows for every
// stage (ProductionStageRole null). A sample whose stage role has rows
// of its own for the test gets those; any other sample - including one
// with no reconciled stage - gets the every-stage rows. The two sets are
// never mixed.
public static class SpecificationLookup
{
    // The stage role of the sample's ProductionStage, or null when it has
    // none (not a Finished Product sample, or an unreconciled stage).
    public static async Task<ProductionStageRole?> SampleStageRoleAsync(IMicroLimsDbContext db, int sampleId, CancellationToken cancellationToken = default) =>
        await db.Samples
            .Where(s => s.Id == sampleId && s.ProductionStageRef != null)
            .Select(s => (ProductionStageRole?)s.ProductionStageRef!.Role)
            .FirstOrDefaultAsync(cancellationToken);

    // The ProductionStageRole value the applicable rows carry: the
    // sample's role when the item has rows for it on this test, else null
    // (the every-stage rows). Filter with `s.ProductionStageRole == result`.
    public static async Task<ProductionStageRole?> ApplicableStageAsync(IMicroLimsDbContext db, int sampleId, int itemId, string testCode, CancellationToken cancellationToken = default)
    {
        var role = await SampleStageRoleAsync(db, sampleId, cancellationToken);
        if (role is null) return null;

        var hasStageRows = await db.Specifications.AnyAsync(
            s => s.ItemId == itemId && s.TestCode == testCode && s.ProductionStageRole == role,
            cancellationToken);
        return hasStageRows ? role : null;
    }

    // The same rule over rows already loaded for one test.
    public static List<Specification> SelectForStage(IEnumerable<Specification> specsForTest, ProductionStageRole? stageRole)
    {
        var list = specsForTest.ToList();
        var applicable = stageRole is not null && list.Any(s => s.ProductionStageRole == stageRole) ? stageRole : null;
        return list.Where(s => s.ProductionStageRole == applicable).ToList();
    }

    // The applicable rows for a sample's test, in display order.
    public static async Task<List<Specification>> ForSampleAsync(IMicroLimsDbContext db, int sampleId, int itemId, string testCode, CancellationToken cancellationToken = default)
    {
        var stage = await ApplicableStageAsync(db, sampleId, itemId, testCode, cancellationToken);
        return await db.Specifications
            .Include(s => s.Stages)
            .Where(s => s.ItemId == itemId && s.TestCode == testCode && s.ProductionStageRole == stage)
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    public static async Task<Specification?> PrimaryAsync(IMicroLimsDbContext db, int sampleId, int itemId, string testCode, CancellationToken cancellationToken = default)
    {
        var stage = await ApplicableStageAsync(db, sampleId, itemId, testCode, cancellationToken);
        return await db.Specifications
            .Where(s => s.ItemId == itemId && s.TestCode == testCode && s.ProductionStageRole == stage)
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
