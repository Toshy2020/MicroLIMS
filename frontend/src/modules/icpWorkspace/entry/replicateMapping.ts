import type {
  IcpAmountUnit,
  IcpMethodMode,
  IcpReplicateDto,
  IcpReplicateInputDto,
  IcpConcentrationInput,
  SaveIcpReplicatesRequest
} from "../types";

/**
 * Parses a string draft to a number.
 * Empty string yields 0. Invalid input yields fallback.
 */
export function parseDraftNumber(draft: string | undefined, fallback = 0): number {
  if (draft === undefined) return fallback;
  const trimmed = draft.trim();
  if (trimmed === "") return 0;
  const parsed = parseFloat(trimmed);
  return Number.isNaN(parsed) ? fallback : parsed;
}

/**
 * Formats quantity codes to user-friendly labels.
 * Spec §4.2:
 * - IcpMgPerKg -> "Content (µg/g)"
 * - IcpMgPerUnit -> "Amount per unit"
 * - IcpPercentLabelClaim -> "% of label claim"
 */
export function formatQuantityLabel(quantity: string): string {
  switch (quantity) {
    case "IcpMgPerKg":
      return "Content (µg/g)";
    case "IcpMgPerUnit":
      return "Amount per unit";
    case "IcpPercentLabelClaim":
      return "% of label claim";
    default:
      return quantity;
  }
}

export interface BuildSaveReplicatesArgs {
  amountUnit: IcpAmountUnit;
  mode: IcpMethodMode;
  unitAmountDraft?: string;
  unitAmountFallback?: number | null;
  replicates: IcpReplicateDto[];
  drafts: Record<string, string>;
  neededElementIds: number[];
}

/**
 * Maps frontend replicate state + draft input strings into the exact SaveIcpReplicatesRequest DTO.
 * Enforces:
 * - ElementalImpurities mode is strictly Gram with unitAmount = null.
 * - Parses string drafts so typed values (e.g. "0.", "1.25") are converted to numbers on save.
 * - Includes exactly one concentration per needed element.
 */
export function buildSaveReplicatesPayload({
  amountUnit,
  mode,
  unitAmountDraft,
  unitAmountFallback,
  replicates,
  drafts,
  neededElementIds
}: BuildSaveReplicatesArgs): SaveIcpReplicatesRequest {
  const resolvedUnit: IcpAmountUnit = mode === "ElementalImpurities" ? "Gram" : amountUnit;

  let unitAmount: number | null = null;
  if (mode === "MineralAssay") {
    if (unitAmountDraft !== undefined) {
      const trimmed = unitAmountDraft.trim();
      if (trimmed !== "") {
        const val = parseFloat(trimmed);
        unitAmount = Number.isNaN(val) || val <= 0 ? null : val;
      }
    } else if (unitAmountFallback != null && unitAmountFallback > 0) {
      unitAmount = unitAmountFallback;
    }
  }

  const replicateInputs: IcpReplicateInputDto[] = replicates.map((rep, idx) => {
    const amtStr = drafts[`${idx}:amount`];
    const volStr = drafts[`${idx}:volume`];
    const dilStr = drafts[`${idx}:dilution`];

    const sampleAmount = amtStr !== undefined ? parseDraftNumber(amtStr, rep.sampleAmount) : rep.sampleAmount;
    const volumeMl = volStr !== undefined ? parseDraftNumber(volStr, rep.volumeMl) : rep.volumeMl;
    const dilutionFactor = dilStr !== undefined ? parseDraftNumber(dilStr, rep.dilutionFactor) : rep.dilutionFactor;

    const concentrations: IcpConcentrationInput[] = neededElementIds.map((elId) => {
      const concStr = drafts[`${idx}:conc_${elId}`];
      const existing = rep.concentrations.find((c) => c.icpMethodElementId === elId)?.solutionMgPerL ?? 0;
      const solutionMgPerL = concStr !== undefined ? parseDraftNumber(concStr, existing) : existing;
      return {
        icpMethodElementId: elId,
        solutionMgPerL
      };
    });

    return {
      sampleAmount,
      volumeMl,
      dilutionFactor,
      concentrations
    };
  });

  return {
    amountUnit: resolvedUnit,
    unitAmount,
    replicates: replicateInputs
  };
}

/**
 * Checks if replicate entries have valid, non-zero values ready for saving.
 */
export function isReplicatesComplete(
  replicates: IcpReplicateDto[],
  neededElementIds: number[],
  drafts: Record<string, string>,
  mode: IcpMethodMode,
  unitAmountDraft?: string,
  unitAmountFallback?: number | null
): boolean {
  if (replicates.length === 0) return false;

  if (mode === "MineralAssay") {
    const uaStr = unitAmountDraft !== undefined ? unitAmountDraft.trim() : "";
    const ua = uaStr !== "" ? parseFloat(uaStr) : (unitAmountFallback ?? 0);
    if (Number.isNaN(ua) || ua <= 0) {
      return false;
    }
  }

  for (let i = 0; i < replicates.length; i++) {
    const rep = replicates[i];
    const amtStr = drafts[`${i}:amount`];
    const amt = amtStr !== undefined ? parseFloat(amtStr.trim()) : rep.sampleAmount;
    if (Number.isNaN(amt) || amt <= 0) return false;

    const volStr = drafts[`${i}:volume`];
    const vol = volStr !== undefined ? parseFloat(volStr.trim()) : rep.volumeMl;
    if (Number.isNaN(vol) || vol <= 0) return false;

    const dilStr = drafts[`${i}:dilution`];
    const dil = dilStr !== undefined ? parseFloat(dilStr.trim()) : rep.dilutionFactor;
    if (Number.isNaN(dil) || dil < 1) return false;

    for (const elId of neededElementIds) {
      const cStr = drafts[`${i}:conc_${elId}`];
      const existing = rep.concentrations.find((c) => c.icpMethodElementId === elId)?.solutionMgPerL;
      const cVal = cStr !== undefined ? (cStr.trim() === "" ? NaN : parseFloat(cStr.trim())) : (existing ?? -1);
      if (Number.isNaN(cVal) || cVal < 0) return false;
    }
  }

  return true;
}
