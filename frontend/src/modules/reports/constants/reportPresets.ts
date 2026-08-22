import { WorkloadWeightConfig } from "../types/reportingTypes";

export const DEFAULT_WORKLOAD_WEIGHTS: WorkloadWeightConfig[] = [
  {
    testCode: "TAMC",
    testName: "Total Aerobic Microbial Count",
    category: "FinishedProduct",
    workloadWeight: 1.0,
    effectiveDate: "2026-01-01",
    status: "Active",
    reasonForChange: "Initial baseline configuration",
    changedBy: "System Administrator",
    changedAt: "2026-01-01T00:00:00Z"
  },
  {
    testCode: "TYMC",
    testName: "Total Yeast & Mold Count",
    category: "FinishedProduct",
    workloadWeight: 1.0,
    effectiveDate: "2026-01-01",
    status: "Active",
    reasonForChange: "Initial baseline configuration",
    changedBy: "System Administrator",
    changedAt: "2026-01-01T00:00:00Z"
  },
  {
    testCode: "WATER_MICRO",
    testName: "Water Microbiology (TAMC)",
    category: "Water",
    workloadWeight: 1.2,
    effectiveDate: "2026-01-01",
    status: "Active",
    reasonForChange: "Filtration and multiple volume plating factor",
    changedBy: "System Administrator",
    changedAt: "2026-01-01T00:00:00Z"
  },
  {
    testCode: "GPT",
    testName: "Growth Promotion Test",
    category: "GPT",
    workloadWeight: 2.0,
    effectiveDate: "2026-01-01",
    status: "Active",
    reasonForChange: "Multiple standard strain inoculations and 5-day verification",
    changedBy: "System Administrator",
    changedAt: "2026-01-01T00:00:00Z"
  },
  {
    testCode: "STERILITY",
    testName: "Sterility Test (Membrane Filtration)",
    category: "FinishedProduct",
    workloadWeight: 3.0,
    effectiveDate: "2026-01-01",
    status: "Active",
    reasonForChange: "Cleanroom gowning, 14-day incubation, dual media manipulation",
    changedBy: "System Administrator",
    changedAt: "2026-01-01T00:00:00Z"
  },
  {
    testCode: "MICROBIAL_ID",
    testName: "Microbial Identification (Gram / Biochemical / Vitek)",
    category: "FinishedProduct",
    workloadWeight: 2.5,
    effectiveDate: "2026-01-01",
    status: "Active",
    reasonForChange: "Staining, subculture, card preparation and confirmation",
    changedBy: "System Administrator",
    changedAt: "2026-01-01T00:00:00Z"
  },
  {
    testCode: "INVESTIGATION",
    testName: "Laboratory Investigation / OOS Retest",
    category: "FinishedProduct",
    workloadWeight: 3.0,
    effectiveDate: "2026-01-01",
    status: "Active",
    reasonForChange: "Comprehensive phase 1 testing checklist and supervisor review",
    changedBy: "System Administrator",
    changedAt: "2026-01-01T00:00:00Z"
  }
];

export const MOCK_ANALYSTS = [
  { id: 101, name: "Amal Hamdy", username: "ahamdy", role: "Analyst" },
  { id: 102, name: "Ahmed Ali", username: "aali", role: "Analyst" },
  { id: 103, name: "Sara Mohamed", username: "smohamed", role: "Analyst" },
  { id: 104, name: "Khaled Omar", username: "komar", role: "Analyst" },
  { id: 105, name: "Nour Ibrahim", username: "nibrahim", role: "Analyst" },
  { id: 106, name: "Youssef Tarek", username: "ytarek", role: "Analyst" }
];
