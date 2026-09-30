export interface EmRoom {
  id: number;
  version?: number;
  name: string;
  departmentId: number;
  gradeClassification: string;
}

export interface EmDepartment {
  id: number;
  version?: number;
  name: string;
  class: string;
  testingFrequency: string;
  rooms: EmRoom[];
}

export interface RoomTestConfig {
  id: number;
  version?: number;
  roomId: number;
  testType: string;
  testCode: string;
  alertLimit: string;
  actionLimit: string;
  specLimit: string;
  unit: string;
}

// Mirrors backend RoomTestTypes. Passive and surface samples report a
// fixed unit; the others report the unit entered on the configuration,
// which the backend requires for them.
export const EM_TEST_TYPES: { value: string; label: string; unitHint: string; needsUnit: boolean }[] = [
  { value: "PassiveAirSample", label: "Passive air sample", unitHint: "e.g. CFU/plate/4 hours", needsUnit: false },
  { value: "ActiveAirSample", label: "Active air sample", unitHint: "e.g. CFU/m³", needsUnit: true },
  { value: "SurfaceAirSample", label: "Surface sample", unitHint: "e.g. CFU/25 cm2", needsUnit: false },
  { value: "CompressedAir", label: "Compressed air", unitHint: "e.g. CFU/m³", needsUnit: true },
  { value: "Drains", label: "Drains", unitHint: "e.g. CFU/swab", needsUnit: true }
];

export const emTestType = (value: string) => EM_TEST_TYPES.find((t) => t.value === value);
export const emTestTypeLabel = (value: string) => emTestType(value)?.label ?? value;

export const EM_GRADES = ["A", "B", "C", "D"];
