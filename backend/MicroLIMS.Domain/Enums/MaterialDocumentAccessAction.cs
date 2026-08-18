namespace MicroLIMS.Domain.Enums;

// Actions recorded in the document access/audit log.
// Generic EF change-tracking captures state mutations (Upload/Supersede/Void)
// in AuditLog automatically; file-read events (View/Download) require
// explicit recording in MaterialDocumentAccessLog.
public enum MaterialDocumentAccessAction
{
    View,
    Download,
    Upload,
    Supersede,
    Void
}
