import { activeLanguage } from "../i18n";

// Dates follow the interface language: Arabic month names, but always Latin
// digits (the "-u-nu-latn" extension) and the Gregorian calendar, so a date
// reads the same in a record, an export and a report. English output is
// unchanged. Times stay on a 24-hour clock in both languages.
function localeTag(): string {
  return activeLanguage() === "ar" ? "ar-u-nu-latn-ca-gregory" : "en-GB";
}

const CLOCK = { hourCycle: "h23" } as const;

// In Arabic every date text is wrapped in a right-to-left isolate (U+2067 ... U+2069), so its
// words keep their order whether it is shown inside an LTR isolate (<Ltr>) or outside one.
// English output is never wrapped.
const RLI = "⁧";
const PDI = "⁩";
function mark(text: string, english: boolean | undefined): string {
  return !english && activeLanguage() === "ar" ? `${RLI}${text}${PDI}` : text;
}
function tagFor(english: boolean | undefined): string {
  return english ? "en-GB" : localeTag();
}

export function formatLabDate(date: string | Date, options: { english?: boolean } = {}): string {
  const d = typeof date === "string" ? new Date(date) : date;
  return mark(d.toLocaleDateString(tagFor(options.english), { day: "2-digit", month: "short", year: "numeric" }), options.english);
}

export function formatLabDateTime(date: string | Date, options: { english?: boolean } = {}): string {
  const d = typeof date === "string" ? new Date(date) : date;
  const tag = tagFor(options.english);
  const text = `${d.toLocaleDateString(tag, { day: "2-digit", month: "short", year: "numeric" })} ${d.toLocaleTimeString(tag, { hour: "2-digit", minute: "2-digit", ...CLOCK })}`;
  return mark(text, options.english);
}

/**
 * Formats an instant with its local time zone name (e.g. for MFA due dates and confirmations).
 * The time zone is always shown: regulated timestamps are never displayed without one.
 */
export function formatDateTimeWithTimeZone(date: string | Date, options: { seconds?: boolean; english?: boolean } = {}): string {
  const d = typeof date === "string" ? new Date(date) : date;
  const text = d.toLocaleString(tagFor(options.english), {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    ...(options.seconds ? { second: "2-digit" as const } : {}),
    ...CLOCK,
    timeZoneName: "short"
  });
  return mark(text, options.english);
}

/**
 * Formats a date-only value (expiry, manufacture) as the same calendar day for every viewer.
 * The API sends these as a date at midnight, so the date part is read as written and formatted
 * in UTC; the browser's time zone must never move it to the neighbouring day or add a clock time.
 */
export function formatLabCalendarDate(value: string, options: { english?: boolean } = {}): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (!match) return formatLabDate(value, options);
  const d = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3])));
  return mark(d.toLocaleDateString(tagFor(options.english), { day: "2-digit", month: "short", year: "numeric", timeZone: "UTC" }), options.english);
}
