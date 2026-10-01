import type { Verdict } from "./VerdictBanner";

// Maps a server-returned outcome status onto the banner. Only the server's
// overall status may be passed in - never a client aggregate of parameter
// statuses - so the verdict stays the server's decision.
export function verdictFromServerStatus(status: string): Verdict {
  if (/OutOfSpecification|Failed/.test(status)) return "Fail";
  if (/WithinLimits|Passed/.test(status)) return "Pass";
  return "Pending";
}
