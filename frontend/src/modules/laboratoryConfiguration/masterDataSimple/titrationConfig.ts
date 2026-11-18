import type { TFunction } from "i18next";
import { i18n } from "../../../i18n";

// Titration test-master configuration: types, conditional-visibility rules and
// client-side required-field hints. The server validates authoritatively
// (TestDefinitionMasterDataService); these hints only save a round trip.

const defaultT = (): TFunction => i18n.getFixedT("en", "masterData");

export type TitrationType = "AcidBase" | "Redox" | "Complexometric" | "Precipitation" | "KarlFischer";
export type TitrationMode = "Direct" | "Residual";
export type TitrationCalculation = "UspFactor" | "Relative";
export type TitrationEndpoint = "Visual" | "Potentiometric";

export const TITRATION_TYPE_OPTIONS: { value: TitrationType; label: string }[] = [
  { value: "AcidBase", label: "Acid-base" },
  { value: "Redox", label: "Redox" },
  { value: "Complexometric", label: "Complexometric" },
  { value: "Precipitation", label: "Precipitation" },
  { value: "KarlFischer", label: "Karl Fischer (volumetric)" }
];
export const TITRATION_MODE_OPTIONS: { value: TitrationMode; label: string }[] = [
  { value: "Direct", label: "Direct" },
  { value: "Residual", label: "Residual (back titration)" }
];
export const TITRATION_CALCULATION_OPTIONS: { value: TitrationCalculation; label: string }[] = [
  { value: "UspFactor", label: "USP equivalency factor" },
  { value: "Relative", label: "Relative to reference standard (SOP)" }
];
export const TITRATION_ENDPOINT_OPTIONS: { value: TitrationEndpoint; label: string }[] = [
  { value: "Visual", label: "Visual (indicator)" },
  { value: "Potentiometric", label: "Potentiometric" }
];

// Values are kept as strings while editing; "" means not set.
export interface TitrationFormState {
  type: TitrationType | "";
  nonAqueous: boolean;
  mode: TitrationMode | "";
  calculation: TitrationCalculation | "";
  titrantSolutionMasterId: number | "";
  equivalencyFactor: string;
  blankRequired: boolean;
  excessSolutionMasterId: number | "";
  excessVolumeMl: string;
  replicateCount: string;
  maxRsdPercent: string;
  endpoint: TitrationEndpoint | "";
  indicatorEntryId: number | ""; // Indicator entry of Reagents & Reference Standards
  tempCorrection: boolean;
  expansionCoefficient: string;
  standardEntryId: number | "";
}

export const DEFAULT_EXPANSION_COEFFICIENT = "0.0011";

export const createInitialTitrationForm = (): TitrationFormState => ({
  type: "",
  nonAqueous: false,
  mode: "Direct",
  calculation: "UspFactor",
  titrantSolutionMasterId: "",
  equivalencyFactor: "",
  blankRequired: false,
  excessSolutionMasterId: "",
  excessVolumeMl: "",
  replicateCount: "3",
  maxRsdPercent: "",
  endpoint: "Visual",
  indicatorEntryId: "",
  tempCorrection: false,
  expansionCoefficient: DEFAULT_EXPANSION_COEFFICIENT,
  standardEntryId: ""
});

// Titration columns on a test definition as returned by the API (camelCase of
// the spec 3.2 column names).
export interface TitrationDefinitionFields {
  titrationType?: TitrationType | null;
  titrationNonAqueous?: boolean | null;
  titrationMode?: TitrationMode | null;
  titrationCalculation?: TitrationCalculation | null;
  titrantSolutionMasterId?: number | null;
  titrationEquivalencyFactor?: number | null;
  titrationBlankRequired?: boolean | null;
  titrationExcessSolutionMasterId?: number | null;
  titrationExcessVolumeMl?: number | null;
  titrationMaxRsdPercent?: number | null;
  titrationEndpoint?: TitrationEndpoint | null;
  titrationIndicatorEntryId?: number | null;
  titrationIndicator?: string | null;
  titrationTempCorrection?: boolean | null;
  titrationExpansionCoefficient?: number | null;
  titrationStandardEntryId?: number | null;
  replicateCount?: number | null;
}

const str = (n: number | null | undefined, fallback = ""): string => (n != null ? String(n) : fallback);

export const titrationFormFromDefinition = (t: TitrationDefinitionFields): TitrationFormState => ({
  type: t.titrationType ?? "",
  nonAqueous: !!t.titrationNonAqueous,
  mode: t.titrationMode ?? "Direct",
  calculation: t.titrationCalculation ?? "UspFactor",
  titrantSolutionMasterId: t.titrantSolutionMasterId ?? "",
  equivalencyFactor: str(t.titrationEquivalencyFactor),
  blankRequired: !!t.titrationBlankRequired,
  excessSolutionMasterId: t.titrationExcessSolutionMasterId ?? "",
  excessVolumeMl: str(t.titrationExcessVolumeMl),
  replicateCount: str(t.replicateCount, "3"),
  maxRsdPercent: str(t.titrationMaxRsdPercent),
  endpoint: t.titrationEndpoint ?? "Visual",
  indicatorEntryId: t.titrationIndicatorEntryId ?? "",
  tempCorrection: !!t.titrationTempCorrection,
  expansionCoefficient: str(t.titrationExpansionCoefficient, DEFAULT_EXPANSION_COEFFICIENT),
  standardEntryId: t.titrationStandardEntryId ?? "",
});

// ---- Conditional visibility (spec section 6) ----
export interface TitrationVisibility {
  nonAqueous: boolean;     // acid-base only
  mode: boolean;           // KF is Direct only
  calculation: boolean;    // KF is UspFactor only
  factor: boolean;         // hidden for KF and Relative
  excess: boolean;         // Residual only
  standard: boolean;       // Relative only (reference standard entry; Direct mode only)
  tempCorrection: boolean; // AcidBase + NonAqueous
  indicator: boolean;      // Visual only
}

export const titrationVisibility = (f: TitrationFormState): TitrationVisibility => {
  const kf = f.type === "KarlFischer";
  return {
    nonAqueous: f.type === "AcidBase",
    mode: !kf,
    calculation: !kf,
    factor: !kf && f.calculation === "UspFactor",
    excess: !kf && f.mode === "Residual",
    standard: !kf && f.mode === "Direct" && f.calculation === "Relative",
    tempCorrection: f.type === "AcidBase" && f.nonAqueous,
    indicator: f.endpoint === "Visual"
  };
};

// Normalises dependent fields after a change: KF forces Direct + UspFactor and
// non-acid-base clears non-aqueous. Pure; returns a new state.
export const normaliseTitrationForm = (f: TitrationFormState): TitrationFormState => {
  const next = { ...f };
  if (f.type === "KarlFischer") {
    next.mode = "Direct";
    next.calculation = "UspFactor";
  }
  if (f.mode === "Residual" && f.calculation === "Relative") next.calculation = "UspFactor"; // Relative is Direct only
  if (f.type !== "AcidBase") next.nonAqueous = false;
  if (!next.nonAqueous) next.tempCorrection = false;
  return next;
};

const positive = (s: string) => s.trim() !== "" && Number(s) > 0;

// Required-field hints; returns the first problem or null.
export const validateTitrationForm = (f: TitrationFormState, t: TFunction = defaultT()): string | null => {
  const v = titrationVisibility(f);
  if (!f.type) return t("titration.errors.typeRequired");
  if (!f.mode) return t("titration.errors.modeRequired");
  if (!f.calculation) return t("titration.errors.calculationRequired");
  if (!f.endpoint) return t("titration.errors.endpointRequired");
  if (f.titrantSolutionMasterId === "") return t("titration.errors.titrantRequired");
  const rep = Number(f.replicateCount);
  if (!Number.isInteger(rep) || rep < 1 || rep > 10) return t("titration.errors.replicateRange");
  if (f.maxRsdPercent.trim() !== "" && !(Number(f.maxRsdPercent) > 0)) return t("titration.errors.maxRsdPositive");
  if (v.indicator && f.indicatorEntryId === "") return t("titration.errors.indicatorRequired");
  if (v.factor && !positive(f.equivalencyFactor)) return t("titration.errors.factorPositive");
  if (v.excess && (f.excessSolutionMasterId === "" || !positive(f.excessVolumeMl))) {
    return t("titration.errors.excessRequired");
  }
  if (f.type === "KarlFischer" && f.mode === "Residual") return t("titration.errors.kfResidual");
  if (f.calculation === "Relative" && f.mode !== "Direct") return t("titration.errors.relativeDirectOnly");
  if (v.standard) {
    if (f.standardEntryId === "") return t("titration.errors.standardRequired");
  }
  if (v.tempCorrection && f.tempCorrection && !positive(f.expansionCoefficient)) {
    return t("titration.errors.expansionPositive");
  }
  return null;
};

const num = (s: string): number | null => (s.trim() !== "" ? Number(s) : null);

// Payload fragment for create/update. Hidden fields go as null so the server
// clears them. Replicate count rides on the existing replicateCount column.
export const titrationPayloadFields = (f: TitrationFormState, active: boolean) => {
  if (!active) {
    return {
      titrationType: null, titrationNonAqueous: null, titrationMode: null, titrationCalculation: null,
      titrantSolutionMasterId: null, titrationEquivalencyFactor: null, titrationBlankRequired: null,
      titrationExcessSolutionMasterId: null, titrationExcessVolumeMl: null, titrationMaxRsdPercent: null,
      titrationEndpoint: null, titrationIndicatorEntryId: null, titrationTempCorrection: null,
      titrationExpansionCoefficient: null, titrationStandardEntryId: null
    };
  }
  const v = titrationVisibility(f);
  return {
    titrationType: f.type || null,
    titrationNonAqueous: v.nonAqueous ? f.nonAqueous : null,
    titrationMode: f.mode || null,
    titrationCalculation: f.calculation || null,
    titrantSolutionMasterId: f.titrantSolutionMasterId === "" ? null : f.titrantSolutionMasterId,
    titrationEquivalencyFactor: v.factor ? num(f.equivalencyFactor) : null,
    titrationBlankRequired: f.blankRequired,
    titrationExcessSolutionMasterId: v.excess && f.excessSolutionMasterId !== "" ? f.excessSolutionMasterId : null,
    titrationExcessVolumeMl: v.excess ? num(f.excessVolumeMl) : null,
    titrationMaxRsdPercent: num(f.maxRsdPercent),
    titrationEndpoint: f.endpoint || null,
    titrationIndicatorEntryId: v.indicator && f.indicatorEntryId !== "" ? f.indicatorEntryId : null,
    titrationTempCorrection: v.tempCorrection ? f.tempCorrection : null,
    titrationExpansionCoefficient: v.tempCorrection && f.tempCorrection ? num(f.expansionCoefficient) : null,
    titrationStandardEntryId: v.standard && f.standardEntryId !== "" ? f.standardEntryId : null
  };
};
