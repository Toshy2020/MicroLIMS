using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class Report
{
    public int Id { get; set; }
    public SampleCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int GeneratedByUserId { get; set; }
    public string? PdfPath { get; set; }
}
