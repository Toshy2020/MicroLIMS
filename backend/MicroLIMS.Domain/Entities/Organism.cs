namespace MicroLIMS.Domain.Entities;

// Canonical organism master list - every OrganismName free-text field
// elsewhere in the system (MediaChallengeSpec, MediaEvaluationChallenge,
// Cryovial, Material) is being converted to an OrganismId FK against this
// table so organism matching (see MediaEvaluationEngine.SelectCryovialAsync)
// is an integer comparison instead of a string comparison that a single
// spelling difference can silently break.
public class Organism
{
    public int Id { get; set; }
    public string ScientificName { get; set; } = string.Empty;
    public string? AtccNumber { get; set; }
    public string? CommonName { get; set; }
    public string? Description { get; set; }
}
