namespace MicroLIMS.Domain.Entities;

public class SupplierStatedAttribute
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public string? Grade { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StatedValue { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
