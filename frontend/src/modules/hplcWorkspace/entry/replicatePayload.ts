import type { HplcReplicateDto, HplcReplicateInput } from "../types";

export function buildReplicatePayload(
  replicates: HplcReplicateDto[],
  isRelatedSubstances: boolean,
  hasInternalStandard: boolean
): HplcReplicateInput[] {
  return replicates.map((r) => {
    const item: HplcReplicateInput = {
      actualWeightMg: isRelatedSubstances ? 0 : (r.actualWeightMg ?? 0),
      responses: r.responses.map((resp) => ({
        hplcMethodAnalyteId: resp.hplcMethodAnalyteId,
        response: resp.response
      }))
    };
    if (hasInternalStandard) {
      item.internalStandardResponse = r.internalStandardResponse ?? null;
    }
    if (isRelatedSubstances && r.peaks) {
      item.peaks = r.peaks
        .filter((p) => p.hplcMethodImpurityId != null || (typeof p.area === "number" && p.area > 0))
        .map((p) => ({
          hplcMethodImpurityId: p.hplcMethodImpurityId ?? null,
          relativeRetention: p.relativeRetention ?? null,
          area: p.area
        }));
    }
    return item;
  });
}
