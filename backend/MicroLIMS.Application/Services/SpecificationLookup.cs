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
//
// Grade (Specification.Grade; the sample's Grade is the one stated on the
// supplier's CoA) is chosen the same way and first: a sample whose grade has
// rows for the test gets those, otherwise the ungraded rows. The stage rule
// then applies inside the chosen grade set.
public static class SpecificationLookup
{
    // The stage role of the sample's ProductionStage, or null when it has
    // none (not a Finished Product sample, or an unreconciled stage).
    public static async Task<ProductionStageRole?> SampleStageRoleAsync(IMicroLimsDbContext db, int sampleId, CancellationToken cancellationToken = default) =>
        await db.Samples
            .Where(s => s.Id == sampleId && s.ProductionStageRef != null)
            .Select(s => (ProductionStageRole?)s.ProductionStageRef!.Role)
            .FirstOrDefaultAsync(cancellationToken);

    // The grade recorded at receiving, or null (ungraded / unknown sample).
    public static async Task<string?> SampleGradeAsync(IMicroLimsDbContext db, int sampleId, CancellationToken cancellationToken = default) =>
        await db.Samples
            .Where(s => s.Id == sampleId)
            .Select(s => s.Grade)
            .FirstOrDefaultAsync(cancellationToken);

    // The Grade value the applicable rows carry: the sample's grade when the
    // item has rows of that grade on this test, else null (the ungraded
    // rows). Filter with `s.Grade == result`.
    public static async Task<string?> ApplicableGradeAsync(IMicroLimsDbContext db, int sampleId, int itemId, string testCode, CancellationToken cancellationToken = default)
    {
        var grade = await SampleGradeAsync(db, sampleId, cancellationToken);
        if (grade is null) return null;

        var hasGradeRows = await db.Specifications.AnyAsync(
            s => s.ItemId == itemId && s.TestCode == testCode && s.Grade == grade,
            cancellationToken);
        return hasGradeRows ? grade : null;
    }

    // The ProductionStageRole value the applicable rows carry: the
    // sample's role when the item has rows for it on this test (within the
    // applicable grade), else null (the every-stage rows). Filter with
    // `s.ProductionStageRole == result`.
    public static async Task<ProductionStageRole?> ApplicableStageAsync(IMicroLimsDbContext db, int sampleId, int itemId, string testCode, CancellationToken cancellationToken = default)
    {
        var role = await SampleStageRoleAsync(db, sampleId, cancellationToken);
        if (role is null) return null;

        var grade = await ApplicableGradeAsync(db, sampleId, itemId, testCode, cancellationToken);
        var hasStageRows = await db.Specifications.AnyAsync(
            s => s.ItemId == itemId && s.TestCode == testCode && s.Grade == grade && s.ProductionStageRole == role,
            cancellationToken);
        return hasStageRows ? role : null;
    }

    // The same rules over rows already loaded for one test.
    public static List<Specification> SelectForStage(IEnumerable<Specification> specsForTest, ProductionStageRole? stageRole, string? sampleGrade = null)
    {
        var list = specsForTest.ToList();
        var grade = sampleGrade is not null && list.Any(s => s.Grade == sampleGrade) ? sampleGrade : null;
        list = list.Where(s => s.Grade == grade).ToList();
        var applicable = stageRole is not null && list.Any(s => s.ProductionStageRole == stageRole) ? stageRole : null;
        return list.Where(s => s.ProductionStageRole == applicable).ToList();
    }

    // The applicable rows for a sample's test, in display order.
    public static async Task<List<Specification>> ForSampleAsync(IMicroLimsDbContext db, int sampleId, int itemId, string testCode, CancellationToken cancellationToken = default)
    {
        var grade = await ApplicableGradeAsync(db, sampleId, itemId, testCode, cancellationToken);
        var stage = await ApplicableStageAsync(db, sampleId, itemId, testCode, cancellationToken);
        return await db.Specifications
            .Include(s => s.Stages)
            .Where(s => s.ItemId == itemId && s.TestCode == testCode && s.Grade == grade && s.ProductionStageRole == stage)
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    public static async Task<Specification?> PrimaryAsync(IMicroLimsDbContext db, int sampleId, int itemId, string testCode, CancellationToken cancellationToken = default)
    {
        var grade = await ApplicableGradeAsync(db, sampleId, itemId, testCode, cancellationToken);
        var stage = await ApplicableStageAsync(db, sampleId, itemId, testCode, cancellationToken);
        return await db.Specifications
            .Where(s => s.ItemId == itemId && s.TestCode == testCode && s.Grade == grade && s.ProductionStageRole == stage)
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
