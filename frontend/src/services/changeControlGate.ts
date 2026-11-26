// How a screen recognises the change-control gate's refusal (CONF-227): by the MESSAGE CODE the
// server sent, never by its text. The codes are ChangeControl.Gate* in MessageCodes.Quality.cs.
export const CHANGE_CONTROL_GATE_CODES: readonly string[] = [
  "ChangeControl.GateRequired",
  "ChangeControl.GateNotFound",
  "ChangeControl.GateNotApproved",
  "ChangeControl.GateWrongLaboratory",
  "ChangeControl.GateRecordNotListed"
];

interface RefusalBody {
  message?: string;
  errorDetails?: Array<{ code?: unknown }>;
}

function bodyOf(error: unknown): RefusalBody | null {
  if (typeof error !== "object" || error === null) return null;
  const data = (error as { response?: { data?: unknown } }).response?.data;
  return typeof data === "object" && data !== null ? (data as RefusalBody) : null;
}

// The refusal sentence (already in the interface language) when the error is one of the gate's
// refusals; null for every other error.
export function changeControlGateRefusal(error: unknown): string | null {
  const body = bodyOf(error);
  const details = Array.isArray(body?.errorDetails) ? body.errorDetails : [];
  const isGate = details.some((d) => typeof d.code === "string" && CHANGE_CONTROL_GATE_CODES.includes(d.code));
  if (!isGate) return null;
  return typeof body?.message === "string" && body.message ? body.message : "";
}

// The number the header accepts: capital letters, digits and hyphens, at most 20 characters.
export const CHANGE_CONTROL_NUMBER_PATTERN = /^[A-Z0-9-]{1,20}$/;
