export interface StatusToneTokens {
  bg: string;
  text: string;
  border: string;
}

export type StatusTone =
  | "detected" | "notDetected" | "inconclusive" | "pending"
  | "action" | "info" | "purple" | "pale";

// detected/notDetected/inconclusive/pending are lifted verbatim from the
// approved dark-mode mockup (darkmode_mockup.html) - not a naive inversion
// of the light values, each dark tone is its own hand-picked
// desaturated-bg + bright-text + mid-tone-border triad. action/info/purple/
// pale extend the same formula to cover every other status this app colors
// (equipment, media lifecycle, task urgency, etc. - see STATUS_TONE below),
// so the whole app shares one token system instead of a per-component map.
export const statusTonesByMode: Record<"light" | "dark", Record<StatusTone, StatusToneTokens>> = {
  light: {
    detected:     { bg: "#FCEAEA", text: "#A21C1C", border: "#E8B9B9" },
    notDetected:  { bg: "#E8F5EC", text: "#1F7A3D", border: "#B7E0C4" },
    inconclusive: { bg: "#FFF4E0", text: "#92620A", border: "#F0D397" },
    pending:      { bg: "#EEF1F4", text: "#5A6472", border: "#D6DBE1" },
    action:       { bg: "#FCEADD", text: "#9A4A0A", border: "#EFC49A" },
    info:         { bg: "#E6EEFB", text: "#1D4E96", border: "#B9CFEF" },
    purple:       { bg: "#F3E8FB", text: "#6B21A8", border: "#DCC0EF" },
    pale:         { bg: "#FEF9C3", text: "#854D0E", border: "#FDE68A" }
  },
  dark: {
    detected:     { bg: "#3A1F1F", text: "#F0A8A8", border: "#5C2C2C" },
    notDetected:  { bg: "#1B3325", text: "#8FDBA8", border: "#2C4E38" },
    inconclusive: { bg: "#3A2E10", text: "#F0CC7A", border: "#574616" },
    pending:      { bg: "#262C35", text: "#99A3B0", border: "#333B47" },
    action:       { bg: "#3A2712", text: "#F0B27A", border: "#5C3D1E" },
    info:         { bg: "#1B2A40", text: "#8FB8EF", border: "#2C4560" },
    purple:       { bg: "#2E1F3A", text: "#D8A8F0", border: "#4A2C5C" },
    pale:         { bg: "#3A3410", text: "#F0DE7A", border: "#5C531C" }
  }
};

export interface CountdownTokens {
  active: { bg: string; text: string };
  locked: { bg: string; text: string };
}

// Incubation countdown chip - locked stays a flat neutral regardless of
// mode, active gets a tinted-primary treatment, per the mockup.
export const countdownTokensByMode: Record<"light" | "dark", CountdownTokens> = {
  light: {
    active: { bg: "#EAF0FB", text: "#2E5AAC" },
    locked: { bg: "#F1F2F4", text: "#606A78" }
  },
  dark: {
    active: { bg: "#22344F", text: "#8AB0F0" },
    locked: { bg: "#262C35", text: "#949DA9" }
  }
};

// Every status string StatusBadge/CategoryBadge/etc. currently colors,
// bucketed into one of the eight tones above. Extend this map (not a
// component-local hex value) when a new status is introduced, so it picks
// up dark-mode tokens automatically instead of needing its own fix later.
const STATUS_TONE: Record<string, StatusTone> = {
  Received: "pending", Waiting: "pending",
  Running: "info", InProgress: "info",
  ReadyToRead: "purple", "Ready to Read": "purple",
  EnterResult: "pale", "Enter Result": "pale",
  Incubating: "action", ResultEntered: "info", Ready: "info", Reviewed: "action",
  PendingReview: "action", "Pending Review": "action",
  Completed: "notDetected", Approved: "notDetected",
  Rejected: "detected", RetestRequested: "action",
  Active: "notDetected", Inactive: "pending", Frozen: "pending",
  InStock: "notDetected", LowStock: "inconclusive", Depleted: "action", Expired: "detected",
  "Pending Evaluation": "pending", "Awaiting Approval": "action", Released: "notDetected", "Out of Stock": "pending", OutOfStock: "pending",
  Overdue: "detected", "Due Soon": "inconclusive", CalibrationDueSoon: "inconclusive",
  InService: "notDetected", OutOfService: "action", Retired: "pending",
  WithinLimits: "notDetected", AlertLimitExceeded: "inconclusive", ActionLimitExceeded: "action", OutOfSpecification: "detected",
  WithinLimit: "notDetected", AlertLevel: "inconclusive", ActionLevel: "action", NotApplicable: "pending",
  LimitsNotConfigured: "inconclusive", "Limits Not Configured": "inconclusive",
  Detected: "detected", Absent: "notDetected",
  DueSoon: "action", DueToday: "action", DueTomorrow: "notDetected",
  Passed: "notDetected", Failed: "detected", Pending: "pending",
  Returned: "action"
};

export function statusTone(status: string): StatusTone {
  return STATUS_TONE[status] ?? "pending";
}
