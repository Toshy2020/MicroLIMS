using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// A system setting whose value has been changed from its default. Definitions
// (type, bounds, default, GxP relevance) live in code; a row exists only once
// someone has changed the value, and every change appends a SystemSettingVersion.
// The panel controls the system, never GxP data: these are operating
// parameters (security thresholds, retention, worker limits).
public class SystemSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public SystemSettingValueType ValueType { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsGxpRelevant { get; set; }
    public string Description { get; set; } = string.Empty;
    public int CurrentVersion { get; set; }

    public ICollection<SystemSettingVersion> Versions { get; set; } = new List<SystemSettingVersion>();
}
