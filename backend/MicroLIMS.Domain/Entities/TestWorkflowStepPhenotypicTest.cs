using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One phenotypic test bundled onto a BiochemicalTest-typed TestWorkflowStep,
// configured in Test Master. A step can now list several (e.g. Gram Stain +
// Oxidase + Identification Kit) instead of needing a separate chained step
// per test type - the analyst still submits one combined free-text result
// and one Detected/Not-Detected decision for the whole step
// (SubmitBiochemicalAsync is unchanged). The older single
// TestWorkflowStep.PhenotypicTestType field is untouched for backward
// compatibility with existing chained-step templates and live data - it
// keeps working exactly as before; this table is purely additive.
public class TestWorkflowStepPhenotypicTest
{
    public int Id { get; set; }

    public int TestWorkflowStepId { get; set; }
    public TestWorkflowStep? TestWorkflowStep { get; set; }

    public PhenotypicTestType PhenotypicTestType { get; set; }

    public int DisplayOrder { get; set; }
}
