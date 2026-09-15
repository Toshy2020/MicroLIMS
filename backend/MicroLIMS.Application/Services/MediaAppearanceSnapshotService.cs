using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Reads the expected colony appearance for a medium/organism pair at the
// moment an observation is submitted. The caller stores the returned
// string on the result row and never updates it again - it is the
// criteria as they stood when the analyst looked at the plate
// (ALCOA+ Original and Contemporaneous).
//
// Resolution links the batch's Material.MediaProductId directly to
// MediaConfiguration.MediaProductId, eliminating any name matching.
// A product can have more than one MediaConfiguration row (different
// incubation profiles); every row of a product carries the same challenge
// organisms, so matching on any one of them is enough.
public class MediaAppearanceSnapshotService
{
    private readonly MicroLimsDbContext _db;
    private readonly ILogger<MediaAppearanceSnapshotService> _logger;

    public MediaAppearanceSnapshotService(MicroLimsDbContext db, ILogger<MediaAppearanceSnapshotService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<string?> GetExpectedAppearanceSnapshotAsync(
        int materialId, int organismId, CancellationToken cancellationToken = default)
    {
        var productId = await _db.Materials
            .Where(m => m.Id == materialId)
            .Select(m => m.MediaProductId)
            .FirstOrDefaultAsync(cancellationToken);

        if (productId is null)
        {
            _logger.LogWarning("No media product linked for material {MaterialId} - appearance snapshot recorded as null.", materialId);
            return null;
        }

        var expected = await _db.MediaConfigurationChallenges
            .Where(c => c.MediaConfiguration!.MediaProductId == productId && c.OrganismId == organismId)
            .Select(c => c.ExpectedDescription)
            .FirstOrDefaultAsync(cancellationToken);

        if (expected is null)
            _logger.LogWarning(
                "No MediaConfigurationChallenge for media product {ProductId} and organism {OrganismId} - appearance snapshot recorded as null.",
                productId, organismId);

        return expected;
    }
}
