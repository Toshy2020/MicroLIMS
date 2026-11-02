import type { HplcSstAnalyteDto, SaveSstAnalyteInput, HplcMethodSnapshot } from "../types";
import type { HplcMethodResponse } from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";

export function findMissingSstFields(
  analytes: HplcSstAnalyteDto[],
  inputs: Record<number, SaveSstAnalyteInput>,
  method: HplcMethodResponse | HplcMethodSnapshot | null | undefined,
  standardIsWeighed = true,
  hasInternalStandard = false
): string[] {
  return analytes.flatMap((a) => {
    const v = inputs[a.id];
    const ma = method?.analytes.find(
      (m) => m.id === a.hplcMethodAnalyteId || m.name === a.analyteName
    );
    const fields: string[] = [];

    if (!v?.standardMaterialId) {
      fields.push("reference standard lot");
    }
    // Only an assay weighs its standard (the backend stores no weight for the other modes)
    if (standardIsWeighed && !(v?.standardWeightMg > 0)) {
      fields.push("actual standard weight");
    }

    const injectionCount = ma?.standardInjections ?? 5;
    const blank = Array.from({ length: injectionCount }, (_, i) => i + 1)
      .filter((n) => !((v?.responses?.[n - 1] ?? 0) > 0));
    if (blank.length > 0) {
      fields.push(`injection ${blank.map((n) => `#${n}`).join(", ")}`);
    }

    if (hasInternalStandard) {
      const isBlank = Array.from({ length: injectionCount }, (_, i) => i + 1)
        .filter((n) => !((v?.internalStandardResponses?.[n - 1] ?? 0) > 0));
      if (isBlank.length > 0) {
        fields.push(`internal standard injection ${isBlank.map((n) => `#${n}`).join(", ")}`);
      }
    }

    if (ma?.sstMinResolution != null && v?.resolution == null) fields.push("resolution");
    if (ma?.sstMaxTailingFactor != null && v?.tailingFactor == null) fields.push("tailing factor");
    if (ma?.sstMinTheoreticalPlates != null && v?.theoreticalPlates == null) fields.push("theoretical plates");
    if (ma?.sstMinRetentionFactor != null && v?.retentionFactor == null) fields.push("retention factor");
    if (ma?.sstMinSignalToNoise != null && v?.signalToNoise == null) fields.push("signal-to-noise");
    if (ma?.sstMinPeakToValley != null && v?.peakToValley == null) fields.push("peak-to-valley");

    return fields.length ? [`${a.analyteName}: ${fields.join(", ")}`] : [];
  });
}
