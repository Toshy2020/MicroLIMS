namespace MicroLIMS.Shared.Constants;

// Machine-readable codes the frontend switches on. ApiResponse has no
// dedicated code field, so these are returned as the first entry of
// ApiResponse.Errors with the human-readable text in Message.
public static class WorkflowErrorCodes
{
    public const string IncubationNotComplete = "INCUBATION_NOT_COMPLETE";

    // The declared window itself is impossible (ends before it starts) or
    // shorter than the step template's mandated minimum. Distinct from
    // IncubationNotComplete, which is about the window having elapsed.
    public const string IncubationWindowInvalid = "INCUBATION_WINDOW_INVALID";
    public const string IncubationWindowTooShort = "INCUBATION_WINDOW_TOO_SHORT";

    // A confirmatory plating run has already been read out for this step.
    public const string ConfirmatoryAlreadyRecorded = "CONFIRMATORY_ALREADY_RECORDED";

    // A confirmatory media panel is already set up for this step and is
    // still awaiting its plate readings. Distinct from
    // ConfirmatoryAlreadyRecorded so the frontend can tell "read out, done"
    // apart from "incubating, go and read it".
    public const string ConfirmatorySetupAlreadySubmitted = "CONFIRMATORY_SETUP_ALREADY_SUBMITTED";

    public const string MediaNotInPermittedList = "MEDIA_NOT_IN_PERMITTED_LIST";
    public const string NoMediaSelected = "NO_MEDIA_SELECTED";
    public const string IncompleteConfirmatorySetup = "INCOMPLETE_CONFIRMATORY_SETUP";
    public const string IncubatorTempOutOfRange = "INCUBATOR_TEMP_OUT_OF_RANGE";
    public const string BiochemicalResultRequired = "BIOCHEMICAL_RESULT_REQUIRED";
    public const string SegregationOfDutiesViolation = "SEGREGATION_OF_DUTIES_VIOLATION";
    public const string TemplateValidationFailed = "TEMPLATE_VALIDATION_FAILED";

    // The SubmitAsDetected/ProceedToBiochemical choice after an
    // all-conforming confirmatory result is single-shot. Distinct from
    // ConfirmatoryAlreadyRecorded, which guards the plate read-out itself
    // rather than the decision made after it.
    public const string AnalystDecisionAlreadyRecorded = "ANALYST_DECISION_ALREADY_RECORDED";
}
