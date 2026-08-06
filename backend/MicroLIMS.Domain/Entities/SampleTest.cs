using System.Text.Json.Serialization;

namespace MicroLIMS.Domain.Entities;

// Join entity: which TestDefinition(s) (by TestCode) an Item is
// configured to trigger automatically on receipt.
public class SampleTest
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    [JsonIgnore]
    public Item? Item { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
