using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Workflows;

// Common contract every workflow engine (Product/Water/EM/AfterCleaning/
// Pathogen/GPT) implements, so Review/Approval/Testing Workspace can
// treat "where is this TestOrder in its workflow" uniformly regardless
// of which domain it belongs to (gap analysis item #1 and #3).
public interface IStatefulWorkflowEngine
{
    // Advances the TestOrder to the next step, recording a WorkflowHistory
    // row, validating preconditions, and returning the resulting step.
    // Throws InvalidOperationException (workflow-order violation) if the
    // TestOrder is not eligible to advance.
    Task<WorkflowStep> AdvanceAsync(int testOrderId, int performedByUserId, string? note = null);

    // Runs the frozen validation rules for the TestOrder's current step
    // without changing state - used by the UI to show "why can't I advance yet".
    Task<List<string>> ValidateAsync(int testOrderId);

    Task<bool> IsCompleteAsync(int testOrderId);
}
