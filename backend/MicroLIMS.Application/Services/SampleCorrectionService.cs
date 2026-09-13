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

    public async Task<SampleDto> CorrectAsync(
        int sampleId,
        string? batchNumber,
        string? controlNumber,
        string? previousProductName = null,
        string? previousProductBatchNumber = null)
    {
        var sample = await _db.Samples
            .Include(s => s.OriginSample)
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

        if (batchNumber is not null)
        {
            sample.BatchNumber = batchNumber;
            if (sample.Category == Domain.Enums.SampleCategory.AfterCleaning)
            {
                sample.PreviousProductBatchNumber = batchNumber;
            }
        }
        if (controlNumber is not null) sample.ControlNumber = controlNumber;
        if (previousProductName is not null) sample.PreviousProductName = previousProductName;
        if (previousProductBatchNumber is not null)
        {
            sample.PreviousProductBatchNumber = previousProductBatchNumber;
            sample.BatchNumber = previousProductBatchNumber;
        }

        await _db.SaveChangesAsync();

        return TestingWorkspaceService.ToDto(sample);
    }

    // Voiding strikes the record - the sample was received in error or its
    // results cannot stand. It is deliberately NOT a rejection: rejection is a
    // Section Head's judgement that the material does not conform, so a void
    // records no approval decision, approver or decision time, and reports
    // and the certificate must never read it as one.
    public async Task<SampleDto> VoidAsync(int sampleId, string reason, int actingUserId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason for voiding is required.");

        var sample = await _db.Samples
            .Include(s => s.OriginSample)
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.CauseOfTesting)
            .Include(s => s.TestOrders)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        if (sample.Status == Domain.Enums.SampleStatus.Voided)
            throw new InvalidOperationException("Sample is already voided.");

        var trimmedReason = reason.Trim();
        sample.Status = Domain.Enums.SampleStatus.Voided;
        // Kept so the reason still shows wherever the sample's remarks are displayed.
        sample.CertificateRemarks = $"Voided: {trimmedReason}";

        // Superseded orders are already history - their retest lives on another sample.
        foreach (var order in sample.TestOrders.Where(t => !t.IsSuperseded))
        {
            order.Status = Domain.Enums.ApprovalStatus.Voided;
        }

        // Reports read the sample status off each result projection row.
        var records = await _db.ResultRecords.Where(r => r.SampleId == sampleId).ToListAsync();
        foreach (var record in records)
        {
            record.SampleStatus = Domain.Enums.SampleStatus.Voided;
            record.UpdatedAt = DateTime.UtcNow;
        }

        await ReviewEventLog.LogAsync(_db, ReviewEntityTypes.Sample, sampleId, actingUserId,
            Domain.Enums.ReviewWorkflowEventType.SampleVoided, trimmedReason);

        await _db.SaveChangesAsync();

        return TestingWorkspaceService.ToDto(sample);
    }
}
