import { activeLanguage } from "../i18n";

// Dates follow the interface language: Arabic month names, but always Latin
// digits (the "-u-nu-latn" extension) and the Gregorian calendar, so a date
// reads the same in a record, an export and a report. English output is
// unchanged. Times stay on a 24-hour clock in both languages.
function localeTag(): string {
  return activeLanguage() === "ar" ? "ar-u-nu-latn-ca-gregory" : "en-GB";
}

const CLOCK = { hourCycle: "h23" } as const;

export function formatLabDate(date: string | Date): string {
  const d = typeof date === "string" ? new Date(date) : date;
  return d.toLocaleDateString(localeTag(), { day: "2-digit", month: "short", year: "numeric" });
}

export function formatLabDateTime(date: string | Date): string {
  const d = typeof date === "string" ? new Date(date) : date;
  const tag = localeTag();
  return `${d.toLocaleDateString(tag, { day: "2-digit", month: "short", year: "numeric" })} ${d.toLocaleTimeString(tag, { hour: "2-digit", minute: "2-digit", ...CLOCK })}`;
}

/**
 * Formats an instant with its local time zone name (e.g. for MFA due dates and confirmations).
 * The time zone is always shown: regulated timestamps are never displayed without one.
 */
export function formatDateTimeWithTimeZone(date: string | Date): string {
  const d = typeof date === "string" ? new Date(date) : date;
  return d.toLocaleString(localeTag(), {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    ...CLOCK,
    timeZoneName: "short"
  });
}
