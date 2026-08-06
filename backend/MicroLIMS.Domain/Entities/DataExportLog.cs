namespace MicroLIMS.Domain.Entities;

// Traceability for raw data leaving the system through the Reports
// module's CSV export - AuditLog's capture mechanism only fires on
// entity Add/Modify/Delete (see MicroLimsDbContext.CaptureAuditEntries),
// so a read-only export has nothing there to hook into. This is a
// separate, purpose-built log written explicitly by
// DataExportAuditService instead. Nothing is ever deleted from it.
public class DataExportLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty; // snapshot, same convention as ResultRecord's *ByName fields
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public string FilterJson { get; set; } = string.Empty; // the full set of filter parameters used
    public int RowCount { get; set; }
    public string ExportType { get; set; } = string.Empty; // e.g. "ResultRecordsCsv"
}
