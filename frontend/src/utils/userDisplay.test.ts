import { describe, expect, it } from "vitest";
import { formatRoleFallback, formatRoleName } from "./userDisplay";

describe("formatRoleName", () => {
  it("formats Qa as Quality Assurance", () => {
    expect(formatRoleName("Qa")).toBe("Quality Assurance");
    expect(formatRoleName("Qa", "QA")).toBe("Quality Assurance");
    expect(formatRoleName(null, "QA")).toBe("Quality Assurance");
    expect(formatRoleName("Qa", "Senior QA Auditor")).toBe("Senior QA Auditor");
  });

  it("keeps existing role labels unchanged", () => {
    expect(formatRoleName("Analyst")).toBe("Analyst");
    expect(formatRoleName("Reviewer")).toBe("Reviewer");
    expect(formatRoleName("SectionHead")).toBe("Section Head");
    expect(formatRoleName("QcManager")).toBe("QC Manager");
    expect(formatRoleName("SystemAdministrator")).toBe("System Administrator");
    expect(formatRoleName("Analyst", "Custom Analyst")).toBe("Custom Analyst");
    expect(formatRoleName("Analyst", "constructor")).toBe("constructor");
  });
});

describe("formatRoleFallback", () => {
  it("formats Qa as Quality Assurance", () => {
    expect(formatRoleFallback("Qa", [])).toBe("Quality Assurance");
    expect(formatRoleFallback("Qa", ["MICRO"], "QA")).toBe("Quality Assurance");
    expect(formatRoleFallback("Qa", ["MICRO"], "Senior QA Auditor")).toBe("Senior QA Auditor");
  });

  it("names the QC Manager role", () => {
    expect(formatRoleFallback("QcManager", [])).toBe("QC Manager");
  });

  it("shows a custom role name as it is written", () => {
    expect(formatRoleFallback("Analyst", ["MICRO"], "Senior Microbiologist")).toBe("Senior Microbiologist");
  });

  it("follows a rename of a built-in role", () => {
    expect(formatRoleFallback("SectionHead", [], "Head of Microbiology")).toBe("Head of Microbiology");
  });

  it("keeps the laboratory prefix for the built-in names", () => {
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
