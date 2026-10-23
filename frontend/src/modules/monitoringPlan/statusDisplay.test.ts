import { describe, expect, it } from "vitest";
import { periodLabel } from "./statusDisplay";

describe("periodLabel", () => {
  it("writes period keys the way people say them", () => {
    expect(periodLabel("2026-W43")).toBe("Week 43, 2026");
    expect(periodLabel("2026-10")).toBe("October 2026");
    expect(periodLabel("D20261022")).toBe("22 October 2026");
    expect(periodLabel("resp-12")).toBe("Follow-up sampling");
    expect(periodLabel("manual-20261028")).toBe("Manual entry");
    expect(periodLabel("something-else")).toBe("something-else");
  });
});
