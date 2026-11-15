import { Box, Chip, List, ListItem, ListItemText, Paper, Stack, Typography } from "@mui/material";
import type {
  MediaIncubationConditionOption,
  MediaProductOption
} from "../../../../services/masterDataOptions";
import { StepChainStrip } from "../../../testingWorkspace/components/StepChainStrip";
import type {
  CurrentStepResponse,
  WorkflowType
} from "../../../testingWorkspace/types/testWorkflowTypes";
import { describeWorkflowAnalystEnters } from "../../../testingWorkspace/utils/workflowPanelMapping";
import { WORKFLOW_TYPE_LABELS } from "../testMaster/testMasterConstants";
import type { DraftWorkflowStep, TestFormModel } from "./testFormModel";

export interface PreviewStep {
  order: number;
  name: string;
  media: string[];
  incubation?: { minHours: number; maxHours: number; tempMin?: number; tempMax?: number };
  stages: string[];
  analystEnters: string[];
}

export interface PreviewLookups {
  mediaProducts?: Pick<MediaProductOption, "id" | "name">[];
  conditions?: MediaIncubationConditionOption[];
}

function buildStepIncubation(
  step: DraftWorkflowStep,
  conditions?: MediaIncubationConditionOption[]
): PreviewStep["incubation"] {
  const windows: { minHours: number; maxHours: number; tempMin?: number; tempMax?: number }[] = [];

  for (const m of step.stepMedia) {
    const cond =
      m.mediaIncubationConditionId !== ""
        ? conditions?.find((c) => c.id === m.mediaIncubationConditionId)
        : undefined;
    const minHours = cond?.incubationMinHours ?? m.incubationMinHours;
    const maxHours = cond?.incubationMaxHours ?? m.incubationMaxHours;
    const tempMin = cond?.temperatureMin ?? m.tempMin;
    const tempMax = cond?.temperatureMax ?? m.tempMax;
    if (minHours != null && maxHours != null) {
      windows.push({ minHours, maxHours, tempMin, tempMax });
    }
  }

  if (windows.length === 0) return undefined;

  const minHours = Math.min(...windows.map((w) => w.minHours));
  const maxHours = Math.max(...windows.map((w) => w.maxHours));
  const tempMins = windows.map((w) => w.tempMin).filter((v): v is number => v != null);
  const tempMaxs = windows.map((w) => w.tempMax).filter((v): v is number => v != null);

  return {
    minHours,
    maxHours,
    ...(tempMins.length > 0 ? { tempMin: Math.min(...tempMins) } : {}),
    ...(tempMaxs.length > 0 ? { tempMax: Math.max(...tempMaxs) } : {})
  };
}

function resolveMediaNames(
  step: DraftWorkflowStep,
  mediaProducts?: Pick<MediaProductOption, "id" | "name">[]
): string[] {
  return step.stepMedia
    .filter((m) => m.mediaProductId !== "" || m.mediaProductName)
    .map((m) => {
      if (m.mediaProductName) return m.mediaProductName;
      const found = mediaProducts?.find((p) => p.id === m.mediaProductId);
      return found ? found.name : `Media #${m.mediaProductId}`;
    });
}

function parseIntOrNull(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const n = Number(trimmed);
  return Number.isFinite(n) ? n : null;
}

export function previewModel(model: TestFormModel, lookups?: PreviewLookups): PreviewStep[] {
  const stages = model.stages.map((s) => `${s.role}: ${s.sampleReplicates}`);
  const isStepDriven =
    model.workflowType === "CountTest" || model.workflowType === "Observation";

  if (isStepDriven) {
    const sorted = model.steps.slice().sort((a, b) => a.stepOrder - b.stepOrder);
    if (sorted.length === 0) {
      return [
        {
          order: 1,
          name: model.displayName.trim() || WORKFLOW_TYPE_LABELS[model.workflowType] || model.workflowType,
          media: [],
          stages,
          analystEnters: describeWorkflowAnalystEnters({
            workflowType: model.workflowType
          })
        }
      ];
    }

    return sorted.map((step, index) => ({
      order: step.stepOrder > 0 ? step.stepOrder : index + 1,
      name: step.stepName.trim() || `Step ${index + 1}`,
      media: resolveMediaNames(step, lookups?.mediaProducts),
      incubation: buildStepIncubation(step, lookups?.conditions),
      stages,
      analystEnters: describeWorkflowAnalystEnters({
        workflowType: model.workflowType,
        stepType: step.stepType,
        stepOptions: {
          requiresIncubationTransfer: step.requiresIncubationTransfer,
          phenotypicTestType: step.phenotypicTestType,
          phenotypicTestTypes: step.phenotypicTestTypes
        }
      })
    }));
  }

  const replicateCount =
    model.workflowType === "Titration"
      ? parseIntOrNull(model.titration.replicateCount)
      : parseIntOrNull(model.replicateCount);

  return [
    {
      order: 1,
      name:
        model.displayName.trim() ||
        WORKFLOW_TYPE_LABELS[model.workflowType] ||
        model.workflowType,
      media: [],
      stages,
      analystEnters: describeWorkflowAnalystEnters({
        workflowType: model.workflowType,
        replicateCount,
        usesTare: model.usesTare,
        perContainer: model.perContainer,
        conditionFields: model.conditionFields,
        titrationBlankRequired: model.titration.blankRequired,
        titrationMode: model.titration.mode,
        titrationEndpoint: model.titration.endpoint,
        uvCalculationMode: model.uvCalculationMode,
        icpMode: model.icp.mode,
        disintegrationStage1Units: parseIntOrNull(model.disintegrationStage1Units),
        disintegrationStage2Units: parseIntOrNull(model.disintegrationStage2Units),
        wvUnitCount: parseIntOrNull(model.wvUnitCount)
      })
    }
  ];
}

function buildPreviewStripResponse(steps: PreviewStep[]): CurrentStepResponse {
  return {
    testOrderId: 0,
    step: null,
    stepNumber: null,
    totalSteps: steps.length,
    testName: "Preview",
    workflowType: "Observation" as WorkflowType,
    sampleContext: {
      sampleName: "Preview",
      batchNumber: null,
      controlNumber: null,
      reason: null,
      systemReferenceNumber: null,
      sampleType: "FinishedProduct"
    },
    incubationLock: null,
    previousSteps: [],
    allStepsComplete: false,
    finalResult: null,
    allSteps: steps.map((s) => ({ stepOrder: s.order, stepName: s.name })),
    completedSteps: []
  };
}

function formatIncubationWindow(incubation: PreviewStep["incubation"]): string | null {
  if (!incubation) return null;
  const hours = `${incubation.minHours}–${incubation.maxHours} h`;
  if (incubation.tempMin != null && incubation.tempMax != null) {
    return `${hours} at ${incubation.tempMin}–${incubation.tempMax} °C`;
  }
  return hours;
}

export function AnalystPreview({ steps }: { steps: PreviewStep[] }) {
  const orderedSteps = steps.slice().sort((a, b) => a.order - b.order);
  const previewCurrent = buildPreviewStripResponse(orderedSteps);

  return (
    <Box
      aria-label="Analyst preview"
      sx={{
        p: 2,
        bgcolor: "action.hover",
        borderRadius: 1,
        border: "1px solid",
        borderColor: "divider"
      }}
    >
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1, mb: 1.5, flexWrap: "wrap" }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          What the analyst will see
        </Typography>
        <Chip size="small" label="Preview — configuration only" color="info" variant="outlined" />
      </Box>

      {orderedSteps.length > 0 && <StepChainStrip current={previewCurrent} />}

      <Stack spacing={1.5}>
        {orderedSteps.map((step) => {
          const windowLabel = formatIncubationWindow(step.incubation);
          return (
            <Paper
              key={`${step.order}-${step.name}`}
              variant="outlined"
              sx={{ p: 1.5, bgcolor: "background.paper" }}
            >
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 0.75 }}>
                {step.order}. {step.name}
              </Typography>

              <Stack spacing={0.5}>
                {step.media.length > 0 && (
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    <strong>Media:</strong> {step.media.join(", ")}
                  </Typography>
                )}

                {windowLabel && (
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    <strong>Incubation:</strong> {windowLabel}
                  </Typography>
                )}

                {step.stages.length > 0 && (
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    <strong>Stage replicates:</strong> {step.stages.join(", ")}
                  </Typography>
                )}

                {step.analystEnters.length > 0 && (
                  <Box>
                    <Typography variant="body2" sx={{ fontWeight: 600, fontSize: 12, mt: 0.5 }}>
                      Analyst enters:
                    </Typography>
                    <List dense disablePadding sx={{ pl: 2 }}>
                      {step.analystEnters.map((entry) => (
                        <ListItem key={entry} disableGutters sx={{ py: 0 }}>
                          <ListItemText
                            primary={entry}
                            slotProps={{ primary: { variant: "body2", color: "text.secondary" } }}
                          />
                        </ListItem>
                      ))}
                    </List>
                  </Box>
                )}
              </Stack>
            </Paper>
          );
        })}
      </Stack>
    </Box>
  );
}
