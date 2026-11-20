using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
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

    // The number is "this month's count + 1", so it is only safe if the next
    // receipt cannot count before this one's sample is committed. On
    // PostgreSQL the caller therefore must have a transaction open that spans
    // both this call and the insert of the sample (UnitOfWork.RunAsync); this
    // takes a transaction-scoped advisory lock, per category and month, that
    // is released at commit or rollback. Other categories and months do not
    // wait. Without a transaction the lock would be released at once, so that
    // is refused. Non-relational providers (unit tests) take no lock.
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
        await LockAsync(prefix);

        var monthStart = TimeZoneInfo.ConvertTimeToUtc(labMonthStart, _labClock.LabTimeZone);
        var monthEnd = TimeZoneInfo.ConvertTimeToUtc(labMonthStart.AddMonths(1), _labClock.LabTimeZone);

        var countThisMonth = await _db.Samples.CountAsync(s =>
            s.Category == category && s.ReceivedAt >= monthStart && s.ReceivedAt < monthEnd);

        var sequence = (countThisMonth + 1).ToString("D3");
        return $"{prefix}{sequence}";
    }

    private async Task LockAsync(string prefix)
    {
        if (!_db.Database.IsRelational())
            return;
        if (_db.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "A sample reference number can only be generated inside a database transaction that also saves the sample.");

        var key = $"sample-ref:{prefix}";
        var seed = AdvisoryLockKeys.SampleNumber;
        await _db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, {seed}))");
    }

    public async Task<string> GenerateOosCodeAsync(CancellationToken cancellationToken = default)
    {
        // The production transaction must span allocation and persistence.
        // In-memory workflow tests use the same sequence without database locks.
        if (_db.Database.IsRelational())
        {
            if (_db.Database.CurrentTransaction is null)
                throw new InvalidOperationException(
                    "An OOS number can only be generated inside a database transaction that also saves the record.");

            await _db.Database.ExecuteSqlAsync(
                $"SELECT pg_advisory_xact_lock({AdvisoryLockKeys.OosNumber})", cancellationToken);
        }

        var labNow = _labClock.ToLabLocal(_time.GetUtcNow().UtcDateTime);
        var prefix = FormattableString.Invariant($"OOS{labNow:MMyy}");
        var legacyNumbers = _db.Samples
            .Where(s => s.OosGroupCode != null && s.OosGroupCode.StartsWith(prefix))
            .Select(s => s.OosGroupCode);
        var investigationNumbers = _db.OosInvestigations
            .Where(i => i.Number.StartsWith(prefix))
            .Select(i => (string?)i.Number);

        var count = await legacyNumbers.Union(investigationNumbers).CountAsync(cancellationToken);
        return FormattableString.Invariant($"{prefix}{count + 1:D3}");
    }
}
