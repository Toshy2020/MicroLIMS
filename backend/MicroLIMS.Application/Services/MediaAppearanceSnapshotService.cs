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
// MediaConfiguration.Name is populated from Material.MaterialName at
// creation time (see the Media Configuration Migration plan Phase 3, and
// the MediaConfiguration admin page's Material picker) rather than typed
// independently on a separate admin page the way the old
// MediaChallengeSpec.MaterialName was - that independence is what let the
// two live mismatches (Burkholderia's leading space, Tryptic Soy Agar's
// suffix) drift silently for real. A product can have more than one
// MediaConfiguration row (different incubation profiles); every row
// sharing a Name carries the same challenge organisms (Phase 3 duplicated
// them for exactly this reason), so matching on any one of them is enough.
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
        var materialName = await _db.Materials
            .Where(m => m.Id == materialId)
            .Select(m => m.MaterialName)
            .FirstOrDefaultAsync(cancellationToken);

        if (materialName is null)
        {
            _logger.LogWarning("No material {MaterialId} - appearance snapshot recorded as null.", materialId);
            return null;
        }

        var expected = await _db.MediaConfigurationChallenges
            .Where(c => c.MediaConfiguration!.Name == materialName && c.OrganismId == organismId)
            .Select(c => c.ExpectedDescription)
            .FirstOrDefaultAsync(cancellationToken);

        if (expected is null)
            _logger.LogWarning(
                "No MediaConfigurationChallenge for material '{MaterialName}' and organism {OrganismId} - appearance snapshot recorded as null.",
                materialName, organismId);

        return expected;
    }
}
