import type { TFunction } from "i18next";
import { i18n } from "../i18n";
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

const MFA_STATE_TONE: Record<MfaUserState, StatusTone> = {
  NotRequired: "pending",
  Enrolled: "notDetected",
  DueByDate: "inconclusive",
  Overdue: "detected",
  TokenPendingConfirmation: "action"
};

/** Human labels and shared status-token tones for backend MfaUserState values. */
export const MFA_STATE_DISPLAY: Record<MfaUserState, MfaStatePresentation> = {
  NotRequired: { label: "Not required", tone: "pending" },
  Enrolled: { label: "Set up", tone: "notDetected" },
  DueByDate: { label: "Due — not set up yet", tone: "inconclusive" },
  Overdue: { label: "Overdue", tone: "detected" },
  TokenPendingConfirmation: { label: "Token awaiting first use", tone: "action" }
};

/** Maps a backend MFA user state string to its label and tone, or null when missing/unknown. */
export function getMfaStateDisplay(state: string | null | undefined, t?: TFunction): MfaStatePresentation | null {
  if (!state || !(state in MFA_STATE_TONE)) return null;
  const typed = state as MfaUserState;
  const translate = t ?? i18n.getFixedT("en", "common");
  return {
    label: translate(`mfaState.${typed}`, { ns: "common" }),
    tone: MFA_STATE_TONE[typed]
  };
}

/** Formats a snapshot user MFA state for Access Review, showing "Not recorded" for legacy snapshots. */
export function formatAccessReviewMfaState(state: string | null | undefined, t?: TFunction): string {
  const translate = t ?? i18n.getFixedT("en", "common");
  if (!state || !state.trim()) return translate("mfaState.NotRecorded", { ns: "common" });
  return getMfaStateDisplay(state, translate)?.label ?? state;
}
