using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;

namespace MicroLIMS.Application.Services;

public record AuditSearchRequest(
    DateTime? FromDate,
    DateTime? ToDate,
    string? BatchNumber,
    string? ControlNumber,
    string? MediaLotNumber,
    string? SampleReferenceNumber,
    string? ReferenceStrainCode,
    string? CryovialCode,
    int? SampleId,
    int? TestOrderId,
    int? UserId,
    string? EntityName,
    string? Action,
    int Take = 200,
    // Keyset cursor: return only rows with Id below this (the last Id of the previous page).
    int? BeforeId = null,
    AuditActionCategory? ActionCategory = null,
    ActorType? ActorType = null,
    Guid? CorrelationId = null,
    // Case-insensitive "contains" on the recorded reason.
    string? Reason = null,
    // True: only rows whose EntityId is negative (a temporary key captured before the save, decision D8).
    bool? NegativeEntityId = null);

public record AuditSearchPage(List<AuditLogDto> Items, int? NextCursor, bool HasMore);

public record AuditLogDto(
    int Id,
    string EntityName,
    string EntityId,
    string Action,
    string? PreviousValue,
    string? NewValue,
    int UserId,
    string UserName,
    // The user's role TODAY, not the one they held when the row was written
    // (the audit trail does not record that), so it can differ from history.
    string? UserCurrentRole,
    string? UserUsername,
    DateTime Timestamp,
    string? BatchNumber,
    string? ControlNumber,
    string? SampleReferenceNumber,
    string? MediaLotNumber,
    string? ReferenceStrainCode,
    string? CryovialCode,
    int? SampleId,
    int? TestOrderId,
    AuditActionCategory? ActionCategory = null,
    ActorType? ActorType = null,
    Guid? CorrelationId = null,
    string? Reason = null,
    // The EntityId is negative: a temporary EF key was recorded instead of the real one.
    bool EntityIdNegative = false,
    // Id of the appended correction row for a negative-EntityId row, when one exists.
    int? CorrectedByAuditLogId = null);

public record SecurityEventDto(
    long Id,
    DateTime OccurredAtUtc,
    string EventCode,
    string Outcome,
    int? ActorUserId,
    bool ActorIsSystem,
    int? TargetUserId,
    string? TargetUsername,
    string? CorrelationId,
    string? IpAddress,
    string? RequestPath,
    string Source,
    string? Reason);

public record SecurityEventPage(List<SecurityEventDto> Items, long? NextCursor, bool HasMore);

public record LoginHistoryPage(List<LoginHistoryResponse> Items, int? NextCursor, bool HasMore);

public class AuditSearchService
{
    // Upper bound on client-supplied Take: an unbounded value can pull the whole immutable audit table into memory.
    public const int MaxTake = 1000;

    private readonly IMicroLimsDbContext _db;

    public AuditSearchService(IMicroLimsDbContext db)
    {
        _db = db;
    }

    public const string CorrectionActionCode = "AuditEntityIdCorrection";

    // Back-compatible list form: the first page of the keyset search.
    public async Task<List<AuditLogDto>> SearchAsync(AuditSearchRequest r) => (await SearchPageAsync(r)).Items;

    // Newest first by Id (monotonic with insertion). The cursor is the Id of the last row returned.
    public async Task<AuditSearchPage> SearchPageAsync(AuditSearchRequest r)
    {
        var take = Math.Clamp(r.Take, 1, MaxTake);
        var rows = await ApplyFilters(r).OrderByDescending(a => a.Id).Take(take + 1).ToListAsync();
        var hasMore = rows.Count > take;
        if (hasMore) rows.RemoveAt(take);
        var items = await ToDtosAsync(rows);
        return new AuditSearchPage(items, hasMore && rows.Count > 0 ? rows[^1].Id : null, hasMore);
    }

    public Task<int> CountAsync(AuditSearchRequest r) => ApplyFilters(r with { BeforeId = null }).CountAsync();

    // Export reader: one keyset page at a time, newest first, bounded by the caller.
    public async Task<List<AuditLogDto>> ReadPageForExportAsync(AuditSearchRequest r, int take)
    {
        var rows = await ApplyFilters(r).OrderByDescending(a => a.Id).Take(Math.Clamp(take, 1, MaxTake)).ToListAsync();
        return await ToDtosAsync(rows);
    }

    private IQueryable<AuditLog> ApplyFilters(AuditSearchRequest r)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (r.BeforeId.HasValue) query = query.Where(a => a.Id < r.BeforeId.Value);
        if (r.FromDate.HasValue) query = query.Where(a => a.Timestamp >= r.FromDate.Value);
        if (r.ToDate.HasValue) query = query.Where(a => a.Timestamp <= r.ToDate.Value);
        if (!string.IsNullOrWhiteSpace(r.BatchNumber)) query = query.Where(a => a.BatchNumber == r.BatchNumber);
        if (!string.IsNullOrWhiteSpace(r.ControlNumber)) query = query.Where(a => a.ControlNumber == r.ControlNumber);
        if (!string.IsNullOrWhiteSpace(r.MediaLotNumber)) query = query.Where(a => a.MediaLotNumber == r.MediaLotNumber);
        if (!string.IsNullOrWhiteSpace(r.SampleReferenceNumber)) query = query.Where(a => a.SampleReferenceNumber == r.SampleReferenceNumber);
        if (!string.IsNullOrWhiteSpace(r.ReferenceStrainCode)) query = query.Where(a => a.ReferenceStrainCode == r.ReferenceStrainCode);
        if (!string.IsNullOrWhiteSpace(r.CryovialCode)) query = query.Where(a => a.CryovialCode == r.CryovialCode);
        if (r.SampleId.HasValue) query = query.Where(a => a.SampleId == r.SampleId);
        if (r.TestOrderId.HasValue) query = query.Where(a => a.TestOrderId == r.TestOrderId);
        if (r.UserId.HasValue) query = query.Where(a => a.UserId == r.UserId);
        if (!string.IsNullOrWhiteSpace(r.EntityName)) query = query.Where(a => a.EntityName == r.EntityName);
        if (!string.IsNullOrWhiteSpace(r.Action)) query = query.Where(a => a.Action == r.Action);
        if (r.ActionCategory.HasValue) query = query.Where(a => a.ActionCategory == r.ActionCategory);
        if (r.ActorType.HasValue) query = query.Where(a => a.ActorType == r.ActorType);
        if (r.CorrelationId.HasValue) query = query.Where(a => a.CorrelationId == r.CorrelationId);
        if (!string.IsNullOrWhiteSpace(r.Reason))
        {
            var needle = r.Reason.Trim().ToLower();
            query = query.Where(a => a.Reason != null && a.Reason.ToLower().Contains(needle));
        }
        if (r.NegativeEntityId == true) query = query.Where(a => a.EntityId.StartsWith("-"));

        return query;
    }

    // Every entry for one record, newest first - no Take limit.
    public async Task<List<AuditLogDto>> GetForEntityAsync(string entityName, string entityId)
    {
        var logs = await _db.AuditLogs
            .Where(a => a.EntityName == entityName && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
        return await ToDtosAsync(logs);
    }

    // Newest first, optionally for one username.
    public async Task<List<LoginHistoryResponse>> GetLoginHistoryAsync(string? username, int take) =>
        (await GetLoginHistoryPageAsync(username, take, null)).Items;

    public async Task<LoginHistoryPage> GetLoginHistoryPageAsync(string? username, int take, int? beforeId)
    {
        take = Math.Clamp(take, 1, MaxTake);
        var query = _db.LoginHistories.AsQueryable();
        if (!string.IsNullOrWhiteSpace(username)) query = query.Where(l => l.Username == username);
        if (beforeId.HasValue) query = query.Where(l => l.Id < beforeId.Value);
        var rows = await query.OrderByDescending(l => l.Id).Take(take + 1).ToListAsync();
        var hasMore = rows.Count > take;
        if (hasMore) rows.RemoveAt(take);
        return new LoginHistoryPage(rows.Select(LoginHistoryResponse.From).ToList(), hasMore ? rows[^1].Id : null, hasMore);
    }

    // The Security Audit Trail (separate from the GxP audit trail). Newest first, keyset on Id.
    public async Task<SecurityEventPage> GetSecurityEventsAsync(string? eventCode, string? username, DateTime? from, DateTime? to, int take, long? beforeId)
    {
        take = Math.Clamp(take, 1, MaxTake);
        var query = _db.SecurityAuditEvents.AsQueryable();
        if (!string.IsNullOrWhiteSpace(eventCode)) query = query.Where(e => e.EventCode == eventCode);
        if (!string.IsNullOrWhiteSpace(username)) query = query.Where(e => e.TargetUsername == username);
        if (from.HasValue) query = query.Where(e => e.OccurredAtUtc >= from.Value);
        if (to.HasValue) query = query.Where(e => e.OccurredAtUtc <= to.Value);
        if (beforeId.HasValue) query = query.Where(e => e.Id < beforeId.Value);
        var rows = await query.OrderByDescending(e => e.Id).Take(take + 1).ToListAsync();
        var hasMore = rows.Count > take;
        if (hasMore) rows.RemoveAt(take);
        var items = rows.Select(e => new SecurityEventDto(e.Id, e.OccurredAtUtc, e.EventCode, e.Outcome.ToString(),
            e.ActorUserId, e.ActorIsSystem, e.TargetUserId, e.TargetUsername, e.CorrelationId, e.IpAddress,
            e.RequestPath, e.Source, e.Reason)).ToList();
        return new SecurityEventPage(items, hasMore ? rows[^1].Id : null, hasMore);
    }

    private async Task<List<AuditLogDto>> ToDtosAsync(List<AuditLog> logs)
    {
        var userIds = logs.Select(l => l.UserId).Where(id => id.HasValue && id.Value > 0).Select(id => id!.Value).Distinct().ToList();
        var userMap = await _db.Users
            .Include(u => u.Role)
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var negativeIds = logs.Where(l => l.EntityId.StartsWith("-")).Select(l => l.Id.ToString()).ToList();
        var corrections = negativeIds.Count == 0
            ? new Dictionary<string, int>()
            : (await _db.AuditLogs
                .Where(c => c.ActionCode == CorrectionActionCode && c.EntityName == "AuditLog" && negativeIds.Contains(c.EntityId))
                .Select(c => new { c.EntityId, c.Id })
                .ToListAsync())
                .GroupBy(c => c.EntityId)
                .ToDictionary(g => g.Key, g => g.Min(c => c.Id));

        return logs.Select(l =>
        {
            User? user = null;
            if (l.UserId.HasValue) userMap.TryGetValue(l.UserId.Value, out user);
            var name = l.ActorType == Domain.Enums.ActorType.System
                ? (l.SystemProcessName ?? "System")
                : (user?.FullName ?? (l.UserId == null || l.UserId == 0 ? "System" : $"User #{l.UserId}"));
            var role = user?.Role?.Name;
            var username = user?.Username;

            return new AuditLogDto(
                l.Id,
                l.EntityName,
                l.EntityId,
                l.Action,
                l.PreviousValue,
                l.NewValue,
                l.UserId ?? 0,
                name,
                role,
                username,
                l.Timestamp,
                l.BatchNumber,
                l.ControlNumber,
                l.SampleReferenceNumber,
                l.MediaLotNumber,
                l.ReferenceStrainCode,
                l.CryovialCode,
                l.SampleId,
                l.TestOrderId,
                l.ActionCategory,
                l.ActorType,
                l.CorrelationId,
                l.Reason,
                l.EntityId.StartsWith("-"),
                corrections.TryGetValue(l.Id.ToString(), out var corrId) ? corrId : null);
        }).ToList();
    }
}
