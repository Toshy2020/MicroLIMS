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

const PHENOTYPIC_LABELS: Record<string, string> = {
  Gram: "Gram Stain",
  Catalase: "Catalase",
  Oxidase: "Oxidase",
  Coagulase: "Coagulase",
  Antibiogram: "Antibiogram",
  IdentificationKit: "Identification Kit"
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
  opts: StepInputDescriptorOptions = {}
): string[] {
  if (stepType === "PlateCount") {
    return [
      "Media lot",
      "Incubator",
      ...(opts.requiresIncubationTransfer ? ["Stage 2 incubator"] : []),
      "Dilution factor",
      "Plate counts (colonies, TNTC, or Uncountable)"
    ];
  }

  switch (resolvePathogenStepPanelKind(stepType)) {
    case "Broth":
      return ["Media lot", "Incubator"];
    case "SelectivePlating":
      return ["Media lot", "Incubator", "Growth observation", "Appearance note (optional)"];
    case "ConfirmatoryPlating":
      return ["Permitted media lot(s) & incubator(s)", "Growth observation per plate"];
    case "BiochemicalTest": {
      const tests =
        opts.phenotypicTestTypes && opts.phenotypicTestTypes.length > 0
          ? opts.phenotypicTestTypes
          : opts.phenotypicTestType
            ? [opts.phenotypicTestType]
            : [];
      const testLabels = tests.map((t) => PHENOTYPIC_LABELS[t] ?? t);
      return [
        testLabels.length > 0
          ? `Phenotypic tests (${testLabels.join(", ")})`
          : "Phenotypic test result",
        "Target organism decision (Detected / Not Detected)"
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

export function describeWorkflowAnalystEnters(input: WorkflowAnalystInputOptions): string[] {
  const panelKind = resolveWorkflowPanelKind(input.workflowType);
  const conditions = parseConditionFields(input.conditionFields);

  switch (panelKind) {
    case "Measurement": {
      const reps = input.replicateCount && input.replicateCount > 0 ? input.replicateCount : 1;
      return [`Equipment`, `Measurement readings (${reps} replicate${reps === 1 ? "" : "s"})`];
    }
    case "Gravimetric": {
      const reps = input.replicateCount && input.replicateCount > 0 ? input.replicateCount : 1;
      return [
        "Equipment",
        ...(input.usesTare ? [`Container tare weight (${reps} replicates)`] : []),
        `Initial & final sample weights (${reps} replicate${reps === 1 ? "" : "s"})`,
        ...conditions
      ];
    }
    case "Titration": {
      const reps = input.replicateCount && input.replicateCount > 0 ? input.replicateCount : 3;
      return [
        "Titrant lot",
        ...(input.titrationEndpoint === "Visual" ? ["Indicator lot"] : []),
        ...(input.titrationBlankRequired ? ["Blank titration volume (mL)"] : []),
        ...(input.titrationMode === "Residual" ? ["Excess titrant lot & volume (mL)"] : []),
        `Sample weights & titrant volumes (${reps} replicate${reps === 1 ? "" : "s"})`
      ];
    }
    case "UvSpectrophotometry": {
      const reps = input.replicateCount && input.replicateCount > 0 ? input.replicateCount : 1;
      return [
        "Equipment",
        ...(input.uvCalculationMode === "StandardComparison"
          ? ["Reference standard lot, weight & absorbance"]
          : []),
        `Sample weight & absorbance (${reps} replicate${reps === 1 ? "" : "s"})`
      ];
    }
    case "Qualitative":
      return [
        input.perContainer ? "Per-container compliance observations" : "Compliance observation",
        "Observation remark (optional)"
      ];
    case "Dissolution":
      return ["Equipment", "Staged dissolution results via HPLC (S1–S3)", ...conditions];
    case "Disintegration": {
      const s1 = input.disintegrationStage1Units ?? 6;
      const s2 = input.disintegrationStage2Units ?? 12;
      return ["Equipment", `Disintegration time per unit (S1: ${s1}, S2: +${s2})`, ...conditions];
    }
    case "WeightVariation": {
      const units = input.wvUnitCount ?? 20;
      return ["Equipment", `Individual unit weights (${units} units)`, ...conditions];
    }
    case "HplcMethodAssay":
      return ["HPLC batch injections & sample weights (HPLC Run)"];
    case "IcpMethodAssay":
      return [
        input.icpMode === "Impurity"
          ? "ICP sample preparation weights (ppm per element)"
          : "ICP sample preparation weights (% of target per element)"
      ];
    case "Count":
      return describeStepAnalystEnters(input.stepType ?? "PlateCount", input.stepOptions);
    case "PathogenChain":
      return input.stepType
        ? describeStepAnalystEnters(input.stepType, input.stepOptions)
        : ["Configured microbiology workflow steps"];
    case "Outsourced":
    case "LocationResults":
      return [];
  }
}
