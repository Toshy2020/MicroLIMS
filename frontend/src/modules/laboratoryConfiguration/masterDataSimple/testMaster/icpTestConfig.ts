// Test Master settings of an ICP method assay test: what it reports, the sample basis for the assay formula,
// and the elements with their target concentration (ppm). The server validates and stores them.

export type IcpTestMode = "Assay" | "Impurity";
export type IcpSampleForm = "Solid" | "Liquid";

export interface IcpElementRow {
  id?: number;            // set for an element that already exists, so item specifications stay linked
  symbol: string;
  targetPpm: string;      // assay only
  cleaningLimitPpm?: string; // optional cleaning limit (ppm)
}

export interface IcpTestForm {
  mode: IcpTestMode;
  sampleForm: IcpSampleForm;
  theoreticalAmount: string;   // Th.Wt (g) for a solid, Th.Vol (mL) for a liquid
  specificGravity: string;     // liquid only
  elements: IcpElementRow[];
}

export interface IcpTestElementDto {
  id: number;
  displayOrder: number;
  symbol: string;
  targetPpm?: number | null;
  cleaningLimitPpm?: number | null;
}

export interface IcpTestDefinitionFields {
  icpMode?: IcpTestMode | null;
  icpSampleForm?: IcpSampleForm | null;
  icpTheoreticalAmount?: number | null;
  icpSpecificGravity?: number | null;
  icpElements?: IcpTestElementDto[] | null;
}

export interface IcpTestPayloadFields {
  icpMode: IcpTestMode;
  icpSampleForm: IcpSampleForm | null;
  icpTheoreticalAmount: number | null;
  icpSpecificGravity: number | null;
  icpElements: { id: number | null; symbol: string; targetPpm: number | null; cleaningLimitPpm: number | null }[];
}

export const createInitialIcpForm = (): IcpTestForm => ({
  mode: "Assay",
  sampleForm: "Solid",
  theoreticalAmount: "",
  specificGravity: "",
  elements: [{ symbol: "", targetPpm: "", cleaningLimitPpm: "" }]
});

export function icpFormFromDefinition(t: IcpTestDefinitionFields): IcpTestForm {
  const initial = createInitialIcpForm();
  const elements = (t.icpElements ?? [])
    .slice()
    .sort((a, b) => a.displayOrder - b.displayOrder)
    .map((e) => ({
      id: e.id,
      symbol: e.symbol,
      targetPpm: e.targetPpm != null ? String(e.targetPpm) : "",
      cleaningLimitPpm: e.cleaningLimitPpm != null ? String(e.cleaningLimitPpm) : ""
    }));
  return {
    mode: t.icpMode ?? initial.mode,
    sampleForm: t.icpSampleForm ?? initial.sampleForm,
    theoreticalAmount: t.icpTheoreticalAmount != null ? String(t.icpTheoreticalAmount) : "",
    specificGravity: t.icpSpecificGravity != null ? String(t.icpSpecificGravity) : "",
    elements: elements.length > 0 ? elements : initial.elements
  };
}

const num = (text?: string | null): number | null => {
  if (!text) return null;
  const t = text.trim();
  if (t === "") return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
};

// Only what applies to the chosen mode is sent: an impurity test carries no target, sample form or weight.
export function icpPayloadFields(form: IcpTestForm): IcpTestPayloadFields {
  const assay = form.mode === "Assay";
  const liquid = assay && form.sampleForm === "Liquid";
  return {
    icpMode: form.mode,
    icpSampleForm: assay ? form.sampleForm : null,
    icpTheoreticalAmount: assay ? num(form.theoreticalAmount) : null,
    icpSpecificGravity: liquid ? num(form.specificGravity) : null,
    icpElements: form.elements
      .filter((e) => e.symbol.trim() !== "")
      .map((e) => ({
        id: e.id ?? null,
        symbol: e.symbol.trim(),
        targetPpm: assay ? num(e.targetPpm) : null,
        cleaningLimitPpm: num(e.cleaningLimitPpm)
      }))
  };
}

// Completeness only; the limits and rules (target > 0, symbols, duplicates) are checked by the server.
export function validateIcpForm(form: IcpTestForm): string | null {
  if (!form.elements.some((e) => e.symbol.trim() !== "")) return "Add at least one element.";
  if (form.mode === "Assay") {
    if (num(form.theoreticalAmount) === null) {
      return form.sampleForm === "Solid" ? "Enter the theoretical weight (g)." : "Enter the theoretical volume (mL).";
    }
    if (form.sampleForm === "Liquid" && num(form.specificGravity) === null) return "Enter the specific gravity.";
    const missing = form.elements.find((e) => e.symbol.trim() !== "" && num(e.targetPpm) === null);
    if (missing) return `Enter the target concentration (ppm) for ${missing.symbol.trim()}.`;
  }
  return null;
}
