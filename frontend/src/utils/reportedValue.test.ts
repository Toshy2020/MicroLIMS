import { describe, expect, it } from "vitest";
import { withUnit } from "./reportedValue";

describe("withUnit", () => {
  it("does not repeat a unit the reported value already carries", () => {
    expect(withUnit("140 cfu/g", "cfu/g")).toBe("140 cfu/g");
    expect(withUnit("<20 cfu/g", "cfu/g")).toBe("<20 cfu/g");
  });

  it("matches the unit regardless of letter case", () => {
    expect(withUnit("140 cfu/g", "CFU/g")).toBe("140 cfu/g");
    expect(withUnit("140 CFU/g", "cfu/g")).toBe("140 CFU/g");
  });

  it("appends the unit when the reported value is a bare number", () => {
    expect(withUnit("98.5", "%")).toBe("98.5 %");
    expect(withUnit("12", "mg")).toBe("12 mg");
    expect(withUnit("<0.5", "ppm")).toBe("<0.5 ppm");
  });

  it("returns the reported value unchanged when there is no unit", () => {
    expect(withUnit("Not Detected", null)).toBe("Not Detected");
    expect(withUnit("Not Detected", undefined)).toBe("Not Detected");
    expect(withUnit("140", "  ")).toBe("140");
  });
});
