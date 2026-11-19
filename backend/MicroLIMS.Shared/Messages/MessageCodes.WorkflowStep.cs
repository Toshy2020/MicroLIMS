namespace MicroLIMS.Shared.Messages;

// WorkflowStep.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string WorkflowStepContiguousStepNumbers = "WorkflowStep.ContiguousStepNumbers";
    public const string WorkflowStepFinalStepAlreadyExists = "WorkflowStep.FinalStepAlreadyExists";
    public const string WorkflowStepIncubationConditionChosenWrongProduct = "WorkflowStep.IncubationConditionChosenWrongProduct";
    public const string WorkflowStepIncubationConditionMissingForMedium = "WorkflowStep.IncubationConditionMissingForMedium";
    public const string WorkflowStepIncubationConditionNotFound = "WorkflowStep.IncubationConditionNotFound";
    public const string WorkflowStepMediaProductInactive = "WorkflowStep.MediaProductInactive";
    public const string WorkflowStepMediaProductNotFound = "WorkflowStep.MediaProductNotFound";
    public const string WorkflowStepStepAlreadyAtEnd = "WorkflowStep.StepAlreadyAtEnd";
    public const string WorkflowStepStepCannotDeleteUsed = "WorkflowStep.StepCannotDeleteUsed";
    public const string WorkflowStepStepNotFound = "WorkflowStep.StepNotFound";
    public const string WorkflowStepTemplateValidationFailed = "WorkflowStep.TemplateValidationFailed";
    public const string WorkflowStepTestNotFound = "WorkflowStep.TestNotFound";
    public const string WorkflowStepTestWorkflowTypeLocked = "WorkflowStep.TestWorkflowTypeLocked";
    public const string WorkflowStepWorkflowTypeHasNoSteps = "WorkflowStep.WorkflowTypeHasNoSteps";

    private static Dictionary<string, string[]> WorkflowStepParameters() => new()
    {
        [WorkflowStepContiguousStepNumbers] = [],
        [WorkflowStepFinalStepAlreadyExists] = [],
        [WorkflowStepIncubationConditionChosenWrongProduct] = ["name"],
        [WorkflowStepIncubationConditionMissingForMedium] = ["name"],
        [WorkflowStepIncubationConditionNotFound] = ["id"],
        [WorkflowStepMediaProductInactive] = ["name"],
        [WorkflowStepMediaProductNotFound] = ["mediaProductId"],
        [WorkflowStepStepAlreadyAtEnd] = [],
        [WorkflowStepStepCannotDeleteUsed] = ["stepName"],
        [WorkflowStepStepNotFound] = ["stepId"],
        [WorkflowStepTemplateValidationFailed] = ["message"],
        [WorkflowStepTestNotFound] = ["id"],
        [WorkflowStepTestWorkflowTypeLocked] = [],
        [WorkflowStepWorkflowTypeHasNoSteps] = ["workflowType"],
    };
}
