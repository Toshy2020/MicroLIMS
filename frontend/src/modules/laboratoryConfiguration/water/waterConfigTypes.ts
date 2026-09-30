export interface SamplingPoint {
  id: number;
  version?: number;
  code: string;
  location: string;
  testingFrequency: string;
  assignedTestCodes: string[];
  waterDepartmentId: number | null;
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
