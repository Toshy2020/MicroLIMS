export type IcpQuantity = "IcpAssayPercent" | "IcpImpurityPpm" | string;

// The calculation the ICP recorder stores with each result (camelCase JSON of IcpMethodAssayRecorder).
export interface IcpCalculationData {
  element: string;
  icpTestElementId: number;
  quantity: IcpQuantity;
  mode: "Assay" | "Impurity" | string;
  sampleForm?: "Solid" | "Liquid" | null;
  targetPpm?: number | null;
  theoreticalAmount?: number | null;
  specificGravity?: number | null;
  actualWeightG?: number | null;
  resultPpm: number;
  reportedValue: number | null;
  display: string;
  runCode: string;
  calibrationCode: string;
  calibrationConfirmedAt: string;
  correlationR?: number | null;
  icpRunId?: number;
  icpRunSampleId?: number;
}

export function formatIcpQuantity(quantity: string | null | undefined): string {
  if (!quantity) return "—";
  switch (quantity) {
    case "IcpAssayPercent":
      return "Assay (%)";
    case "IcpImpurityPpm":
      return "Impurity (ppm)";
    default:
      return quantity;
  }
}

// The formula the result was calculated with, as shown to the reviewer.
export function icpFormulaText(calc: Pick<IcpCalculationData, "mode" | "sampleForm">): string {
  if (calc.mode !== "Assay") {
    return "Result = instrument concentration (ppm); judged against the item's not-more-than limit (0 = not detected).";
  }
  return calc.sampleForm === "Liquid"
    ? "Assay % = result × Th.Vol × Sp.gr × 100 / (target × Act.Wt)"
    : "Assay % = result × Th.Wt × 100 / (target × Act.Wt)";
}

export function isIcpCalculation(json: string | null | undefined): boolean {
  if (!json) return false;
  try {
    const parsed = JSON.parse(json);
    return typeof parsed?.quantity === "string" && parsed.quantity.startsWith("Icp");
  } catch {
    return typeof json === "string" && json.includes('"quantity"') && json.includes('"Icp');
  }
}
