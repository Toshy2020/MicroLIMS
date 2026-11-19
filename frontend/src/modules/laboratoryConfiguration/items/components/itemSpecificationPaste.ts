import { parseDecimal } from "../../../../components/bench/NumericEntryField";
import type { PasteAcceptedRow, PastePlan } from "../../../../components/entry/PastePreviewDialog";
import type {
  CreateSpecificationPayload,
  LimitType,
  ToleranceMode
} from "../../specifications/services/SpecificationService";
import { LIMIT_TYPE_OPTIONS, TOLERANCE_MODE_OPTIONS } from "../specWizard/specHelpers";

export const MAX_ITEM_SPECIFICATION_BATCH_ROWS = 200;

const fold = (value: string) => value.trim().toLowerCase();

export interface AssignedTestOption {
  testCode: string;
  displayName: string;
}

export interface PendingItemSpecification {
  testCode: string;
  parameterName: string;
  limitType: LimitType;
  limitTypeLabel: string;
  lowerLimit: number | null;
  upperLimit: number | null;
  target: number | null;
  tolerance: number | null;
  toleranceMode: ToleranceMode | null;
  toleranceModeLabel: string;
  alertLimit: string;
  actionLimit: string;
  specLimit: string;
  expectedResultText: string;
  unit: string;
  errors?: Record<string, string>;
}

/**
 * Resolves a pasted test code or display name against the tests offered by the add-specification form.
 */
export function resolveAssignedTest(
  raw: string,
  assignedTests: AssignedTestOption[]
): { test: AssignedTestOption | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { test: null, error: "Test is required." };
  }
  const key = fold(trimmed);
  const byCode = assignedTests.filter((t) => fold(t.testCode) === key);
  if (byCode.length === 1) {
    return { test: byCode[0], error: null };
  }
  if (byCode.length > 1) {
    return { test: null, error: `Ambiguous test '${trimmed}'.` };
  }
  const byName = assignedTests.filter((t) => fold(t.displayName) === key);
  if (byName.length === 1) {
    return { test: byName[0], error: null };
  }
  if (byName.length > 1) {
    return { test: null, error: `Ambiguous test '${trimmed}'.` };
  }
  return { test: null, error: `Unknown test '${trimmed}'.` };
}

/**
 * Resolves a pasted limit type against the values and labels used by the add-specification form's select.
 */
export function resolveLimitType(
  raw: string
): { option: { value: LimitType; label: string } | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { option: null, error: "Limit type is required." };
  }
  const key = fold(trimmed);
  const matched = LIMIT_TYPE_OPTIONS.find(
    (opt) => fold(opt.value) === key || fold(opt.label) === key
  );
  if (!matched) {
    return { option: null, error: `Unknown limit type '${trimmed}'.` };
  }
  return { option: matched, error: null };
}

/**
 * Resolves an optional tolerance mode against the values and labels used by the add-specification form's select.
 */
export function resolveToleranceMode(
  raw: string
): { option: { value: ToleranceMode; label: string } | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { option: null, error: null };
  }
  const key = fold(trimmed);
  const matched = TOLERANCE_MODE_OPTIONS.find(
    (opt) => fold(opt.value) === key || fold(opt.label) === key
  );
  if (!matched) {
    return { option: null, error: `Unknown tolerance mode '${trimmed}'.` };
  }
  return { option: matched, error: null };
}

/**
 * Parses an optional numeric limit cell using the form's shared decimal parser (allows leading minus sign).
 */
export function parseOptionalDecimal(raw: string): { value: number | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { value: null, error: null };
  }
  const parsed = parseDecimal(trimmed);
  if (parsed === null) {
    return {
      value: null,
      error: "Enter a number using digits, at most one decimal point, and an optional leading minus sign."
    };
  }
  return { value: parsed === 0 ? 0 : parsed, error: null };
}

/**
 * Builds the `PastePlan` for item specifications matching `SpecificationParameterDialog`'s plain-field order.
 * Does not evaluate or compare limits or enforce per-limit-type required fields; the backend owns all limit logic.
 */
export function buildItemSpecificationPastePlan({
  assignedTests
}: {
  assignedTests: AssignedTestOption[];
}): PastePlan {
  return {
    title: "Paste preview — Item specifications",
    maxRows: MAX_ITEM_SPECIFICATION_BATCH_ROWS,
    refusal:
      assignedTests.length === 0
        ? "None of this item's assigned tests belong to your laboratory."
        : undefined,
    columns: [
      {
        key: "testCode",
        label: "Test",
        check: (v) => resolveAssignedTest(v, assignedTests).error,
        normalize: (v) => resolveAssignedTest(v, assignedTests).test?.testCode ?? v.trim()
      },
      {
        key: "parameterName",
        label: "Parameter name",
        check: () => null,
        normalize: (v) => v.trim()
      },
      {
        key: "limitType",
        label: "Limit type",
        check: (v) => resolveLimitType(v).error,
        normalize: (v) => resolveLimitType(v).option?.label ?? v.trim()
      },
      {
        key: "lowerLimit",
        label: "Lower limit",
        check: (v) => parseOptionalDecimal(v).error,
        normalize: (v) => v.trim()
      },
      {
        key: "upperLimit",
        label: "Upper limit",
        check: (v) => parseOptionalDecimal(v).error,
        normalize: (v) => v.trim()
      },
      {
        key: "target",
        label: "Target",
        check: (v) => parseOptionalDecimal(v).error,
        normalize: (v) => v.trim()
      },
      {
        key: "tolerance",
        label: "Tolerance",
        check: (v) => parseOptionalDecimal(v).error,
        normalize: (v) => v.trim()
      },
      {
        key: "toleranceMode",
        label: "Tolerance mode",
        check: (v) => resolveToleranceMode(v).error,
        normalize: (v) => resolveToleranceMode(v).option?.label ?? v.trim()
      },
      {
        key: "alertLimit",
        label: "Alert limit",
        check: () => null,
        normalize: (v) => v.trim()
      },
      {
        key: "actionLimit",
        label: "Action limit",
        check: () => null,
        normalize: (v) => v.trim()
      },
      {
        key: "specLimit",
        label: "Spec limit",
        check: () => null,
        normalize: (v) => v.trim()
      },
      {
        key: "expectedResultText",
        label: "Expected result text",
        check: () => null,
        normalize: (v) => v.trim()
      },
      {
        key: "unit",
        label: "Unit",
        check: () => null,
        normalize: (v) => v.trim()
      }
    ]
  };
}

/**
 * Converts rows accepted from `PastePreviewDialog` into typed pending item specifications.
 */
export function toPendingItemSpecifications(
  accepted: PasteAcceptedRow[],
  assignedTests: AssignedTestOption[]
): PendingItemSpecification[] {
  return accepted.map((row) => {
    const testRes = resolveAssignedTest(row.values.testCode ?? "", assignedTests);
    const limitTypeRes = resolveLimitType(row.values.limitType ?? "");
    const limitType: LimitType = limitTypeRes.option?.value ?? "Range";
    const tolModeRes = resolveToleranceMode(row.values.toleranceMode ?? "");

    return {
      testCode: testRes.test?.testCode ?? (row.values.testCode ?? "").trim(),
      parameterName: (row.values.parameterName ?? "").trim(),
      limitType,
      limitTypeLabel: limitTypeRes.option?.label ?? (row.values.limitType ?? "").trim(),
      lowerLimit: parseOptionalDecimal(row.values.lowerLimit ?? "").value,
      upperLimit: parseOptionalDecimal(row.values.upperLimit ?? "").value,
      target: parseOptionalDecimal(row.values.target ?? "").value,
      tolerance: parseOptionalDecimal(row.values.tolerance ?? "").value,
      toleranceMode: tolModeRes.option?.value ?? null,
      toleranceModeLabel: tolModeRes.option?.label ?? "",
      alertLimit: (row.values.alertLimit ?? "").trim(),
      actionLimit: (row.values.actionLimit ?? "").trim(),
      specLimit: (row.values.specLimit ?? "").trim(),
      expectedResultText: (row.values.expectedResultText ?? "").trim(),
      unit: (row.values.unit ?? "").trim()
    };
  });
}

/**
 * Maps a pending specification into the `CreateSpecificationRequest` DTO for the batch endpoint.
 */
export function toCreateSpecificationPayload(
  itemId: number,
  spec: PendingItemSpecification
): CreateSpecificationPayload {
  return {
    itemId,
    testCode: spec.testCode,
    parameterName: spec.parameterName.trim() === "" ? null : spec.parameterName.trim(),
    limitType: spec.limitType,
    lowerLimit: spec.lowerLimit,
    upperLimit: spec.upperLimit,
    target: spec.target,
    tolerance: spec.tolerance,
    toleranceMode: spec.toleranceMode,
    alertLimit: spec.alertLimit.trim() === "" ? null : spec.alertLimit.trim(),
    actionLimit: spec.actionLimit.trim() === "" ? null : spec.actionLimit.trim(),
    specLimit: spec.specLimit.trim() === "" ? null : spec.specLimit.trim(),
    expectedResultText: spec.expectedResultText.trim() === "" ? null : spec.expectedResultText.trim(),
    unit: spec.unit.trim() === "" ? null : spec.unit.trim()
  };
}
