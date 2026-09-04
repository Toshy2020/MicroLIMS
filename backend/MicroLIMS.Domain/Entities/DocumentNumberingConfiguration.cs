namespace MicroLIMS.Domain.Entities;

public class DocumentNumberingConfiguration
{
    public int Id { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public string NumberFormat { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int ModifiedByUserId { get; set; }
    public User ModifiedByUser { get; set; } = null!;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
