import type { TestDefinitionOption } from "../../../../hooks/useTestDefinitions";
import type {
  CreateTestDefinitionPayload,
  MoistureRole,
  UpdateTestDefinitionPayload,
  UvCalculationMode
} from "../../../../services/masterDataOptions";
import { PageArea, PhyschemArea, defaultAreaFor } from "../testMasterArea";
import { TestMasterLab } from "../testMaster/testMasterConstants";
import {
  IcpTestForm,
  createInitialIcpForm,
  icpFormFromDefinition,
  icpPayloadFields,
  validateIcpForm
} from "../testMaster/icpTestConfig";
import {
  TitrationFormState,
  createInitialTitrationForm,
  titrationFormFromDefinition,
  titrationPayloadFields,
  validateTitrationForm
} from "../titrationConfig";
import { titrationChangeReasonCheck } from "../TitrationChangeReasonField";

export type TestWizardStep = "basics" | "typeSettings" | "stages" | "steps";

export const TEST_WIZARD_STEP_ORDER: readonly TestWizardStep[] = [
  "basics",
  "typeSettings",
  "stages",
  "steps"
];

// What the Add Test form OPENS with for the Dissolution, Disintegration and
// Weight Variation acceptance fields. These numbers are only a starting point:
// they are shown in the form, the user can edit or clear them, and they are
// NEVER applied on save. Saving rejects a blank field, and the saved values
// always come from what the user entered.
export const PREFILL_METHOD_FIELDS = {
  dissolutionS1Offset: "5",
  dissolutionS2MinOffset: "15",
  dissolutionS3MinOffset: "25",
  dissolutionS3MaxBelowS2Min: "2",
  disintegrationStage1Units: "6",
  disintegrationStage2Units: "12",
  disintegrationMaxStage1Failures: "2",
  disintegrationMinPassTotal: "16",
  wvUnitCount: "20",
  wvTabletBand1MaxMg: "130",
  wvTabletBand1Percent: "10",
  wvTabletBand2MaxMg: "324",
  wvTabletBand2Percent: "7.5",
  wvTabletBand3Percent: "5",
  wvTabletMaxOutside: "2",
  wvCapsuleInnerPercent: "10",
  wvCapsuleOuterPercent: "25",
  wvCapsuleS1MaxOutside: "2",
  wvCapsuleS1MaxForRetest: "6",
  wvCapsuleS2ExtraUnits: "40",
  wvCapsuleS2MaxOutside: "6"
} as const;

export type MethodGroupKey = keyof typeof PREFILL_METHOD_FIELDS;

// Sent as null for every workflow type that does not own these fields.
export const NO_METHOD_GROUP_FIELDS: Record<MethodGroupKey, null> = {
  dissolutionS1Offset: null,
  dissolutionS2MinOffset: null,
  dissolutionS3MinOffset: null,
  dissolutionS3MaxBelowS2Min: null,
  disintegrationStage1Units: null,
  disintegrationStage2Units: null,
  disintegrationMaxStage1Failures: null,
  disintegrationMinPassTotal: null,
  wvUnitCount: null,
  wvTabletBand1MaxMg: null,
  wvTabletBand1Percent: null,
  wvTabletBand2MaxMg: null,
  wvTabletBand2Percent: null,
  wvTabletBand3Percent: null,
  wvTabletMaxOutside: null,
  wvCapsuleInnerPercent: null,
  wvCapsuleOuterPercent: null,
  wvCapsuleS1MaxOutside: null,
  wvCapsuleS1MaxForRetest: null,
  wvCapsuleS2ExtraUnits: null,
  wvCapsuleS2MaxOutside: null
};

export type MeasurementEvaluationBasis = "Mean" | "EachValue" | "Min" | "Max";

export interface TestModelContext {
  lab?: TestMasterLab;
  area?: PageArea;
  defaultSectionId?: number | "";
}

/**
 * Full form state + lab/edit context for a Test Master definition.
 * Mirrors every state variable of the TestMasterPage Add/Edit dialog so
 * validation, type switching, and payload shaping live in one place.
 */
export interface TestFormModel {
  // Page / edit identity context
  lab: TestMasterLab;
  area?: PageArea;
  editingId: number | null;
  editingSectionId: number | null;
  editingTest: TestDefinitionOption | null;
  duplicateOfCode: string | null;

  // Basics
  code: string;
  displayName: string;
  coaDisplayOrder: string;
  sectionId: number | "";
  workflowType: string;
  equationType: string;
  physchemArea: PhyschemArea | null;

  // Type settings
  requiresSystemSuitability: boolean;
  methodAbbreviation: string;
  hplcMethodId: number | "";
  icp: IcpTestForm;
  replicateCount: string;
  evaluationBasis: MeasurementEvaluationBasis;
  conditionFields: string;
  usesTare: boolean;
  perContainer: boolean;
  moistureRole: MoistureRole;
  titration: TitrationFormState;
  changeReason: string;
  uvCalculationMode: UvCalculationMode | "";
  uvSpecificAbsorbance: string;
  uvTotalVolumeMl: string;
  uvStandardVolumeMl: string;
  uvStandardEntryId: number | "";

  // Dissolution
  dissolutionS1Offset: string;
  dissolutionS2MinOffset: string;
  dissolutionS3MinOffset: string;
  dissolutionS3MaxBelowS2Min: string;

  // Disintegration
  disintegrationStage1Units: string;
  disintegrationStage2Units: string;
  disintegrationMaxStage1Failures: string;
  disintegrationMinPassTotal: string;

  // Weight Variation
  wvUnitCount: string;
  wvTabletBand1MaxMg: string;
  wvTabletBand1Percent: string;
  wvTabletBand2MaxMg: string;
  wvTabletBand2Percent: string;
  wvTabletBand3Percent: string;
  wvTabletMaxOutside: string;
  wvCapsuleInnerPercent: string;
  wvCapsuleOuterPercent: string;
  wvCapsuleS1MaxOutside: string;
  wvCapsuleS1MaxForRetest: string;
  wvCapsuleS2ExtraUnits: string;
  wvCapsuleS2MaxOutside: string;
}

export function createEmptyTestFormModel(overrides: Partial<TestFormModel> = {}): TestFormModel {
  const lab: TestMasterLab = overrides.lab ?? "fp";
  const isFp = lab === "fp";
  const defaultWorkflowType = isFp ? "HplcMethodAssay" : "Observation";
  const area = overrides.area;

  return {
    lab,
    area,
    editingId: null,
    editingSectionId: null,
    editingTest: null,
    duplicateOfCode: null,
    code: "",
    displayName: "",
    coaDisplayOrder: "",
    sectionId: "",
    workflowType: defaultWorkflowType,
    equationType: isFp ? "HplcMethodAssay" : "None",
    physchemArea: area ? defaultAreaFor(area) : null,
    requiresSystemSuitability: false,
    methodAbbreviation: "",
    hplcMethodId: "",
    icp: createInitialIcpForm(),
    replicateCount: "1",
    evaluationBasis: "Mean",
    conditionFields: "",
    usesTare: false,
    perContainer: false,
    moistureRole: "None",
    titration: createInitialTitrationForm(),
    changeReason: "",
    uvCalculationMode: "",
    uvSpecificAbsorbance: "",
    uvTotalVolumeMl: "",
    uvStandardVolumeMl: "",
    uvStandardEntryId: "",
    dissolutionS1Offset: PREFILL_METHOD_FIELDS.dissolutionS1Offset,
    dissolutionS2MinOffset: PREFILL_METHOD_FIELDS.dissolutionS2MinOffset,
    dissolutionS3MinOffset: PREFILL_METHOD_FIELDS.dissolutionS3MinOffset,
    dissolutionS3MaxBelowS2Min: PREFILL_METHOD_FIELDS.dissolutionS3MaxBelowS2Min,
    disintegrationStage1Units: PREFILL_METHOD_FIELDS.disintegrationStage1Units,
    disintegrationStage2Units: PREFILL_METHOD_FIELDS.disintegrationStage2Units,
    disintegrationMaxStage1Failures: PREFILL_METHOD_FIELDS.disintegrationMaxStage1Failures,
    disintegrationMinPassTotal: PREFILL_METHOD_FIELDS.disintegrationMinPassTotal,
    wvUnitCount: PREFILL_METHOD_FIELDS.wvUnitCount,
    wvTabletBand1MaxMg: PREFILL_METHOD_FIELDS.wvTabletBand1MaxMg,
    wvTabletBand1Percent: PREFILL_METHOD_FIELDS.wvTabletBand1Percent,
    wvTabletBand2MaxMg: PREFILL_METHOD_FIELDS.wvTabletBand2MaxMg,
    wvTabletBand2Percent: PREFILL_METHOD_FIELDS.wvTabletBand2Percent,
    wvTabletBand3Percent: PREFILL_METHOD_FIELDS.wvTabletBand3Percent,
    wvTabletMaxOutside: PREFILL_METHOD_FIELDS.wvTabletMaxOutside,
    wvCapsuleInnerPercent: PREFILL_METHOD_FIELDS.wvCapsuleInnerPercent,
    wvCapsuleOuterPercent: PREFILL_METHOD_FIELDS.wvCapsuleOuterPercent,
    wvCapsuleS1MaxOutside: PREFILL_METHOD_FIELDS.wvCapsuleS1MaxOutside,
    wvCapsuleS1MaxForRetest: PREFILL_METHOD_FIELDS.wvCapsuleS1MaxForRetest,
    wvCapsuleS2ExtraUnits: PREFILL_METHOD_FIELDS.wvCapsuleS2ExtraUnits,
    wvCapsuleS2MaxOutside: PREFILL_METHOD_FIELDS.wvCapsuleS2MaxOutside,
    ...overrides
  };
}

function defaultEquationForWorkflow(workflowType: string): string {
  switch (workflowType) {
    case "HplcMethodAssay":
      return "HplcMethodAssay";
    case "IcpMethodAssay":
      return "IcpMethodAssay";
    case "Measurement":
      return "Measurement";
    case "Gravimetric":
      return "GravimetricLoss";
    case "Qualitative":
      return "Qualitative";
    case "Dissolution":
      return "Dissolution";
    case "Disintegration":
      return "Disintegration";
    case "WeightVariation":
      return "WeightVariation";
    case "Titration":
      return "Titration";
    case "UvSpectrophotometry":
      return "UvSpectrophotometry";
    default:
      return "None";
  }
}

export function fromTestDefinition(
  t: TestDefinitionOption,
  context: TestModelContext = {}
): TestFormModel {
  const inferredLab: TestMasterLab =
    context.lab ?? (t.workflowType === "CountTest" || t.workflowType === "Observation" ? "micro" : "fp");
  const isFp = inferredLab === "fp";
  const defaultWorkflowType = isFp ? "HplcMethodAssay" : "Observation";
  const workflowType = t.workflowType || defaultWorkflowType;
  const equationType = t.equationType || defaultEquationForWorkflow(workflowType);
  const requiresSystemSuitability =
    t.workflowType === "Dissolution" || t.workflowType === "HplcMethodAssay"
      ? true
      : t.workflowType === "Disintegration" ||
        t.workflowType === "WeightVariation" ||
        t.workflowType === "IcpMethodAssay" ||
        t.workflowType === "UvSpectrophotometry"
      ? false
      : !!t.requiresSystemSuitability;

  return {
    lab: inferredLab,
    area: context.area,
    editingId: t.id,
    editingSectionId: t.sectionId ?? null,
    editingTest: t,
    duplicateOfCode: null,
    code: t.code,
    displayName: t.displayName,
    coaDisplayOrder: t.coaDisplayOrder != null ? String(t.coaDisplayOrder) : "",
    sectionId: t.sectionId ?? context.defaultSectionId ?? "",
    workflowType,
    equationType,
    requiresSystemSuitability,
    methodAbbreviation: t.methodAbbreviation ?? "",
    hplcMethodId: t.hplcMethodId ?? "",
    icp: icpFormFromDefinition(t),
    replicateCount: t.replicateCount != null ? String(t.replicateCount) : "1",
    evaluationBasis: t.evaluationBasis || "Mean",
    conditionFields: t.conditionFields || "",
    usesTare: !!t.usesTare,
    perContainer: !!t.perContainer,
    moistureRole: t.moistureRole ?? "None",
    titration: titrationFormFromDefinition(t),
    changeReason: "",
    uvCalculationMode: t.uvCalculationMode ?? "",
    uvSpecificAbsorbance: t.uvSpecificAbsorbance != null ? String(t.uvSpecificAbsorbance) : "",
    uvTotalVolumeMl: t.uvTotalVolumeMl != null ? String(t.uvTotalVolumeMl) : "",
    uvStandardVolumeMl: t.uvStandardVolumeMl != null ? String(t.uvStandardVolumeMl) : "",
    uvStandardEntryId: t.uvStandardEntryId ?? "",
    dissolutionS1Offset:
      t.dissolutionS1Offset != null ? String(t.dissolutionS1Offset) : PREFILL_METHOD_FIELDS.dissolutionS1Offset,
    dissolutionS2MinOffset:
      t.dissolutionS2MinOffset != null ? String(t.dissolutionS2MinOffset) : PREFILL_METHOD_FIELDS.dissolutionS2MinOffset,
    dissolutionS3MinOffset:
      t.dissolutionS3MinOffset != null ? String(t.dissolutionS3MinOffset) : PREFILL_METHOD_FIELDS.dissolutionS3MinOffset,
    dissolutionS3MaxBelowS2Min:
      t.dissolutionS3MaxBelowS2Min != null
        ? String(t.dissolutionS3MaxBelowS2Min)
        : PREFILL_METHOD_FIELDS.dissolutionS3MaxBelowS2Min,
    disintegrationStage1Units:
      t.disintegrationStage1Units != null
        ? String(t.disintegrationStage1Units)
        : PREFILL_METHOD_FIELDS.disintegrationStage1Units,
    disintegrationStage2Units:
      t.disintegrationStage2Units != null
        ? String(t.disintegrationStage2Units)
        : PREFILL_METHOD_FIELDS.disintegrationStage2Units,
    disintegrationMaxStage1Failures:
      t.disintegrationMaxStage1Failures != null
        ? String(t.disintegrationMaxStage1Failures)
        : PREFILL_METHOD_FIELDS.disintegrationMaxStage1Failures,
    disintegrationMinPassTotal:
      t.disintegrationMinPassTotal != null
        ? String(t.disintegrationMinPassTotal)
        : PREFILL_METHOD_FIELDS.disintegrationMinPassTotal,
    wvUnitCount: t.wvUnitCount != null ? String(t.wvUnitCount) : PREFILL_METHOD_FIELDS.wvUnitCount,
    wvTabletBand1MaxMg:
      t.wvTabletBand1MaxMg != null ? String(t.wvTabletBand1MaxMg) : PREFILL_METHOD_FIELDS.wvTabletBand1MaxMg,
    wvTabletBand1Percent:
      t.wvTabletBand1Percent != null ? String(t.wvTabletBand1Percent) : PREFILL_METHOD_FIELDS.wvTabletBand1Percent,
    wvTabletBand2MaxMg:
      t.wvTabletBand2MaxMg != null ? String(t.wvTabletBand2MaxMg) : PREFILL_METHOD_FIELDS.wvTabletBand2MaxMg,
    wvTabletBand2Percent:
      t.wvTabletBand2Percent != null ? String(t.wvTabletBand2Percent) : PREFILL_METHOD_FIELDS.wvTabletBand2Percent,
    wvTabletBand3Percent:
      t.wvTabletBand3Percent != null ? String(t.wvTabletBand3Percent) : PREFILL_METHOD_FIELDS.wvTabletBand3Percent,
    wvTabletMaxOutside:
      t.wvTabletMaxOutside != null ? String(t.wvTabletMaxOutside) : PREFILL_METHOD_FIELDS.wvTabletMaxOutside,
    wvCapsuleInnerPercent:
      t.wvCapsuleInnerPercent != null ? String(t.wvCapsuleInnerPercent) : PREFILL_METHOD_FIELDS.wvCapsuleInnerPercent,
    wvCapsuleOuterPercent:
      t.wvCapsuleOuterPercent != null ? String(t.wvCapsuleOuterPercent) : PREFILL_METHOD_FIELDS.wvCapsuleOuterPercent,
    wvCapsuleS1MaxOutside:
      t.wvCapsuleS1MaxOutside != null ? String(t.wvCapsuleS1MaxOutside) : PREFILL_METHOD_FIELDS.wvCapsuleS1MaxOutside,
    wvCapsuleS1MaxForRetest:
      t.wvCapsuleS1MaxForRetest != null
        ? String(t.wvCapsuleS1MaxForRetest)
        : PREFILL_METHOD_FIELDS.wvCapsuleS1MaxForRetest,
    wvCapsuleS2ExtraUnits:
      t.wvCapsuleS2ExtraUnits != null ? String(t.wvCapsuleS2ExtraUnits) : PREFILL_METHOD_FIELDS.wvCapsuleS2ExtraUnits,
    wvCapsuleS2MaxOutside:
      t.wvCapsuleS2MaxOutside != null ? String(t.wvCapsuleS2MaxOutside) : PREFILL_METHOD_FIELDS.wvCapsuleS2MaxOutside,
    physchemArea: t.physchemArea ?? (context.area ? defaultAreaFor(context.area) : null)
  };
}

export function withWorkflowType(model: TestFormModel, next: string): TestFormModel {
  const nextModel: TestFormModel = { ...model, workflowType: next };
  if (next !== "Qualitative") {
    nextModel.perContainer = false;
  }
  if (next === "HplcMethodAssay") {
    nextModel.equationType = "HplcMethodAssay";
    nextModel.requiresSystemSuitability = true;
    nextModel.moistureRole = "None";
  } else if (next === "IcpMethodAssay") {
    nextModel.equationType = "IcpMethodAssay";
    nextModel.requiresSystemSuitability = false;
    nextModel.moistureRole = "None";
  } else if (next === "Measurement") {
    nextModel.equationType = "Measurement";
    nextModel.requiresSystemSuitability = false;
    nextModel.moistureRole = "None";
  } else if (next === "Gravimetric") {
    let nextEq = model.equationType;
    if (model.equationType !== "GravimetricLoss" && model.equationType !== "GravimetricResidue") {
      nextEq = "GravimetricLoss";
      nextModel.equationType = "GravimetricLoss";
    }
    if (nextEq !== "GravimetricLoss" && model.moistureRole === "LossOnDrying") {
      nextModel.moistureRole = "None";
    } else if (model.moistureRole === "Water") {
      nextModel.moistureRole = "None";
    }
    nextModel.requiresSystemSuitability = false;
  } else if (next === "Qualitative") {
    nextModel.equationType = "Qualitative";
    nextModel.requiresSystemSuitability = false;
    nextModel.moistureRole = "None";
  } else if (next === "Dissolution") {
    nextModel.equationType = "Dissolution";
    nextModel.requiresSystemSuitability = true;
    nextModel.moistureRole = "None";
  } else if (next === "Disintegration") {
    nextModel.equationType = "Disintegration";
    nextModel.requiresSystemSuitability = false;
    nextModel.moistureRole = "None";
  } else if (next === "WeightVariation") {
    nextModel.equationType = "WeightVariation";
    nextModel.requiresSystemSuitability = false;
    nextModel.moistureRole = "None";
  } else if (next === "Titration") {
    nextModel.equationType = "Titration";
    nextModel.requiresSystemSuitability = false;
    if (model.titration.type !== "KarlFischer" && model.moistureRole === "Water") {
      nextModel.moistureRole = "None";
    } else if (model.moistureRole === "LossOnDrying") {
      nextModel.moistureRole = "None";
    }
  } else if (next === "UvSpectrophotometry") {
    nextModel.equationType = "UvSpectrophotometry";
    nextModel.requiresSystemSuitability = false;
    nextModel.moistureRole = "None";
    nextModel.replicateCount = "1";
  } else {
    nextModel.equationType = "None";
    nextModel.requiresSystemSuitability = false;
    nextModel.moistureRole = "None";
  }
  return nextModel;
}

export function withEquationType(model: TestFormModel, next: string): TestFormModel {
  const nextModel: TestFormModel = { ...model, equationType: next };
  if (model.workflowType === "Gravimetric" && next !== "GravimetricLoss" && model.moistureRole === "LossOnDrying") {
    nextModel.moistureRole = "None";
  }
  if (next === "HplcMethodAssay") {
    nextModel.workflowType = "HplcMethodAssay";
    nextModel.requiresSystemSuitability = true;
    nextModel.moistureRole = "None";
    nextModel.perContainer = false;
  } else if (next === "IcpMethodAssay") {
    nextModel.workflowType = "IcpMethodAssay";
    nextModel.requiresSystemSuitability = false;
    nextModel.moistureRole = "None";
    nextModel.perContainer = false;
  } else if (next === "Disintegration") {
    nextModel.workflowType = "Disintegration";
    nextModel.requiresSystemSuitability = false;
    nextModel.moistureRole = "None";
    nextModel.perContainer = false;
  } else if (next === "WeightVariation") {
    nextModel.workflowType = "WeightVariation";
    nextModel.requiresSystemSuitability = false;
    nextModel.moistureRole = "None";
    nextModel.perContainer = false;
  } else if (next === "Dissolution") {
    nextModel.workflowType = "Dissolution";
    nextModel.requiresSystemSuitability = true;
    nextModel.moistureRole = "None";
    nextModel.perContainer = false;
  } else if (next === "UvSpectrophotometry") {
    nextModel.workflowType = "UvSpectrophotometry";
    nextModel.requiresSystemSuitability = false;
    nextModel.moistureRole = "None";
    nextModel.perContainer = false;
  }
  return nextModel;
}

// Reads the numeric fields of one method-configuration group. A blank field is
// reported first (the first one in on-screen order), then a non-numeric one;
// nothing is ever substituted for a missing value.
function readRequiredNumbers(
  group: string,
  fields: ReadonlyArray<readonly [key: MethodGroupKey, label: string, text: string]>
): { error: string; field: MethodGroupKey } | { values: number[] } {
  const blank = fields.find(([, , text]) => text.trim() === "");
  if (blank) return { error: `${group}: "${blank[1]}" is required.`, field: blank[0] };
  const values: number[] = [];
  for (const [key, label, text] of fields) {
    const n = Number(text);
    if (!Number.isFinite(n)) return { error: `${group}: "${label}" must be a number.`, field: key };
    values.push(n);
  }
  return { values };
}

export function validateStep(step: TestWizardStep, model: TestFormModel): Record<string, string> {
  const errors: Record<string, string> = {};

  if (step === "basics") {
    const trimmedCode = model.code.trim();
    const trimmedDisplayName = model.displayName.trim();
    if (!trimmedCode || !trimmedDisplayName) {
      if (!trimmedCode) {
        errors.code = "Both Code and Display Name are required.";
      }
      if (!trimmedDisplayName) {
        errors.displayName = "Both Code and Display Name are required.";
      }
    }
    if (model.sectionId === "") {
      errors.sectionId = "Laboratory section is required.";
    }
    const trimmedCoaOrder = model.coaDisplayOrder.trim();
    if (trimmedCoaOrder !== "" && !/^[1-9]\d{0,3}$/.test(trimmedCoaOrder)) {
      errors.coaDisplayOrder = "CoA order must be a whole number from 1 to 9999, or empty.";
    }
    return errors;
  }

  if (step === "typeSettings") {
    const { workflowType } = model;
    const isFp = model.lab === "fp";
    const isDissolution = workflowType === "Dissolution";
    const isDisintegration = workflowType === "Disintegration";
    const isWeightVariation = workflowType === "WeightVariation";
    const isMeasurement = workflowType === "Measurement";
    const isGravimetric = workflowType === "Gravimetric";
    const isHplcMethodAssay = workflowType === "HplcMethodAssay";
    const isIcpMethodAssay = workflowType === "IcpMethodAssay";
    const isTitration = workflowType === "Titration";
    const isUv = workflowType === "UvSpectrophotometry";

    if (isDissolution) {
      const trimmedAbbr = model.methodAbbreviation.trim().toUpperCase();
      if (!trimmedAbbr) {
        errors.methodAbbreviation = "Method abbreviation is required when system suitability is enabled.";
      } else if (!/^[A-Z0-9-]{1,20}$/.test(trimmedAbbr)) {
        errors.methodAbbreviation = "Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.";
      }

      const read = readRequiredNumbers("Dissolution", [
        ["dissolutionS1Offset", "S1: each unit ≥ Q +", model.dissolutionS1Offset],
        ["dissolutionS2MinOffset", "S2: no unit < Q −", model.dissolutionS2MinOffset],
        ["dissolutionS3MinOffset", "S3: no unit < Q −", model.dissolutionS3MinOffset],
        ["dissolutionS3MaxBelowS2Min", "S3: max units < Q − S2 value", model.dissolutionS3MaxBelowS2Min]
      ]);
      if ("error" in read) {
        errors[read.field] = read.error;
      } else {
        const [s1, s2, s3, maxBelow] = read.values;
        if (s1 < 0 || s2 < 0 || s3 < 0 || maxBelow < 0) {
          const badField: MethodGroupKey =
            s1 < 0
              ? "dissolutionS1Offset"
              : s2 < 0
              ? "dissolutionS2MinOffset"
              : s3 < 0
              ? "dissolutionS3MinOffset"
              : "dissolutionS3MaxBelowS2Min";
          errors[badField] = "Dissolution stage offsets must be greater than or equal to zero.";
        } else if (model.conditionFields.length > 500) {
          errors.conditionFields = "Condition fields cannot exceed 500 characters.";
        }
      }
    }

    if (isDisintegration) {
      const read = readRequiredNumbers("Disintegration", [
        ["disintegrationStage1Units", "Stage 1 Units", model.disintegrationStage1Units],
        ["disintegrationStage2Units", "Stage 2 Units", model.disintegrationStage2Units],
        ["disintegrationMaxStage1Failures", "Max S1 Failures", model.disintegrationMaxStage1Failures],
        ["disintegrationMinPassTotal", "Min Pass Total", model.disintegrationMinPassTotal]
      ]);
      if ("error" in read) {
        errors[read.field] = read.error;
      } else {
        const [s1, s2, maxFail, minPass] = read.values;
        if (s1 < 1 || s2 < 1) {
          errors[s1 < 1 ? "disintegrationStage1Units" : "disintegrationStage2Units"] =
            "Disintegration stage units must be greater than or equal to 1.";
        } else if (maxFail < 0 || maxFail >= s1) {
          errors.disintegrationMaxStage1Failures = `Disintegration maximum Stage 1 failures must be between 0 and ${s1 - 1}.`;
        } else if (minPass < 1 || minPass > s1 + s2) {
          errors.disintegrationMinPassTotal = `Disintegration minimum pass total must be between 1 and ${s1 + s2}.`;
        } else if (model.conditionFields.length > 500) {
          errors.conditionFields = "Condition fields cannot exceed 500 characters.";
        }
      }
    }

    if (isWeightVariation) {
      const read = readRequiredNumbers("Weight variation", [
        ["wvUnitCount", "Unit Count (Stage 1)", model.wvUnitCount],
        ["wvTabletBand1MaxMg", "Band 1 Max (mg)", model.wvTabletBand1MaxMg],
        ["wvTabletBand1Percent", "Band 1 Deviation (%)", model.wvTabletBand1Percent],
        ["wvTabletBand2MaxMg", "Band 2 Max (mg)", model.wvTabletBand2MaxMg],
        ["wvTabletBand2Percent", "Band 2 Deviation (%)", model.wvTabletBand2Percent],
        ["wvTabletBand3Percent", "Band 3 Deviation (%)", model.wvTabletBand3Percent],
        ["wvTabletMaxOutside", "Max Outside Count", model.wvTabletMaxOutside],
        ["wvCapsuleInnerPercent", "Inner Limit (%)", model.wvCapsuleInnerPercent],
        ["wvCapsuleOuterPercent", "Outer Limit (%)", model.wvCapsuleOuterPercent],
        ["wvCapsuleS1MaxOutside", "Stage 1 Max Outside", model.wvCapsuleS1MaxOutside],
        ["wvCapsuleS1MaxForRetest", "Stage 1 Max For Retest", model.wvCapsuleS1MaxForRetest],
        ["wvCapsuleS2ExtraUnits", "Stage 2 Extra Units", model.wvCapsuleS2ExtraUnits],
        ["wvCapsuleS2MaxOutside", "Stage 2 Max Outside", model.wvCapsuleS2MaxOutside]
      ]);
      if ("error" in read) {
        errors[read.field] = read.error;
      } else {
        const [
          uCount,
          b1Mg,
          b1Pct,
          b2Mg,
          b2Pct,
          b3Pct,
          tabMaxOut,
          capInner,
          capOuter,
          capS1Out,
          capS1Retest,
          capS2Extra,
          capS2Out
        ] = read.values;

        if (uCount < 1 || capS2Extra < 1) {
          errors[uCount < 1 ? "wvUnitCount" : "wvCapsuleS2ExtraUnits"] =
            "Weight variation unit count and extra units must be greater than or equal to 1.";
        } else if (tabMaxOut < 0 || capS1Out < 0 || capS2Out < 0) {
          const badField: MethodGroupKey =
            tabMaxOut < 0
              ? "wvTabletMaxOutside"
              : capS1Out < 0
              ? "wvCapsuleS1MaxOutside"
              : "wvCapsuleS2MaxOutside";
          errors[badField] = "Weight variation maximum outside counts must be greater than or equal to 0.";
        } else if (b1Mg <= 0 || b2Mg <= 0) {
          errors[b1Mg <= 0 ? "wvTabletBand1MaxMg" : "wvTabletBand2MaxMg"] =
            "Weight variation tablet band weight limits must be greater than zero.";
        } else if (b1Pct <= 0 || b2Pct <= 0 || b3Pct <= 0 || capInner <= 0 || capOuter <= 0) {
          const badField: MethodGroupKey =
            b1Pct <= 0
              ? "wvTabletBand1Percent"
              : b2Pct <= 0
              ? "wvTabletBand2Percent"
              : b3Pct <= 0
              ? "wvTabletBand3Percent"
              : capInner <= 0
              ? "wvCapsuleInnerPercent"
              : "wvCapsuleOuterPercent";
          errors[badField] = "Weight variation percentages must be greater than zero.";
        } else if (b1Mg >= b2Mg) {
          errors.wvTabletBand1MaxMg = "Weight variation Tablet Band 1 Max Mg must be less than Band 2 Max Mg.";
        } else if (capInner >= capOuter) {
          errors.wvCapsuleInnerPercent =
            "Weight variation capsule inner percentage must be less than outer percentage.";
        } else if (capS1Out >= capS1Retest || capS1Retest > uCount) {
          errors.wvCapsuleS1MaxOutside =
            "Weight variation capsule Stage 1 max outside must be less than Stage 1 max for retest, which must be less than or equal to unit count.";
        } else if (capS2Out >= uCount + capS2Extra) {
          errors.wvCapsuleS2MaxOutside = `Weight variation capsule Stage 2 max outside must be less than total units (${uCount + capS2Extra}).`;
        } else if (model.conditionFields.length > 500) {
          errors.conditionFields = "Condition fields cannot exceed 500 characters.";
        }
      }
    }

    if (isUv) {
      if (!model.uvCalculationMode) {
        errors.uvCalculationMode = "A UV test needs a calculation mode.";
      } else {
        const rep = Number(model.replicateCount);
        if (!rep || rep < 1 || rep > 30) {
          errors.replicateCount = "Replicate count must be between 1 and 30.";
        }
        if (model.uvCalculationMode === "SpecificAbsorbanceAssay") {
          const a1 = Number(model.uvSpecificAbsorbance);
          if (!model.uvSpecificAbsorbance || isNaN(a1) || a1 <= 0) {
            errors.uvSpecificAbsorbance = "This UV mode needs a specific absorbance above 0.";
          }
          const vol = Number(model.uvTotalVolumeMl);
          if (!model.uvTotalVolumeMl || isNaN(vol) || vol <= 0) {
            errors.uvTotalVolumeMl = "This UV mode needs a total volume above 0.";
          }
        } else if (model.uvCalculationMode === "SpecificAbsorbanceValue") {
          const vol = Number(model.uvTotalVolumeMl);
          if (!model.uvTotalVolumeMl || isNaN(vol) || vol <= 0) {
            errors.uvTotalVolumeMl = "This UV mode needs a total volume above 0.";
          }
        } else if (model.uvCalculationMode === "StandardComparison") {
          const vol = Number(model.uvTotalVolumeMl);
          if (!model.uvTotalVolumeMl || isNaN(vol) || vol <= 0) {
            errors.uvTotalVolumeMl = "This UV mode needs a total volume above 0.";
          }
          const stdVol = Number(model.uvStandardVolumeMl);
          if (!model.uvStandardVolumeMl || isNaN(stdVol) || stdVol <= 0) {
            errors.uvStandardVolumeMl = "A UV test against a standard needs a standard volume above 0.";
          }
          if (model.uvStandardEntryId === "") {
            errors.uvStandardEntryId = "The standard material is required for an assay against standard.";
          }
        }
      }
    }

    if (isTitration) {
      const hint = validateTitrationForm(model.titration);
      if (hint) {
        errors.titration = hint;
      }
    }

    if (model.editingId) {
      const reasonCheck = titrationChangeReasonCheck(
        model.editingTest,
        model.titration,
        model.workflowType,
        model.changeReason,
        isFp ? model.physchemArea : null,
        model.moistureRole
      );
      if (reasonCheck.error) {
        errors.changeReason = reasonCheck.error;
      }
    }

    if (isHplcMethodAssay && !model.hplcMethodId) {
      errors.hplcMethodId = "HPLC method is required for HPLC method assay tests.";
    }

    if (isIcpMethodAssay) {
      const icpProblem = validateIcpForm(model.icp);
      if (icpProblem) {
        errors.icp = icpProblem;
      }
    }

    if (isMeasurement) {
      const rep = Number(model.replicateCount);
      if (!rep || rep < 1 || rep > 30) {
        errors.replicateCount = "Replicate count must be between 1 and 30 for Measurement tests.";
      }
      if (!model.evaluationBasis) {
        errors.evaluationBasis = "Evaluation basis is required for Measurement tests.";
      }
    }

    if (isGravimetric) {
      const rep = Number(model.replicateCount);
      if (!rep || rep < 1 || rep > 30) {
        errors.replicateCount = "Replicate count must be between 1 and 30 for Gravimetric tests.";
      }
      if (model.equationType !== "GravimetricLoss" && model.equationType !== "GravimetricResidue") {
        errors.equationType = "Equation type must be GravimetricLoss or GravimetricResidue for Gravimetric tests.";
      }
      if (model.conditionFields.length > 500) {
        errors.conditionFields = "Condition fields cannot exceed 500 characters.";
      }
    }

    return errors;
  }

  return errors;
}

export function toTestPayload(
  model: TestFormModel
): CreateTestDefinitionPayload | UpdateTestDefinitionPayload {
  const isFp = model.lab === "fp";
  const trimmedCode = model.code.trim();
  const trimmedDisplayName = model.displayName.trim();
  const trimmedCoaOrder = model.coaDisplayOrder.trim();
  const coaOrderValue = trimmedCoaOrder === "" ? null : Number(trimmedCoaOrder);
  const chosenSectionId = model.sectionId === "" ? null : Number(model.sectionId);

  const { workflowType, equationType } = model;
  const isDissolution = workflowType === "Dissolution";
  const isDisintegration = workflowType === "Disintegration";
  const isWeightVariation = workflowType === "WeightVariation";
  const isMeasurement = workflowType === "Measurement";
  const isGravimetric = workflowType === "Gravimetric";
  const isQualitative = workflowType === "Qualitative";
  const isHplcMethodAssay = workflowType === "HplcMethodAssay";
  const isIcpMethodAssay = workflowType === "IcpMethodAssay";
  const isTitration = workflowType === "Titration";
  const isUv = workflowType === "UvSpectrophotometry";

  const dissolutionFields: Partial<Pick<CreateTestDefinitionPayload, MethodGroupKey>> = isDissolution
    ? {
        dissolutionS1Offset: Number(model.dissolutionS1Offset),
        dissolutionS2MinOffset: Number(model.dissolutionS2MinOffset),
        dissolutionS3MinOffset: Number(model.dissolutionS3MinOffset),
        dissolutionS3MaxBelowS2Min: Number(model.dissolutionS3MaxBelowS2Min)
      }
    : {};

  const disintegrationFields: Partial<Pick<CreateTestDefinitionPayload, MethodGroupKey>> = isDisintegration
    ? {
        disintegrationStage1Units: Number(model.disintegrationStage1Units),
        disintegrationStage2Units: Number(model.disintegrationStage2Units),
        disintegrationMaxStage1Failures: Number(model.disintegrationMaxStage1Failures),
        disintegrationMinPassTotal: Number(model.disintegrationMinPassTotal)
      }
    : {};

  const weightVariationFields: Partial<Pick<CreateTestDefinitionPayload, MethodGroupKey>> = isWeightVariation
    ? {
        wvUnitCount: Number(model.wvUnitCount),
        wvTabletBand1MaxMg: Number(model.wvTabletBand1MaxMg),
        wvTabletBand1Percent: Number(model.wvTabletBand1Percent),
        wvTabletBand2MaxMg: Number(model.wvTabletBand2MaxMg),
        wvTabletBand2Percent: Number(model.wvTabletBand2Percent),
        wvTabletBand3Percent: Number(model.wvTabletBand3Percent),
        wvTabletMaxOutside: Number(model.wvTabletMaxOutside),
        wvCapsuleInnerPercent: Number(model.wvCapsuleInnerPercent),
        wvCapsuleOuterPercent: Number(model.wvCapsuleOuterPercent),
        wvCapsuleS1MaxOutside: Number(model.wvCapsuleS1MaxOutside),
        wvCapsuleS1MaxForRetest: Number(model.wvCapsuleS1MaxForRetest),
        wvCapsuleS2ExtraUnits: Number(model.wvCapsuleS2ExtraUnits),
        wvCapsuleS2MaxOutside: Number(model.wvCapsuleS2MaxOutside)
      }
    : {};

  // Every key is null unless that workflow type is being saved; the values
  // are the ones validated above, from the user's entry.
  const methodGroupFields: Pick<CreateTestDefinitionPayload, MethodGroupKey> = {
    ...NO_METHOD_GROUP_FIELDS,
    ...dissolutionFields,
    ...disintegrationFields,
    ...weightVariationFields
  };

  const resolvedEquationType = isHplcMethodAssay
    ? "HplcMethodAssay"
    : isIcpMethodAssay
    ? "IcpMethodAssay"
    : isMeasurement
    ? "Measurement"
    : isGravimetric
    ? equationType
    : isQualitative
    ? "Qualitative"
    : isDissolution
    ? "Dissolution"
    : isDisintegration
    ? "Disintegration"
    : isWeightVariation
    ? "WeightVariation"
    : isTitration
    ? "Titration"
    : isUv
    ? "UvSpectrophotometry"
    : "None";

  const resolvedMoistureRole: MoistureRole =
    isGravimetric && equationType === "GravimetricLoss"
      ? model.moistureRole === "LossOnDrying"
        ? "LossOnDrying"
        : "None"
      : isTitration && model.titration.type === "KarlFischer"
      ? model.moistureRole === "Water"
        ? "Water"
        : "None"
      : "None";

  const shared = {
    code: trimmedCode,
    displayName: trimmedDisplayName,
    sectionId: chosenSectionId,
    workflowType,
    equationType: resolvedEquationType,
    requiresSystemSuitability: isDissolution || isHplcMethodAssay ? true : false,
    methodAbbreviation:
      isDissolution || isHplcMethodAssay || isIcpMethodAssay
        ? model.methodAbbreviation.trim().toUpperCase()
        : null,
    replicateCount: isTitration
      ? Number(model.titration.replicateCount)
      : isMeasurement || isGravimetric || isUv
      ? Number(model.replicateCount)
      : null,
    evaluationBasis: isMeasurement ? model.evaluationBasis : null,
    conditionFields:
      isGravimetric || isDissolution || isDisintegration || isWeightVariation
        ? model.conditionFields.trim() || null
        : null,
    usesTare: isGravimetric ? model.usesTare : null,
    ...methodGroupFields,
    ...titrationPayloadFields(model.titration, isTitration),
    physchemArea: isFp ? model.physchemArea ?? defaultAreaFor(model.area ?? "fp") : null,
    perContainer: isQualitative ? model.perContainer : false,
    moistureRole: resolvedMoistureRole,
    hplcMethodId:
      (isHplcMethodAssay || workflowType === "Dissolution") && model.hplcMethodId !== ""
        ? Number(model.hplcMethodId)
        : null,
    ...(isIcpMethodAssay ? icpPayloadFields(model.icp) : {}),
    uvCalculationMode: isUv && model.uvCalculationMode ? model.uvCalculationMode : null,
    uvSpecificAbsorbance:
      isUv && model.uvCalculationMode === "SpecificAbsorbanceAssay" && model.uvSpecificAbsorbance !== ""
        ? Number(model.uvSpecificAbsorbance)
        : null,
    uvTotalVolumeMl:
      isUv && model.uvCalculationMode !== "AbsorbanceLimit" && model.uvTotalVolumeMl !== ""
        ? Number(model.uvTotalVolumeMl)
        : null,
    uvStandardVolumeMl:
      isUv && model.uvCalculationMode === "StandardComparison" && model.uvStandardVolumeMl !== ""
        ? Number(model.uvStandardVolumeMl)
        : null,
    uvStandardEntryId:
      isUv && model.uvCalculationMode === "StandardComparison" && model.uvStandardEntryId !== ""
        ? model.uvStandardEntryId
        : null
  };

  if (model.editingId) {
    const reasonCheck = titrationChangeReasonCheck(
      model.editingTest,
      model.titration,
      model.workflowType,
      model.changeReason,
      isFp ? model.physchemArea : null,
      model.moistureRole
    );
    const updatePayload: UpdateTestDefinitionPayload = {
      // 0 clears a previously set order on the server.
      coaDisplayOrder: coaOrderValue ?? 0,
      ...shared,
      changeReason: reasonCheck.payload
    };
    return updatePayload;
  }

  const createPayload: CreateTestDefinitionPayload = {
    coaDisplayOrder: coaOrderValue,
    ...shared
  };
  return createPayload;
}
