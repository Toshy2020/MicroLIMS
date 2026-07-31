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

    public async Task<List<AuditLog>> GetForEntityAsync(string entityName, string entityId) =>
        await _db.AuditLogs
            .Where(a => a.EntityName == entityName && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
}
