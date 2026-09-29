using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;

namespace MicroLIMS.Application.Services;

// Read-only access to the audit trail captured automatically by
// IMicroLimsDbContext.SaveChanges. Nothing here ever deletes a record.
public class AuditService
{
    private readonly IMicroLimsDbContext _db;

    public AuditService(IMicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<AuditLogDto>> GetForEntityAsync(string entityName, string entityId)
    {
        var logs = await _db.AuditLogs
            .Where(a => a.EntityName == entityName && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();

        var userIds = logs.Select(l => l.UserId).Where(id => id.HasValue && id.Value > 0).Select(id => id!.Value).Distinct().ToList();
        var userMap = await _db.Users
            .Include(u => u.Role)
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

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
                l.TestOrderId);
        }).ToList();
    }

    // Newest first, optionally for one username.
    public async Task<List<LoginHistoryResponse>> GetLoginHistoryAsync(string? username, int take)
    {
        var query = _db.LoginHistories.AsQueryable();
        if (!string.IsNullOrWhiteSpace(username)) query = query.Where(l => l.Username == username);
        return (await query.OrderByDescending(l => l.Timestamp).Take(take).ToListAsync()).Select(LoginHistoryResponse.From).ToList();
    }
}
