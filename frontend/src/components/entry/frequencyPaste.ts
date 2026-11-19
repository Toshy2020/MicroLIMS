import { emptyFrequency } from "../configHierarchy";
import type { FrequencyValue } from "../configHierarchy";

const WEEKDAYS = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"] as const;
const MONTHS = [
  "January",
  "February",
  "March",
  "April",
  "May",
  "June",
  "July",
  "August",
  "September",
  "October",
  "November",
  "December"
] as const;

const FREQUENCY_FORMAT_HINT =
  'Enter Daily, Weekly on <weekday>, or Monthly (e.g. "Weekly on Monday", "Monthly, week 2").';

const fold = (value: string) => value.trim().toLowerCase();

function matchWeekday(raw: string): string | null {
  const key = fold(raw);
  return WEEKDAYS.find((d) => d.toLowerCase() === key) ?? null;
}

function matchMonthIndex(raw: string): number | null {
  const key = fold(raw);
  const idx = MONTHS.findIndex((m) => m.toLowerCase() === key);
  return idx >= 0 ? idx + 1 : null;
}

export interface ParseFrequencyOptions {
  weekdayRequired?: boolean;
}

/**
 * Parses a pasted frequency string (matching `frequencyLabel` or backend `FrequencyRules.Describe`)
 * into a structured `FrequencyValue`.
 */
export function parseFrequencyInput(
  raw: string,
  options?: ParseFrequencyOptions
): { value: FrequencyValue; error: string | null } {
  const weekdayRequired = options?.weekdayRequired ?? true;
  const normalized = raw.trim().replace(/\s+/g, " ");
  if (normalized === "") {
    return { value: emptyFrequency, error: null };
  }

  if (/^daily(?:\s*\(working days\)|\s*,\s*working days)?$/i.test(normalized)) {
    return {
      value: { type: "Daily", interval: 1, weekday: "", anchorMonth: "", weekOfMonth: "" },
      error: null
    };
  }

  const weeklyMatch = /^(?:weekly|every\s+(\d+)\s+weeks?)(?:(?:\s+on|\s*,|\s+)\s*([a-z]+))?$/i.exec(normalized);
  if (weeklyMatch) {
    const interval = weeklyMatch[1] ? Number(weeklyMatch[1]) : 1;
    if (interval < 1 || interval > 52) {
      return { value: emptyFrequency, error: "Frequency interval must be between 1 and 52." };
    }
    let weekday = "";
    if (!weeklyMatch[2]) {
      if (weekdayRequired) {
        return { value: emptyFrequency, error: "A weekly frequency needs a sampling weekday." };
      }
    } else {
      const matched = matchWeekday(weeklyMatch[2]);
      if (!matched) {
        return { value: emptyFrequency, error: `Unknown weekday '${weeklyMatch[2]}'.` };
      }
      weekday = matched;
    }
    return {
      value: { type: "Weekly", interval, weekday, anchorMonth: "", weekOfMonth: "" },
      error: null
    };
  }

  const monthlyMatch = /^(?:monthly|every\s+(\d+)\s+months?)(?:\s+(.*)|,\s*(.*))?$/i.exec(normalized);
  if (monthlyMatch) {
    const interval = monthlyMatch[1] ? Number(monthlyMatch[1]) : 1;
    if (interval < 1 || interval > 12) {
      return { value: emptyFrequency, error: "A monthly interval cannot exceed 12 months." };
    }

    let rest = (monthlyMatch[2] ?? monthlyMatch[3] ?? "").trim();
    let anchorMonth: number | "" = "";
    let weekOfMonth: number | "" = "";
    let weekday = "";

    const fromMatch = /\bfrom\s+([a-z]+)\b/i.exec(rest);
    if (fromMatch) {
      const monthIdx = matchMonthIndex(fromMatch[1]);
      if (monthIdx === null) {
        return { value: emptyFrequency, error: `Unknown month '${fromMatch[1]}'.` };
      }
      if (interval === 1) {
        return { value: emptyFrequency, error: "An anchor month only applies to every-N-months rules." };
      }
      anchorMonth = monthIdx;
      rest = rest.replace(fromMatch[0], " ");
    }

    const weekMatch = /\bweek\s+(\d+)\b/i.exec(rest);
    if (weekMatch) {
      const weekNum = Number(weekMatch[1]);
      if (weekNum < 1 || weekNum > 4) {
        return { value: emptyFrequency, error: "Week of the month must be between 1 and 4." };
      }
      weekOfMonth = weekNum;
      rest = rest.replace(weekMatch[0], " ");
    }

    const leftover = rest.replace(/,/g, " ").replace(/\s+/g, " ").trim();
    if (leftover !== "") {
      const dayMatch = /^(?:on\s+)?([a-z]+)$/i.exec(leftover);
      if (!dayMatch) {
        return { value: emptyFrequency, error: FREQUENCY_FORMAT_HINT };
      }
      const matchedDay = matchWeekday(dayMatch[1]);
      if (!matchedDay) {
        return { value: emptyFrequency, error: `Unknown weekday '${dayMatch[1]}'.` };
      }
      weekday = matchedDay;
    }

    return {
      value: { type: "Monthly", interval, weekday, anchorMonth, weekOfMonth },
      error: null
    };
  }

  return { value: emptyFrequency, error: FREQUENCY_FORMAT_HINT };
}
