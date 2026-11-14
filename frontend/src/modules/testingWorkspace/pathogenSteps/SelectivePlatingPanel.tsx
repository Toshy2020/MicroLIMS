import { useCallback, useEffect, useState } from "react";
import { TextField, Button, Stack, Alert, CircularProgress } from "@mui/material";
import { TestWorkflowService } from "../services/TestWorkflowService";
import {
  TestWorkflowStepDto, CurrentStepResponse, PermittedConfirmatoryMediaEntry, GrowthObservation
} from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";
import { serverIncubationWindow } from "../utils/incubationWindow";
import { DialogBodySkeleton } from "../../../components/DialogBodySkeleton";
import { DraftRestoreNotice } from "../../../components/bench/DraftRestoreNotice";
import { useBenchDraft } from "../../../hooks/useBenchDraft";
import { useBenchMode } from "../../../theme/BenchModeContext";
import { OfflineDraftNotice, classifySaveFailure, draftHeaderArgs, useKeptOffline } from "../benchSave";
import {
  StepActionBar, StepDetailList, IncubationTimeline, FixedMediumRow, MediaLotIncubatorFields,
  ExpectedAppearanceNote, ObservationOptions
} from "./StepLayout";
import { IncubationWaitingView } from "./IncubationWaitingView";

interface SelectivePlatingDraftValues {
  mediaLotId: number | "";
  equipmentId: number | "";
  observation: GrowthObservation | "";
  appearanceNote: string;
}

function pickSelectiveSetupDraft(v: SelectivePlatingDraftValues): Partial<SelectivePlatingDraftValues> {
  return { mediaLotId: v.mediaLotId, equipmentId: v.equipmentId };
}

function pickSelectiveReadoutDraft(v: SelectivePlatingDraftValues): Partial<SelectivePlatingDraftValues> {
  return { observation: v.observation, appearanceNote: v.appearanceNote };
}

const OBSERVATION_OPTIONS: { value: GrowthObservation; title: string; detail: string }[] = [
  { value: "NoGrowth", title: "No growth", detail: "Target absent" },
  { value: "GrowthNonConforming", title: "Growth, does not match expected appearance", detail: "Target absent" },
  { value: "GrowthConforming", title: "Growth matching expected appearance", detail: "Presumptive positive" }
];

interface Props {
  testOrderId: number;
  step: TestWorkflowStepDto;
  current: CurrentStepResponse;
  onSubmitted: () => void;
}

export function SelectivePlatingPanel({ testOrderId, step, current, onSubmitted }: Props) {
  const { benchMode } = useBenchMode();
  const [keptOffline, setKeptOffline] = useKeptOffline();
  const [medium, setMedium] = useState<PermittedConfirmatoryMediaEntry | null>(null);
  const [incubators, setIncubators] = useState<{ id: number; name: string; code: string; setTemperature: number }[]>([]);
  const [mediaLotId, setMediaLotId] = useState<number | "">("");
  const [equipmentId, setEquipmentId] = useState<number | "">("");
  const [observation, setObservation] = useState<GrowthObservation | "">("");
  const [appearanceNote, setAppearanceNote] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  // Set by the waiting screen when its countdown reaches zero.
  const [countdownReady, setCountdownReady] = useState(false);
  const handleReady = useCallback(() => setCountdownReady(true), []);

  useEffect(() => {
    setLoading(true);
    setError(null);
    TestWorkflowService.getPermittedConfirmatoryMedia(testOrderId, step.stepName)
      .then(async (res) => {
        const only = res.permittedMedia[0] ?? null;
        setMedium(only);
        if (only) {
          const eligible = await TestWorkflowService.getEligibleIncubators(testOrderId, only.stepMediaId);
          setIncubators(eligible.eligibleIncubators);
        }
      })
      .catch((e) => setError(workflowErrorDisplayMessage(parseWorkflowError(e))))
      .finally(() => setLoading(false));
  }, [testOrderId, step.stepName]);

  // Find open incubation row from previousSteps (matches BrothWaitingPanel pattern)
  const openIncubationRow = step
    ? (current?.previousSteps ?? []).find(
        (p) => p.stepName === step.stepName && p.status === "Incubating"
      )
    : null;
  const isStepIncubating = Boolean(openIncubationRow || current?.incubationLock?.isLocked);

  // Window, start, readiness and expected end exactly as the server resolved
  // them from this test's own medium - no local hour calculations.
  const { incubationStartUtc, minReadyAt, expectedEndAt, windowNotConfigured, isTimeReady } = serverIncubationWindow(step, current);

  const phase: "setup" | "waiting" | "readout" =
    !isStepIncubating ? "setup" :
    !(isTimeReady || countdownReady) || windowNotConfigured ? "waiting" :
    "readout";

  const draft = useBenchDraft<SelectivePlatingDraftValues>({
    formKey: `pathogen-selective-${phase}`,
    recordKey: `${testOrderId}:${step.stepName}`,
    recordVersion: null,
    title: `${step.stepName}: #${testOrderId}`,
    values: { mediaLotId, equipmentId, observation, appearanceNote },
    pick: phase === "setup" ? pickSelectiveSetupDraft : pickSelectiveReadoutDraft,
    enabled: benchMode && !loading && Boolean(medium) && phase !== "waiting"
  });

  const restoreDraft = () => {
    const saved = draft.acceptRestore();
    if (!saved) return;
    if (phase === "setup") {
      if (typeof saved.mediaLotId === "number" && medium?.availableLots.some((l) => l.id === saved.mediaLotId)) {
        setMediaLotId(saved.mediaLotId);
      }
      if (typeof saved.equipmentId === "number" && incubators.some((i) => i.id === saved.equipmentId)) {
        setEquipmentId(saved.equipmentId);
      }
    } else if (phase === "readout") {
      if (saved.observation === "NoGrowth" || saved.observation === "GrowthNonConforming" || saved.observation === "GrowthConforming") {
        setObservation(saved.observation);
      }
      if (typeof saved.appearanceNote === "string") {
        setAppearanceNote(saved.appearanceNote);
      }
    }
  };

  // Phase 1: Start incubation
  const handleStartIncubation = async () => {
    setError(null);
    if (!medium || !mediaLotId || !equipmentId) {
      setError("Select a media lot and an incubator.");
      return;
    }
    setSubmitting(true);
    try {
      const headerArgs = draftHeaderArgs(draft.submitHeaders());
      if (headerArgs.length > 0) {
        await TestWorkflowService.startSelectivePlatingIncubation(
          testOrderId, step.stepName, Number(mediaLotId), Number(equipmentId), undefined, headerArgs[0]
        );
      } else {
        await TestWorkflowService.startSelectivePlatingIncubation(
          testOrderId, step.stepName, Number(mediaLotId), Number(equipmentId)
        );
      }
      await draft.onSubmitted();
      setKeptOffline(false);
      onSubmitted();
    } catch (e) {
      const failure = classifySaveFailure(e);
      if (failure.kind === "network") {
        setKeptOffline(true);
      } else if (failure.kind === "conflict") {
        await draft.onRejected(failure.message);
        setError(failure.message);
      } else {
        setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
      }
    } finally {
      setSubmitting(false);
    }
  };

  // Phase 2: Submit observation after minHours
  const handleSubmitObservation = async () => {
    setError(null);
    if (observation === "") {
      setError("Select an observation.");
      return;
    }
    setSubmitting(true);
    try {
      await TestWorkflowService.submitSelectivePlatingObservation(
        testOrderId,
        step.stepName,
        observation,
        appearanceNote.trim() || undefined,
        ...draftHeaderArgs(draft.submitHeaders())
      );
      await draft.onSubmitted();
      setKeptOffline(false);
      onSubmitted();
    } catch (e) {
      const failure = classifySaveFailure(e);
      if (failure.kind === "network") {
        setKeptOffline(true);
      } else if (failure.kind === "conflict") {
        await draft.onRejected(failure.message);
        setError(failure.message);
      } else {
        setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
      }
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <DialogBodySkeleton blocks={3} blockHeight={48} />;
  if (error && !medium) return <Alert severity="error">{error}</Alert>;
  if (!medium) return <Alert severity="error">This step has no assigned medium configured in Test Master.</Alert>;

  return (
    <Stack spacing={2}>
      {benchMode && phase !== "waiting" && (
        <DraftRestoreNotice startedAt={draft.restoredStartedAt} onRestore={restoreDraft} onDiscard={() => void draft.discard()} />
      )}
      {benchMode && keptOffline && phase !== "waiting" && (
        <OfflineDraftNotice onTryAgain={() => void (phase === "setup" ? handleStartIncubation() : handleSubmitObservation())} />
      )}
      {error && <Alert severity="error">{error}</Alert>}

      <FixedMediumRow mediaName={medium.mediaName} />
      <ExpectedAppearanceNote mediaName={medium.mediaName} expectedAppearance={medium.expectedAppearance} />

      {/* Phase 1: Setup (Start Incubation) */}
      {phase === "setup" && (
        <>
          <MediaLotIncubatorFields
            idPrefix="selective-plating"
            lots={medium.availableLots}
            incubators={incubators}
            tempMin={medium.tempMin}
            tempMax={medium.tempMax}
            mediaLotId={mediaLotId}
            equipmentId={equipmentId}
            onLotChange={setMediaLotId}
            onIncubatorChange={setEquipmentId}
          />

          <IncubationTimeline
            points={[
              { label: "Start", value: "0 h", active: true },
              { label: "Ready from", value: `${step.incubationMinHours} h` },
              { label: "Read by", value: `${step.incubationMaxHours} h` }
            ]}
          />

          <StepActionBar
            note={mediaLotId && equipmentId
              ? "Start time, media lot and incubator are recorded in the audit trail."
              : "Select a media lot and an incubator to continue."}
          >
            <Button
              variant="contained"
              onClick={handleStartIncubation}
              disabled={!mediaLotId || !equipmentId || submitting}
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              {submitting ? "Starting..." : "Start Incubation"}
            </Button>
          </StepActionBar>
        </>
      )}

      {/* Phase 2: Waiting - countdown to the plate reading */}
      {phase === "waiting" && (
        <IncubationWaitingView
          testOrderId={testOrderId}
          step={step}
          current={current}
          onSubmitted={onSubmitted}
          nextLabel="plate reading"
          onReady={handleReady}
          renderAction={() => (
            <Button variant="contained" disabled>Record Observation</Button>
          )}
        />
      )}

      {/* Phase 3: Readout (Ready for observation) */}
      {phase === "readout" && (
        <>
          <Alert severity="success">Incubation complete. Read the plate and record what you observe.</Alert>
          <StepDetailList
            rows={[
              ...(incubationStartUtc ? [{ label: "Started", value: incubationStartUtc.toLocaleString() }] : []),
              ...(minReadyAt ? [{ label: "Ready from", value: minReadyAt.toLocaleString() }] : []),
              ...(expectedEndAt ? [{ label: "Read by", value: expectedEndAt.toLocaleString() }] : [])
            ]}
          />

          <ObservationOptions
            name="selective-plating-observation"
            value={observation}
            onChange={setObservation}
            options={OBSERVATION_OPTIONS}
          />

          <TextField
            id="selective-plating-note"
            multiline
            minRows={2}
            label="Observed appearance note (optional)"
            value={appearanceNote}
            onChange={(e) => setAppearanceNote(e.target.value)}
            helperText="Colony characteristics seen on the plate."
          />

          <StepActionBar note={observation ? undefined : "Select an observation to continue."}>
            <Button
              variant="contained"
              onClick={handleSubmitObservation}
              disabled={!observation || submitting}
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              {submitting ? "Recording..." : "Record Observation"}
            </Button>
          </StepActionBar>
        </>
      )}
    </Stack>
  );
}
