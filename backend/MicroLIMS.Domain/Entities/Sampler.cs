namespace MicroLIMS.Domain.Entities;

// Shared master list across all six receiving categories (Section Head
// managed) - suggestion source for the free-text "Sampled By" field on
// Sample, not an FK: existing Sample.SampledBy values are never validated
// against this list, so removing a name here never affects historical
// records.
public class Sampler
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
