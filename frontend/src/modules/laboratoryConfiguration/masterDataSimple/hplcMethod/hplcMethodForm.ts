import {
  ElutionMode,
  HplcDetectorType,
  HplcMethodResponse,
  HplcMethodMobilePhaseResponse,
  HplcMethodAnalyteResponse,
  SaveHplcMethodRequest
} from "../services/HplcMethodService";
import { SolutionMaster } from "../services/SolutionMasterService";
import { MaterialMasterEntry } from "../services/MaterialMasterService";

export interface MobilePhaseRowState {
  channel: string;
  solutionMasterId: number | "";
  ratioPercent: string | number;
}

export interface GradientStepRowState {
  timeMin: string | number;
  percentA: string | number;
  percentB: string | number;
  percentC: string | number;
  percentD: string | number;
}

export interface AnalyteRowState {
  id?: number;
  name: string;
  wavelengthNm: string | number;
  standardEntryId: number | "";
  theoreticalWeightStdMg: string | number;
  theoreticalWeightTestMg: string | number;
  standardInjections: string | number;
  sstMaxRsdPercent: string | number;
  sstMinResolution: string | number;
  sstMaxTailingFactor: string | number;
  sstMinTheoreticalPlates: string | number;
  sstMinRetentionFactor: string | number;
  sstMinSignalToNoise: string | number;
  sstMinPeakToValley: string | number;
}

export interface HplcMethodFormState {
  name: string;
  abbreviation: string;
  effectiveDate: string;
  sectionId: number | "";
  columnDesignation: string;
  columnLengthMm: string | number;
  columnInternalDiameterMm: string | number;
  particleSizeUm: string | number;
  columnBrand: string;
  columnPartNumber: string;
  columnTemperatureC: string | number;
  elutionMode: ElutionMode;
  equilibrationMin: string | number;
  flowRateMlPerMin: string | number;
  detectorType: HplcDetectorType;
  injectionVolumeUl: string | number;
  runTimeMin: string | number;
  diluentSolutionId: number | "";
  mobilePhases: MobilePhaseRowState[];
  gradientSteps: GradientStepRowState[];
  analytes: AnalyteRowState[];
}

export const ELUTION_MODE_OPTIONS: Array<{ value: ElutionMode; label: string }> = [
  { value: "Isocratic", label: "Isocratic" },
  { value: "Gradient", label: "Gradient" }
];

export const DETECTOR_TYPE_OPTIONS: Array<{ value: HplcDetectorType; label: string }> = [
  { value: "UV", label: "UV" },
  { value: "PDA", label: "PDA (Photodiode Array)" },
  { value: "FLD", label: "FLD (Fluorescence)" },
  { value: "RI", label: "RI (Refractive Index)" },
  { value: "ELSD", label: "ELSD (Evaporative Light Scattering)" },
  { value: "Other", label: "Other" }
];

export const CHANNELS = ["A", "B", "C", "D"];

export function createInitialHplcMethodFormState(defaultSectionId: number | "" = ""): HplcMethodFormState {
  return {
    name: "",
    abbreviation: "",
    effectiveDate: new Date().toISOString().split("T")[0],
    sectionId: defaultSectionId,
    columnDesignation: "",
    columnLengthMm: "150",
    columnInternalDiameterMm: "4.6",
    particleSizeUm: "5",
    columnBrand: "",
    columnPartNumber: "",
    columnTemperatureC: "25",
    elutionMode: "Isocratic",
    equilibrationMin: "",
    flowRateMlPerMin: "1.0",
    detectorType: "UV",
    injectionVolumeUl: "10",
    runTimeMin: "15",
    diluentSolutionId: "",
    mobilePhases: [{ channel: "A", solutionMasterId: "", ratioPercent: "100" }],
    gradientSteps: [
      { timeMin: "0", percentA: "100", percentB: "0", percentC: "0", percentD: "0" },
      { timeMin: "10", percentA: "50", percentB: "50", percentC: "0", percentD: "0" }
    ],
    analytes: [
      {
        name: "",
        wavelengthNm: "254",
        standardEntryId: "",
        theoreticalWeightStdMg: "50",
        theoreticalWeightTestMg: "50",
        standardInjections: "5",
        sstMaxRsdPercent: "2.0",
        sstMinResolution: "",
        sstMaxTailingFactor: "2.0",
        sstMinTheoreticalPlates: "2000",
        sstMinRetentionFactor: "",
        sstMinSignalToNoise: "",
        sstMinPeakToValley: ""
      }
    ]
  };
}

export function hplcMethodEntityToFormState(m: HplcMethodResponse): HplcMethodFormState {
  return {
    name: m.name,
    abbreviation: m.abbreviation,
    effectiveDate: m.effectiveDate ? m.effectiveDate.split("T")[0] : "",
    sectionId: m.sectionId,
    columnDesignation: m.columnDesignation,
    columnLengthMm: m.columnLengthMm,
    columnInternalDiameterMm: m.columnInternalDiameterMm,
    particleSizeUm: m.particleSizeUm,
    columnBrand: m.columnBrand ?? "",
    columnPartNumber: m.columnPartNumber ?? "",
    columnTemperatureC: m.columnTemperatureC,
    elutionMode: m.elutionMode,
    equilibrationMin: m.equilibrationMin != null ? m.equilibrationMin : "",
    flowRateMlPerMin: m.flowRateMlPerMin,
    detectorType: m.detectorType,
    injectionVolumeUl: m.injectionVolumeUl,
    runTimeMin: m.runTimeMin,
    diluentSolutionId: m.diluentSolutionId,
    mobilePhases:
      m.mobilePhases.length > 0
        ? m.mobilePhases.map((p) => ({
            channel: p.channel,
            solutionMasterId: p.solutionMasterId,
            ratioPercent: p.ratioPercent != null ? p.ratioPercent : ""
          }))
        : [{ channel: "A", solutionMasterId: "", ratioPercent: "100" }],
    gradientSteps:
      m.gradientSteps.length > 0
        ? m.gradientSteps.map((s) => ({
            timeMin: s.timeMin,
            percentA: s.percentA,
            percentB: s.percentB,
            percentC: s.percentC,
            percentD: s.percentD
          }))
        : [
            { timeMin: "0", percentA: "100", percentB: "0", percentC: "0", percentD: "0" },
            { timeMin: "10", percentA: "50", percentB: "50", percentC: "0", percentD: "0" }
          ],
    analytes:
      m.analytes.length > 0
        ? m.analytes.map((a) => ({
            id: a.id,
            name: a.name,
            wavelengthNm: a.wavelengthNm,
            standardEntryId: a.standardEntryId,
            theoreticalWeightStdMg: a.theoreticalWeightStdMg,
            theoreticalWeightTestMg: a.theoreticalWeightTestMg,
            standardInjections: a.standardInjections,
            sstMaxRsdPercent: a.sstMaxRsdPercent != null ? a.sstMaxRsdPercent : "",
            sstMinResolution: a.sstMinResolution != null ? a.sstMinResolution : "",
            sstMaxTailingFactor: a.sstMaxTailingFactor != null ? a.sstMaxTailingFactor : "",
            sstMinTheoreticalPlates: a.sstMinTheoreticalPlates != null ? a.sstMinTheoreticalPlates : "",
            sstMinRetentionFactor: a.sstMinRetentionFactor != null ? a.sstMinRetentionFactor : "",
            sstMinSignalToNoise: a.sstMinSignalToNoise != null ? a.sstMinSignalToNoise : "",
            sstMinPeakToValley: a.sstMinPeakToValley != null ? a.sstMinPeakToValley : ""
          }))
        : []
  };
}

export { validateHplcMethodForm } from "./hplcMethodValidation";
export type { HplcMethodErrors } from "./hplcMethodValidation";

export function formToSaveRequest(
  form: HplcMethodFormState,
  resolvedSectionId: number | null
): SaveHplcMethodRequest {
  const isGradient = form.elutionMode === "Gradient";

  return {
    name: form.name.trim(),
    abbreviation: form.abbreviation.trim().toUpperCase(),
    effectiveDate: form.effectiveDate,
    columnDesignation: form.columnDesignation.trim(),
    columnLengthMm: Number(form.columnLengthMm),
    columnInternalDiameterMm: Number(form.columnInternalDiameterMm),
    particleSizeUm: Number(form.particleSizeUm),
    columnBrand: form.columnBrand.trim() || null,
    columnPartNumber: form.columnPartNumber.trim() || null,
    columnTemperatureC: Number(form.columnTemperatureC),
    elutionMode: form.elutionMode,
    flowRateMlPerMin: Number(form.flowRateMlPerMin),
    equilibrationMin: form.equilibrationMin !== "" ? Number(form.equilibrationMin) : null,
    detectorType: form.detectorType,
    injectionVolumeUl: Number(form.injectionVolumeUl),
    runTimeMin: Number(form.runTimeMin),
    diluentSolutionId: Number(form.diluentSolutionId),
    sectionId: resolvedSectionId,
    mobilePhases: form.mobilePhases.map((p) => ({
      channel: p.channel,
      solutionMasterId: Number(p.solutionMasterId),
      ratioPercent: !isGradient && p.ratioPercent !== "" ? Number(p.ratioPercent) : null
    })),
    gradientSteps: isGradient
      ? form.gradientSteps.map((s) => ({
          timeMin: Number(s.timeMin),
          percentA: Number(s.percentA || 0),
          percentB: Number(s.percentB || 0),
          percentC: Number(s.percentC || 0),
          percentD: Number(s.percentD || 0)
        }))
      : [],
    analytes: form.analytes.map((a) => ({
      id: a.id ?? null,
      name: a.name.trim(),
      wavelengthNm: Number(a.wavelengthNm),
      standardEntryId: Number(a.standardEntryId),
      theoreticalWeightStdMg: Number(a.theoreticalWeightStdMg),
      theoreticalWeightTestMg: Number(a.theoreticalWeightTestMg),
      standardInjections: Number(a.standardInjections),
      sstMaxRsdPercent: a.sstMaxRsdPercent !== "" ? Number(a.sstMaxRsdPercent) : null,
      sstMinResolution: a.sstMinResolution !== "" ? Number(a.sstMinResolution) : null,
      sstMaxTailingFactor: a.sstMaxTailingFactor !== "" ? Number(a.sstMaxTailingFactor) : null,
      sstMinTheoreticalPlates: a.sstMinTheoreticalPlates !== "" ? Number(a.sstMinTheoreticalPlates) : null,
      sstMinRetentionFactor: a.sstMinRetentionFactor !== "" ? Number(a.sstMinRetentionFactor) : null,
      sstMinSignalToNoise: a.sstMinSignalToNoise !== "" ? Number(a.sstMinSignalToNoise) : null,
      sstMinPeakToValley: a.sstMinPeakToValley !== "" ? Number(a.sstMinPeakToValley) : null
    }))
  };
}

export function getAvailableDiluents(
  sectionId: number | null,
  solutions: SolutionMaster[],
  currentDiluentId?: number | null
): SolutionMaster[] {
  if (!sectionId) return [];
  return solutions.filter(
    (s) => s.type === "Diluent" && s.sectionId === sectionId && (s.isActive || s.id === currentDiluentId)
  );
}

export function getAvailableMobilePhases(
  sectionId: number | null,
  solutions: SolutionMaster[],
  currentMethodPhases?: HplcMethodMobilePhaseResponse[]
): SolutionMaster[] {
  if (!sectionId) return [];
  const currentIds = new Set((currentMethodPhases ?? []).map((p) => p.solutionMasterId));
  return solutions.filter(
    (s) => s.type === "MobilePhase" && s.sectionId === sectionId && (s.isActive || currentIds.has(s.id))
  );
}

export function getAvailableReferenceStandards(
  sectionId: number | null,
  materials: MaterialMasterEntry[],
  currentAnalytes?: HplcMethodAnalyteResponse[]
): MaterialMasterEntry[] {
  if (!sectionId) return [];
  const currentStdIds = new Set((currentAnalytes ?? []).map((a) => a.standardEntryId));
  return materials.filter(
    (m) =>
      m.category === "ReferenceStandard" &&
      m.sectionId === sectionId &&
      (m.isActive || currentStdIds.has(m.id))
  );
}
