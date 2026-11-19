import type { TFunction } from "i18next";
import { i18n } from "../../../../i18n";
import type {
  CreateSpecificationPayload,
  DosageForm,
  ExpectedPresence,
  LimitType,
  ProductionStageRole,
  RelatedSubstanceRow,
  ResultBasis,
  SpecificationDto,
  ToleranceMode,
  UpdateSpecificationPayload
} from "../../specifications/services/SpecificationService";
import { gradesOf } from "../../specifications/services/gradeGroups";
import type { HplcResultMode } from "../../masterDataSimple/services/HplcMethodService";
import {
  titrationBasisNeedsLabelClaim,
  validateTitrationSpecBasis
} from "../components/TitrationSpecBasisFields";

export type SpecWizardStep = "test" | "method" | "limit" | "unit";

export const SPEC_WIZARD_STEP_ORDER: readonly SpecWizardStep[] = [
  "test",
  "method",
  "limit",
  "unit"
];

export interface SpecStageDraft {
  stageNumber: number;
  stageLabel: string;
  acceptanceCriteriaText: string;
}

export interface SpecTestSummary {
  workflowType?: string;
  equationType?: string;
  icpMode?: "Assay" | "Impurity" | null;
  uvCalculationMode?: string | null;
}

export interface SpecModelContext {
  itemId?: number;
  itemCategory?: string;
  workflowType?: string;
  testDef?: SpecTestSummary;
  hplcMethodResultMode?: HplcResultMode;
  defaultHplcAnalyteId?: number | null;
  existingSpecs?: ReadonlyArray<Pick<SpecificationDto, "id" | "testCode" | "limitType" | "displayOrder">>;
}

/**
 * Full form state + method/item context for a specification parameter.
 * Mirrors every state variable of SpecificationParameterDialog so validation
 * and payload shaping live in one place.
 */
export interface SpecFormModel {
  // Identity & test/item context
  itemId?: number;
  editingId?: number | null;
  displayOrder?: number;
  itemCategory?: string;
  workflowType?: string;
  equationType?: string;
  uvCalculationMode?: string | null;
  hplcMethodResultMode?: HplcResultMode;
  icpMode?: "Assay" | "Impurity" | null;
  defaultHplcAnalyteId?: number | null;
  editingHasRelatedSubstanceRow?: boolean;
  editingResultBasis?: string | null;
  existingSpecs?: ReadonlyArray<Pick<SpecificationDto, "id" | "testCode" | "limitType" | "displayOrder">>;

  // Test & parameter
  testCode: string;
  parameterName: string;
  productionStageRole: ProductionStageRole | "";
  grades: string[];

  // Method / basis
  resultBasis: ResultBasis | "";
  labelClaim: string;
  labelClaimUnit: string;
  hplcMethodAnalyteId: number | "";
  hplcMethodImpurityId: number | "";
  relatedSubstanceRow: RelatedSubstanceRow | "";
  icpTestElementId: number | "";

  // Limit type & fields
  limitType: LimitType;
  lowerLimit: string;
  upperLimit: string;
  lowerInclusive: boolean;
  upperInclusive: boolean;
  target: string;
  tolerance: string;
  toleranceMode: ToleranceMode;
  alertLimit: string;
  actionLimit: string;
  specLimit: string;
  expectedResultText: string;
  expectedState: ExpectedPresence;
  sampleQuantity: string;
  sampleQuantityUnit: string;
  stages: SpecStageDraft[];
  dosageForm: DosageForm | "";

  // Unit, reference standard & dilution
  unit: string;
  referenceStandard: string;
  dilutionFactor: string;
}

export interface SpecMethodFlags {
  isHplcMethodAssay: boolean;
  isRelatedSubstances: boolean;
  isResidualSolvents: boolean;
  isIcpMethodAssay: boolean;
  isIcpElementalImpurities: boolean;
  isTitration: boolean;
  isUv: boolean;
  isUvLimit: boolean;
  supportsProductionStage: boolean;
  supportsGrade: boolean;
}

export function formatTrimmedDecimal(val: number | string | null | undefined): string {
  if (val == null || val === "") return "";
  const n = typeof val === "number" ? val : Number(val);
  if (Number.isNaN(n)) return String(val);
  return n.toString();
}

export function createEmptySpecFormModel(overrides: Partial<SpecFormModel> = {}): SpecFormModel {
  return {
    testCode: "",
    parameterName: "",
    productionStageRole: "",
    grades: [],
    resultBasis: "MgPerKg",
    labelClaim: "",
    labelClaimUnit: "",
    hplcMethodAnalyteId: "",
    hplcMethodImpurityId: "",
    relatedSubstanceRow: "",
    icpTestElementId: "",
    limitType: "Range",
    lowerLimit: "",
    upperLimit: "",
    lowerInclusive: true,
    upperInclusive: true,
    target: "",
    tolerance: "",
    toleranceMode: "Absolute",
    alertLimit: "",
    actionLimit: "",
    specLimit: "",
    expectedResultText: "",
    expectedState: "Absence",
    sampleQuantity: "",
    sampleQuantityUnit: "",
    stages: [],
    dosageForm: "",
    unit: "",
    referenceStandard: "",
    dilutionFactor: "",
    ...overrides
  };
}

export function resolveSpecMethodFlags(model: SpecFormModel): SpecMethodFlags {
  const isHplcMethodAssay =
    model.equationType === "HplcMethodAssay" ||
    model.workflowType === "HplcMethodAssay";
  const isRelatedSubstances =
    isHplcMethodAssay &&
    (model.hplcMethodResultMode === "RelatedSubstances" || Boolean(model.editingHasRelatedSubstanceRow));
  const isResidualSolvents =
    isHplcMethodAssay &&
    (model.hplcMethodResultMode === "ResidualSolvents" || model.editingResultBasis === "Ppm");
  const isIcpMethodAssay =
    model.equationType === "IcpMethodAssay" ||
    model.workflowType === "IcpMethodAssay";
  const isIcpElementalImpurities =
    isIcpMethodAssay && model.icpMode === "Impurity";
  const isTitration = model.workflowType === "Titration";
  const isUv = model.workflowType === "UvSpectrophotometry";
  const isUvLimit = isUv && model.uvCalculationMode === "AbsorbanceLimit";
  const supportsProductionStage = model.itemCategory === "FinishedProduct";
  const supportsGrade = model.itemCategory === "RawMaterial";

  return {
    isHplcMethodAssay,
    isRelatedSubstances,
    isResidualSolvents,
    isIcpMethodAssay,
    isIcpElementalImpurities,
    isTitration,
    isUv,
    isUvLimit,
    supportsProductionStage,
    supportsGrade
  };
}

export function fromSpecification(
  spec: SpecificationDto,
  context: SpecModelContext = {}
): SpecFormModel {
  const editDef = context.testDef;
  const workflowType = editDef?.workflowType || context.workflowType;
  const equationType = editDef?.equationType;
  const isEditHplcAssay =
    equationType === "HplcMethodAssay" ||
    workflowType === "HplcMethodAssay";
  const isEditIcpAssay =
    equationType === "IcpMethodAssay" ||
    workflowType === "IcpMethodAssay";
  const isEditUv = workflowType === "UvSpectrophotometry";
  const editUvMode = editDef?.uvCalculationMode;
  const editBasisFallback: ResultBasis | "" = isEditUv
    ? (editUvMode === "AbsorbanceLimit" || editUvMode === "SpecificAbsorbanceValue" ? "" : "PercentAsIs")
    : isEditIcpAssay
    ? ""
    : (isEditHplcAssay ? "PercentLabelClaim" : "MgPerKg");

  const limitType = (spec.limitType as LimitType) || "CountTiered";
  const unit = limitType === "WeightVariation" ? "mg" : (spec.unit ?? "");

  return {
    itemId: context.itemId ?? spec.itemId,
    editingId: spec.id ?? null,
    displayOrder: spec.displayOrder,
    itemCategory: context.itemCategory,
    workflowType,
    equationType,
    uvCalculationMode: editDef?.uvCalculationMode ?? null,
    hplcMethodResultMode: context.hplcMethodResultMode,
    icpMode: editDef?.icpMode ?? null,
    defaultHplcAnalyteId: context.defaultHplcAnalyteId ?? null,
    editingHasRelatedSubstanceRow: Boolean(spec.relatedSubstanceRow),
    editingResultBasis: spec.resultBasis ?? null,
    existingSpecs: context.existingSpecs,
    testCode: spec.testCode,
    parameterName: spec.parameterName ?? "",
    limitType,
    referenceStandard: spec.referenceStandard ?? "",
    unit,
    dilutionFactor: spec.dilutionFactor != null ? String(spec.dilutionFactor) : "",
    dosageForm: (spec.dosageForm as DosageForm) || "",
    productionStageRole: (spec.productionStageRole as ProductionStageRole) || "",
    grades: gradesOf(spec),
    hplcMethodAnalyteId: spec.hplcMethodAnalyteId ?? "",
    hplcMethodImpurityId: spec.hplcMethodImpurityId ?? "",
    relatedSubstanceRow: (spec.relatedSubstanceRow as RelatedSubstanceRow) || "",
    icpTestElementId: spec.icpTestElementId ?? "",
    resultBasis:
      spec.resultBasis === null
        ? ""
        : (spec.resultBasis as ResultBasis) || editBasisFallback,
    labelClaim: formatTrimmedDecimal(spec.labelClaim),
    labelClaimUnit: spec.labelClaimUnit ?? "",
    lowerLimit: formatTrimmedDecimal(spec.lowerLimit),
    upperLimit: formatTrimmedDecimal(spec.upperLimit),
    lowerInclusive: spec.lowerInclusive ?? true,
    upperInclusive: spec.upperInclusive ?? true,
    target: formatTrimmedDecimal(spec.target),
    tolerance: formatTrimmedDecimal(spec.tolerance),
    toleranceMode: (spec.toleranceMode as ToleranceMode) || "Absolute",
    alertLimit: spec.alertLimit ?? "",
    actionLimit: spec.actionLimit ?? "",
    specLimit: spec.specLimit ?? "",
    expectedResultText: spec.expectedResultText ?? "",
    expectedState: (spec.expectedState as ExpectedPresence) || "Absence",
    sampleQuantity: formatTrimmedDecimal(spec.sampleQuantity),
    sampleQuantityUnit: spec.sampleQuantityUnit ?? "",
    stages:
      spec.stages && spec.stages.length > 0
        ? spec.stages.map((s) => ({
            stageNumber: s.stageNumber,
            stageLabel: s.stageLabel,
            acceptanceCriteriaText: s.acceptanceCriteriaText
          }))
        : [{ stageNumber: 1, stageLabel: "Stage 1", acceptanceCriteriaText: "" }]
  };
}

export function validateStep(
  step: SpecWizardStep,
  model: SpecFormModel,
  t: TFunction<"labConfig"> = i18n.getFixedT("en", "labConfig")
): Record<string, string> {
  const errors: Record<string, string> = {};
  const flags = resolveSpecMethodFlags(model);

  if (step === "test") {
    if (!model.testCode) {
      errors.testCode = t("specWizard.errors.testRequired");
    }
    return errors;
  }

  if (step === "method") {
    if (flags.isHplcMethodAssay) {
      if (flags.isRelatedSubstances) {
        if (!model.relatedSubstanceRow) {
          errors.relatedSubstanceRow = t("specWizard.errors.relatedSubstanceRow");
        } else if (model.relatedSubstanceRow === "Named" && !model.hplcMethodImpurityId) {
          errors.hplcMethodImpurityId = t("specWizard.errors.impurityRequired");
        }
      } else if (flags.isResidualSolvents) {
        if (!model.hplcMethodAnalyteId) {
          errors.hplcMethodAnalyteId = t("specWizard.errors.solventRequired");
        }
        if (model.resultBasis !== "Ppm") {
          errors.resultBasis = t("specWizard.errors.residualSolventBasis");
        }
      } else {
        if (!model.hplcMethodAnalyteId) {
          errors.hplcMethodAnalyteId = t("specWizard.errors.analyteRequired");
        }
        const isRawMaterial = model.itemCategory === "RawMaterial";
        const validBases: ResultBasis[] = isRawMaterial
          ? ["PercentLabelClaim", "MgPerUnit", "PercentAsIs", "PercentDriedBasis", "PercentAnhydrousBasis"]
          : ["PercentLabelClaim", "MgPerUnit"];
        if (!model.resultBasis || !validBases.includes(model.resultBasis as ResultBasis)) {
          errors.resultBasis = isRawMaterial
            ? t("specWizard.errors.basisRawMaterial")
            : t("specWizard.errors.basisStandard");
        }
        if (model.resultBasis === "MgPerUnit") {
          const lcNum = Number(model.labelClaim);
          if (!model.labelClaim.trim() || isNaN(lcNum) || lcNum <= 0) {
            errors.labelClaim = t("specWizard.errors.labelClaimPositive");
          }
          if (!model.labelClaimUnit.trim()) {
            errors.labelClaimUnit = t("specWizard.errors.labelClaimUnitRequired");
          }
        }
      }
    }

    if (flags.isIcpMethodAssay) {
      if (!model.icpTestElementId) {
        errors.icpTestElementId = t("specWizard.errors.icpElementRequired");
      }
    }

    if (flags.isTitration) {
      const basisError = validateTitrationSpecBasis(model.resultBasis, model.labelClaim, t);
      if (basisError) {
        if (titrationBasisNeedsLabelClaim(model.resultBasis)) {
          errors.labelClaim = basisError;
        } else {
          errors.resultBasis = basisError;
        }
      }
    }

    return errors;
  }

  if (step === "limit") {
    const existingSpecs = model.existingSpecs ?? [];

    if (model.limitType === "DissolutionQ") {
      const qNum = Number(model.lowerLimit);
      if (!model.lowerLimit.trim() || isNaN(qNum) || qNum <= 0 || qNum > 100) {
        errors.lowerLimit = t("specWizard.errors.dissolutionQRange");
      }
      const lcNum = Number(model.labelClaim);
      if (!model.labelClaim.trim() || isNaN(lcNum) || lcNum <= 0) {
        errors.labelClaim = t("specWizard.errors.dissolutionLabelClaimPositive");
      }
      const otherDissolutionSpec = existingSpecs.find(
        (s) => s.testCode === model.testCode && s.id !== model.editingId && s.limitType === "DissolutionQ"
      );
      if (otherDissolutionSpec) {
        errors.limitType = t("specWizard.errors.dissolutionSingleSpec");
      }
    }

    if (model.limitType === "DisintegrationTime") {
      const tNum = Number(model.upperLimit);
      if (!model.upperLimit.trim() || isNaN(tNum) || tNum <= 0) {
        errors.upperLimit = t("specWizard.errors.disintegrationTimePositive");
      }
      const otherDisintegrationSpec = existingSpecs.find(
        (s) => s.testCode === model.testCode && s.id !== model.editingId && s.limitType === "DisintegrationTime"
      );
      if (otherDisintegrationSpec) {
        errors.limitType = t("specWizard.errors.disintegrationSingleSpec");
      }
    }

    if (model.limitType === "WeightVariation") {
      if (!model.dosageForm) {
        errors.dosageForm = t("specWizard.errors.weightVariationDosageForm");
      }
      const otherWvSpec = existingSpecs.find(
        (s) => s.testCode === model.testCode && s.id !== model.editingId && s.limitType === "WeightVariation"
      );
      if (otherWvSpec) {
        errors.limitType = t("specWizard.errors.weightVariationSingleSpec");
      }
    }

    return errors;
  }

  return errors;
}

export function toBasePayload(model: SpecFormModel): UpdateSpecificationPayload {
  const {
    isHplcMethodAssay,
    isRelatedSubstances,
    isResidualSolvents,
    isIcpMethodAssay,
    isTitration,
    isUv,
    isUvLimit,
    supportsProductionStage,
    supportsGrade
  } = resolveSpecMethodFlags(model);

  const testSpecs = (model.existingSpecs ?? []).filter((s) => s.testCode === model.testCode);
  const maxOrder = testSpecs.reduce((max, s) => Math.max(max, s.displayOrder ?? 0), -1);
  const displayOrder = model.displayOrder ?? maxOrder + 1;

  const {
    testCode,
    parameterName,
    limitType,
    referenceStandard,
    unit,
    dosageForm,
    dilutionFactor,
    lowerLimit,
    upperLimit,
    lowerInclusive,
    upperInclusive,
    target,
    tolerance,
    toleranceMode,
    expectedResultText,
    expectedState,
    sampleQuantity,
    sampleQuantityUnit,
    alertLimit,
    actionLimit,
    specLimit,
    stages,
    hplcMethodAnalyteId,
    relatedSubstanceRow,
    hplcMethodImpurityId,
    icpTestElementId,
    resultBasis,
    labelClaim,
    labelClaimUnit,
    productionStageRole
  } = model;

  return {
    testCode,
    parameterName: parameterName.trim() || undefined,
    displayOrder,
    limitType: isRelatedSubstances ? "NotMoreThan" : limitType,
    referenceStandard: referenceStandard.trim() || null,
    unit:
      isRelatedSubstances
        ? "%"
        : isResidualSolvents
        ? "ppm"
        : limitType === "WeightVariation"
        ? "mg"
        : limitType === "DisintegrationTime"
          ? "min"
          : (unit.trim() || null),
    dosageForm: limitType === "WeightVariation" ? (dosageForm || null) : null,
    dilutionFactor:
      limitType === "CountTiered" && dilutionFactor.trim() !== ""
        ? Number(dilutionFactor)
        : null,
    lowerLimit:
      limitType === "DissolutionQ"
        ? (lowerLimit.trim() !== "" ? Number(lowerLimit) : null)
        : (limitType === "Range" || limitType === "NotLessThan"
          ? (lowerLimit.trim() !== "" ? Number(lowerLimit) : null)
          : null),
    upperLimit:
      limitType === "DisintegrationTime"
        ? (upperLimit.trim() !== "" ? Number(upperLimit) : null)
        : (limitType === "Range" || limitType === "NotMoreThan"
          ? (upperLimit.trim() !== "" ? Number(upperLimit) : null)
          : null),
    lowerInclusive: limitType === "Range" ? lowerInclusive : undefined,
    upperInclusive: limitType === "Range" ? upperInclusive : undefined,
    target:
      limitType === "TargetWithTolerance" && target.trim() !== ""
        ? Number(target)
        : null,
    tolerance:
      limitType === "TargetWithTolerance" && tolerance.trim() !== ""
        ? Number(tolerance)
        : null,
    toleranceMode: limitType === "TargetWithTolerance" ? toleranceMode : null,
    expectedResultText:
      limitType === "Qualitative" ? expectedResultText.trim() : null,
    expectedState: limitType === "PresenceAbsence" ? expectedState : null,
    sampleQuantity:
      limitType === "PresenceAbsence" && sampleQuantity.trim() !== ""
        ? Number(sampleQuantity)
        : null,
    sampleQuantityUnit:
      limitType === "PresenceAbsence" ? sampleQuantityUnit.trim() || null : null,
    alertLimit: limitType === "CountTiered" ? alertLimit.trim() || "" : null,
    actionLimit: limitType === "CountTiered" ? actionLimit.trim() || "" : null,
    specLimit:
      limitType === "WeightVariation"
        ? (dosageForm === "Tablet"
          ? "USP <2091>: tablets, limit by average weight"
          : "USP <2091>: net content 90-110 % of average")
        : (limitType === "DisintegrationTime"
          ? (upperLimit.trim() !== "" ? `NMT ${upperLimit.trim()} min` : null)
          : (limitType === "DissolutionQ"
            ? (lowerLimit.trim() !== "" ? `Q = ${lowerLimit.trim()} %` : null)
            : (limitType === "CountTiered" ? specLimit.trim() : null))),
    stages:
      limitType === "MultiStage"
        ? stages.map((s, idx) => ({
            stageNumber: s.stageNumber || idx + 1,
            stageLabel: s.stageLabel.trim(),
            acceptanceCriteriaText: s.acceptanceCriteriaText.trim()
          }))
        : undefined,
    hplcMethodAnalyteId: isHplcMethodAssay
      ? isRelatedSubstances
        ? (model.defaultHplcAnalyteId ?? (hplcMethodAnalyteId !== "" ? Number(hplcMethodAnalyteId) : null))
        : (hplcMethodAnalyteId !== "" ? Number(hplcMethodAnalyteId) : null)
      : null,
    relatedSubstanceRow:
      isRelatedSubstances && relatedSubstanceRow ? (relatedSubstanceRow as RelatedSubstanceRow) : null,
    hplcMethodImpurityId:
      isRelatedSubstances && relatedSubstanceRow === "Named" && hplcMethodImpurityId !== ""
        ? Number(hplcMethodImpurityId)
        : null,
    icpTestElementId: isIcpMethodAssay && icpTestElementId !== "" ? Number(icpTestElementId) : null,
    resultBasis: isRelatedSubstances
      ? "PercentAsIs"
      : isIcpMethodAssay
      ? null
      : isUv
      ? (isUvLimit ? null : resultBasis ? (resultBasis as ResultBasis) : null)
      : (isHplcMethodAssay || isTitration) && resultBasis
      ? (resultBasis as ResultBasis)
      : null,
    labelClaim:
      limitType === "WeightVariation" || limitType === "DisintegrationTime"
        ? null
        : (limitType === "DissolutionQ"
          ? (labelClaim.trim() !== "" ? Number(labelClaim) : null)
          : isIcpMethodAssay
          ? null
          : (((isHplcMethodAssay && resultBasis === "MgPerUnit") ||
              (isTitration && titrationBasisNeedsLabelClaim(resultBasis))) &&
              labelClaim.trim() !== ""
            ? Number(labelClaim)
            : null)),
    labelClaimUnit:
      limitType === "WeightVariation" || limitType === "DisintegrationTime"
        ? null
        : (limitType === "DissolutionQ"
          ? "mg"
          : isIcpMethodAssay
          ? null
          : (isTitration
            ? (titrationBasisNeedsLabelClaim(resultBasis) ? "mg" : null)
            : isHplcMethodAssay && resultBasis === "MgPerUnit"
            ? labelClaimUnit.trim() || null
            : null)),
    productionStageRole: supportsProductionStage && productionStageRole ? productionStageRole : null,
    grade: supportsGrade ? (model.grades[0] ?? null) : null
  };
}

export function toPayload(model: SpecFormModel): CreateSpecificationPayload | UpdateSpecificationPayload {
  const base = toBasePayload(model);
  if (model.editingId == null && model.itemId != null) {
    return { itemId: model.itemId, ...base };
  }
  return base;
}
