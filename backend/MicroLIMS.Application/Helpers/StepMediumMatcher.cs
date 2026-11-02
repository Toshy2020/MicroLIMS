namespace MicroLIMS.Application.Helpers;

using MicroLIMS.Domain.Entities;

public static class StepMediumMatcher
{
    // A step medium's product: the Culture Media entry configured in Test Master.
    public static int? ProductOf(TestWorkflowStepMedia stepMedium) => stepMedium.MediaProductId;

    // A lot fits when it is of the step medium's product. There is no fallback to the exact batch.
    public static bool Matches(TestWorkflowStepMedia stepMedium, int? lotProductId) =>
        lotProductId != null && stepMedium.MediaProductId == lotProductId;
}
