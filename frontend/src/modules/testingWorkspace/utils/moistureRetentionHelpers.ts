import { SampleMoistureResult } from "../types/testWorkflowTypes";

export const RETENTION_UNITS = ["g", "mg", "kg", "mL", "L"] as const;
export type RetentionUnit = typeof RETENTION_UNITS[number];

/**
 * Formats a read-only moisture line from the sample's own moisture result.
 * Format: "Loss on drying {percent} % (from {testCode})" for LossOnDrying
 *         "Water {percent} % (from {testCode})" for Water
 */
export function formatMoistureLabel(moisture: SampleMoistureResult): string {
  const prefix = moisture.role === "Water" ? "Water" : "Loss on drying";
  return `${prefix} ${moisture.percent} % (from ${moisture.testCode})`;
}

/**
 * Generates the prompt message when a dried or anhydrous basis assay needs
 * a moisture result but none has been recorded yet.
 */
export function getMissingMoistureMessage(specifications: { resultBasis?: string | null }[]): string {
  const hasAnhydrous = specifications.some((s) => s.resultBasis === "PercentAnhydrousBasis");
  if (hasAnhydrous) {
    return "Record the sample's water content (Karl Fischer) result first.";
  }
  return "Record the sample's loss on drying result first.";
}

/**
 * Checks whether the review step should display retention quantity and unit.
 * Condition: sample's category is raw material AND section is physicochemical (section code "FP").
 */
export function isRawMaterialPhyschemReview(
  category: string | null | undefined,
  sectionCode: string | null | undefined
): boolean {
  if (!category || !sectionCode) return false;
  const normalizedCategory = category.replace(/\s+/g, "").toLowerCase();
  return normalizedCategory === "rawmaterial" && sectionCode.trim().toUpperCase() === "FP";
}

/**
 * Validates retention quantity and unit:
 * Required (> 0 and a valid unit in g, mg, kg, mL, L).
 */
export function isRetentionValid(
  quantity: number | string | null | undefined,
  unit: string | null | undefined
): boolean {
  if (quantity === null || quantity === undefined || quantity === "") return false;
  const numVal = typeof quantity === "number" ? quantity : Number(quantity);
  if (isNaN(numVal) || numVal <= 0) return false;
  if (!unit || !unit.trim()) return false;
  const trimmedUnit = unit.trim();
  return (RETENTION_UNITS as readonly string[]).includes(trimmedUnit);
}

/**
 * Formats the read-only display string for retention quantity and unit in the sample summary.
 * Returns "{quantity} {unit}" without repeating the "Retention" label.
 */
export function formatRetentionDisplay(
  quantity: number | null | undefined,
  unit: string | null | undefined
): string | null {
  if (quantity == null) return null;
  return `${quantity} ${unit ?? ""}`.trim();
}
