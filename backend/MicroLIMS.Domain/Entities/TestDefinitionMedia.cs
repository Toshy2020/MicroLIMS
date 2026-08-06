namespace MicroLIMS.Domain.Entities;

// Which MediaType(s) are approved to run a given TestDefinition, optionally
// scoped to one step of a multi-step chain (Pathogen: "TSB"/"Detection"/
// "RVS"/"XLD_TSI"). StepName null means "the one step" - used by count
// tests (TAMC/TYMC), which have a single incubation step.
public class TestDefinitionMedia
{
    public int Id { get; set; }
    public int TestDefinitionId { get; set; }
    public TestDefinition? TestDefinition { get; set; }
    public int MediaTypeId { get; set; }
    public MediaType? MediaType { get; set; }
    public string? StepName { get; set; }
}
