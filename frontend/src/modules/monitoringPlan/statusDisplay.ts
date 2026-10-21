import type { MonitoringCategory, OccurrenceStatus } from "./types";

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

export type StatusTone = "success" | "warning" | "error" | "info" | "default";

export const STATUS_TONE: Record<OccurrenceStatus, StatusTone> = {
  Upcoming: "default",
  Open: "info",
  DueToday: "warning",
  PartlyPerformed: "info",
  InTesting: "info",
  AwaitingReview: "info",
  AwaitingApproval: "info",
  Performed: "success",
  PerformedExcursion: "warning",
  Overdue: "error",
  Missed: "error",
  Skipped: "default",
  Retired: "default"
};

export const CATEGORY_LABEL: Record<MonitoringCategory, string> = {
  Water: "Water",
  EnvironmentalMonitoring: "Environmental Monitoring",
  AfterCleaning: "After Cleaning"
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
