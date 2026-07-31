using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// The exact data used to generate a Report, frozen at generation time -
// so a previously issued report can always be reproduced identically
// even if underlying records are later amended (GMP requirement).
public class ReportSnapshot
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public Report? Report { get; set; }
    public SampleCategory Category { get; set; }
    public string DataJson { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
