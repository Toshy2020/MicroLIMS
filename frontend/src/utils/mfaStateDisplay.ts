import type { StatusTone } from "../theme/statusTokens";

export type MfaUserState =
  | "NotRequired"
  | "Enrolled"
  | "DueByDate"
  | "Overdue"
  | "TokenPendingConfirmation";

export interface MfaStatePresentation {
  label: string;
  tone: StatusTone;
}

/** Human labels and shared status-token tones for backend MfaUserState values. */
export const MFA_STATE_DISPLAY: Record<MfaUserState, MfaStatePresentation> = {
  NotRequired: { label: "Not required", tone: "pending" },
  Enrolled: { label: "Set up", tone: "notDetected" },
  DueByDate: { label: "Due — not set up yet", tone: "inconclusive" },
  Overdue: { label: "Overdue", tone: "detected" },
  TokenPendingConfirmation: { label: "Token awaiting first use", tone: "action" }
};

/** Maps a backend MFA user state string to its label and tone, or null when missing/unknown. */
export function getMfaStateDisplay(state: string | null | undefined): MfaStatePresentation | null {
  if (!state || !(state in MFA_STATE_DISPLAY)) return null;
  return MFA_STATE_DISPLAY[state as MfaUserState];
}

/** Formats a snapshot user MFA state for Access Review, showing "Not recorded" for legacy snapshots. */
export function formatAccessReviewMfaState(state: string | null | undefined): string {
  if (!state || !state.trim()) return "Not recorded";
  return getMfaStateDisplay(state)?.label ?? state;
}
