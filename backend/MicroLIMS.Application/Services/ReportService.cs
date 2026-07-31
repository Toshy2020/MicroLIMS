using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Persistence.DbContext;
using System.Text.Json;

namespace MicroLIMS.Application.Services;

// Pulls real data for each report type, freezes it into a ReportSnapshot
// (so a previously issued report can always be reproduced identically -
// GMP requirement even if underlying records are later amended), then
// renders it to PDF. This is the plain fallback renderer used until the
// Section-Head-configurable Certificate template designer (Phase 5) is built.
public class ReportService : IReportService
{
    private readonly IPdfGenerator _pdfGenerator;
    private readonly MicroLimsDbContext _db;

    public ReportService(IPdfGenerator pdfGenerator, MicroLimsDbContext db)
    {
        _pdfGenerator = pdfGenerator;
        _db = db;
    }

    public async Task<byte[]> GenerateProductReportPdfAsync(int sampleId)
    {
        var sample = await LoadSample(sampleId);

        var lines = new List<string>
        {
            $"Reference: {sample.ReferenceNumber}",
            $"Item: {sample.Item?.Name}   Stage: {sample.ProductionStage}",
            $"Batch: {sample.BatchNumber}   Control: {sample.ControlNumber}",
            $"Cause: {sample.CauseOfTesting?.Name}   Received: {sample.ReceivedAt:dd-MMM-yyyy HH:mm}",
            $"Mfg: {sample.MfgDate:dd-MMM-yyyy}   Exp: {sample.ExpDate:dd-MMM-yyyy}",
            $"Status: {sample.Status}",
            ""
        };
        AppendTestLines(sample, lines);

        await SaveSnapshotAsync(sample.Category, sample.Id, SnapshotPayload(sample));
        return await _pdfGenerator.GenerateFromLinesAsync($"Product Report - {sample.ReferenceNumber}", lines);
    }

    public async Task<byte[]> GenerateWaterReportPdfAsync(DateTime date)
    {
        var samples = await _db.Samples
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.TestOrders).ThenInclude(t => t.Results)
            .Where(s => s.Category == SampleCategory.Water && s.ReceivedAt.Date == date.Date)
            .ToListAsync();

        var lines = new List<string> { $"Date: {date:dd-MMM-yyyy}", "" };
        foreach (var sample in samples)
        {
            foreach (var order in sample.TestOrders)
            {
                var latest = order.Results.OrderByDescending(r => r.EnteredAt).FirstOrDefault();
                lines.Add($"{sample.WaterSamplingPoint?.Code} - {order.TestCode}: {latest?.InterpretedValue ?? "(pending)"}");
            }
        }

        await SaveSnapshotAsync(SampleCategory.Water, 0, new { date, samples = samples.Select(s => s.ReferenceNumber) });
        return await _pdfGenerator.GenerateFromLinesAsync($"Water Daily Report - {date:dd-MMM-yyyy}", lines);
    }

    public async Task<byte[]> GenerateEMReportPdfAsync(DateTime date)
    {
        var monitorings = await _db.RoomMonitorings
            .Where(m => m.SampledAt.Date == date.Date)
            .Include(m => m.TestOrder)
            .ToListAsync();

        var lines = new List<string> { $"Date: {date:dd-MMM-yyyy}", "" };
        foreach (var m in monitorings)
        {
            lines.Add($"TestOrder #{m.TestOrderId}: Final count={m.Step2Count} " +
                      $"{(m.IsOutOfTrend ? "OUT OF TREND" : "Within trend")}");
        }

        await SaveSnapshotAsync(SampleCategory.EnvironmentalMonitoring, 0,
            new { date, monitorings = monitorings.Select(m => new { m.TestOrderId, m.Step2Count, m.IsOutOfTrend }) });

        return await _pdfGenerator.GenerateFromLinesAsync($"EM Daily Report - {date:dd-MMM-yyyy}", lines);
    }

    public async Task<byte[]> GenerateAfterCleaningReportPdfAsync(int sampleId)
    {
        var sample = await LoadSample(sampleId);

        var lines = new List<string>
        {
            $"Reference: {sample.ReferenceNumber}",
            $"Machine: {sample.Machine?.Name}",
            $"Cause: {sample.CauseOfTesting?.Name}   Received: {sample.ReceivedAt:dd-MMM-yyyy HH:mm}",
            ""
        };
        AppendTestLines(sample, lines);

        await SaveSnapshotAsync(SampleCategory.AfterCleaning, sample.Id, SnapshotPayload(sample));
        return await _pdfGenerator.GenerateFromLinesAsync($"After Cleaning Report - {sample.ReferenceNumber}", lines);
    }

    private async Task<Sample> LoadSample(int sampleId) =>
        await _db.Samples
            .Include(s => s.Item)
            .Include(s => s.Machine)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.CauseOfTesting)
            .Include(s => s.TestOrders).ThenInclude(t => t.Results)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

    private static void AppendTestLines(Sample sample, List<string> lines)
    {
        foreach (var order in sample.TestOrders)
        {
            var latest = order.Results.OrderByDescending(r => r.EnteredAt).FirstOrDefault();
            lines.Add($"{order.TestCode}: {order.Status} - Result: {latest?.InterpretedValue ?? latest?.RawValue ?? "(pending)"}");
        }
    }

    private static object SnapshotPayload(Sample sample) => new
    {
        sample.ReferenceNumber, sample.BatchNumber, sample.ControlNumber, sample.Status,
        TestOrders = sample.TestOrders.Select(t => new { t.TestCode, t.Status })
    };

    private async Task SaveSnapshotAsync(SampleCategory category, int relatedId, object data)
    {
        var report = new Report
        {
            Category = category,
            Title = $"{category} Report {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            GeneratedByUserId = 0
        };
        _db.Reports.Add(report);
        await _db.SaveChangesAsync();

        _db.ReportSnapshots.Add(new ReportSnapshot
        {
            ReportId = report.Id,
            Category = category,
            DataJson = JsonSerializer.Serialize(data)
        });
        await _db.SaveChangesAsync();
    }
}
