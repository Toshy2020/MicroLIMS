using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Writes DataExportLog rows - the only place anything in the Reports
// module is allowed to touch that table. Kept separate from the
// read-only ReportingQueryService so that service's "never writes"
// contract stays true.
public class DataExportAuditService
{
    private readonly MicroLimsDbContext _db;

    public DataExportAuditService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task LogExportAsync(int userId, string filterJson, int rowCount, string exportType)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        _db.DataExportLogs.Add(new DataExportLog
        {
            UserId = userId,
            UserName = user?.FullName ?? string.Empty,
            ExportedAt = DateTime.UtcNow,
            FilterJson = filterJson,
            RowCount = rowCount,
            ExportType = exportType
        });

        await _db.SaveChangesAsync();
    }
}
