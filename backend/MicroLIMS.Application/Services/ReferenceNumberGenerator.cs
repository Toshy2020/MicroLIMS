using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

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

    private readonly MicroLimsDbContext _db;

    public ReferenceNumberGenerator(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<string> GenerateAsync(SampleCategory category)
    {
        var code = CategoryCodes.TryGetValue(category, out var c) ? c : "SM";
        var now = DateTime.UtcNow;
        var mm = now.ToString("MM");
        var yy = now.ToString("yy");
        var prefix = $"{code}{mm}{yy}";

        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var countThisMonth = await _db.Samples.CountAsync(s =>
            s.Category == category && s.ReceivedAt >= monthStart && s.ReceivedAt < monthEnd);

        var sequence = (countThisMonth + 1).ToString("D3");
        return $"{prefix}{sequence}";
    }

    public async Task<string> GenerateOosCodeAsync()
    {
        var now = DateTime.UtcNow;
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
