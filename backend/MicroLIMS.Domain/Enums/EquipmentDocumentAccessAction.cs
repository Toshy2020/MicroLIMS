namespace MicroLIMS.Domain.Enums;

// Read/download access actions for equipment documents.
// Business mutations (Upload, Supersede, Void) are captured directly in the
// document record and AuditLog.
public enum EquipmentDocumentAccessAction
{
    View = 1,
    Download = 2
}
