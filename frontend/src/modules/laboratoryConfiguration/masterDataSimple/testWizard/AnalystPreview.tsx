import { Box, Chip, List, ListItem, ListItemText, Paper, Stack, Typography } from "@mui/material";
import type { TFunction } from "i18next";
import { useTranslation } from "react-i18next";
import { i18n } from "../../../../i18n";
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
  t?: TFunction;
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
  mediaProducts?: Pick<MediaProductOption, "id" | "name">[],
  t: TFunction = i18n.getFixedT("en", "masterData")
): string[] {
  return step.stepMedia
    .filter((m) => m.mediaProductId !== "" || m.mediaProductName)
    .map((m) => {
      if (m.mediaProductName) return m.mediaProductName;
      const found = mediaProducts?.find((p) => p.id === m.mediaProductId);
      return found ? found.name : t("testWizard.preview.mediaFallback", { id: m.mediaProductId });
    });
}

function parseIntOrNull(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const n = Number(trimmed);
  return Number.isFinite(n) ? n : null;
}

export function previewModel(model: TestFormModel, lookups?: PreviewLookups): PreviewStep[] {
  const t = lookups?.t ?? i18n.getFixedT("en", "masterData");
  const stages = model.stages.map((s) => `${s.role}: ${s.sampleReplicates}`);
  const isStepDriven =
    model.workflowType === "CountTest" || model.workflowType === "Observation";

  if (isStepDriven) {
    const sorted = model.steps.slice().sort((a, b) => a.stepOrder - b.stepOrder);
    if (sorted.length === 0) {
      return [
        {
          order: 1,
          name:
            model.displayName.trim() ||
            t(`testMaster.constants.workflowTypes.${model.workflowType}`, {
              defaultValue: WORKFLOW_TYPE_LABELS[model.workflowType] ?? model.workflowType
            }),
          media: [],
          stages,
          analystEnters: describeWorkflowAnalystEnters({
            workflowType: model.workflowType
          }, t)
        }
      ];
    }

    return sorted.map((step, index) => ({
      order: step.stepOrder > 0 ? step.stepOrder : index + 1,
      name: step.stepName.trim() || t("testWizard.preview.stepFallback", { index: index + 1 }),
      media: resolveMediaNames(step, lookups?.mediaProducts, t),
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
      }, t)
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
        t(`testMaster.constants.workflowTypes.${model.workflowType}`, {
          defaultValue: WORKFLOW_TYPE_LABELS[model.workflowType] ?? model.workflowType
        }),
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
      }, t)
    }
  ];
}

function buildPreviewStripResponse(steps: PreviewStep[], previewName: string): CurrentStepResponse {
  return {
    testOrderId: 0,
    step: null,
    stepNumber: null,
    totalSteps: steps.length,
    testName: previewName,
    workflowType: "Observation" as WorkflowType,
    sampleContext: {
      sampleName: previewName,
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

function formatIncubationWindow(incubation: PreviewStep["incubation"], t: TFunction): string | null {
  if (!incubation) return null;
  const hours = t("testWizard.preview.hours", { min: incubation.minHours, max: incubation.maxHours });
  if (incubation.tempMin != null && incubation.tempMax != null) {
    return t("testWizard.preview.hoursAtTemp", { hours, tempMin: incubation.tempMin, tempMax: incubation.tempMax });
  }
  return hours;
}

export function AnalystPreview({ steps }: { steps: PreviewStep[] }) {
  const { t } = useTranslation(["masterData", "testingWorkspace"]);
  const orderedSteps = steps.slice().sort((a, b) => a.order - b.order);
  const previewCurrent = buildPreviewStripResponse(orderedSteps, t("testWizard.preview.previewContextName"));

  return (
    <Box
      aria-label={t("testWizard.preview.ariaLabel")}
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
          {t("testWizard.preview.title")}
        </Typography>
        <Chip size="small" label={t("testWizard.preview.chip")} color="info" variant="outlined" />
      </Box>

      {orderedSteps.length > 0 && <StepChainStrip current={previewCurrent} />}

      <Stack spacing={1.5}>
        {orderedSteps.map((step) => {
          const windowLabel = formatIncubationWindow(step.incubation, t);
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
                    <strong>{t("testWizard.preview.mediaLabel")}</strong> {step.media.join(", ")}
                  </Typography>
                )}

                {windowLabel && (
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    <strong>{t("testWizard.preview.incubationLabel")}</strong> {windowLabel}
                  </Typography>
                )}

                {step.stages.length > 0 && (
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    <strong>{t("testWizard.preview.stageReplicatesLabel")}</strong> {step.stages.join(", ")}
                  </Typography>
                )}

                {step.analystEnters.length > 0 && (
                  <Box>
                    <Typography variant="body2" sx={{ fontWeight: 600, fontSize: 12, mt: 0.5 }}>
                      {t("testWizard.preview.analystEntersLabel")}
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
