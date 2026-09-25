using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// A test may only use media prepared from its own laboratory section's
// material (REQ-DEPT-023): using another section's lot is a traceability
// error, not a convenience. A prepared lot (Media) and a cryovial belong to
// the section of the Material they were made from.
public static class SectionMediaRule
{
    public static void EnsureLot(Media lot, int sectionId)
    {
        var lotSection = lot.Material?.SectionId
            ?? throw new InvalidOperationException($"Media lot \"{lot.LotNumber}\" has no source material loaded.");
        if (lotSection != sectionId)
            throw new InvalidOperationException(
                $"Media lot \"{lot.LotNumber}\" was prepared from another laboratory section's material and cannot be used for this test.");
    }

    public static async Task EnsureLotForTestOrderAsync(MicroLimsDbContext db, Media lot, int testOrderId, CancellationToken ct = default)
    {
        var sectionId = await db.TestOrders.Where(t => t.Id == testOrderId).Select(t => t.SectionId).FirstAsync(ct);
        EnsureLot(lot, sectionId);
    }

    public static async Task EnsureMaterialsAsync(MicroLimsDbContext db, IEnumerable<int> materialIds, int sectionId, CancellationToken ct = default)
    {
        var ids = materialIds.Distinct().ToList();
        var foreign = await db.Materials
            .Where(m => ids.Contains(m.Id) && m.SectionId != sectionId)
            .Select(m => m.MaterialName)
            .ToListAsync(ct);
        if (foreign.Count > 0)
            throw new InvalidOperationException(
                $"{string.Join(", ", foreign.Select(n => $"\"{n}\""))} belongs to another laboratory section and cannot be used for this test.");
    }

    public static async Task EnsureLotsAsync(MicroLimsDbContext db, IEnumerable<int> mediaLotIds, int sectionId, CancellationToken ct = default)
    {
        var ids = mediaLotIds.Distinct().ToList();
        var foreign = await db.Media
            .Where(m => ids.Contains(m.Id) && m.Material!.SectionId != sectionId)
            .Select(m => m.LotNumber)
            .ToListAsync(ct);
        if (foreign.Count > 0)
            throw new InvalidOperationException(
                $"Media lot {string.Join(", ", foreign.Select(n => $"\"{n}\""))} was prepared from another laboratory section's material and cannot be used for this test.");
    }
}
