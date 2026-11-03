import { describe, expect, it } from "vitest";
import { formatRoleFallback } from "./userDisplay";

describe("formatRoleFallback", () => {
  it("names the QC Manager role", () => {
    expect(formatRoleFallback("QcManager", [])).toBe("QC Manager");
  });
});
