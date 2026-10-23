import type { StatusTone } from "../../theme/statusTokens";
import type {
  CalendarArea,
  CalendarEventType,
  EquipmentTaskStatus,
  EquipmentTaskType,
  MonitoringCategory,
  OccurrenceStatus
} from "./types";

export const STATUS_LABEL: Record<OccurrenceStatus, string> = {
  Upcoming: "Upcoming",
  Open: "Open",
  DueToday: "Due today",
  PartlyPerformed: "Partly performed",
  InTesting: "In testing",
  AwaitingReview: "Awaiting review",
  AwaitingApproval: "Awaiting approval",
  Performed: "Performed",
  PerformedExcursion: "Performed – excursion",
  Overdue: "Overdue",
  Missed: "Missed",
  Skipped: "Skipped",
  Retired: "Retired"
};

// Each status maps onto the app-wide status tokens (theme.custom.status), so
// the colours follow light and dark mode like every other badge. The symbol
// is a second, non-colour cue.
export const STATUS_TOKEN: Record<OccurrenceStatus, StatusTone> = {
  Upcoming: "pending",
  Open: "info",
  DueToday: "inconclusive",
  PartlyPerformed: "info",
  InTesting: "info",
  AwaitingReview: "info",
  AwaitingApproval: "info",
  Performed: "notDetected",
  PerformedExcursion: "inconclusive",
  Overdue: "detected",
  Missed: "detected",
  Skipped: "pending",
  Retired: "pending"
};

export const STATUS_SYMBOL: Record<OccurrenceStatus, string> = {
  Upcoming: "○",
  Open: "◐",
  DueToday: "●",
  PartlyPerformed: "◑",
  InTesting: "◐",
  AwaitingReview: "◐",
  AwaitingApproval: "◐",
  Performed: "✓",
  PerformedExcursion: "!",
  Overdue: "▲",
  Missed: "▲",
  Skipped: "–",
  Retired: "–"
};

export const CATEGORY_LABEL: Record<MonitoringCategory, string> = {
  Water: "Water",
  EnvironmentalMonitoring: "Environmental Monitoring",
  AfterCleaning: "After Cleaning"
};

export const CALENDAR_AREA_LABEL: Record<CalendarArea, string> = {
  Water: "Water",
  EnvironmentalMonitoring: "Environmental Monitoring",
  AfterCleaning: "After Cleaning",
  Calibration: "Calibration",
  PreventiveMaintenance: "Preventive maintenance",
  Events: "Events"
};

export const EVENT_TYPE_LABEL: Record<CalendarEventType, string> = {
  VendorVisit: "Vendor visit",
  BreakdownRepair: "Equipment breakdown repair",
  Audit: "Audit",
  ProficiencyTesting: "Proficiency testing",
  Other: "Other"
};

export const EQUIPMENT_TASK_LABEL: Record<EquipmentTaskType, string> = {
  Calibration: "Calibration",
  PreventiveMaintenance: "Preventive maintenance"
};

export const EQUIPMENT_STATUS_LABEL: Record<EquipmentTaskStatus, string> = {
  Upcoming: "Upcoming",
  Due: "Due",
  Overdue: "Overdue",
  Done: "Done",
  Retired: "Retired"
};

export const EQUIPMENT_STATUS_TOKEN: Record<EquipmentTaskStatus, StatusTone> = {
  Upcoming: "pending",
  Due: "inconclusive",
  Overdue: "detected",
  Done: "notDetected",
  Retired: "pending"
};

export const EQUIPMENT_STATUS_SYMBOL: Record<EquipmentTaskStatus, string> = {
  Upcoming: "○",
  Due: "●",
  Overdue: "▲",
  Done: "✓",
  Retired: "–"
};

export const MONTH_NAMES = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December"
];

// The lab week runs Sunday to Thursday, so Friday and Saturday are the weekend.
export const WEEKDAY_LABELS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
export const isWeekend = (dayOfWeek: number) => dayOfWeek === 5 || dayOfWeek === 6;

export function isoDate(y: number, m: number, d: number): string {
  return `${y}-${String(m + 1).padStart(2, "0")}-${String(d).padStart(2, "0")}`;
}

// A period key as people say it: "2026-W43" is "Week 43, 2026", "D20261022" is "22 October 2026".
export function periodLabel(key: string): string {
  let m = /^(\d{4})-W(\d{2})$/.exec(key);
  if (m) return `Week ${Number(m[2])}, ${m[1]}`;
  m = /^(\d{4})-(\d{2})$/.exec(key);
  if (m) return `${MONTH_NAMES[Number(m[2]) - 1]} ${m[1]}`;
  m = /^D(\d{4})(\d{2})(\d{2})$/.exec(key);
  if (m) return `${Number(m[3])} ${MONTH_NAMES[Number(m[2]) - 1]} ${m[1]}`;
  if (key.startsWith("resp-")) return "Follow-up sampling";
  if (key.startsWith("manual-")) return "Manual entry";
  return key;
}
