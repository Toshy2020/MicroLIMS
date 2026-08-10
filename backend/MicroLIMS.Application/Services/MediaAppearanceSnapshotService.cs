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
// MediaChallengeSpec keys on MaterialName rather than MaterialId, so the
// material is resolved to its name first.
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

        var expected = await _db.MediaChallengeSpecs
            .Where(s => s.MaterialName == materialName && s.OrganismId == organismId)
            .Select(s => s.ExpectedDescription)
            .FirstOrDefaultAsync(cancellationToken);

        if (expected is null)
            _logger.LogWarning(
                "No MediaChallengeSpec for material '{MaterialName}' and organism {OrganismId} - appearance snapshot recorded as null.",
                materialName, organismId);

        return expected;
    }
}
