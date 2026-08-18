using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

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
    int Take = 200);

public record AuditLogDto(
    int Id,
    string EntityName,
    string EntityId,
    string Action,
    string? PreviousValue,
    string? NewValue,
    int UserId,
    string UserName,
    string? UserRole,
    string? UserUsername,
    DateTime Timestamp,
    string? BatchNumber,
    string? ControlNumber,
    string? SampleReferenceNumber,
    string? MediaLotNumber,
    string? ReferenceStrainCode,
    string? CryovialCode,
    int? SampleId,
    int? TestOrderId);

public class AuditSearchService
{
    private readonly MicroLimsDbContext _db;

    public AuditSearchService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<AuditLogDto>> SearchAsync(AuditSearchRequest r)
    {
        var query = _db.AuditLogs.AsQueryable();

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

        var logs = await query.OrderByDescending(a => a.Timestamp).Take(r.Take).ToListAsync();

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
