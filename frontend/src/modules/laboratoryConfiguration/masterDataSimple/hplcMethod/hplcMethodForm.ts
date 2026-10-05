import {
  ElutionMode,
  HplcDetectorType,
  HplcTechnique,
  HplcResultMode,
  CarrierGas,
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

export interface GcOvenStepRowState {
  id?: number;
  stepNo?: number;
  rateCPerMin: string | number; // empty string for initial step
  temperatureC: string | number;
  holdMin: string | number;
}

export interface AnalyteRowState {
  id?: number;
  name: string;
  wavelengthNm: string | number;
  standardEntryId: number | "";
  theoreticalWeightStdMg: string | number;
  theoreticalWeightTestMg: string | number;
  standardDilution: string | number;
  standardInjections: string | number;
  sstMaxRsdPercent: string | number;
  sstMinResolution: string | number;
  sstMaxTailingFactor: string | number;
  sstMinTheoreticalPlates: string | number;
  sstMinRetentionFactor: string | number;
  sstMinSignalToNoise: string | number;
  sstMinPeakToValley: string | number;
  standardConcentrationUgPerMl: string | number;
}

export interface HplcMethodFormState {
  technique: HplcTechnique;
  resultMode: HplcResultMode;
  name: string;
  abbreviation: string;
  effectiveDate: string;
  sectionId: number | "";
  columnDesignation: string;
  columnLength: string | number; // m for GC, mm for HPLC
  columnInternalDiameterMm: string | number;
  particleSizeUm: string | number;
  filmThicknessUm: string | number;
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
  carrierGas: CarrierGas | "";
  splitRatio: string | number;
  inletTemperatureC: string | number;
  detectorTemperatureC: string | number;
  headspaceEnabled: boolean;
  headspaceEquilibrationTemperatureC: string | number;
  headspaceEquilibrationMin: string | number;
  headspaceTransferLineTemperatureC: string | number;
  sampleSolutionVolumeMl: string | number;
  ovenSteps: GcOvenStepRowState[];
  mobilePhases: MobilePhaseRowState[];
  gradientSteps: GradientStepRowState[];
  analytes: AnalyteRowState[];
}

export const TECHNIQUE_OPTIONS: Array<{ value: HplcTechnique; label: string }> = [
  { value: "Hplc", label: "HPLC" },
  { value: "Gc", label: "GC" }
];

export const RESULT_MODE_OPTIONS: Array<{ value: HplcResultMode; label: string }> = [
  { value: "Assay", label: "Assay" },
  { value: "ResidualSolvents", label: "Residual Solvents" }
];

export const CARRIER_GAS_OPTIONS: Array<{ value: CarrierGas; label: string }> = [
  { value: "Helium", label: "Helium (He)" },
  { value: "Nitrogen", label: "Nitrogen (N2)" },
  { value: "Hydrogen", label: "Hydrogen (H2)" }
];

export const ELUTION_MODE_OPTIONS: Array<{ value: ElutionMode; label: string }> = [
  { value: "Isocratic", label: "Isocratic" },
  { value: "Gradient", label: "Gradient" }
];

export const HPLC_DETECTOR_OPTIONS: Array<{ value: HplcDetectorType; label: string }> = [
  { value: "UV", label: "UV" },
  { value: "PDA", label: "PDA (Photodiode Array)" },
  { value: "FLD", label: "FLD (Fluorescence)" },
  { value: "RI", label: "RI (Refractive Index)" },
  { value: "ELSD", label: "ELSD (Evaporative Light Scattering)" },
  { value: "Other", label: "Other" }
];

export const GC_DETECTOR_OPTIONS: Array<{ value: HplcDetectorType; label: string }> = [
  { value: "Fid", label: "FID" },
  { value: "Tcd", label: "TCD" },
  { value: "Ecd", label: "ECD" },
  { value: "Ms", label: "MS" }
];

export const DETECTOR_TYPE_OPTIONS: Array<{ value: HplcDetectorType; label: string }> = [
  ...HPLC_DETECTOR_OPTIONS,
  ...GC_DETECTOR_OPTIONS
];

export const CHANNELS = ["A", "B", "C", "D"];

export function createInitialHplcMethodFormState(
  defaultSectionId: number | "" = "",
  technique: HplcTechnique = "Hplc"
): HplcMethodFormState {
  if (technique === "Gc") {
    return {
      technique: "Gc",
      resultMode: "Assay",
      name: "",
      abbreviation: "",
      effectiveDate: new Date().toISOString().split("T")[0],
      sectionId: defaultSectionId,
      columnDesignation: "",
      columnLength: "30",
      columnInternalDiameterMm: "0.32",
      particleSizeUm: "",
      filmThicknessUm: "1.8",
      columnBrand: "",
      columnPartNumber: "",
      columnTemperatureC: "",
      elutionMode: "Isocratic",
      equilibrationMin: "",
      flowRateMlPerMin: "2.0",
      detectorType: "Fid",
      injectionVolumeUl: "1",
      runTimeMin: "20",
      diluentSolutionId: "",
      carrierGas: "Nitrogen",
      splitRatio: "",
      inletTemperatureC: "200",
      detectorTemperatureC: "250",
      headspaceEnabled: false,
      headspaceEquilibrationTemperatureC: "",
      headspaceEquilibrationMin: "",
      headspaceTransferLineTemperatureC: "",
      sampleSolutionVolumeMl: "",
      ovenSteps: [{ rateCPerMin: "", temperatureC: "40", holdMin: "5" }],
      mobilePhases: [],
      gradientSteps: [],
      analytes: [
        {
          name: "",
          wavelengthNm: "",
          standardEntryId: "",
          theoreticalWeightStdMg: "50",
          theoreticalWeightTestMg: "50",
          standardDilution: "",
          standardInjections: "5",
          sstMaxRsdPercent: "2.0",
          sstMinResolution: "",
          sstMaxTailingFactor: "2.0",
          sstMinTheoreticalPlates: "2000",
          sstMinRetentionFactor: "",
          sstMinSignalToNoise: "",
          sstMinPeakToValley: "",
          standardConcentrationUgPerMl: ""
        }
      ]
    };
  }

  return {
    technique: "Hplc",
    resultMode: "Assay",
    name: "",
    abbreviation: "",
    effectiveDate: new Date().toISOString().split("T")[0],
    sectionId: defaultSectionId,
    columnDesignation: "",
    columnLength: "150",
    columnInternalDiameterMm: "4.6",
    particleSizeUm: "5",
    filmThicknessUm: "",
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
    carrierGas: "",
    splitRatio: "",
    inletTemperatureC: "",
    detectorTemperatureC: "",
    headspaceEnabled: false,
    headspaceEquilibrationTemperatureC: "",
    headspaceEquilibrationMin: "",
    headspaceTransferLineTemperatureC: "",
    sampleSolutionVolumeMl: "",
    ovenSteps: [],
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
        standardDilution: "",
        standardInjections: "5",
        sstMaxRsdPercent: "2.0",
        sstMinResolution: "",
        sstMaxTailingFactor: "2.0",
        sstMinTheoreticalPlates: "2000",
        sstMinRetentionFactor: "",
        sstMinSignalToNoise: "",
        sstMinPeakToValley: "",
        standardConcentrationUgPerMl: ""
      }
    ]
  };
}

export function hplcMethodEntityToFormState(m: HplcMethodResponse): HplcMethodFormState {
  const technique: HplcTechnique = m.technique ?? "Hplc";
  const resultMode: HplcResultMode = m.resultMode ?? "Assay";

  // Column length: GC entered in metres, stored in mm
  const lengthDisplay =
    technique === "Gc"
      ? m.columnLengthMm != null
        ? String(m.columnLengthMm / 1000)
        : ""
      : m.columnLengthMm != null
      ? String(m.columnLengthMm)
      : "";

  return {
    technique,
    resultMode,
    name: m.name,
    abbreviation: m.abbreviation,
    effectiveDate: m.effectiveDate ? m.effectiveDate.split("T")[0] : "",
    sectionId: m.sectionId,
    columnDesignation: m.columnDesignation,
    columnLength: lengthDisplay,
    columnInternalDiameterMm: m.columnInternalDiameterMm != null ? String(m.columnInternalDiameterMm) : "",
    particleSizeUm: m.particleSizeUm != null ? String(m.particleSizeUm) : "",
    filmThicknessUm: m.filmThicknessUm != null ? String(m.filmThicknessUm) : "",
    columnBrand: m.columnBrand ?? "",
    columnPartNumber: m.columnPartNumber ?? "",
    columnTemperatureC: m.columnTemperatureC != null ? String(m.columnTemperatureC) : "",
    elutionMode: m.elutionMode,
    equilibrationMin: m.equilibrationMin != null ? String(m.equilibrationMin) : "",
    flowRateMlPerMin: m.flowRateMlPerMin != null ? String(m.flowRateMlPerMin) : "",
    detectorType: m.detectorType,
    injectionVolumeUl: m.injectionVolumeUl != null ? String(m.injectionVolumeUl) : "",
    runTimeMin: m.runTimeMin != null ? String(m.runTimeMin) : "",
    diluentSolutionId: m.diluentSolutionId,
    carrierGas: m.carrierGas ?? "",
    splitRatio: m.splitRatio != null ? String(m.splitRatio) : "",
    inletTemperatureC: m.inletTemperatureC != null ? String(m.inletTemperatureC) : "",
    detectorTemperatureC: m.detectorTemperatureC != null ? String(m.detectorTemperatureC) : "",
    headspaceEnabled: Boolean(m.headspaceEnabled),
    headspaceEquilibrationTemperatureC:
      m.headspaceEquilibrationTemperatureC != null ? String(m.headspaceEquilibrationTemperatureC) : "",
    headspaceEquilibrationMin:
      m.headspaceEquilibrationMin != null ? String(m.headspaceEquilibrationMin) : "",
    headspaceTransferLineTemperatureC:
      m.headspaceTransferLineTemperatureC != null ? String(m.headspaceTransferLineTemperatureC) : "",
    sampleSolutionVolumeMl:
      m.sampleSolutionVolumeMl != null ? String(m.sampleSolutionVolumeMl) : "",
    ovenSteps:
      m.ovenSteps && m.ovenSteps.length > 0
        ? m.ovenSteps.map((s) => ({
            id: s.id,
            stepNo: s.stepNo,
            rateCPerMin: s.rateCPerMin != null ? String(s.rateCPerMin) : "",
            temperatureC: String(s.temperatureC),
            holdMin: String(s.holdMin)
          }))
        : technique === "Gc"
        ? [{ rateCPerMin: "", temperatureC: "40", holdMin: "5" }]
        : [],
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
            wavelengthNm: a.wavelengthNm != null ? a.wavelengthNm : "",
            standardEntryId: a.standardEntryId,
            theoreticalWeightStdMg: a.theoreticalWeightStdMg,
            theoreticalWeightTestMg: a.theoreticalWeightTestMg,
            standardDilution: a.standardDilution ?? "",
            standardInjections: a.standardInjections,
            sstMaxRsdPercent: a.sstMaxRsdPercent != null ? a.sstMaxRsdPercent : "",
            sstMinResolution: a.sstMinResolution != null ? a.sstMinResolution : "",
            sstMaxTailingFactor: a.sstMaxTailingFactor != null ? a.sstMaxTailingFactor : "",
            sstMinTheoreticalPlates: a.sstMinTheoreticalPlates != null ? a.sstMinTheoreticalPlates : "",
            sstMinRetentionFactor: a.sstMinRetentionFactor != null ? a.sstMinRetentionFactor : "",
            sstMinSignalToNoise: a.sstMinSignalToNoise != null ? a.sstMinSignalToNoise : "",
            sstMinPeakToValley: a.sstMinPeakToValley != null ? a.sstMinPeakToValley : "",
            standardConcentrationUgPerMl:
              a.standardConcentrationUgPerMl != null ? String(a.standardConcentrationUgPerMl) : ""
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
  const isGc = form.technique === "Gc";
  const rawLength = form.columnLength;

  if (isGc) {
    const isResidual = form.resultMode === "ResidualSolvents";
    // GC column length entered in metres -> sent as mm (x1000)
    const lengthMm = Number(rawLength) * 1000;

    return {
      technique: "Gc",
      resultMode: form.resultMode,
      name: form.name.trim(),
      abbreviation: form.abbreviation.trim().toUpperCase(),
      effectiveDate: form.effectiveDate,
      columnDesignation: form.columnDesignation.trim(),
      columnLengthMm: lengthMm,
      columnInternalDiameterMm: Number(form.columnInternalDiameterMm),
      particleSizeUm: null,
      columnBrand: form.columnBrand.trim() || null,
      columnPartNumber: form.columnPartNumber.trim() || null,
      columnTemperatureC: null,
      elutionMode: "Isocratic",
      flowRateMlPerMin: Number(form.flowRateMlPerMin),
      equilibrationMin: null,
      detectorType: form.detectorType,
      injectionVolumeUl: Number(form.injectionVolumeUl),
      runTimeMin: Number(form.runTimeMin),
      diluentSolutionId: Number(form.diluentSolutionId),
      sectionId: resolvedSectionId,
      mobilePhases: [],
      gradientSteps: [],
      filmThicknessUm: Number(form.filmThicknessUm),
      carrierGas: form.carrierGas as CarrierGas,
      splitRatio: form.splitRatio !== "" && form.splitRatio != null ? Number(form.splitRatio) : null,
      inletTemperatureC: Number(form.inletTemperatureC),
      detectorTemperatureC: Number(form.detectorTemperatureC),
      headspaceEnabled: Boolean(form.headspaceEnabled),
      headspaceEquilibrationTemperatureC:
        form.headspaceEnabled && form.headspaceEquilibrationTemperatureC !== ""
          ? Number(form.headspaceEquilibrationTemperatureC)
          : null,
      headspaceEquilibrationMin:
        form.headspaceEnabled && form.headspaceEquilibrationMin !== ""
          ? Number(form.headspaceEquilibrationMin)
          : null,
      headspaceTransferLineTemperatureC:
        form.headspaceEnabled && form.headspaceTransferLineTemperatureC !== ""
          ? Number(form.headspaceTransferLineTemperatureC)
          : null,
      sampleSolutionVolumeMl:
        isResidual && form.sampleSolutionVolumeMl !== ""
          ? Number(form.sampleSolutionVolumeMl)
          : null,
      ovenSteps: form.ovenSteps.map((s, idx) => ({
        rateCPerMin:
          idx === 0 || s.rateCPerMin === "" || s.rateCPerMin == null ? null : Number(s.rateCPerMin),
        temperatureC: Number(s.temperatureC),
        holdMin: Number(s.holdMin || 0)
      })),
      analytes: form.analytes.map((a) => {
        if (isResidual) {
          return {
            id: a.id ?? null,
            name: a.name.trim(),
            wavelengthNm: null,
            standardEntryId: Number(a.standardEntryId),
            theoreticalWeightStdMg: 0,
            theoreticalWeightTestMg: 0,
            standardDilution: null,
            standardConcentrationUgPerMl: Number(a.standardConcentrationUgPerMl),
            standardInjections: Number(a.standardInjections),
            sstMaxRsdPercent: a.sstMaxRsdPercent !== "" ? Number(a.sstMaxRsdPercent) : null,
            sstMinResolution: a.sstMinResolution !== "" ? Number(a.sstMinResolution) : null,
            sstMaxTailingFactor: a.sstMaxTailingFactor !== "" ? Number(a.sstMaxTailingFactor) : null,
            sstMinTheoreticalPlates: a.sstMinTheoreticalPlates !== "" ? Number(a.sstMinTheoreticalPlates) : null,
            sstMinRetentionFactor: a.sstMinRetentionFactor !== "" ? Number(a.sstMinRetentionFactor) : null,
            sstMinSignalToNoise: a.sstMinSignalToNoise !== "" ? Number(a.sstMinSignalToNoise) : null,
            sstMinPeakToValley: a.sstMinPeakToValley !== "" ? Number(a.sstMinPeakToValley) : null
          };
        }
        return {
          id: a.id ?? null,
          name: a.name.trim(),
          wavelengthNm: null,
          standardEntryId: Number(a.standardEntryId),
          theoreticalWeightStdMg: Number(a.theoreticalWeightStdMg),
          theoreticalWeightTestMg: Number(a.theoreticalWeightTestMg),
          standardDilution:
            a.standardDilution === "" || a.standardDilution == null ? null : Number(a.standardDilution),
          standardConcentrationUgPerMl: null,
          standardInjections: Number(a.standardInjections),
          sstMaxRsdPercent: a.sstMaxRsdPercent !== "" ? Number(a.sstMaxRsdPercent) : null,
          sstMinResolution: a.sstMinResolution !== "" ? Number(a.sstMinResolution) : null,
          sstMaxTailingFactor: a.sstMaxTailingFactor !== "" ? Number(a.sstMaxTailingFactor) : null,
          sstMinTheoreticalPlates: a.sstMinTheoreticalPlates !== "" ? Number(a.sstMinTheoreticalPlates) : null,
          sstMinRetentionFactor: a.sstMinRetentionFactor !== "" ? Number(a.sstMinRetentionFactor) : null,
          sstMinSignalToNoise: a.sstMinSignalToNoise !== "" ? Number(a.sstMinSignalToNoise) : null,
          sstMinPeakToValley: a.sstMinPeakToValley !== "" ? Number(a.sstMinPeakToValley) : null
        };
      })
    };
  }

  // HPLC
  const isGradient = form.elutionMode === "Gradient";

  return {
    technique: "Hplc",
    resultMode: "Assay",
    name: form.name.trim(),
    abbreviation: form.abbreviation.trim().toUpperCase(),
    effectiveDate: form.effectiveDate,
    columnDesignation: form.columnDesignation.trim(),
    columnLengthMm: Number(rawLength),
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
    filmThicknessUm: null,
    carrierGas: null,
    splitRatio: null,
    inletTemperatureC: null,
    detectorTemperatureC: null,
    headspaceEnabled: false,
    headspaceEquilibrationTemperatureC: null,
    headspaceEquilibrationMin: null,
    headspaceTransferLineTemperatureC: null,
    sampleSolutionVolumeMl: null,
    ovenSteps: [],
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
      standardDilution:
        a.standardDilution === "" || a.standardDilution == null ? null : Number(a.standardDilution),
      standardConcentrationUgPerMl: null,
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
