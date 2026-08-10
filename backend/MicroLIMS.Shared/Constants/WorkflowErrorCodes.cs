namespace MicroLIMS.Shared.Constants;

// Machine-readable codes the frontend switches on. ApiResponse has no
// dedicated code field, so these are returned as the first entry of
// ApiResponse.Errors with the human-readable text in Message.
public static class WorkflowErrorCodes
{
    public const string IncubationNotComplete = "INCUBATION_NOT_COMPLETE";
    public const string MediaNotInPermittedList = "MEDIA_NOT_IN_PERMITTED_LIST";
    public const string NoMediaSelected = "NO_MEDIA_SELECTED";
    public const string IncompleteConfirmatorySetup = "INCOMPLETE_CONFIRMATORY_SETUP";
    public const string IncubatorTempOutOfRange = "INCUBATOR_TEMP_OUT_OF_RANGE";
    public const string BiochemicalResultRequired = "BIOCHEMICAL_RESULT_REQUIRED";
    public const string SegregationOfDutiesViolation = "SEGREGATION_OF_DUTIES_VIOLATION";
    public const string TemplateValidationFailed = "TEMPLATE_VALIDATION_FAILED";
}
