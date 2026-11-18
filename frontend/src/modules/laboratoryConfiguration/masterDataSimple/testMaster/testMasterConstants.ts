import type { TFunction } from "i18next";
import { i18n } from "../../../../i18n";
import { ProductionStageRole } from "../../../../services/masterDataOptions";

// Microbiology and the Finished Product (chemistry) lab each have their own
// Test Master page: same component, filtered to the lab's section and
// workflow types. FP tests have no workflow steps or media.
export type TestMasterLab = "micro" | "fp";
export const FP_SECTION_CODE = "FP";
export const WORKFLOW_TYPES_BY_LAB: Record<TestMasterLab, string[]> = {
  micro: ["CountTest", "Observation"],
  fp: ["HplcMethodAssay", "IcpMethodAssay", "Measurement", "Gravimetric", "Qualitative", "Dissolution", "Disintegration", "WeightVariation", "Titration", "UvSpectrophotometry"]
};
export const WORKFLOW_TYPE_LABELS: Record<string, string> = {
  CountTest: "Count Test",
  Observation: "Observation",
  HplcMethodAssay: "HPLC method assay",
  IcpMethodAssay: "ICP method assay",
  Measurement: "Measurement",
  Gravimetric: "Gravimetric",
  Qualitative: "Qualitative",
  Dissolution: "Dissolution",
  Disintegration: "Disintegration",
  WeightVariation: "Weight Variation",
  Titration: "Titration (assay)",
  UvSpectrophotometry: "UV Spectrophotometry"
};

export const EQUATION_TYPES = [
  "None",
  "HplcMethodAssay",
  "IcpMethodAssay",
  "SystemSuitability",
  "Measurement",
  "GravimetricLoss",
  "GravimetricResidue",
  "Qualitative",
  "Dissolution",
  "Disintegration",
  "WeightVariation",
  "Titration",
  "UvSpectrophotometry"
];
export const EQUATION_TYPE_LABELS: Record<string, string> = {
  None: "None",
  HplcMethodAssay: "HPLC method assay",
  IcpMethodAssay: "ICP method assay",
  SystemSuitability: "System Suitability",
  Measurement: "Measurement (pH, density…)",
  GravimetricLoss: "Loss on drying / Gravimetric loss",
  GravimetricResidue: "Ash / Gravimetric residue",
  Qualitative: "Qualitative (appearance, ID)",
  Dissolution: "Dissolution (HPLC finish, staged S1-S3)",
  Disintegration: "Disintegration (time per unit, staged)",
  WeightVariation: "Weight Variation (USP <2091>, staged)",
  Titration: "Titration (USP <541>)",
  UvSpectrophotometry: "UV Spectrophotometry"
};
export const STEP_TYPES = ["PlateCount", "BrothEnrichment", "SelectiveBroth", "SelectivePlating", "ConfirmatoryPlating", "BiochemicalTest"];
export const STEP_TYPES_REQUIRING_ORGANISM = ["SelectivePlating", "ConfirmatoryPlating"];
export const STEP_TYPES_WITH_NO_MEDIA = ["BiochemicalTest"];
// Not server-enforced for PlateCount, but Broth/SelectiveBroth/SelectivePlating
// each require exactly one medium (WorkflowTemplateValidator rules 1-2) - this
// caps the editor at one row so the analyst gets that feedback immediately
// instead of only on server rejection.
export const STEP_TYPES_SINGLE_MEDIA = ["BrothEnrichment", "SelectiveBroth", "SelectivePlating"];

export const PHENOTYPIC_TEST_TYPES = ["Gram", "Catalase", "Oxidase", "Coagulase", "Antibiogram", "IdentificationKit"];
export const PHENOTYPIC_TEST_TYPE_LABELS: Record<string, string> = {
  Gram: "Gram Stain", Catalase: "Catalase", Oxidase: "Oxidase", Coagulase: "Coagulase",
  Antibiogram: "Antibiogram", IdentificationKit: "Identification Kit"
};

// Each step medium names a Culture Media product and picks one incubation
// condition of that product. The server copies that condition's temperature and hours onto
// the step medium when it saves (MasterDataController.BuildStepMediaAsync).
export type StepMediaRow = { mediaProductId: number | ""; mediaIncubationConditionId: number | ""; isRequired: boolean; displayOrder: number };

export interface StepFormState {
  stepName?: string;
  isFinalStep: boolean;
  stepType: string;
  targetOrganismId: number | null;
  stepMedia: StepMediaRow[];
  // PlateCount only (backend TestWorkflowStep.RequiresIncubationTransfer /
  // WorkflowTemplateValidator rule 7). Stage 2's own window is a separate
  // TestWorkflowStepIncubationStage row (StageNumber 2), not more columns
  // on this step - see stage2* fields below.
  requiresIncubationTransfer: boolean;
  stage2TempMin?: string | number;
  stage2TempMax?: string | number;
  stage2IncubationMinHours?: string | number;
  stage2IncubationMaxHours?: string | number;
  // Older single-value field, kept for backward compatibility with
  // existing chained-step templates - the Add Step form below now uses
  // phenotypicTestTypes (a bundle of one or more) instead, letting one
  // step cover e.g. Gram Stain + Oxidase + Identification Kit together.
  phenotypicTestType?: string | null;
  phenotypicTestTypes: string[];
}

// A function rather than a shared constant object, so every reset gets its
// own stepMedia array instead of every WorkflowStepsSection instance (one
// per expanded Test Master row) sharing a single mutable reference.
export const defaultStepForm = (): StepFormState => ({
  isFinalStep: false, stepType: "PlateCount", targetOrganismId: null, stepMedia: [], requiresIncubationTransfer: false,
  phenotypicTestType: null, phenotypicTestTypes: []
});

// Mirrors WorkflowTemplateValidator's six structural rules (backend
// MicroLIMS.Application/Services/WorkflowTemplateValidator.cs) so the admin
// gets immediate feedback - the server is still authoritative and any
// rejection it returns is surfaced as-is.
export function validateStepForm(
  form: StepFormState,
  t: TFunction = i18n.getFixedT("en", "masterData")
): string | null {
  const isBroth = form.stepType === "BrothEnrichment" || form.stepType === "SelectiveBroth";
  const isSelectivePlating = form.stepType === "SelectivePlating";
  const isConfirmatory = form.stepType === "ConfirmatoryPlating";
  const isBiochemical = form.stepType === "BiochemicalTest";

  if (isBroth && (form.stepMedia.length !== 1 || !form.stepMedia[0].isRequired))
    return t("testMaster.constants.validation.brothMediaRequired");
  if (isBroth && form.targetOrganismId)
    return t("testMaster.constants.validation.brothNoOrganism");
  if (isSelectivePlating && (form.stepMedia.length !== 1 || !form.stepMedia[0].isRequired))
    return t("testMaster.constants.validation.selectivePlatingMediaRequired");
  if (isSelectivePlating && !form.targetOrganismId)
    return t("testMaster.constants.validation.selectivePlatingOrganismRequired");
  if (isConfirmatory && form.stepMedia.length === 0)
    return t("testMaster.constants.validation.confirmatoryMediaRequired");
  if (isConfirmatory && !form.targetOrganismId)
    return t("testMaster.constants.validation.confirmatoryOrganismRequired");
  if (isBiochemical && form.stepMedia.length > 0)
    return t("testMaster.constants.validation.biochemicalNoMedia");
  if (isBiochemical && form.targetOrganismId)
    return t("testMaster.constants.validation.biochemicalNoOrganism");
  if (isBiochemical && !form.phenotypicTestType && form.phenotypicTestTypes.length === 0)
    return t("testMaster.constants.validation.biochemicalPhenotypicRequired");
  if (isBiochemical && new Set(form.phenotypicTestTypes).size !== form.phenotypicTestTypes.length)
    return t("testMaster.constants.validation.biochemicalDuplicatePhenotypic");
  if (!isBiochemical && (form.phenotypicTestType || form.phenotypicTestTypes.length > 0))
    return t("testMaster.constants.validation.nonBiochemicalNoPhenotypic");
  // Mirrors WorkflowTemplateValidator rule 8 - every non-biochemical step
  // needs at least one medium. Rules above already cover Broth/Selective/
  // Confirmatory; this is PlateCount's only media check.
  if (form.stepType === "PlateCount" && form.stepMedia.length === 0)
    return t("testMaster.constants.validation.plateCountMediaRequired");
  for (const m of form.stepMedia) {
    if (m.mediaProductId === "") return t("testMaster.constants.validation.mediaProductRequired");
    if (m.mediaIncubationConditionId === "") return t("testMaster.constants.validation.mediaConditionRequired");
  }
  const productIds = form.stepMedia.map((m) => m.mediaProductId);
  if (new Set(productIds).size !== productIds.length) return t("testMaster.constants.validation.duplicateMedia");

  // Mirrors WorkflowTemplateValidator rule 7 - the server is still
  // authoritative, this only spares a round trip for the common case.
  if (form.stepType === "PlateCount" && form.requiresIncubationTransfer) {
    const { stage2TempMin, stage2TempMax, stage2IncubationMinHours, stage2IncubationMaxHours } = form;
    if (stage2TempMin === undefined || stage2TempMin === "" || stage2TempMax === undefined || stage2TempMax === "" ||
        stage2IncubationMinHours === undefined || stage2IncubationMinHours === "" ||
        stage2IncubationMaxHours === undefined || stage2IncubationMaxHours === "")
      return t("testMaster.constants.validation.stage2Incomplete");
    if (Number(stage2TempMin) >= Number(stage2TempMax))
      return t("testMaster.constants.validation.stage2TempOrder");
    if (Number(stage2IncubationMinHours) <= 0 || Number(stage2IncubationMaxHours) < Number(stage2IncubationMinHours))
      return t("testMaster.constants.validation.stage2HoursInvalid");
  }
  return null;
}

// True for rows the pathogen-workflow migration could not backfill
// (TargetOrganismId/StepMedia are per-step and the migration had no source
// data for them) - flags templates that will fail validation the first time
// an analyst tries to run them, so an admin can find them here instead.
// The step's own incubationMinHours/MaxHours/temperatureMin/Max are no
// longer authoritative (see TestWorkflowEngine.cs) - a step with more than
// one permitted medium can have genuinely different windows per medium
// (e.g. Confirmatory Plating's XLD vs TSI), so stage 1's display is built
// from the picked media's own ranges instead, joined when they differ.
export interface WorkflowStepMediaItem {
  mediaProductId: number;
  mediaProductName?: string;
  mediaIncubationConditionId?: number | string | null;
  isRequired?: boolean;
  displayOrder?: number;
  incubationMinHours?: number;
  incubationMaxHours?: number;
  tempMin?: number;
  tempMax?: number;
  [key: string]: unknown;
}

export function stage1Ranges(stepMedia: WorkflowStepMediaItem[] | undefined, min: string, max: string): string {
  if (!stepMedia?.length) return "—";
  const distinct = Array.from(new Set(stepMedia.map((m) => `${String(m[min])}-${String(m[max])}`)));
  return distinct.join("; ");
}

export interface StepCheckItem {
  stepType?: string;
  targetOrganismId?: number | null;
  stepMedia?: unknown[];
  phenotypicTestType?: string | null;
  phenotypicTestTypes?: string[];
}

export function stepNeedsConfiguration(s: StepCheckItem): boolean {
  if (s.stepType && STEP_TYPES_REQUIRING_ORGANISM.includes(s.stepType) && !s.targetOrganismId) return true;
  if (s.stepType !== "BiochemicalTest" && (s.stepMedia?.length ?? 0) === 0) return true;
  if (s.stepType === "BiochemicalTest" && !s.phenotypicTestType && (s.phenotypicTestTypes?.length ?? 0) === 0) return true;
  return false;
}


export const ALL_STAGE_ROLES: ProductionStageRole[] = [
  "Bulk",
  "InProcess",
  "Finished",
  "Stability",
  "Other"
];
