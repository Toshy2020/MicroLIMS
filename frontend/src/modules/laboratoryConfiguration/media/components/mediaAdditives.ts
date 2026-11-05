// Optional additives (e.g. a neutralizer) added while preparing a media lot.
// The backend owns every rule (section, usability, stock); this only shapes the
// payload and catches an incomplete row before the round trip.

export type AdditiveKind = "StockLot" | "PreparedSolution";

export interface AdditiveRow {
  key: number;
  kind: AdditiveKind;
  sourceId: number | "";
  quantity: string;
  unit: string;
}

export interface AdditivePayload {
  kind: AdditiveKind;
  materialId: number | null;
  solutionPreparationId: number | null;
  quantity: number;
  unit: string;
}

export const newAdditiveRow = (key: number, kind: AdditiveKind = "StockLot"): AdditiveRow => ({
  key,
  kind,
  sourceId: "",
  quantity: "",
  unit: kind === "PreparedSolution" ? "mL" : ""
});

export function validateAdditives(rows: AdditiveRow[]): string | null {
  const seen = new Set<string>();
  for (let i = 0; i < rows.length; i++) {
    const r = rows[i];
    const n = i + 1;
    if (r.sourceId === "") return `Additive ${n}: choose ${r.kind === "StockLot" ? "the stock lot" : "the prepared solution"}, or remove the row.`;
    if (!(Number(r.quantity) > 0)) return `Additive ${n}: enter a quantity greater than zero.`;
    if (!r.unit.trim()) return `Additive ${n}: enter the unit.`;
    const id = `${r.kind}:${r.sourceId}`;
    if (seen.has(id)) return `Additive ${n}: the same ${r.kind === "StockLot" ? "stock lot" : "prepared solution"} is listed twice.`;
    seen.add(id);
  }
  return null;
}

export function buildAdditivesPayload(rows: AdditiveRow[]): AdditivePayload[] {
  return rows.map((r) => ({
    kind: r.kind,
    materialId: r.kind === "StockLot" ? Number(r.sourceId) : null,
    solutionPreparationId: r.kind === "PreparedSolution" ? Number(r.sourceId) : null,
    quantity: Number(r.quantity),
    unit: r.unit.trim()
  }));
}
