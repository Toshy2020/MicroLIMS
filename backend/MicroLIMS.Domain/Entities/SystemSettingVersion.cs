namespace MicroLIMS.Domain.Entities;

// One change of a system setting. APPEND-ONLY (database trigger
// prevent_audit_modification + TRUNCATE guard): a correction is a new version.
public class SystemSettingVersion
{
    public int Id { get; set; }
    public int SettingId { get; set; }
    public SystemSetting? Setting { get; set; }
    public int Version { get; set; }
    public string Value { get; set; } = string.Empty;

    // The effective value before this change (the default for version 1).
    public string PreviousValue { get; set; } = string.Empty;
    public int ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = string.Empty;

    // Set for GxP-relevant keys.
    public int? ElectronicSignatureId { get; set; }
    public ElectronicSignature? ElectronicSignature { get; set; }
}
