using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Corrects a Sample's BatchNumber/ControlNumber after receiving - a
// clerical fix, not a re-receive. Locked out once incubation has
// started for any of the sample's TestOrders, since at that point the
// identifiers are already tied to physical work in progress. Works
// across every category (Water/EM/After Cleaning too, not just
// Product/RM/PM), which is why this isn't on ReceivingService (Product-only,
// no DbContext).
public class SampleCorrectionService
{
    private readonly MicroLimsDbContext _db;

    public SampleCorrectionService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<SampleDto> CorrectAsync(int sampleId, string? batchNumber, string? controlNumber)
    {
        var sample = await _db.Samples
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.CauseOfTesting)
            .Include(s => s.TestOrders)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        var testOrderIds = sample.TestOrders.Select(t => t.Id).ToList();
        if (await _db.Incubations.AnyAsync(i => i.TestOrderId != null && testOrderIds.Contains(i.TestOrderId.Value)))
            throw new InvalidOperationException("This sample cannot be corrected because incubation has already started.");

        if (batchNumber is not null) sample.BatchNumber = batchNumber;
        if (controlNumber is not null) sample.ControlNumber = controlNumber;

        await _db.SaveChangesAsync();

        return TestingWorkspaceService.ToDto(sample);
    }
}
