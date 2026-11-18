import { describe, expect, it } from "vitest";

// MUI's sx understands m/p with t/r/b/l/x/y and the full logical names
// (marginInlineStart, paddingInlineEnd, ...), but not the Bootstrap-style
// shorthands ms/me/ps/pe: those are passed through as unknown CSS and the
// spacing is silently lost. Right-to-left layout does not need them, because
// the RTL style cache (stylis-plugin-rtl) already mirrors ml/mr/pl/pr.
const sources = import.meta.glob<string>(["../**/*.tsx", "!../**/*.test.tsx"], {
  query: "?raw",
  import: "default",
  eager: true
});

const UNSUPPORTED = /[{\s,](ms|me|ps|pe):\s*[-\d"'{]/;

export function unsupportedSpacingKeys(files: Record<string, string>): string[] {
  const found: string[] = [];
  for (const [path, text] of Object.entries(files)) {
    text.split("\n").forEach((line, index) => {
      if (UNSUPPORTED.test(line)) found.push(`${path}:${index + 1}`);
    });
  }
  return found;
}

describe("sx spacing keys", () => {
  it("uses no spacing shorthand that MUI ignores", () => {
    expect(unsupportedSpacingKeys(sources)).toEqual([]);
  });

  it("detects an ignored shorthand", () => {
    expect(unsupportedSpacingKeys({ "a.tsx": 'x\n<Box sx={{ me: "auto", ps: 1 }} />' })).toEqual(["a.tsx:2"]);
    expect(unsupportedSpacingKeys({ "b.tsx": "<Box sx={{ mr: 1, pl: 0.5, items: 2 }} />" })).toEqual([]);
  });
});
