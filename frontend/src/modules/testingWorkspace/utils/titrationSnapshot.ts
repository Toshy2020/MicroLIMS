// Reads the TitrationSnapshot stored on TestAnalysis.ConditionsJson. Key names
// are matched case-insensitively against a few likely spellings so a small
// naming difference on the server does not blank the display.

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
    const factor = pick(rec, "factorUsed", "factor");
    const warnings = pick(rec, "warnings");
    return {
      titrantCode: (pick(rec, "titrantPreparationCode", "titrantCode", "titrantPrepCode") as string | null),
      factor: factor != null && !isNaN(Number(factor)) ? Number(factor) : null,
      factorState: (pick(rec, "factorState") as string | null),
      warnings: Array.isArray(warnings) ? warnings.map(String) : []
    };
  } catch {
    return null;
  }
}
