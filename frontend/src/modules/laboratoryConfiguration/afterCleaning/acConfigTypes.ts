export interface MachinePart {
  id: number;
  version?: number;
  name: string;
  machineId: number;
}

export interface Machine {
  id: number;
  version?: number;
  name: string;
  parts: MachinePart[];
}

export interface PartConfig {
  id: number;
  version?: number;
  machinePartId: number;
  testType: string;
  testCode: string;
  alertLimit: string;
  actionLimit: string;
  specLimit: string;
  isPathogenTest: boolean;
  unit: string;
}

// Count (CFU) sample types; their unit is fixed by the backend
// (DeriveBatchLocationUnit), the unit field here is informational.
export const AC_COUNT_TYPES = [
  { value: "Swab", label: "Swab", unitHint: "CFU/25 cm2" },
  { value: "Rinse", label: "Rinse", unitHint: "CFU/mL" }
];

export const PATHOGEN_TEST_TYPE = "Pathogen";

// Older rows may carry the pathogen's code as TestType, so the flag is
// the primary signal.
export const isPathogenConfig = (c: PartConfig) => c.isPathogenTest || c.testType === PATHOGEN_TEST_TYPE;

export const acTestTypeLabel = (value: string) => AC_COUNT_TYPES.find((t) => t.value === value)?.label ?? value;
