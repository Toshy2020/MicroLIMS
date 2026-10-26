import { describe, expect, it } from "vitest";
import { formatRoleFallback } from "./userDisplay";

describe("formatRoleFallback", () => {
  it("shows a custom role name as it is written", () => {
    expect(formatRoleFallback("Analyst", ["MICRO"], "Senior Microbiologist")).toBe("Senior Microbiologist");
  });

  it("follows a rename of a built-in role", () => {
    expect(formatRoleFallback("SectionHead", [], "Head of Microbiology")).toBe("Head of Microbiology");
  });

  it("keeps the laboratory prefix for the four built-in names", () => {
    expect(formatRoleFallback("Analyst", ["MICRO"], "Analyst")).toBe("Microbiology Analyst");
    expect(formatRoleFallback("SectionHead", ["FP"], "Section Head")).toBe("Physicochemical Section Head");
    expect(formatRoleFallback("Reviewer", ["MICRO"], "Reviewer")).toBe("Quality Reviewer");
    expect(formatRoleFallback("SystemAdministrator", ["MICRO"], "System Administrator")).toBe("System Administrator");
  });

  it("adds no prefix for a user in several laboratories or none", () => {
    expect(formatRoleFallback("Analyst", ["MICRO", "FP"], "Analyst")).toBe("Analyst");
    expect(formatRoleFallback("Analyst", [], "Analyst")).toBe("Analyst");
  });

  it("does not apply the prefix to a custom role that shares a base type", () => {
    expect(formatRoleFallback("Analyst", ["MICRO"], "QC Trainee")).toBe("QC Trainee");
  });

  it("treats a role name that matches an object property as an ordinary custom name", () => {
    expect(formatRoleFallback("Analyst", [], "constructor")).toBe("constructor");
    expect(formatRoleFallback("Analyst", [], "toString")).toBe("toString");
  });

  it("falls back to the role type when no name is known yet", () => {
    expect(formatRoleFallback("Analyst", ["MICRO"])).toBe("Microbiology Analyst");
    expect(formatRoleFallback("Reviewer", [], null)).toBe("Quality Reviewer");
    expect(formatRoleFallback("Analyst", [], "  ")).toBe("Analyst");
  });

  it("says Staff when there is neither a name nor a type", () => {
    expect(formatRoleFallback(null, [])).toBe("Staff");
    expect(formatRoleFallback(null, [], null)).toBe("Staff");
  });
});
