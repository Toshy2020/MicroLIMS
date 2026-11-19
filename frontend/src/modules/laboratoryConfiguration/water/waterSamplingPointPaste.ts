import { frequencyLabel, frequencyRule } from "../../../components/configHierarchy";
import type { FrequencyValue } from "../../../components/configHierarchy";
import { parseFrequencyInput } from "../../../components/entry/frequencyPaste";
import type { PasteAcceptedRow, PasteMatchRow, PastePlan } from "../../../components/entry/PastePreviewDialog";
import type { TestDefinitionOption } from "../../../hooks/useTestDefinitions";
import type { CreateWaterSamplingPointPayload, SamplingPoint, WaterDept } from "./waterConfigTypes";

export { parseFrequencyInput };

export const MAX_WATER_BATCH_ROWS = 200;

const fold = (value: string) => value.trim().toLowerCase();

export interface PendingWaterPoint {
  code: string;
  waterDepartmentId: number;
  waterDepartmentName: string;
  location: string;
  frequency: FrequencyValue;
  testingFrequency: string;
  assignedTestCodes: string[];
  errors?: Record<string, string>;
}

/**
 * Resolves a pasted water system name against the departments loaded on the page.
 */
export function resolveWaterDepartment(
  raw: string,
  departments: WaterDept[]
): { dept: WaterDept | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { dept: null, error: "Water system is required." };
  }
  const key = fold(trimmed);
  const matches = departments.filter((d) => fold(d.name) === key);
  if (matches.length === 1) {
    return { dept: matches[0], error: null };
  }
  if (matches.length > 1) {
    return { dept: null, error: `Ambiguous water system '${trimmed}'.` };
  }
  return { dept: null, error: `Unknown water system '${trimmed}'.` };
}

/**
 * Resolves comma- or semicolon-separated test codes or display names against the loaded Test Master list.
 */
export function resolveTestCodes(
  raw: string,
  options: TestDefinitionOption[]
): { codes: string[]; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { codes: [], error: null };
  }

  const tokens = trimmed
    .split(/[,;]/)
    .map((t) => t.trim())
    .filter((t) => t.length > 0);

  if (tokens.length === 0) {
    return { codes: [], error: "Enter valid test codes or names." };
  }

  const resolved: string[] = [];
  const seen = new Set<string>();

  for (const token of tokens) {
    const key = fold(token);
    const byCode = options.filter((o) => fold(o.code) === key);
    let matched: TestDefinitionOption;

    if (byCode.length === 1) {
      matched = byCode[0];
    } else if (byCode.length > 1) {
      return { codes: [], error: `Ambiguous test '${token}'.` };
    } else {
      const byName = options.filter((o) => fold(o.displayName) === key);
      if (byName.length === 1) {
        matched = byName[0];
      } else if (byName.length > 1) {
        return { codes: [], error: `Ambiguous test '${token}'.` };
      } else {
        return { codes: [], error: `Unknown test '${token}'.` };
      }
    }

    if (!seen.has(matched.code)) {
      seen.add(matched.code);
      resolved.push(matched.code);
    }
  }

  return { codes: resolved, error: null };
}

/**
 * Builds the `PastePlan` for Water Sampling Points matching the create form's field order.
 */
export function buildWaterSamplingPointPastePlan({
  departments,
  existingPoints,
  testOptions
}: {
  departments: WaterDept[];
  existingPoints: SamplingPoint[];
  testOptions: TestDefinitionOption[];
}): PastePlan {
  const existingCodes = new Set(existingPoints.map((p) => fold(p.code)).filter(Boolean));

  return {
    title: "Paste preview — Water sampling points",
    maxRows: MAX_WATER_BATCH_ROWS,
    refusal: departments.length === 0 ? "Create a water system before pasting sample locations." : undefined,
    columns: [
      {
        key: "code",
        label: "Point code",
        check: (v) => {
          const trimmed = v.trim();
          if (!trimmed) return "Point code is required.";
          if (existingCodes.has(fold(trimmed))) return "Already exists.";
          return null;
        },
        normalize: (v) => v.trim()
      },
      {
        key: "waterDepartment",
        label: "Water system",
        check: (v) => resolveWaterDepartment(v, departments).error,
        normalize: (v) => resolveWaterDepartment(v, departments).dept?.name ?? v.trim()
      },
      {
        key: "location",
        label: "Point name",
        check: () => null,
        normalize: (v) => v.trim()
      },
      {
        key: "frequency",
        label: "Frequency",
        check: (v) => parseFrequencyInput(v).error,
        normalize: (v) => {
          const parsed = parseFrequencyInput(v);
          return parsed.error === null ? frequencyLabel(parsed.value) : v.trim();
        }
      },
      {
        key: "assignedTestCodes",
        label: "Assigned tests",
        check: (v) => resolveTestCodes(v, testOptions).error,
        normalize: (v) => resolveTestCodes(v, testOptions).codes.join(", ")
      }
    ],
    match: (cells) => {
      const codeCounts = new Map<string, number>();
      for (const r of cells) {
        const key = fold(r[0] ?? "");
        if (key) codeCounts.set(key, (codeCounts.get(key) ?? 0) + 1);
      }

      const rows: PasteMatchRow[] = cells.map((r) => {
        const code = (r[0] ?? "").trim();
        const key = fold(code);
        const duplicateInPaste = key !== "" && (codeCounts.get(key) ?? 0) > 1;
        return {
          target: null,
          values: {
            code,
            waterDepartment: (r[1] ?? "").trim(),
            location: (r[2] ?? "").trim(),
            frequency: (r[3] ?? "").trim(),
            assignedTestCodes: (r[4] ?? "").trim()
          },
          problem: duplicateInPaste ? "Duplicate point code in paste." : undefined
        };
      });

      return { rows, unmatchedTargets: [] };
    }
  };
}

/**
 * Converts rows accepted from `PastePreviewDialog` into typed pending water sampling points.
 */
export function toPendingWaterPoints(
  accepted: PasteAcceptedRow[],
  departments: WaterDept[],
  testOptions: TestDefinitionOption[]
): PendingWaterPoint[] {
  return accepted.map((row) => {
    const dept = resolveWaterDepartment(row.values.waterDepartment ?? "", departments).dept;
    const freq = parseFrequencyInput(row.values.frequency ?? "").value;
    const tests = resolveTestCodes(row.values.assignedTestCodes ?? "", testOptions).codes;

    return {
      code: (row.values.code ?? "").trim(),
      waterDepartmentId: dept?.id ?? 0,
      waterDepartmentName: dept?.name ?? (row.values.waterDepartment ?? "").trim(),
      location: (row.values.location ?? "").trim(),
      frequency: freq,
      testingFrequency: frequencyLabel(freq),
      assignedTestCodes: tests
    };
  });
}

/**
 * Maps a pending water point into the `CreateWaterSamplingPointRequest` DTO for the batch endpoint.
 */
export function toCreateWaterSamplingPointPayload(point: PendingWaterPoint): CreateWaterSamplingPointPayload {
  return {
    code: point.code,
    location: point.location,
    testingFrequency: point.testingFrequency,
    assignedTestCodes: point.assignedTestCodes,
    waterDepartmentId: point.waterDepartmentId,
    frequency: frequencyRule(point.frequency)
  };
}
