import type { KpiGap } from "./kpiTypes";

// Ratios (utilisation, forwardLoad, weightedOnTime, weightedRightFirstTime) are fractions: 1 = 100%.
// A rate is null when fewer than five tests stand behind it.
export interface AnalystWorkloadRow {
  userId: number;
  fullName: string;
  capacityUnits: number;
  deliveredUnits: number;
  utilisation: number | null;
  utilisationCheckProfile: boolean;
  openUnits: number;
  forwardCapacityUnits: number;
  forwardLoad: number | null;
  forwardLoadOverCapacity: boolean;
  weightedOnTime: number | null;
  weightedRightFirstTime: number | null;
  deliveredTests: number;
  unweightedTests: number;
  unweightedOpenTests: number;
  testsOnDefaultTarget: number;
}

export interface AnalystWorkload {
  from: string;
  to: string;
  capacityUsesDefaults: boolean;
  onlyOwnRow: boolean;
  unassignedOpenTests: number;
  rows: AnalystWorkloadRow[];
  gaps: KpiGap[];
}

export interface KpiSettings {
  sectionId: number;
  hoursPerAnalystDay: number;
  workingWeekdays: number;
  loadHorizonDays: number;
  isDefault: boolean;
  version: number;
}

export interface UpdateKpiSettingsRequest {
  hoursPerAnalystDay: number;
  workingWeekdays: number;
  loadHorizonDays: number;
  reason: string;
}

export interface AnalystAvailability {
  id: number;
  userId: number;
  userName: string;
  fromDate: string;
  toDate: string;
  fractionAvailable: number;
  reason: string;
  enteredByName: string;
  enteredAt: string;
}

export interface SaveAvailabilityRequest {
  userId: number;
  fromDate: string;
  toDate: string;
  fractionAvailable: number;
  reason: string;
}
