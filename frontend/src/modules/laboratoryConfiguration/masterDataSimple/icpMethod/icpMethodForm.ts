import type {
  IcpMethodMode,
  IcpAnalyteView,
  IcpMethodResponse,
  SaveIcpMethodRequest
} from "../services/IcpMethodService";
import type { MaterialMasterEntry } from "../services/MaterialMasterService";

export interface IcpElementRowState {
  id?: number | null;
  symbol: string;
  wavelengthNm: string | number;
  view: IcpAnalyteView;
  conversionFactor: string | number;
}

export interface IcpMethodFormState {
  name: string;
  abbreviation: string;
  effectiveDate: string;
  sectionId: number | "";
  mode: IcpMethodMode;
  standardLevelsMgPerL: string;
  calibrationStandardEntryId: number | "";
  minCorrelation: string | number;
  sampleVolumeMl: string | number;
  dilutionFactor: string | number;
  maxCalibrationAgeHours: string | number;
  requireBlank: boolean;
  blankMaxMgPerL: string | number;
  requireIcv: boolean;
  icvStandardEntryId: number | "";
  icvNominalMgPerL: string | number;
  icvRecoveryLowPercent: string | number;
  icvRecoveryHighPercent: string | number;
  requireCcv: boolean;
  ccvNominalMgPerL: string | number;
  ccvRecoveryLowPercent: string | number;
  ccvRecoveryHighPercent: string | number;
  elements: IcpElementRowState[];
}

export const ICP_MODE_OPTIONS: Array<{ value: IcpMethodMode; label: string; description: string }> = [
  {
    value: "MineralAssay",
    label: "Mineral assay",
    description: "Multi-element quantitative assay for minerals/nutrients (basis mg/unit or % label claim)."
  },
  {
    value: "ElementalImpurities",
    label: "Elemental impurities",
    description: "ICH Q3D elemental impurity screening (fixed basis µg/g with Not More Than limits)."
  }
];

export const ANALYTE_VIEW_OPTIONS: Array<{ value: IcpAnalyteView; label: string }> = [
  { value: "Axial", label: "Axial" },
  { value: "Radial", label: "Radial" }
];

export function normaliseSymbol(symbol: string): string {
  const s = symbol.trim();
  if (!s) return "";
  return s.charAt(0).toUpperCase() + s.slice(1).toLowerCase();
}

export function createInitialIcpMethodFormState(
  defaultSectionId: number | "" = ""
): IcpMethodFormState {
  return {
    name: "",
    abbreviation: "",
    effectiveDate: new Date().toISOString().split("T")[0],
    sectionId: defaultSectionId,
    mode: "MineralAssay",
    standardLevelsMgPerL: "0.1, 0.5, 1, 3, 6",
    calibrationStandardEntryId: "",
    minCorrelation: "0.995",
    sampleVolumeMl: "10",
    dilutionFactor: "1",
    maxCalibrationAgeHours: "24",
    requireBlank: false,
    blankMaxMgPerL: "",
    requireIcv: false,
    icvStandardEntryId: "",
    icvNominalMgPerL: "",
    icvRecoveryLowPercent: "90",
    icvRecoveryHighPercent: "110",
    requireCcv: false,
    ccvNominalMgPerL: "",
    ccvRecoveryLowPercent: "90",
    ccvRecoveryHighPercent: "110",
    elements: [
      {
        symbol: "Zn",
        wavelengthNm: "213.857",
        view: "Axial",
        conversionFactor: "1"
      }
    ]
  };
}

export function icpMethodEntityToFormState(m: IcpMethodResponse): IcpMethodFormState {
  return {
    name: m.name,
    abbreviation: m.abbreviation,
    effectiveDate: m.effectiveDate ? m.effectiveDate.split("T")[0] : "",
    sectionId: m.sectionId,
    mode: m.mode,
    standardLevelsMgPerL: m.standardLevelsMgPerL ?? "",
    calibrationStandardEntryId: m.calibrationStandardEntryId,
    minCorrelation: m.minCorrelation != null ? String(m.minCorrelation) : "",
    sampleVolumeMl: m.sampleVolumeMl != null ? String(m.sampleVolumeMl) : "",
    dilutionFactor: m.dilutionFactor != null ? String(m.dilutionFactor) : "1",
    maxCalibrationAgeHours: m.maxCalibrationAgeHours != null ? String(m.maxCalibrationAgeHours) : "24",
    requireBlank: Boolean(m.requireBlank),
    blankMaxMgPerL: m.blankMaxMgPerL != null ? String(m.blankMaxMgPerL) : "",
    requireIcv: Boolean(m.requireIcv),
    icvStandardEntryId: m.icvStandardEntryId != null ? m.icvStandardEntryId : "",
    icvNominalMgPerL: m.icvNominalMgPerL != null ? String(m.icvNominalMgPerL) : "",
    icvRecoveryLowPercent: m.icvRecoveryLowPercent != null ? String(m.icvRecoveryLowPercent) : "",
    icvRecoveryHighPercent: m.icvRecoveryHighPercent != null ? String(m.icvRecoveryHighPercent) : "",
    requireCcv: Boolean(m.requireCcv),
    ccvNominalMgPerL: m.ccvNominalMgPerL != null ? String(m.ccvNominalMgPerL) : "",
    ccvRecoveryLowPercent: m.ccvRecoveryLowPercent != null ? String(m.ccvRecoveryLowPercent) : "",
    ccvRecoveryHighPercent: m.ccvRecoveryHighPercent != null ? String(m.ccvRecoveryHighPercent) : "",
    elements:
      m.elements && m.elements.length > 0
        ? m.elements.map((e) => ({
            id: e.id,
            symbol: e.symbol,
            wavelengthNm: e.wavelengthNm != null ? String(e.wavelengthNm) : "",
            view: e.view,
            conversionFactor: e.conversionFactor != null ? String(e.conversionFactor) : "1"
          }))
        : []
  };
}

export function formToSaveRequest(
  form: IcpMethodFormState,
  resolvedSectionId: number | null
): SaveIcpMethodRequest {
  const formatIsoDate = (d: string) => {
    if (!d) return new Date().toISOString();
    if (d.includes("T")) return d.endsWith("Z") ? d : `${d}Z`;
    return `${d}T00:00:00Z`;
  };

  return {
    name: form.name.trim(),
    abbreviation: form.abbreviation.trim().toUpperCase(),
    effectiveDate: formatIsoDate(form.effectiveDate),
    sectionId: resolvedSectionId,
    mode: form.mode,
    standardLevelsMgPerL: form.standardLevelsMgPerL.trim(),
    calibrationStandardEntryId: Number(form.calibrationStandardEntryId),
    minCorrelation: Number(form.minCorrelation),
    sampleVolumeMl: Number(form.sampleVolumeMl),
    dilutionFactor: form.dilutionFactor !== "" ? Number(form.dilutionFactor) : 1,
    maxCalibrationAgeHours: form.maxCalibrationAgeHours !== "" ? Number(form.maxCalibrationAgeHours) : 24,

    // Optional checks: null when toggle is OFF
    requireBlank: Boolean(form.requireBlank),
    blankMaxMgPerL:
      form.requireBlank && form.blankMaxMgPerL !== "" ? Number(form.blankMaxMgPerL) : null,

    requireIcv: Boolean(form.requireIcv),
    icvStandardEntryId:
      form.requireIcv && form.icvStandardEntryId !== "" ? Number(form.icvStandardEntryId) : null,
    icvNominalMgPerL:
      form.requireIcv && form.icvNominalMgPerL !== "" ? Number(form.icvNominalMgPerL) : null,
    icvRecoveryLowPercent:
      form.requireIcv && form.icvRecoveryLowPercent !== "" ? Number(form.icvRecoveryLowPercent) : null,
    icvRecoveryHighPercent:
      form.requireIcv && form.icvRecoveryHighPercent !== "" ? Number(form.icvRecoveryHighPercent) : null,

    requireCcv: Boolean(form.requireCcv),
    ccvNominalMgPerL:
      form.requireCcv && form.ccvNominalMgPerL !== "" ? Number(form.ccvNominalMgPerL) : null,
    ccvRecoveryLowPercent:
      form.requireCcv && form.ccvRecoveryLowPercent !== "" ? Number(form.ccvRecoveryLowPercent) : null,
    ccvRecoveryHighPercent:
      form.requireCcv && form.ccvRecoveryHighPercent !== "" ? Number(form.ccvRecoveryHighPercent) : null,

    elements: form.elements.map((e) => ({
      id: e.id ?? null,
      symbol: normaliseSymbol(e.symbol),
      wavelengthNm: Number(e.wavelengthNm),
      view: e.view,
      conversionFactor: e.conversionFactor !== "" ? Number(e.conversionFactor) : 1
    }))
  };
}

export function getAvailableReferenceStandards(
  sectionId: number | null,
  materials: MaterialMasterEntry[],
  currentStandardIds?: (number | null | undefined)[]
): MaterialMasterEntry[] {
  if (!sectionId) return [];
  const currentSet = new Set((currentStandardIds ?? []).filter((id): id is number => id != null));
  return materials.filter(
    (m) =>
      m.category === "ReferenceStandard" &&
      m.sectionId === sectionId &&
      (m.isActive || currentSet.has(m.id))
  );
}
