namespace MicroLIMS.Domain.Entities;

public class ConfigurationSetting
{
    public int Id { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public string SettingGroup { get; set; } = "General";
    public int? ModifiedByUserId { get; set; }
    public User? ModifiedByUser { get; set; }
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
