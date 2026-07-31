namespace MicroLIMS.Domain.Entities;

// Section Head master data: for a given Selective MediaType + target
// organism, the expected colony description an Indication test must
// match (e.g. "pink colonies with black centers" for XLD + Salmonella).
public class ExpectedIndicationResult
{
    public int Id { get; set; }
    public int MediaTypeId { get; set; }
    public MediaType? MediaType { get; set; }
    public string OrganismName { get; set; } = string.Empty;
    public string ExpectedDescription { get; set; } = string.Empty;
}
