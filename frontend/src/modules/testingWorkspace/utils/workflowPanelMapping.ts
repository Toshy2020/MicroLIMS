import type { TFunction } from "i18next";
import { i18n } from "../../../i18n";

// Canonical pure mapping from Test Master workflowType and step configuration
// to the active result-entry panel and the inputs an analyst enters there.
// Shared by TestWorkflowDialog, PathogenStepDialog, and Test Master AnalystPreview.

export type WorkflowPanelKind =
  | "Outsourced"
  | "LocationResults"
  | "Measurement"
  | "Gravimetric"
  | "Titration"
  | "UvSpectrophotometry"
  | "Qualitative"
  | "Dissolution"
  | "Disintegration"
  | "WeightVariation"
  | "HplcMethodAssay"
  | "IcpMethodAssay"
  | "Count"
  | "PathogenChain";

export type PathogenStepPanelKind =
  | "Broth"
  | "SelectivePlating"
  | "ConfirmatoryPlating"
  | "BiochemicalTest"
  | "Unsupported";

const PHENOTYPIC_LABEL_KEYS: Record<string, string> = {
  Gram: "workflowMapping.phenotypic.Gram",
  Catalase: "workflowMapping.phenotypic.Catalase",
  Oxidase: "workflowMapping.phenotypic.Oxidase",
  Coagulase: "workflowMapping.phenotypic.Coagulase",
  Antibiogram: "workflowMapping.phenotypic.Antibiogram",
  IdentificationKit: "workflowMapping.phenotypic.IdentificationKit"
};

export interface WorkflowPanelResolutionOptions {
  outsourcedLab?: string | null;
  locationEntry?: boolean;
  category?: string;
}

const DIRECT_WORKFLOW_PANELS = new Set<WorkflowPanelKind>([
  "Measurement",
  "Gravimetric",
  "Titration",
  "UvSpectrophotometry",
  "Qualitative",
  "Dissolution",
  "Disintegration",
  "WeightVariation",
  "HplcMethodAssay",
  "IcpMethodAssay"
]);

export function resolveWorkflowPanelKind(
  workflowType: string,
  options: WorkflowPanelResolutionOptions = {}
): WorkflowPanelKind {
  if (options.outsourcedLab) return "Outsourced";
  if (options.locationEntry) return "LocationResults";
  if (DIRECT_WORKFLOW_PANELS.has(workflowType as WorkflowPanelKind)) {
    return workflowType as WorkflowPanelKind;
  }
  const isEmOrAfterCleaning =
    options.category === "EnvironmentalMonitoring" || options.category === "AfterCleaning";
  if (workflowType === "CountTest" || isEmOrAfterCleaning) {
    return "Count";
  }
  return "PathogenChain";
}

export function resolvePathogenStepPanelKind(stepType: string): PathogenStepPanelKind {
  if (stepType === "BrothEnrichment" || stepType === "SelectiveBroth") return "Broth";
  if (stepType === "SelectivePlating") return "SelectivePlating";
  if (stepType === "ConfirmatoryPlating") return "ConfirmatoryPlating";
  if (stepType === "BiochemicalTest") return "BiochemicalTest";
  return "Unsupported";
}

export interface StepInputDescriptorOptions {
  requiresIncubationTransfer?: boolean;
  phenotypicTestType?: string | null;
  phenotypicTestTypes?: string[];
}

export function describeStepAnalystEnters(
  stepType: string,
  opts: StepInputDescriptorOptions = {},
  t?: TFunction
): string[] {
  const translate = t ?? i18n.getFixedT("en", "testingWorkspace");
  const tr = (key: string, values?: Record<string, unknown>) =>
    translate(key, { ns: "testingWorkspace", ...values });

  if (stepType === "PlateCount") {
    return [
      tr("workflowMapping.mediaLot"),
      tr("workflowMapping.incubator"),
      ...(opts.requiresIncubationTransfer ? [tr("workflowMapping.stage2Incubator")] : []),
      tr("workflowMapping.dilutionFactor"),
      tr("workflowMapping.plateCounts")
    ];
  }

  switch (resolvePathogenStepPanelKind(stepType)) {
    case "Broth":
      return [tr("workflowMapping.mediaLot"), tr("workflowMapping.incubator")];
    case "SelectivePlating":
      return [
        tr("workflowMapping.mediaLot"),
        tr("workflowMapping.incubator"),
        tr("workflowMapping.growthObservation"),
        tr("workflowMapping.appearanceNoteOptional")
      ];
    case "ConfirmatoryPlating":
      return [
        tr("workflowMapping.permittedMediaAndIncubator"),
        tr("workflowMapping.growthObservationPerPlate")
      ];
    case "BiochemicalTest": {
      const tests =
        opts.phenotypicTestTypes && opts.phenotypicTestTypes.length > 0
          ? opts.phenotypicTestTypes
          : opts.phenotypicTestType
            ? [opts.phenotypicTestType]
            : [];
      const testLabels = tests.map((pt) =>
        PHENOTYPIC_LABEL_KEYS[pt] ? tr(PHENOTYPIC_LABEL_KEYS[pt]) : pt
      );
      return [
        testLabels.length > 0
          ? tr("workflowMapping.phenotypicTests", { tests: testLabels.join(", ") })
          : tr("workflowMapping.phenotypicTestResult"),
        tr("workflowMapping.targetOrganismDecision")
      ];
    }
    case "Unsupported":
      return [];
  }
}

export interface WorkflowAnalystInputOptions {
  workflowType: string;
  replicateCount?: number | null;
  usesTare?: boolean;
  perContainer?: boolean;
  conditionFields?: string | null;
  titrationBlankRequired?: boolean;
  titrationMode?: string;
  titrationEndpoint?: string;
  uvCalculationMode?: string;
  icpMode?: string;
  disintegrationStage1Units?: number | null;
  disintegrationStage2Units?: number | null;
  wvUnitCount?: number | null;
  stepType?: string;
  stepOptions?: StepInputDescriptorOptions;
}

function parseConditionFields(conditionFields?: string | null): string[] {
  if (!conditionFields) return [];
  return conditionFields
    .split(",")
    .map((s) => s.trim())
    .filter(Boolean);
}

export function describeWorkflowAnalystEnters(input: WorkflowAnalystInputOptions, t?: TFunction): string[] {
  const translate = t ?? i18n.getFixedT("en", "testingWorkspace");
  const tr = (key: string, values?: Record<string, unknown>) =>
    translate(key, { ns: "testingWorkspace", ...values });
  const panelKind = resolveWorkflowPanelKind(input.workflowType);
  const conditions = parseConditionFields(input.conditionFields);

  switch (panelKind) {
    case "Measurement": {
      const reps = input.replicateCount && input.replicateCount > 0 ? input.replicateCount : 1;
      const replicates = tr("workflowMapping.replicates", { count: reps });
      return [tr("workflowMapping.equipment"), tr("workflowMapping.measurementReadings", { replicates })];
    }
    case "Gravimetric": {
      const reps = input.replicateCount && input.replicateCount > 0 ? input.replicateCount : 1;
      const replicates = tr("workflowMapping.replicates", { count: reps });
      return [
        tr("workflowMapping.equipment"),
        ...(input.usesTare ? [tr("workflowMapping.containerTareWeight", { count: reps })] : []),
        tr("workflowMapping.initialFinalWeights", { replicates }),
        ...conditions
      ];
    }
    case "Titration": {
      const reps = input.replicateCount && input.replicateCount > 0 ? input.replicateCount : 3;
      const replicates = tr("workflowMapping.replicates", { count: reps });
      return [
        tr("workflowMapping.titrantLot"),
        ...(input.titrationEndpoint === "Visual" ? [tr("workflowMapping.indicatorLot")] : []),
        ...(input.titrationBlankRequired ? [tr("workflowMapping.blankTitrationVolume")] : []),
        ...(input.titrationMode === "Residual" ? [tr("workflowMapping.excessTitrantLotVolume")] : []),
        tr("workflowMapping.sampleWeightsTitrantVolumes", { replicates })
      ];
    }
    case "UvSpectrophotometry": {
      const reps = input.replicateCount && input.replicateCount > 0 ? input.replicateCount : 1;
      const replicates = tr("workflowMapping.replicates", { count: reps });
      return [
        tr("workflowMapping.equipment"),
        ...(input.uvCalculationMode === "StandardComparison"
          ? [tr("workflowMapping.refStandardLotWeightAbsorbance")]
          : []),
        tr("workflowMapping.sampleWeightAbsorbance", { replicates })
      ];
    }
    case "Qualitative":
      return [
        input.perContainer ? tr("workflowMapping.perContainerCompliance") : tr("workflowMapping.complianceObservation"),
        tr("workflowMapping.observationRemarkOptional")
      ];
    case "Dissolution":
      return [tr("workflowMapping.equipment"), tr("workflowMapping.stagedDissolutionHplc"), ...conditions];
    case "Disintegration": {
      const s1 = input.disintegrationStage1Units ?? 6;
      const s2 = input.disintegrationStage2Units ?? 12;
      return [tr("workflowMapping.equipment"), tr("workflowMapping.disintegrationTimePerUnit", { s1, s2 }), ...conditions];
    }
    case "WeightVariation": {
      const units = input.wvUnitCount ?? 20;
      return [tr("workflowMapping.equipment"), tr("workflowMapping.individualUnitWeights", { units }), ...conditions];
    }
    case "HplcMethodAssay":
      return [tr("workflowMapping.hplcBatchInjections")];
    case "IcpMethodAssay":
      return [
        input.icpMode === "Impurity"
          ? tr("workflowMapping.icpImpurity")
          : tr("workflowMapping.icpTarget")
      ];
    case "Count":
      return describeStepAnalystEnters(input.stepType ?? "PlateCount", input.stepOptions, translate);
    case "PathogenChain":
      return input.stepType
        ? describeStepAnalystEnters(input.stepType, input.stepOptions, translate)
        : [tr("workflowMapping.configuredMicroWorkflowSteps")];
    case "Outsourced":
    case "LocationResults":
      return [];
  }
}
