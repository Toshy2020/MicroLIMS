using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services;

// Writes DataExportLog rows - the only place anything in the Reports
// module is allowed to touch that table. Kept separate from the
// read-only ReportingQueryService so that service's "never writes"
// contract stays true.
public class DataExportAuditService
{
    private readonly IMicroLimsDbContext _db;
    private readonly TimeProvider _time;

    public DataExportAuditService(IMicroLimsDbContext db, TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
    }

    public async Task LogExportAsync(int userId, string filterJson, int rowCount, string exportType)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        _db.DataExportLogs.Add(new DataExportLog
        {
            UserId = userId,
            UserName = user?.FullName ?? string.Empty,
            ExportedAt = _time.GetUtcNow().UtcDateTime,
            FilterJson = filterJson,
            RowCount = rowCount,
            ExportType = exportType
        });

        await _db.SaveChangesAsync();
    }
}
