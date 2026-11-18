import type { TFunction } from "i18next";
import { i18n } from "../../../../i18n";
import type {
  LimitType,
  ResultBasis,
  SpecificationDto
} from "../../specifications/services/SpecificationService";
import { planGradeWrites, siblingsOf } from "../../specifications/services/gradeGroups";
import type {
  HplcMethodAnalyteResponse,
  HplcMethodImpurityResponse,
  HplcMethodResponse
} from "../../masterDataSimple/services/HplcMethodService";
import type { PhyschemArea } from "../../masterDataSimple/testMasterArea";
import {
  createEmptySpecFormModel,
  fromSpecification,
  resolveSpecMethodFlags,
  toBasePayload,
  type SpecFormModel
} from "./specFormModel";

export interface TestDefinitionSummary {
  id: number;
  code: string;
  displayName: string;
  workflowType: string;
  equationType?: string;
  sectionId?: number | null;
  hplcMethodId?: number | null;
  icpMode?: "Assay" | "Impurity" | null;
  icpElements?: { id: number; displayOrder: number; symbol: string; targetPpm?: number | null }[] | null;
  physchemArea?: PhyschemArea | null;
  uvCalculationMode?: string | null;
  uvSpecificAbsorbance?: number | null;
  uvTotalVolumeMl?: number | null;
  uvStandardVolumeMl?: number | null;
}

export function getLimitTypeOptions(t: TFunction<"labConfig">): { value: LimitType; label: string }[] {
  return [
    { value: "Range", label: t("specWizard.limitTypeOptions.Range") },
    { value: "NotMoreThan", label: t("specWizard.limitTypeOptions.NotMoreThan") },
    { value: "NotLessThan", label: t("specWizard.limitTypeOptions.NotLessThan") },
    { value: "TargetWithTolerance", label: t("specWizard.limitTypeOptions.TargetWithTolerance") },
    { value: "CountTiered", label: t("specWizard.limitTypeOptions.CountTiered") },
    { value: "Qualitative", label: t("specWizard.limitTypeOptions.Qualitative") },
    { value: "PresenceAbsence", label: t("specWizard.limitTypeOptions.PresenceAbsence") },
    { value: "MultiStage", label: t("specWizard.limitTypeOptions.MultiStage") },
    { value: "DissolutionQ", label: t("specWizard.limitTypeOptions.DissolutionQ") },
    { value: "DisintegrationTime", label: t("specWizard.limitTypeOptions.DisintegrationTime") },
    { value: "WeightVariation", label: t("specWizard.limitTypeOptions.WeightVariation") }
  ];
}

export const LIMIT_TYPE_OPTIONS: { value: LimitType; label: string }[] =
  getLimitTypeOptions(i18n.getFixedT("en", "labConfig"));

export function getToleranceModeOptions(t: TFunction<"labConfig">) {
  return [
    { value: "Absolute", label: t("specWizard.toleranceModeOptions.Absolute") },
    { value: "Percent", label: t("specWizard.toleranceModeOptions.Percent") }
  ] as const;
}

export const TOLERANCE_MODE_OPTIONS = getToleranceModeOptions(i18n.getFixedT("en", "labConfig"));

export const SPEC_DRAFT_FIELDS: (keyof SpecFormModel)[] = [
  "testCode",
  "parameterName",
  "productionStageRole",
  "grades",
  "resultBasis",
  "labelClaim",
  "labelClaimUnit",
  "hplcMethodAnalyteId",
  "hplcMethodImpurityId",
  "relatedSubstanceRow",
  "icpTestElementId",
  "limitType",
  "lowerLimit",
  "upperLimit",
  "lowerInclusive",
  "upperInclusive",
  "target",
  "tolerance",
  "toleranceMode",
  "alertLimit",
  "actionLimit",
  "specLimit",
  "expectedResultText",
  "expectedState",
  "sampleQuantity",
  "sampleQuantityUnit",
  "stages",
  "dosageForm",
  "unit",
  "referenceStandard",
  "dilutionFactor"
];

export interface SpecWizardLookupData {
  assignedTests: ReadonlyArray<{ testCode: string; displayName?: string }>;
  selectableAssignedTests: ReadonlyArray<{ testCode: string; displayName?: string }>;
  isTestDisabled: boolean;
  testDefs: Record<string, TestDefinitionSummary>;
  testDefsFailed: boolean;
  workflowTypeByCode: Record<string, string>;
  hplcMethodById: Record<number, HplcMethodResponse>;
  loadingHplcMethodId: number | null;
  ensureHplcMethod: (methodId: number) => void;
}

export function getDefaultLimitType(workflowType?: string): LimitType {
  if (workflowType === "CountTest") return "CountTiered";
  if (workflowType === "Observation") return "PresenceAbsence";
  if (workflowType === "Dissolution") return "DissolutionQ";
  if (workflowType === "Disintegration") return "DisintegrationTime";
  if (workflowType === "WeightVariation") return "WeightVariation";
  return "Range";
}

export function hplcParameterName(analyteName: string, basis: ResultBasis | ""): string {
  return basis === "MgPerUnit" ? `${analyteName} (amount per unit)` : analyteName;
}

export function formatAssignedTestName(
  assignedTests: ReadonlyArray<{ testCode: string; displayName?: string }>,
  code: string
): string {
  if (!code) return "";
  const match = assignedTests.find((t) => t.testCode === code);
  return match?.displayName && match.displayName !== code
    ? `${match.displayName} (${code})`
    : match?.displayName || code;
}

export function getHplcMethodLists(
  model: SpecFormModel,
  testDefs: Record<string, TestDefinitionSummary>,
  hplcMethodById: Record<number, HplcMethodResponse>
): {
  analytes: HplcMethodAnalyteResponse[];
  impurities: HplcMethodImpurityResponse[];
} {
  const methodId = testDefs[model.testCode]?.hplcMethodId;
  const method = methodId != null ? hplcMethodById[methodId] : undefined;
  return {
    analytes: method?.analytes ?? [],
    impurities: method?.impurities ?? []
  };
}

export function buildTestChangePatch(
  newCode: string,
  assignedTests: ReadonlyArray<{ testCode: string; displayName?: string }>,
  testDefs: Record<string, TestDefinitionSummary>,
  workflowTypeByCode: Record<string, string>,
  hplcMethodById: Record<number, HplcMethodResponse>
): Partial<SpecFormModel> {
  const match = assignedTests.find((t) => t.testCode === newCode);
  const def = testDefs[newCode];
  const wf = def?.workflowType || workflowTypeByCode[newCode];
  const isHplcAssay =
    def?.equationType === "HplcMethodAssay" ||
    wf === "HplcMethodAssay";
  const isIcpAssay =
    def?.equationType === "IcpMethodAssay" ||
    wf === "IcpMethodAssay";
  const isDis = wf === "Dissolution";
  const isDisint = wf === "Disintegration";
  const isWv = wf === "WeightVariation";
  const isTit = wf === "Titration";
  const isUvAssay = wf === "UvSpectrophotometry";

  const base: Partial<SpecFormModel> = {
    testCode: newCode,
    parameterName: match?.displayName || newCode,
    workflowType: wf,
    equationType: def?.equationType,
    uvCalculationMode: def?.uvCalculationMode ?? null,
    icpMode: def?.icpMode ?? null,
    hplcMethodResultMode: undefined,
    defaultHplcAnalyteId: null
  };

  if (isUvAssay) {
    const mode = def?.uvCalculationMode;
    return {
      ...base,
      limitType: mode === "AbsorbanceLimit" ? "Qualitative" : "Range",
      resultBasis:
        mode === "AbsorbanceLimit" || mode === "SpecificAbsorbanceValue" ? "" : "PercentAsIs",
      unit: mode === "AbsorbanceLimit" ? "AU" : mode === "SpecificAbsorbanceValue" ? "" : "%",
      dilutionFactor: "",
      hplcMethodAnalyteId: "",
      icpTestElementId: "",
      relatedSubstanceRow: "",
      labelClaim: "",
      labelClaimUnit: ""
    };
  }

  if (isTit) {
    return {
      ...base,
      limitType: "Range",
      dilutionFactor: "",
      resultBasis: "PercentAsIs",
      unit: "%",
      labelClaim: "",
      labelClaimUnit: ""
    };
  }

  if (isHplcAssay) {
    const cachedMethod = def?.hplcMethodId ? hplcMethodById[def.hplcMethodId] : undefined;
    const mode = cachedMethod?.resultMode;
    const firstAnalyteId = cachedMethod?.analytes?.[0]?.id ?? null;
    return {
      ...base,
      hplcMethodResultMode: mode,
      defaultHplcAnalyteId: firstAnalyteId,
      limitType: mode === "ResidualSolvents" || mode === "RelatedSubstances" ? "NotMoreThan" : "Range",
      hplcMethodAnalyteId: mode === "RelatedSubstances" && firstAnalyteId ? firstAnalyteId : "",
      hplcMethodImpurityId: "",
      relatedSubstanceRow: "",
      dilutionFactor: "",
      resultBasis:
        mode === "ResidualSolvents"
          ? "Ppm"
          : mode === "RelatedSubstances"
          ? "PercentAsIs"
          : "PercentLabelClaim",
      unit: mode === "ResidualSolvents" ? "ppm" : "%",
      labelClaim: "",
      labelClaimUnit: ""
    };
  }

  if (isIcpAssay) {
    const isImpurity = def?.icpMode === "Impurity";
    return {
      ...base,
      limitType: isImpurity ? "NotMoreThan" : "Range",
      icpTestElementId: "",
      hplcMethodAnalyteId: "",
      dilutionFactor: "",
      resultBasis: "",
      unit: isImpurity ? "ppm" : "%",
      labelClaim: "",
      labelClaimUnit: ""
    };
  }

  if (isDis) {
    return { ...base, limitType: "DissolutionQ", dilutionFactor: "" };
  }
  if (isDisint) {
    return { ...base, limitType: "DisintegrationTime", dilutionFactor: "", unit: "min" };
  }
  if (isWv) {
    return { ...base, limitType: "WeightVariation", dilutionFactor: "", unit: "mg", dosageForm: "" };
  }

  const defType = getDefaultLimitType(wf);
  return {
    ...base,
    limitType: defType,
    ...(defType !== "CountTiered" ? { dilutionFactor: "" } : {})
  };
}

export function buildInitialSpecModel(params: {
  itemId: number;
  itemCategory: string;
  editingSpec?: SpecificationDto | null;
  preselectedTestCode?: string | null;
  assignedTests: ReadonlyArray<{ testCode: string; displayName?: string }>;
  selectableAssignedTests: ReadonlyArray<{ testCode: string; displayName?: string }>;
  workflowTypeByCode: Record<string, string>;
  testDefs: Record<string, TestDefinitionSummary>;
  existingSpecs: SpecificationDto[];
}): SpecFormModel {
  const {
    itemId,
    itemCategory,
    editingSpec,
    preselectedTestCode,
    assignedTests,
    selectableAssignedTests,
    workflowTypeByCode,
    testDefs,
    existingSpecs
  } = params;

  if (editingSpec) {
    return fromSpecification(editingSpec, {
      itemId,
      itemCategory,
      workflowType: workflowTypeByCode[editingSpec.testCode],
      testDef: testDefs[editingSpec.testCode],
      existingSpecs
    });
  }

  const initialCode = preselectedTestCode || selectableAssignedTests[0]?.testCode || "";
  const match = assignedTests.find((t) => t.testCode === initialCode);
  const initialParam = match?.displayName || initialCode;
  const def = testDefs[initialCode];
  const wf = def?.workflowType || workflowTypeByCode[initialCode];
  const isInitHplc = def?.equationType === "HplcMethodAssay" || wf === "HplcMethodAssay";
  const isInitIcp = def?.equationType === "IcpMethodAssay" || wf === "IcpMethodAssay";
  const isInitUv = wf === "UvSpectrophotometry";
  const initUvMode = def?.uvCalculationMode;
  const defaultType: LimitType =
    isInitIcp && def?.icpMode === "Impurity"
      ? "NotMoreThan"
      : isInitHplc || isInitIcp
      ? "Range"
      : isInitUv
      ? (initUvMode === "AbsorbanceLimit" ? "Qualitative" : "Range")
      : getDefaultLimitType(wf);

  const unit =
    isInitIcp && def?.icpMode === "Impurity"
      ? "ppm"
      : isInitHplc || isInitIcp
      ? "%"
      : isInitUv
      ? (initUvMode === "AbsorbanceLimit" ? "AU" : initUvMode === "SpecificAbsorbanceValue" ? "" : "%")
      : defaultType === "WeightVariation"
      ? "mg"
      : defaultType === "DisintegrationTime"
      ? "min"
      : "";

  const resultBasis: ResultBasis | "" = isInitIcp
    ? ""
    : isInitHplc
    ? "PercentLabelClaim"
    : isInitUv
    ? (initUvMode === "AbsorbanceLimit" || initUvMode === "SpecificAbsorbanceValue" ? "" : "PercentAsIs")
    : "MgPerKg";

  return createEmptySpecFormModel({
    itemId,
    itemCategory,
    workflowType: wf,
    equationType: def?.equationType,
    uvCalculationMode: def?.uvCalculationMode ?? null,
    icpMode: def?.icpMode ?? null,
    existingSpecs,
    testCode: initialCode,
    parameterName: initialParam,
    limitType: defaultType,
    unit,
    resultBasis,
    stages: [
      { stageNumber: 1, stageLabel: "Stage 1 (S1, n=6)", acceptanceCriteriaText: "" },
      { stageNumber: 2, stageLabel: "Stage 2 (S1+S2, n=12)", acceptanceCriteriaText: "" },
      { stageNumber: 3, stageLabel: "Stage 3 (S1+S2+S3, n=24)", acceptanceCriteriaText: "" }
    ]
  });
}

export function methodStepApplies(model: SpecFormModel): boolean {
  const flags = resolveSpecMethodFlags(model);
  return (
    flags.isHplcMethodAssay ||
    flags.isIcpMethodAssay ||
    flags.isTitration ||
    (flags.isUv && !flags.isUvLimit)
  );
}

export function modelToDisplaySpec(model: SpecFormModel): SpecificationDto {
  const payload = toBasePayload(model);
  return {
    testCode: payload.testCode,
    parameterName: payload.parameterName ?? null,
    displayOrder: payload.displayOrder,
    limitType: payload.limitType,
    referenceStandard: payload.referenceStandard ?? null,
    lowerLimit: payload.lowerLimit ?? null,
    upperLimit: payload.upperLimit ?? null,
    lowerInclusive: payload.lowerInclusive ?? null,
    upperInclusive: payload.upperInclusive ?? null,
    target: payload.target ?? null,
    tolerance: payload.tolerance ?? null,
    toleranceMode: payload.toleranceMode ?? null,
    expectedResultText: payload.expectedResultText ?? null,
    expectedState: payload.expectedState ?? null,
    sampleQuantity: payload.sampleQuantity ?? null,
    sampleQuantityUnit: payload.sampleQuantityUnit ?? null,
    alertLimit: payload.alertLimit ?? null,
    actionLimit: payload.actionLimit ?? null,
    specLimit: payload.specLimit ?? null,
    unit: payload.unit ?? null,
    dilutionFactor: payload.dilutionFactor ?? null,
    stages: payload.stages,
    resultBasis: payload.resultBasis ?? null,
    labelClaim: payload.labelClaim ?? null,
    labelClaimUnit: payload.labelClaimUnit ?? null,
    dosageForm: payload.dosageForm ?? null,
    productionStageRole: payload.productionStageRole ?? null,
    grade: payload.grade ?? null
  };
}

/**
 * Shared grade create/update/remove plan used by both save and reasonRequired.
 */
export function planSpecGradeOperations(
  model: SpecFormModel,
  isEditing: boolean,
  editingSpec?: SpecificationDto | null
) {
  const { supportsGrade } = resolveSpecMethodFlags(model);
  const chosenGrades = supportsGrade ? model.grades : [];
  return planGradeWrites(isEditing && editingSpec ? siblingsOf(editingSpec) : [], chosenGrades);
}

export interface AssignedTestRef {
  testCode: string;
  displayName?: string;
}

/**
 * Next of this laboratory's assigned tests that has no specification yet,
 * in the item's own test order (for "Save and add next").
 */
export function findNextTestWithoutSpec(params: {
  isEditing: boolean;
  currentTestCode: string;
  ownLabAssignedTests: ReadonlyArray<AssignedTestRef>;
  existingSpecs: ReadonlyArray<Pick<SpecificationDto, "testCode">>;
}): AssignedTestRef | null {
  const { isEditing, currentTestCode, ownLabAssignedTests, existingSpecs } = params;
  if (isEditing) return null;
  const specified = new Set(existingSpecs.map((s) => s.testCode));
  return ownLabAssignedTests.find((t) => t.testCode !== currentTestCode && !specified.has(t.testCode)) ?? null;
}
