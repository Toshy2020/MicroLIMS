import type { IcpEntryElementDto, IcpReportElementValueDto, IcpTestMode, SaveIcpSampleEntryRequest } from "../types";

// Pure helpers for the ICP result entry form. The frontend only shapes what the analyst typed; the server
// calculates, judges and decides whether the entry can be saved or sent.

export function formatQuantityLabel(quantity: string): string {
  switch (quantity) {
    case "IcpAssayPercent":
      return "Assay (%)";
    case "IcpImpurityPpm":
      return "Impurity (ppm)";
    default:
      return quantity;
  }
}

// "" / non-numeric -> null; otherwise the number.
export function parseDecimal(text: string | undefined | null): number | null {
  const t = (text ?? "").trim();
  if (t === "") return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

export interface EntryDraft {
  weight: string;
  results: Record<number, string>;
}

export function draftFromEntry(elements: IcpEntryElementDto[], actualWeightG?: number | null): EntryDraft {
  const results: Record<number, string> = {};
  for (const el of elements) results[el.icpTestElementId] = el.resultPpm != null ? String(el.resultPpm) : "";
  return { weight: actualWeightG != null ? String(actualWeightG) : "", results };
}

// Assay needs the actual weight; impurity tests do not use it.
export function buildSaveEntryPayload(
  mode: IcpTestMode,
  elements: IcpEntryElementDto[],
  draft: EntryDraft
): SaveIcpSampleEntryRequest {
  return {
    actualWeightG: mode === "Assay" ? parseDecimal(draft.weight) : null,
    concentrations: elements.flatMap((el) => {
      const ppm = parseDecimal(draft.results[el.icpTestElementId]);
      return ppm === null ? [] : [{ icpTestElementId: el.icpTestElementId, resultPpm: ppm }];
    })
  };
}

// Enables the Save button: a positive weight (assay) and a non-negative result for every element.
export function isEntryComplete(mode: IcpTestMode, elements: IcpEntryElementDto[], draft: EntryDraft): boolean {
  if (elements.length === 0) return false;
  if (mode === "Assay") {
    const w = parseDecimal(draft.weight);
    if (w === null || w <= 0) return false;
  }
  return elements.every((el) => {
    const v = parseDecimal(draft.results[el.icpTestElementId]);
    return v !== null && v >= 0;
  });
}

// Fills the draft with the ppm read from the instrument report. Only elements with a usable (non-negative)
// reading are touched; nothing is saved - the analyst reviews the draft and saves it as their own entry.
export function applyReportValues(draft: EntryDraft, values: IcpReportElementValueDto[]): EntryDraft {
  const results = { ...draft.results };
  for (const v of values) {
    if (v.resultPpm != null && v.resultPpm >= 0) results[v.icpTestElementId] = String(v.resultPpm);
  }
  return { ...draft, results };
}
