export type IcpQuantity = "IcpMgPerKg" | "IcpMgPerUnit" | "IcpPercentLabelClaim" | string;

export interface IcpReplicateCalculationData {
  replicateNo: number;
  sampleAmount: number;
  volumeMl: number;
  dilutionFactor: number;
  solutionMgPerL: number;
  contentPerAmount: number;
  belowLoq: boolean;
}

export interface IcpCcvReadingData {
  measuredMgPerL: number;
  recoveryPercent: number;
  passed: boolean;
  enteredAt: string;
}

export interface IcpCalculationData {
  element: string;
  icpMethodElementId: number;
  quantity: IcpQuantity;
  amountUnit: "Gram" | "Milliliter" | string;
  unitAmount?: number | null;
  conversionFactor?: number;
  labelClaim?: number | null;
  labelClaimUnit?: string | null;
  standardLevelsMgPerL?: number[];
  replicates: IcpReplicateCalculationData[];
  meanContentPerAmount: number | null;
  reportedValue: number;
  display: string;
  runCode: string;
  calibrationCode: string;
  calibrationConfirmedAt: string;
  correlationR?: number | null;
  ccv?: IcpCcvReadingData[];
  icpMethodId?: number;
  icpRunId?: number;
  icpRunSampleId?: number;
}

export function formatIcpQuantity(quantity: string | null | undefined): string {
  if (!quantity) return "—";
  switch (quantity) {
    case "IcpMgPerKg":
    case "MgPerKg":
      return "Content (µg/g)";
    case "IcpMgPerUnit":
    case "MgPerUnit":
      return "Amount per unit";
    case "IcpPercentLabelClaim":
    case "PercentLabelClaim":
      return "% of label claim";
    default:
      return quantity;
  }
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
