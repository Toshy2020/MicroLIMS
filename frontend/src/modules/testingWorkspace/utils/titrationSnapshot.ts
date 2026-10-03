// Reads the TitrationSnapshot stored on TestAnalysis.ConditionsJson
// (TitrationRecorder: titrant details nested under "titrant"). Keys are
// matched case-insensitively.

const pick = (o: Record<string, unknown>, ...names: string[]): unknown => {
  const lower = new Map(Object.keys(o).map((k) => [k.toLowerCase(), k]));
  for (const n of names) {
    const k = lower.get(n.toLowerCase());
    if (k !== undefined && o[k] !== null && o[k] !== undefined && o[k] !== "") return o[k];
  }
  return null;
};

export interface TitrationSnapshotSummary {
  titrantCode: string | null;
  factor: number | null;
  factorState: string | null;
  warnings: string[];
}

export function parseTitrationSnapshot(conditionsJson: string | null | undefined): TitrationSnapshotSummary | null {
  if (!conditionsJson) return null;
  try {
    const o = JSON.parse(conditionsJson);
    if (typeof o !== "object" || o === null || Array.isArray(o)) return null;
    const rec = o as Record<string, unknown>;
    // The server nests the titrant details: { titrant: { code, factorUsed, factorState, ... } }.
    const nested = pick(rec, "titrant");
    const titrant = typeof nested === "object" && nested !== null && !Array.isArray(nested)
      ? (nested as Record<string, unknown>)
      : rec;
    const factor = pick(titrant, "factorUsed", "factor");
    const warnings = pick(rec, "warnings");
    return {
      titrantCode: (pick(titrant, "code", "titrantPreparationCode", "titrantCode") as string | null),
      factor: factor != null && !isNaN(Number(factor)) ? Number(factor) : null,
      factorState: (pick(titrant, "factorState") as string | null),
      warnings: Array.isArray(warnings) ? warnings.map(String) : []
    };
  } catch {
    return null;
  }
}
