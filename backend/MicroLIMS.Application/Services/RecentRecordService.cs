using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

public sealed class RecentRecordService : IRecentRecordService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IRecordLabelResolver _labels;
    private readonly TimeProvider _time;

    public RecentRecordService(IMicroLimsDbContext db, IRecordLabelResolver labels, TimeProvider? timeProvider = null)
    {
        _db = db;
        _labels = labels;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task TouchAsync(int userId, string recordType, int recordId, CancellationToken ct = default)
    {
        RequireKnownType(recordType);

        // Unknown and unreadable look the same to the caller, so the endpoint cannot be used to probe.
        var readable = await _labels.ResolveAsync(userId, new[] { (recordType, recordId) }, ct);
        if (!readable.ContainsKey((recordType, recordId))) return;

        var now = _time.GetUtcNow().UtcDateTime;
        var keepOthers = RecentRecord.MaxPerUser - 1;

        // One statement: upsert this record, and delete everything beyond the newest 20. A data-modifying CTE
        // does not see its own insert in the other branch, so the keep set is "the newest 19 others" and this row.
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
WITH upserted AS (
    INSERT INTO ""RecentRecords"" (""UserId"", ""RecordType"", ""RecordId"", ""OpenedAt"")
    VALUES ({userId}, {recordType}, {recordId}, {now})
    ON CONFLICT (""UserId"", ""RecordType"", ""RecordId"") DO UPDATE SET ""OpenedAt"" = EXCLUDED.""OpenedAt""
    RETURNING 1
)
DELETE FROM ""RecentRecords"" r
WHERE r.""UserId"" = {userId}
  AND NOT (r.""RecordType"" = {recordType} AND r.""RecordId"" = {recordId})
  AND (r.""RecordType"", r.""RecordId"") NOT IN (
      SELECT k.""RecordType"", k.""RecordId"" FROM ""RecentRecords"" k
      WHERE k.""UserId"" = {userId}
        AND NOT (k.""RecordType"" = {recordType} AND k.""RecordId"" = {recordId})
      ORDER BY k.""OpenedAt"" DESC, k.""RecordType"", k.""RecordId""
      LIMIT {keepOthers})", ct);
    }

    public async Task<IReadOnlyList<RecentRecordDto>> ListAsync(int userId, CancellationToken ct = default)
    {
        var rows = await _db.RecentRecords.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.OpenedAt).ThenBy(r => r.RecordType).ThenBy(r => r.RecordId)
            .Take(RecentRecord.MaxPerUser)
            .Select(r => new { r.RecordType, r.RecordId, r.OpenedAt })
            .ToListAsync(ct);
        if (rows.Count == 0) return Array.Empty<RecentRecordDto>();

        var labels = await _labels.ResolveAsync(userId, rows.Select(r => (r.RecordType, r.RecordId)).ToList(), ct);

        var result = new List<RecentRecordDto>(rows.Count);
        foreach (var r in rows)
        {
            // Unresolved = deleted, or the user may no longer read it: the row is dropped and its label never shown.
            if (!labels.TryGetValue((r.RecordType, r.RecordId), out var label)) continue;
            result.Add(new RecentRecordDto(r.RecordType, r.RecordId, label.Identifier, label.Title,
                DateTime.SpecifyKind(r.OpenedAt, DateTimeKind.Utc)));
        }
        return result;
    }

    public async Task ClearAsync(int userId, CancellationToken ct = default) =>
        await _db.RecentRecords.Where(r => r.UserId == userId).ExecuteDeleteAsync(ct);

    private static void RequireKnownType(string recordType)
    {
        if (!RecentRecordTypes.IsKnown(recordType))
            throw new BusinessRuleException($"Unknown record type. Use one of: {string.Join(", ", RecentRecordTypes.All)}.");
    }
}
