import type { TFunction } from "i18next";
import { i18n } from "../../i18n";
import type { StatusTone } from "../../theme/statusTokens";
import type {
  CalendarArea,
  CalendarEventType,
  EquipmentTaskStatus,
  EquipmentTaskType,
  ItemProgress,
  MonitoringCategory,
  OccurrenceStatus
} from "./types";

export const STATUS_KEY: Record<OccurrenceStatus, string> = {
  Upcoming: "statuses.Upcoming",
  Open: "statuses.Open",
  DueToday: "statuses.DueToday",
  PartlyPerformed: "statuses.PartlyPerformed",
  InTesting: "statuses.InTesting",
  AwaitingReview: "statuses.AwaitingReview",
  AwaitingApproval: "statuses.AwaitingApproval",
  Performed: "statuses.Performed",
  PerformedExcursion: "statuses.PerformedExcursion",
  Overdue: "statuses.Overdue",
  Missed: "statuses.Missed",
  Skipped: "statuses.Skipped",
  Retired: "statuses.Retired"
};

export const ITEM_PROGRESS_KEY: Record<ItemProgress, string> = {
  None: "itemProgress.None",
  InTesting: "itemProgress.InTesting",
  AwaitingReview: "itemProgress.AwaitingReview",
  AwaitingApproval: "itemProgress.AwaitingApproval",
  Performed: "itemProgress.Performed",
  PerformedExcursion: "itemProgress.PerformedExcursion"
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

export const CATEGORY_KEY: Record<MonitoringCategory, string> = {
  Water: "categories.Water",
  EnvironmentalMonitoring: "categories.EnvironmentalMonitoring",
  AfterCleaning: "categories.AfterCleaning"
};

export const CALENDAR_AREA_KEY: Record<CalendarArea, string> = {
  Water: "calendarAreas.Water",
  EnvironmentalMonitoring: "calendarAreas.EnvironmentalMonitoring",
  AfterCleaning: "calendarAreas.AfterCleaning",
  Calibration: "calendarAreas.Calibration",
  PreventiveMaintenance: "calendarAreas.PreventiveMaintenance",
  Events: "calendarAreas.Events"
};

export const EVENT_TYPE_KEY: Record<CalendarEventType, string> = {
  VendorVisit: "eventTypes.VendorVisit",
  BreakdownRepair: "eventTypes.BreakdownRepair",
  Audit: "eventTypes.Audit",
  ProficiencyTesting: "eventTypes.ProficiencyTesting",
  Other: "eventTypes.Other"
};

export const EQUIPMENT_TASK_KEY: Record<EquipmentTaskType, string> = {
  Calibration: "equipmentTaskTypes.Calibration",
  PreventiveMaintenance: "equipmentTaskTypes.PreventiveMaintenance"
};

export const EQUIPMENT_STATUS_KEY: Record<EquipmentTaskStatus, string> = {
  Upcoming: "equipmentStatuses.Upcoming",
  Due: "equipmentStatuses.Due",
  Overdue: "equipmentStatuses.Overdue",
  Done: "equipmentStatuses.Done",
  Retired: "equipmentStatuses.Retired"
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

export const MONTH_INDICES = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11] as const;

// The lab week runs Sunday to Thursday, so Friday and Saturday are the weekend.
export const WEEKDAY_INDICES = [0, 1, 2, 3, 4, 5, 6] as const;
export const isWeekend = (dayOfWeek: number) => dayOfWeek === 5 || dayOfWeek === 6;

export function isoDate(y: number, m: number, d: number): string {
  return `${y}-${String(m + 1).padStart(2, "0")}-${String(d).padStart(2, "0")}`;
}

// "8 Jan", or "8 Jan 2025" when it is not in the current year.
export function formatShortDate(iso: string, thisYear: string, t: TFunction): string {
  const monthShort = t(`monthsShort.${Number(iso.slice(5, 7)) - 1}`);
  return `${Number(iso.slice(8))} ${monthShort}${iso.startsWith(thisYear) ? "" : ` ${iso.slice(0, 4)}`}`;
}

// A period key as people say it: "2026-W43" is "Week 43, 2026", "D20261022" is "22 October 2026".
export function periodLabel(key: string, t?: TFunction): string {
  const tr = (k: string, opts?: Record<string, unknown>) =>
    t ? t(k, opts) : i18n.t(k, { ns: "monitoring", ...opts });

  let m = /^(\d{4})-W(\d{2})$/.exec(key);
  if (m) return tr("period.week", { week: Number(m[2]), year: m[1] });
  m = /^(\d{4})-(\d{2})$/.exec(key);
  if (m) return tr("period.monthYear", { month: tr(`months.${Number(m[2]) - 1}`), year: m[1] });
  m = /^D(\d{4})(\d{2})(\d{2})$/.exec(key);
  if (m) return tr("period.dayMonthYear", { day: Number(m[3]), month: tr(`months.${Number(m[2]) - 1}`), year: m[1] });
  if (key.startsWith("resp-")) return tr("period.followUp");
  if (key.startsWith("manual-")) return tr("period.manual");
  return key;
}
