using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;

namespace MicroLIMS.Application.Services;

// Format: {CategoryCode}{MM}{YY}{seq:D3}, e.g. FP0107026 = the 1st
// Finished Product sample received in July 2026. Sequence resets
// monthly. Replaces the old paper "Samples Receiving Record Page No."
public class ReferenceNumberGenerator
{
    private static readonly Dictionary<SampleCategory, string> CategoryCodes = new()
    {
        [SampleCategory.FinishedProduct] = "FP",
        [SampleCategory.RawMaterial] = "RM",
        [SampleCategory.PackagingMaterial] = "PM",
        [SampleCategory.Water] = "WT",
        [SampleCategory.EnvironmentalMonitoring] = "EM",
        [SampleCategory.AfterCleaning] = "AC"
    };

    private readonly IMicroLimsDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILabClock _labClock;

    public ReferenceNumberGenerator(IMicroLimsDbContext db, TimeProvider? timeProvider = null, ILabClock? labClock = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
        _labClock = labClock ?? new LabClock(_time, LabClock.ResolveTimeZone("Africa/Cairo"));
    }

    public async Task<string> GenerateAsync(SampleCategory category)
    {
        var code = CategoryCodes.TryGetValue(category, out var c) ? c : "SM";
        // The month rolls over at lab midnight, not UTC midnight, so a
        // sample received just after midnight in Cairo on the 1st is
        // numbered in the new month.
        var labNow = _labClock.ToLabLocal(_time.GetUtcNow().UtcDateTime);
        var mm = labNow.ToString("MM");
        var yy = labNow.ToString("yy");
        var prefix = $"{code}{mm}{yy}";

        var labMonthStart = new DateTime(labNow.Year, labNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var monthStart = TimeZoneInfo.ConvertTimeToUtc(labMonthStart, _labClock.LabTimeZone);
        var monthEnd = TimeZoneInfo.ConvertTimeToUtc(labMonthStart.AddMonths(1), _labClock.LabTimeZone);

        var countThisMonth = await _db.Samples.CountAsync(s =>
            s.Category == category && s.ReceivedAt >= monthStart && s.ReceivedAt < monthEnd);

        var sequence = (countThisMonth + 1).ToString("D3");
        return $"{prefix}{sequence}";
    }

    public async Task<string> GenerateOosCodeAsync()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var mm = now.ToString("MM");
        var yy = now.ToString("yy");
        var prefix = $"OOS{mm}{yy}";

        var countThisMonth = await _db.Samples
            .Where(s => s.OosGroupCode != null && s.OosGroupCode.StartsWith(prefix))
            .Select(s => s.OosGroupCode)
            .Distinct()
            .CountAsync();

        var sequence = (countThisMonth + 1).ToString("D3");
        return $"{prefix}{sequence}";
    }
}
