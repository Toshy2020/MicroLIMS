import type { FrequencyColumns } from "../../../components/configHierarchy";

export interface WaterPointLabFrequency extends FrequencyColumns {
  sectionId: number;
  sectionCode: string;
  type?: "Daily" | "Weekly" | "Monthly" | null;
  interval?: number | null;
  anchorMonth?: number | null;
  weekOfMonth?: number | null;
  description?: string;
}

export interface SamplingPoint extends FrequencyColumns {
  id: number;
  version?: number;
  code: string;
  location: string;
  testingFrequency: string;
  assignedTestCodes: string[];
  waterDepartmentId: number | null;
  labFrequencies?: WaterPointLabFrequency[];
}

export interface WaterDept {
  id: number;
  version?: number;
  name: string;
  samplingPoints: SamplingPoint[];
}

export interface SamplingConfig {
  id: number;
  version?: number;
  waterSamplingPointId: number;
  testCode: string;
  alertLimit: string;
  actionLimit: string;
  specLimit: string;
  unit: string;
  limitType?: "Range" | "NotMoreThan" | "NotLessThan" | "Qualitative" | null;
  lowerLimit?: number | null;
  upperLimit?: number | null;
  lowerInclusive?: boolean;
  upperInclusive?: boolean;
  expectedResultText?: string | null;
}

// Only count tests (e.g. TAMC-Water) carry Alert/Action/Spec limits;
// pathogen tests are presence/absence.
export function countTestsOf(point: SamplingPoint, isCountTest: (code: string) => boolean): string[] {
  return (point.assignedTestCodes ?? []).filter(isCountTest);
}

export interface PointHealth {
  noTests: boolean;
  missingLimitCodes: string[];
}

export function pointHealth(point: SamplingPoint, configs: SamplingConfig[], isCountTest: (code: string) => boolean): PointHealth {
  const configured = new Set(configs.map((c) => c.testCode));
  return {
    noTests: (point.assignedTestCodes ?? []).length === 0,
    missingLimitCodes: countTestsOf(point, isCountTest).filter((code) => !configured.has(code))
  };
}

export const needsAttention = (h: PointHealth) => h.noTests || h.missingLimitCodes.length > 0;

export const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;
