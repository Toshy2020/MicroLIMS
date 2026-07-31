namespace MicroLIMS.Domain.Entities;

// Supports multi-step incubation workflows (EM Step 1/Step 2, Salmonella
// TSB -> RVS -> XLD+TSI chain).
public class Incubation
{
    public int Id { get; set; }
    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public int StepNumber { get; set; }
    public string StepName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? Outcome { get; set; }
}
