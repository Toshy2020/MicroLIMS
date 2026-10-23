import { TestOrderSummary } from "../types/receivingTypes";

type AnalystTest = Pick<TestOrderSummary, "testCode" | "assignedAnalystName">;

// Each test carries its own analyst, so one sample can have several.
export function assignedAnalystNames(tests: AnalystTest[]): string[] {
  return Array.from(new Set(tests.map((t) => t.assignedAnalystName).filter((n): n is string => !!n)));
}

// "Mona Ali" for one analyst, "Mona Ali +1" for two, null when unassigned.
export function assignedAnalystsLabel(tests: AnalystTest[]): string | null {
  const names = assignedAnalystNames(tests);
  if (names.length === 0) return null;
  return names.length === 1 ? names[0] : `${names[0]} +${names.length - 1}`;
}

// "TAMC: Mona Ali, TYMC: Omar Saleh" - who is on which test.
export function assignedAnalystsByTest(tests: AnalystTest[]): string {
  return tests
    .filter((t) => t.assignedAnalystName)
    .map((t) => `${t.testCode}: ${t.assignedAnalystName}`)
    .join(", ");
}
