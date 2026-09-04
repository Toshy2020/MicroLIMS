namespace MicroLIMS.Domain.Entities;

public class DocumentType
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DefaultReviewCycleMonths { get; set; }
    public bool IsActive { get; set; } = true;
}
