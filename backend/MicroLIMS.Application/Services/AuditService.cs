using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Read-only access to the audit trail captured automatically by
// MicroLimsDbContext.SaveChanges. Nothing here ever deletes a record.
public class AuditService
{
    private readonly MicroLimsDbContext _db;

    public AuditService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<AuditLogDto>> GetForEntityAsync(string entityName, string entityId)
    {
        var logs = await _db.AuditLogs
            .Where(a => a.EntityName == entityName && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();

        var userIds = logs.Select(l => l.UserId).Distinct().Where(id => id > 0).ToList();
        var userMap = await _db.Users
            .Include(u => u.Role)
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        return logs.Select(l =>
        {
            userMap.TryGetValue(l.UserId, out var user);
            var name = user?.FullName ?? (l.UserId == 0 ? "System" : $"User #{l.UserId}");
            var role = user?.Role?.Name;
            var username = user?.Username;

            return new AuditLogDto(
                l.Id,
                l.EntityName,
                l.EntityId,
                l.Action,
                l.PreviousValue,
                l.NewValue,
                l.UserId,
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
}
