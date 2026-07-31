namespace MicroLIMS.Domain.Entities;

public class Specification
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string AlertLimit { get; set; } = string.Empty;
    public string ActionLimit { get; set; } = string.Empty;
    public string SpecLimit { get; set; } = string.Empty;
}
