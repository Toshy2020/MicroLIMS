import { describe, expect, it } from "vitest";
import { buildCsv } from "./exportCsv";

describe("buildCsv", () => {
  it("strips Unicode bidi control characters from every cell", () => {
    const csv = buildCsv([["Meta", "\u2067x\u2069"]], ["\u200Ea", "b\u202Ec"], [["\u2067 20 Nov 2026\u2069", "\u200Fz\u2066"]]);
    expect(csv).not.toMatch(/[\u200E\u200F\u202A-\u202E\u2066-\u2069]/);
    expect(csv).toContain("a,bc");
    expect(csv).toContain(" 20 Nov 2026,z");
  });
});
