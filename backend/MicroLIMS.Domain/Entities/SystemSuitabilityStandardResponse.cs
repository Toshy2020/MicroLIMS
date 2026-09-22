namespace MicroLIMS.Domain.Entities;

// Replicate response (peak area or titrant volume) for a standard in a suitability run (REQ-FP-001/001a/SC-1).
public class SystemSuitabilityStandardResponse
{
    public int Id { get; set; }

    public int SystemSuitabilityRunAnalyteId { get; set; }
    public SystemSuitabilityRunAnalyte? SystemSuitabilityRunAnalyte { get; set; }

    // 1-based replicate index (1, 2, 3...)
    public int Index { get; set; }

    // Replicate response: peak area today, titrant volume later
    public decimal Response { get; set; }
}
