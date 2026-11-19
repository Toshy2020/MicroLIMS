import { describe, expect, it } from "vitest";
import {
  MAX_ITEM_SPECIFICATION_BATCH_ROWS,
  buildItemSpecificationPastePlan,
  parseOptionalDecimal,
  resolveAssignedTest,
  resolveLimitType,
  resolveToleranceMode,
  toCreateSpecificationPayload,
  toPendingItemSpecifications,
  type AssignedTestOption
} from "./itemSpecificationPaste";

const ASSIGNED_TESTS: AssignedTestOption[] = [
  { testCode: "PH", displayName: "pH" },
  { testCode: "ASSAY", displayName: "Assay" },
  { testCode: "DUP-A", displayName: "Duplicate Test" },
  { testCode: "DUP-B", displayName: "Duplicate Test" }
];

describe("itemSpecificationPaste resolvers", () => {
  it("resolves assigned tests by code or display name and flags unknown or ambiguous tests", () => {
    expect(resolveAssignedTest("ph", ASSIGNED_TESTS).test?.testCode).toBe("PH");
    expect(resolveAssignedTest("Assay", ASSIGNED_TESTS).test?.testCode).toBe("ASSAY");
    expect(resolveAssignedTest("Duplicate Test", ASSIGNED_TESTS).error).toMatch(/ambiguous test/i);
    expect(resolveAssignedTest("MISSING", ASSIGNED_TESTS).error).toMatch(/unknown test/i);
    expect(resolveAssignedTest("   ", ASSIGNED_TESTS).error).toMatch(/test is required/i);
  });

  it("resolves limit types by value or select label and flags unknown limit types", () => {
    expect(resolveLimitType("Range").option?.value).toBe("Range");
    expect(resolveLimitType("nmt (not more than)").option?.value).toBe("NotMoreThan");
    expect(resolveLimitType("Target ± Tolerance").option?.value).toBe("TargetWithTolerance");
    expect(resolveLimitType("Count-Tiered (Alert / Action / Spec)").option?.value).toBe("CountTiered");
    expect(resolveLimitType("Qualitative (descriptive text)").option?.value).toBe("Qualitative");
    expect(resolveLimitType("BogusType").error).toMatch(/unknown limit type/i);
    expect(resolveLimitType("").error).toMatch(/limit type is required/i);
  });

  it("resolves tolerance mode by value or label, allows blank, and flags unknown tolerance modes", () => {
    expect(resolveToleranceMode("")).toEqual({ option: null, error: null });
    expect(resolveToleranceMode("   ")).toEqual({ option: null, error: null });
    expect(resolveToleranceMode("absolute").option?.value).toBe("Absolute");
    expect(resolveToleranceMode("Percent").option?.value).toBe("Percent");
    expect(resolveToleranceMode("Absolute (± value)").option?.value).toBe("Absolute");
    expect(resolveToleranceMode("percent (± %)").option?.value).toBe("Percent");
    expect(resolveToleranceMode("Relative").error).toMatch(/unknown tolerance mode/i);
  });

  it("parses optional decimal cells allowing a leading minus sign and rejecting non-numeric text", () => {
    expect(parseOptionalDecimal("")).toEqual({ value: null, error: null });
    expect(parseOptionalDecimal("  -1.5  ")).toEqual({ value: -1.5, error: null });
    expect(parseOptionalDecimal("0")).toEqual({ value: 0, error: null });
    expect(parseOptionalDecimal("12.75")).toEqual({ value: 12.75, error: null });
    for (const bad of ["abc", "1.2.3", "< 5", "1e3", "-", "1-"]) {
      expect(parseOptionalDecimal(bad).error).not.toBeNull();
    }
  });
});

describe("buildItemSpecificationPastePlan", () => {
  it("uses the exact plain-field columns in SpecificationParameterDialog order and refuses when no tests are offered", () => {
    const emptyPlan = buildItemSpecificationPastePlan({ assignedTests: [] });
    expect(emptyPlan.refusal).toMatch(/assigned tests/i);

    const plan = buildItemSpecificationPastePlan({ assignedTests: ASSIGNED_TESTS });
    expect(plan.maxRows).toBe(MAX_ITEM_SPECIFICATION_BATCH_ROWS);
    expect(plan.refusal).toBeUndefined();
    expect(plan.columns.map((c) => c.label)).toEqual([
      "Test",
      "Parameter name",
      "Limit type",
      "Lower limit",
      "Upper limit",
      "Target",
      "Tolerance",
      "Tolerance mode",
      "Alert limit",
      "Action limit",
      "Spec limit",
      "Expected result text",
      "Unit"
    ]);
  });

  it("maps accepted rows to CreateSpecificationRequest payloads without lowerInclusive/upperInclusive and with toleranceMode", () => {
    const pending = toPendingItemSpecifications(
      [
        {
          target: null,
          values: {
            testCode: "pH",
            parameterName: "  pH (1% solution)  ",
            limitType: "Range (NLT — NMT)",
            lowerLimit: "-1.5",
            upperLimit: "7.5",
            target: "",
            tolerance: "",
            toleranceMode: "",
            alertLimit: "",
            actionLimit: "",
            specLimit: "",
            expectedResultText: "",
            unit: " pH "
          }
        },
        {
          target: null,
          values: {
            testCode: "ASSAY",
            parameterName: "Assay target",
            limitType: "TargetWithTolerance",
            lowerLimit: "",
            upperLimit: "",
            target: "100",
            tolerance: "5",
            toleranceMode: "Percent (± %)",
            alertLimit: "",
            actionLimit: "",
            specLimit: "",
            expectedResultText: "",
            unit: "%"
          }
        },
        {
          target: null,
          values: {
            testCode: "ASSAY",
            parameterName: "Microbial count",
            limitType: "CountTiered",
            lowerLimit: "",
            upperLimit: "",
            target: "",
            tolerance: "",
            toleranceMode: "",
            alertLimit: "  < 10  ",
            actionLimit: " <= 50 ",
            specLimit: " NMT 100 cfu/g ",
            expectedResultText: "",
            unit: ""
          }
        }
      ],
      ASSIGNED_TESTS
    );

    const rangePayload = toCreateSpecificationPayload(42, pending[0]);
    expect(rangePayload).toEqual({
      itemId: 42,
      testCode: "PH",
      parameterName: "pH (1% solution)",
      limitType: "Range",
      lowerLimit: -1.5,
      upperLimit: 7.5,
      target: null,
      tolerance: null,
      toleranceMode: null,
      alertLimit: null,
      actionLimit: null,
      specLimit: null,
      expectedResultText: null,
      unit: "pH"
    });
    expect("lowerInclusive" in rangePayload).toBe(false);
    expect("upperInclusive" in rangePayload).toBe(false);

    expect(toCreateSpecificationPayload(42, pending[1])).toEqual({
      itemId: 42,
      testCode: "ASSAY",
      parameterName: "Assay target",
      limitType: "TargetWithTolerance",
      lowerLimit: null,
      upperLimit: null,
      target: 100,
      tolerance: 5,
      toleranceMode: "Percent",
      alertLimit: null,
      actionLimit: null,
      specLimit: null,
      expectedResultText: null,
      unit: "%"
    });

    expect(toCreateSpecificationPayload(42, pending[2])).toEqual({
      itemId: 42,
      testCode: "ASSAY",
      parameterName: "Microbial count",
      limitType: "CountTiered",
      lowerLimit: null,
      upperLimit: null,
      target: null,
      tolerance: null,
      toleranceMode: null,
      alertLimit: "< 10",
      actionLimit: "<= 50",
      specLimit: "NMT 100 cfu/g",
      expectedResultText: null,
      unit: null
    });
  });
});
