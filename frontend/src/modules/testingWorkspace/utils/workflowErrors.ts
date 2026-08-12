// Every workflow error the backend can throw as a WorkflowStepException
// (backend/MicroLIMS.Shared/Constants/WorkflowErrorCodes.cs). ApiResponse
// has no error-code field, so the code travels as errors[0] and, for an
// incubation-lock failure, remainingSeconds travels as errors[1] - see
// TestWorkflowController.RunAsync. TEMPLATE_VALIDATION_FAILED is declared
// backend-side but never thrown (a plain-text InvalidOperationException is
// used instead for Test Master validation) - deliberately absent here.
export type WorkflowErrorCode =
  | "INCUBATION_NOT_COMPLETE"
  | "INCUBATION_WINDOW_INVALID"
  | "INCUBATION_WINDOW_TOO_SHORT"
  | "CONFIRMATORY_ALREADY_RECORDED"
  | "CONFIRMATORY_SETUP_ALREADY_SUBMITTED"
  | "MEDIA_NOT_IN_PERMITTED_LIST"
  | "NO_MEDIA_SELECTED"
  | "INCOMPLETE_CONFIRMATORY_SETUP"
  | "INCUBATOR_TEMP_OUT_OF_RANGE"
  | "BIOCHEMICAL_RESULT_REQUIRED"
  | "SEGREGATION_OF_DUTIES_VIOLATION";

export interface ParsedWorkflowError {
  code: WorkflowErrorCode | null;
  message: string;
  remainingSeconds: number | null;
}

// Read the structured code (and remainingSeconds, for an incubation lock)
// off an ApiResponse.Fail envelope. Falls back to the free-text message
// with code: null for anything the server didn't throw as a
// WorkflowStepException (e.g. a plain InvalidOperationException, or a
// network error with no response at all).
export function parseWorkflowError(e: any): ParsedWorkflowError {
  const data = e?.response?.data;
  const message: string = data?.message ?? "Something went wrong. Please try again.";
  const errors: string[] | undefined = data?.errors;
  const rawCode = errors?.[0];
  const knownCodes: WorkflowErrorCode[] = [
    "INCUBATION_NOT_COMPLETE", "INCUBATION_WINDOW_INVALID", "INCUBATION_WINDOW_TOO_SHORT",
    "CONFIRMATORY_ALREADY_RECORDED", "CONFIRMATORY_SETUP_ALREADY_SUBMITTED", "MEDIA_NOT_IN_PERMITTED_LIST",
    "NO_MEDIA_SELECTED", "INCOMPLETE_CONFIRMATORY_SETUP", "INCUBATOR_TEMP_OUT_OF_RANGE",
    "BIOCHEMICAL_RESULT_REQUIRED", "SEGREGATION_OF_DUTIES_VIOLATION"
  ];
  const code = knownCodes.includes(rawCode as WorkflowErrorCode) ? (rawCode as WorkflowErrorCode) : null;
  const remainingSeconds = code === "INCUBATION_NOT_COMPLETE" && errors?.[1] ? Number(errors[1]) : null;
  return { code, message, remainingSeconds };
}

// Per-code display text for the cases the plan calls out specifically.
// Falls back to the server's own message for every other code, which is
// already written to be analyst-readable (see WorkflowStepException
// call sites in TestWorkflowEngine.cs).
export function workflowErrorDisplayMessage(parsed: ParsedWorkflowError): string {
  switch (parsed.code) {
    case "CONFIRMATORY_ALREADY_RECORDED":
      return "This confirmatory plating has already been read out. Showing the recorded result.";
    case "CONFIRMATORY_SETUP_ALREADY_SUBMITTED":
      return "Confirmatory media have already been selected for this step and are incubating - go read the plates instead of selecting again.";
    default:
      return parsed.message;
  }
}
