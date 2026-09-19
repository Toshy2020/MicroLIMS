using System.Text.Json.Serialization;

namespace MicroLIMS.Domain.Entities;

public class SpecificationStage
{
    public int Id { get; set; }
    public int SpecificationId { get; set; }
    [JsonIgnore]
    public Specification? Specification { get; set; }
    public int StageNumber { get; set; }
    public string StageLabel { get; set; } = string.Empty;
    public string AcceptanceCriteriaText { get; set; } = string.Empty;
}
