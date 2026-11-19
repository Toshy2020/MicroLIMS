import { afterEach, describe, expect, it } from "vitest";
import { forceLanguage, i18n } from "../i18n";
import { formatDateTimeWithTimeZone, formatLabCalendarDate, formatLabDate, formatLabDateTime } from "./formatDate";

const INSTANT = "2026-12-05T14:30:00Z";

afterEach(async () => {
  await i18n.changeLanguage("en");
});

describe("formatDate", () => {
  it("keeps the English output as it was", () => {
    expect(formatLabDate(INSTANT)).toBe("05 Dec 2026");
    expect(formatLabDateTime(INSTANT)).toMatch(/^05 Dec 2026 \d{2}:\d{2}$/);
    expect(formatDateTimeWithTimeZone(INSTANT)).toMatch(/^05 Dec 2026, \d{2}:\d{2} .+$/);
  });

  it("shows an Arabic month name with Latin digits in Arabic", async () => {
    await i18n.changeLanguage("ar");
    const date = formatLabDate(INSTANT);
    expect(date).toContain("ديسمبر");
    expect(date).toContain("2026");
    expect(date).not.toMatch(/[٠-٩]/);
    expect(formatLabDateTime(INSTANT)).not.toMatch(/[٠-٩]/);
  });

  it("always shows the time zone on a regulated timestamp, in both languages", async () => {
    const english = formatDateTimeWithTimeZone(INSTANT);
    await i18n.changeLanguage("ar");
    const arabic = formatDateTimeWithTimeZone(INSTANT);
    expect(arabic).toContain("ديسمبر");
    expect(arabic).not.toMatch(/[٠-٩]/);
    // Same clock time in both; the zone name or offset follows it.
    const clock = /\d{2}:\d{2}/;
    expect(english.match(clock)?.[0]).toBe(arabic.match(clock)?.[0]);
    expect(english.split(clock)[1].trim().length).toBeGreaterThan(0);
    expect(arabic.split(clock)[1].trim().length).toBeGreaterThan(0);
  });

  it("uses a 24-hour clock in Arabic", async () => {
    await i18n.changeLanguage("ar");
    const time = (formatLabDateTime(INSTANT).split(" ").pop() ?? "").replace(/⁩/g, "");
    expect(time).toMatch(/^\d{2}:\d{2}$/);
  });

  it("is English while a printable record forces English, then follows the interface again", async () => {
    await i18n.changeLanguage("ar");
    const release = forceLanguage("en");
    expect(formatLabDate(INSTANT)).toBe("05 Dec 2026");
    release();
    expect(formatLabDate(INSTANT)).toContain("ديسمبر");
  });

  it("prints English month names with a zone for an export while the interface is Arabic", async () => {
    await i18n.changeLanguage("ar");
    const text = formatDateTimeWithTimeZone(INSTANT, { english: true });
    expect(text).toContain("Dec 2026");
    expect(text).toMatch(/\d{2}:\d{2} [A-Za-z][A-Za-z0-9+:-]*$/);
  });

  it("wraps every Arabic date text in a right-to-left isolate and never English text", async () => {
    await i18n.changeLanguage("ar");
    const wrapped = (t: string) => t.startsWith("⁧") && t.endsWith("⁩");
    expect(wrapped(formatLabDate(INSTANT))).toBe(true);
    expect(wrapped(formatLabDateTime(INSTANT))).toBe(true);
    expect(wrapped(formatDateTimeWithTimeZone(INSTANT))).toBe(true);
    expect(wrapped(formatLabCalendarDate("2026-12-05"))).toBe(true);
    expect(wrapped(formatLabCalendarDate("2026/12/05"))).toBe(true);
    await i18n.changeLanguage("en");
    expect(formatLabDate(INSTANT)).toBe("05 Dec 2026");
    expect(formatLabCalendarDate("2026-12-05")).toBe("05 Dec 2026");
  });

  it("the english option ignores the interface language", async () => {
    await i18n.changeLanguage("ar");
    expect(formatLabDate(INSTANT, { english: true })).toBe("05 Dec 2026");
    expect(formatLabDateTime(INSTANT, { english: true })).toMatch(/^05 Dec 2026 \d{2}:\d{2}$/);
    expect(formatLabCalendarDate("2026-12-05", { english: true })).toBe("05 Dec 2026");
    expect(formatDateTimeWithTimeZone(INSTANT, { english: true })).toMatch(/^05 Dec 2026, /);
  });
});
