import { afterEach, describe, expect, it, vi } from "vitest";
import { formatCount } from "./formatCount";

afterEach(() => {
  vi.restoreAllMocks();
});

describe("formatCount", () => {
  it("keeps thousands separators", () => {
    expect(formatCount(1234567)).toBe("1,234,567");
    expect(formatCount(2)).toBe("2");
  });

  it("does not depend on the default locale of the browser", () => {
    // A browser whose default locale is Arabic formats Number#toLocaleString() with Arabic-Indic digits.
    const original = Number.prototype.toLocaleString;
    vi.spyOn(Number.prototype, "toLocaleString").mockImplementation(function (this: number, locales?: Intl.LocalesArgument, options?: Intl.NumberFormatOptions) {
      return original.call(this, locales ?? "ar-EG", options);
    });
    expect(formatCount(2)).toBe("2");
    expect(formatCount(1234)).toBe("1,234");
  });
});
