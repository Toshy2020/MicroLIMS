import { useCallback, useEffect, useState } from "react";
import { Box, Typography, Alert, Button, useTheme } from "@mui/material";
import { DialogBodySkeleton } from "../../components/DialogBodySkeleton";
import { noteOwnSave } from "../../hooks/useRecordChanged";
import { useTranslation } from "../../i18n";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { CurrentStepResponse } from "./types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "./utils/workflowErrors";
import { BrothStepPanel } from "./pathogenSteps/BrothStepPanel";
import { BrothWaitingPanel } from "./pathogenSteps/BrothWaitingPanel";
import { SelectivePlatingPanel } from "./pathogenSteps/SelectivePlatingPanel";
import { UnsupportedStepPanel } from "./pathogenSteps/UnsupportedStepPanel";
import { ConfirmatoryPlatingPanel } from "./pathogenSteps/ConfirmatoryPlatingPanel";
import { BiochemicalTestPanel } from "./pathogenSteps/BiochemicalTestPanel";
import { StepChainStrip } from "./components/StepChainStrip";
import { serverIncubationWindow } from "./utils/incubationWindow";
import { StepHeading, StepChipTone } from "./pathogenSteps/StepLayout";
import { resolvePathogenStepPanelKind } from "./utils/workflowPanelMapping";

interface Props { testOrderId: number; testCode: string; displayName: string; onClose?: () => void; }

// Shared lookup so the Inconclusive-terminal check and the BiochemicalTest
// panel's confirmatoryOutcome prop can never drift apart - both read the
// same completedSteps entry.
function getConfirmatoryOutcome(current: CurrentStepResponse): string | null {
  const confirmatoryStep = current.completedSteps.find((s) => s.stepType === "ConfirmatoryPlating");
  return confirmatoryStep?.outcome ?? null;
}

export function PathogenStepDialog({ testOrderId, onClose }: Props) {
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();
  const [current, setCurrent] = useState<CurrentStepResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (signal?: AbortSignal) => {
    setError(null);
    try {
      const data = await TestWorkflowService.getCurrentStep(testOrderId, signal);
      if (!signal?.aborted) setCurrent(data);
    } catch (e) {
      if (!signal?.aborted) setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    }
  }, [testOrderId]);

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  const handleSubmitted = (saved = true) => {
    if (saved) noteOwnSave("testOrder", testOrderId);
    void load();
  };

  if (!current) {
    return (
      <Box sx={{ py: 4 }}>
        {error ? <Alert severity="error">{error}</Alert> : <DialogBodySkeleton blocks={3} blockHeight={56} />}
      </Box>
    );
  }

  if (current.allStepsComplete) {
    return (
      <Box>
        <StepChainStrip current={current} />
        {current.finalResult ? (
          <Box sx={{ backgroundColor: theme.custom.status.notDetected.bg, border: "1px solid", borderColor: theme.custom.status.notDetected.border, borderRadius: 1, p: 2, mt: 1, mb: 2 }}>
            <Typography variant="body2" sx={{ fontWeight: 600, color: theme.custom.status.notDetected.text }}>
              {t("pathogenSteps.dialog.finalResult", { result: current.finalResult })}
            </Typography>
          </Box>
        ) : (
          <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>—</Typography>
        )}
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mt: 2 }}>
          <Box />
          {onClose && (
            <Button variant="contained" onClick={onClose} sx={{ fontWeight: 600, textTransform: "none" }}>
              {t("pathogenSteps.dialog.doneClose")}
            </Button>
          )}
        </Box>
      </Box>
    );
  }

  const step = current.step;
  if (!step) return <Alert severity="error">{t("pathogenSteps.dialog.noCurrentStep")}</Alert>;

  const stepPanelKind = resolvePathogenStepPanelKind(step.stepType);
  const isBrothStep = stepPanelKind === "Broth";
  const isPlatingStep = stepPanelKind === "SelectivePlating" || stepPanelKind === "ConfirmatoryPlating";
  const isIncubating = current.incubationLock != null
    || (current.previousSteps ?? []).some((p) => p.stepName === step.stepName && p.status === "Incubating");
  const { isTimeReady } = serverIncubationWindow(step, current);

  const stepChip: { label: string; tone: StepChipTone } | null =
    isBrothStep ? (isIncubating ? { label: t("pathogenSteps.dialog.chip.incubating"), tone: "purple" } : { label: t("pathogenSteps.dialog.chip.preparation"), tone: "info" })
    : isPlatingStep ? (!isIncubating ? { label: t("pathogenSteps.dialog.chip.setup"), tone: "info" } : isTimeReady ? { label: t("pathogenSteps.dialog.chip.readyToRead"), tone: "notDetected" } : { label: t("pathogenSteps.dialog.chip.incubating"), tone: "purple" })
    : stepPanelKind === "BiochemicalTest" ? { label: t("pathogenSteps.dialog.chip.resultEntry"), tone: "info" }
    : null;
  const stepHelp = t(`pathogenSteps.dialog.help.${step.stepType}`, { defaultValue: "" }) || undefined;

  return (
    <Box>
      <StepChainStrip current={current} />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <StepHeading
        title={step.stepName}
        chip={stepChip}
        caption={step.isFinalStep ? t("pathogenSteps.dialog.determinesFinal") : undefined}
        help={stepHelp}
      />

      {stepPanelKind === "Broth" ? (
        current.incubationLock != null ? (
          <BrothWaitingPanel 
            testOrderId={testOrderId} 
            step={step} 
            current={current}
            onSubmitted={handleSubmitted} 
          />
        ) : (
          <BrothStepPanel testOrderId={testOrderId} step={step} current={current} onSubmitted={handleSubmitted} />
        )
      ) : stepPanelKind === "SelectivePlating" ? (
        <SelectivePlatingPanel testOrderId={testOrderId} step={step} current={current} onSubmitted={handleSubmitted} />
      ) : stepPanelKind === "ConfirmatoryPlating" ? (
        <ConfirmatoryPlatingPanel testOrderId={testOrderId} step={step} current={current} onSubmitted={handleSubmitted} />
      ) : stepPanelKind === "BiochemicalTest" ? (
        <BiochemicalTestPanel
          testOrderId={testOrderId} step={step}
          confirmatoryOutcome={getConfirmatoryOutcome(current)}
          onSubmitted={handleSubmitted}
        />
      ) : (
        <UnsupportedStepPanel stepType={step.stepType} />
      )}
    </Box>
  );
}
