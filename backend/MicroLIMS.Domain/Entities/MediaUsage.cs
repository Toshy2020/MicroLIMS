namespace MicroLIMS.Domain.Entities;

// Links a Media lot to the specific TestOrder it was used for -
// required for GMP traceability ("which media batch was this result
// generated with").
public class MediaUsage
{
    public int Id { get; set; }
    public int MediaId { get; set; }
    public Media? Media { get; set; }
    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;
    public int UsedByUserId { get; set; }
}
