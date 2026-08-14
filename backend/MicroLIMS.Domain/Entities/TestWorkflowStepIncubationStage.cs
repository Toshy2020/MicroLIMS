namespace MicroLIMS.Domain.Entities;

// Stage 2+ (StageNumber >= 2) of a PlateCount step's incubation window,
// only meaningful when the owning TestWorkflowStep.RequiresIncubationTransfer
// is true. Stage 1 stays on TestWorkflowStep itself - see that entity's
// comment. Keyed by (TestWorkflowStepId, StageNumber) so a third stage can
// be added later without a schema change, even though only StageNumber == 2
// is used today.
public class TestWorkflowStepIncubationStage
{
    public int Id { get; set; }

    public int TestWorkflowStepId { get; set; }
    public TestWorkflowStep? TestWorkflowStep { get; set; }

    public int StageNumber { get; set; }

    public decimal TempMin { get; set; }
    public decimal TempMax { get; set; }
    public int IncubationMinHours { get; set; }
    public int IncubationMaxHours { get; set; }
}
