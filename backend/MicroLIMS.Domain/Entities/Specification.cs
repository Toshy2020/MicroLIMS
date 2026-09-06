using System.Text.Json.Serialization;

namespace MicroLIMS.Domain.Entities;

public class Specification
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    [JsonIgnore]
    public Item? Item { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string AlertLimit { get; set; } = string.Empty;
    public string ActionLimit { get; set; } = string.Empty;
    public string SpecLimit { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    // Only meaningful for count-type tests (TAMC/TYMC) - pathogen
    // presence/absence tests never set this. Null means "not configured
    // yet", which RecordCountTestAsync treats as blocking result entry
    // rather than silently defaulting to 1, so an unconfigured DF can't
    // slip through unnoticed.
    public decimal? DilutionFactor { get; set; }
}
