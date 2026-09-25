using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Workflows;

// Finished Product (FP section) tests have no Test Preparation stage - the
// sample is weighed and diluted inside the method itself and recorded with
// the result. Only tests of other sections (Microbiology) need the sample's
// preparation signed off before they start. A sample whose tests are all
// FP tests is therefore Ready from the moment it is received; a mixed
// sample still needs preparation, but only for its non-FP tests.
public static class PreparationRules
{
    public const string NoPreparationSectionCode = "FP";

    public static async Task<bool> TestOrderSkipsPreparationAsync(MicroLimsDbContext db, int testOrderId) =>
        await db.TestOrders.AnyAsync(t => t.Id == testOrderId && t.Section!.Code == NoPreparationSectionCode);

    public static async Task<SamplePreparationStatus> InitialStatusAsync(MicroLimsDbContext db, IEnumerable<int> testSectionIds)
    {
        var ids = testSectionIds.Distinct().ToList();
        if (ids.Count == 0)
            return SamplePreparationStatus.NeedsPreparation;

        var needsPreparation = await db.DocumentSections
            .AnyAsync(s => ids.Contains(s.Id) && s.Code != NoPreparationSectionCode);
        return needsPreparation ? SamplePreparationStatus.NeedsPreparation : SamplePreparationStatus.Ready;
    }
}
