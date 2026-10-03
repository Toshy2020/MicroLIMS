import type { TitrantPreparationOption, TitrationContext, TitrationSpecInfo } from "./types/testWorkflowTypes";

// Display-only preview of the titration calculation. The server
// (TitrationEngine) is authoritative; nothing here decides pass/fail.

export interface PreviewInput {
  ctx: TitrationContext;
  titrant: TitrantPreparationOption | null;
  excess: TitrantPreparationOption | null;
  blankMl: number | null;
  titrationTempC: number | null;
  lossPercent: number | null;
  avgUnitWeightMg: number | null;
  standards: { materialId: number; weightMg: number; titreMl: number }[];
}

const fin = (x: number): number | null => (Number.isFinite(x) ? x : null);

// Volume corrected to the standardization temperature (non-aqueous acid-base).
const corrected = (v: number, i: PreviewInput): number => {
  const { ctx, titrant, titrationTempC } = i;
  if (!ctx.tempCorrection || titrant?.standardizationTemperatureC == null || titrationTempC == null) return v;
  return v * (1 + (titrant.standardizationTemperatureC - titrationTempC) * (ctx.expansionCoefficient ?? 0.0011));
};

// Mean empirical factor K (mg analyte per mL titrant) from the standard titrations (relative method).
export const previewK = (i: PreviewInput): number | null => {
  const b = corrected(i.blankMl ?? 0, i);
  const ks: number[] = [];
  for (const s of i.standards) {
    const lot = i.ctx.standardLots.find((l) => l.materialId === s.materialId);
    if (!lot) return null;
    const denom = corrected(s.titreMl, i) - b;
    if (!(denom > 0) || !(s.weightMg > 0)) return null;
    ks.push(((s.weightMg * lot.purityPercent) / 100 * (100 - (lot.moisturePercent ?? 0))) / 100 / denom);
  }
  if (ks.length === 0) return null;
  return ks.reduce((a, c) => a + c, 0) / ks.length;
};

// mg of analyte in the weighed sample for one replicate.
export const previewMg = (i: PreviewInput, weightMg: number, volumeMl: number): number | null => {
  const { ctx, titrant, excess } = i;
  if (!(weightMg > 0) || !(volumeMl > 0) || !titrant || titrant.factor == null) return null;
  const v = corrected(volumeMl, i);
  const b = corrected(i.blankMl ?? 0, i);
  const n = ctx.titrant.nominalStrength;
  if (ctx.calculation === "Relative") {
    const k = previewK(i);
    return k == null || !(v > b) ? null : fin((v - b) * k);
  }
  const f = ctx.titrationType === "KarlFischer" ? 1 : ctx.equivalencyFactor;
  if (f == null) return null;
  if (ctx.mode === "Residual") {
    if (ctx.blankRequired) return !(b > v) ? null : fin((b - v) * n * titrant.factor * f);
    if (!excess || excess.factor == null || ctx.excessTitrant == null || ctx.excessVolumeMl == null) return null;
    return fin((ctx.excessVolumeMl * ctx.excessTitrant.nominalStrength * excess.factor - v * n * titrant.factor) * f);
  }
  return !(v > b) ? null : fin((v - b) * n * titrant.factor * f);
};

// Replicate result in the units of the specification basis.
export const previewResult = (i: PreviewInput, spec: TitrationSpecInfo, weightMg: number, volumeMl: number): number | null => {
  const mg = previewMg(i, weightMg, volumeMl);
  if (mg == null) return null;
  const pct = (mg / weightMg) * 100;
  switch (spec.resultBasis) {
    case "PercentAsIs":
      return fin(pct);
    case "PercentDriedBasis":
    case "PercentAnhydrousBasis":
      return i.lossPercent != null && i.lossPercent >= 0 && i.lossPercent < 100 ? fin((pct * 100) / (100 - i.lossPercent)) : null;
    case "PercentLabelClaim":
      return i.avgUnitWeightMg && spec.labelClaim ? fin((mg / weightMg) * i.avgUnitWeightMg / spec.labelClaim * 100) : null;
    case "MgPerUnit":
      return i.avgUnitWeightMg ? fin((mg / weightMg) * i.avgUnitWeightMg) : null;
    default:
      return null;
  }
};

export const meanAndRsd = (xs: number[]): { mean: number; rsd: number | null } | null => {
  if (xs.length === 0) return null;
  const mean = xs.reduce((a, c) => a + c, 0) / xs.length;
  if (xs.length < 2 || mean === 0) return { mean, rsd: null };
  const sd = Math.sqrt(xs.reduce((a, c) => a + (c - mean) ** 2, 0) / (xs.length - 1));
  return { mean, rsd: (sd / Math.abs(mean)) * 100 };
};
