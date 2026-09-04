namespace MicroLIMS.Domain.Entities;

public class AuditEventChange
{
    public int Id { get; set; }
    public int AuditLogId { get; set; }
    public AuditLog AuditLog { get; set; } = null!;
    public string FieldName { get; set; } = string.Empty;
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
}
