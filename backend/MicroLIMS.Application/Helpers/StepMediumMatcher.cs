namespace MicroLIMS.Application.Helpers;

using MicroLIMS.Domain.Entities;

public static class StepMediumMatcher
{
    // A step medium's product: its IncubationCondition's product, else its Material's.
    public static int? ProductOf(TestWorkflowStepMedia stepMedium) =>
        stepMedium.IncubationCondition?.MediaProductId ?? stepMedium.Material?.MediaProductId;

    // A lot fits when both sides resolve to the same product; legacy rows without a product fall back to the exact batch.
    public static bool Matches(TestWorkflowStepMedia stepMedium, int lotMaterialId, int? lotProductId) =>
        stepMedium.MaterialId == lotMaterialId || (lotProductId != null && ProductOf(stepMedium) == lotProductId);
}
