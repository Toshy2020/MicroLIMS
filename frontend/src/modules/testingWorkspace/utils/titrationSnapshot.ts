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

export interface DueTitrantAcknowledgementSnapshot {
  justification: string;
  titrantCodes: string[];
  acknowledgedAt: string;
}

export interface TitrationSnapshotSummary {
  titrantCode: string | null;
  factor: number | null;
  factorState: string | null;
  warnings: string[];
  dueTitrantAcknowledgement?: DueTitrantAcknowledgementSnapshot | null;
  engineVersion?: string | null;
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

    const rawAck = pick(rec, "dueTitrantAcknowledgement");
    let dueTitrantAcknowledgement: DueTitrantAcknowledgementSnapshot | null = null;
    if (typeof rawAck === "object" && rawAck !== null && !Array.isArray(rawAck)) {
      const ackRec = rawAck as Record<string, unknown>;
      const justification = pick(ackRec, "justification");
      const titrantCodes = pick(ackRec, "titrantCodes");
      const acknowledgedAt = pick(ackRec, "acknowledgedAt");
      if (justification) {
        dueTitrantAcknowledgement = {
          justification: String(justification),
          titrantCodes: Array.isArray(titrantCodes) ? titrantCodes.map(String) : [],
          acknowledgedAt: acknowledgedAt ? String(acknowledgedAt) : ""
        };
      }
    }
    const engineVersion = pick(rec, "engineVersion");

    return {
      titrantCode: (pick(titrant, "code", "titrantPreparationCode", "titrantCode") as string | null),
      factor: factor != null && !isNaN(Number(factor)) ? Number(factor) : null,
      factorState: (pick(titrant, "factorState") as string | null),
      warnings: Array.isArray(warnings) ? warnings.map(String) : [],
      dueTitrantAcknowledgement,
      engineVersion: engineVersion != null ? String(engineVersion) : null
    };
  } catch {
    return null;
  }
}
