import { useCallback, useEffect, useState } from "react";
import {
  Box, Typography, Button, Stack, Alert,
  Checkbox, FormControlLabel, CircularProgress
} from "@mui/material";
import { TestWorkflowService } from "../services/TestWorkflowService";
import {
  TestWorkflowStepDto, CurrentStepResponse, PermittedConfirmatoryMediaEntry,
  GrowthObservation, AnalystDecision
} from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";
import { serverIncubationWindow } from "../utils/incubationWindow";
import { DialogBodySkeleton } from "../../../components/DialogBodySkeleton";
import {
  StepActionBar, IncubationTimeline, FixedMediumRow, MediaLotIncubatorFields,
  ExpectedAppearanceNote, ObservationOptions
} from "./StepLayout";
import { IncubationWaitingView } from "./IncubationWaitingView";

const OBSERVATION_OPTIONS: { value: GrowthObservation; title: string; detail?: string }[] = [
  { value: "NoGrowth", title: "No growth" },
  { value: "GrowthNonConforming", title: "Growth, does not match expected appearance" },
  { value: "GrowthConforming", title: "Growth matching expected appearance", detail: "Conforming" }
];

interface Props {
  testOrderId: number;
  step: TestWorkflowStepDto;
  current: CurrentStepResponse;
  onSubmitted: () => void;
}

type Phase = "setup" | "waiting" | "readout" | "decision";

interface IncubatorOption { id: number; name: string; code: string; setTemperature: number; }

interface SetupRow {
  entry: PermittedConfirmatoryMediaEntry;
  checked: boolean;
  mediaLotId: number | "";
  equipmentId: number | "";
  incubators: IncubatorOption[];
}

interface ReadoutMedium {
  stepMediaId: number;
  mediaProductId: number;
  // The analyst's actual lot's material: observations are recorded against the lot used, not the product.
  materialId: number;
  mediaName: string;
  expectedAppearance: string | null;
}

export function ConfirmatoryPlatingPanel({ testOrderId, step, current, onSubmitted }: Props) {
  const [permitted, setPermitted] = useState<PermittedConfirmatoryMediaEntry[]>([]);
  const [organism, setOrganism] = useState<{ id: number; name: string } | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [alreadyRecordedMessage, setAlreadyRecordedMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  // Set by the waiting screen when its countdown reaches zero.
  const [countdownReady, setCountdownReady] = useState(false);
  const handleReady = useCallback(() => setCountdownReady(true), []);

  // Setup phase state, keyed by stepMediaId.
  const [setupRows, setSetupRows] = useState<Record<number, SetupRow>>({});

  // Read-out phase state.
  const [readoutMedia, setReadoutMedia] = useState<ReadoutMedium[]>([]);
  const [readoutUncertain, setReadoutUncertain] = useState(false);
  const [readoutChecked, setReadoutChecked] = useState<Record<number, boolean>>({});
  const [observations, setObservations] = useState<Record<number, GrowthObservation | "">>({});

  // Check incubation status from current step response (aligned with BrothWaitingPanel / SelectivePlatingPanel)
  const openIncubationRow = step
    ? (current?.previousSteps ?? []).find(
        (p) => p.stepName === step.stepName && p.status === "Incubating"
      )
    : null;
  const isStepIncubating = Boolean(openIncubationRow || current?.incubationLock?.isLocked);

  // Window, start, readiness and expected end exactly as the server resolved
  // them (the panel's longest window across its chosen media) - no local
  // hour calculations.
  const { windowNotConfigured, isTimeReady } = serverIncubationWindow(step, current);

  const initialPhase: Phase =
    !isStepIncubating ? "setup" :
    !(isTimeReady || countdownReady) || windowNotConfigured ? "waiting" :
    "readout";

  const [phase, setPhase] = useState<Phase>(initialPhase);

  useEffect(() => {
    setPhase(initialPhase);
  }, [initialPhase]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    TestWorkflowService.getPermittedConfirmatoryMedia(testOrderId, step.stepName)
      .then(async (res) => {
        if (cancelled) return;
        setPermitted(res.permittedMedia);
        setOrganism(res.organism);
        const withIncubators = await Promise.all(
          res.permittedMedia.map((m) =>
            TestWorkflowService.getEligibleIncubators(testOrderId, m.stepMediaId)
              .then((r) => [m.stepMediaId, r.eligibleIncubators] as const)
          )
        );
        if (cancelled) return;
        const rows: Record<number, SetupRow> = {};
        for (const m of res.permittedMedia) {
          const incubators = withIncubators.find(([id]) => id === m.stepMediaId)?.[1] ?? [];
          rows[m.stepMediaId] = { entry: m, checked: false, mediaLotId: "", equipmentId: "", incubators };
        }
        setSetupRows(rows);
      })
      .catch((e) => { if (!cancelled) setError(workflowErrorDisplayMessage(parseWorkflowError(e))); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [testOrderId, step.stepName]);

  // When in readout phase and readoutMedia is empty, populate from permitted
  useEffect(() => {
    if (phase === "readout" && readoutMedia.length === 0 && permitted.length > 0) {
      // The server's recorded selection says which lot (and so which material) was plated.
      const rows = permitted.filter((m) => m.selection).map((m) => ({
        stepMediaId: m.stepMediaId,
        mediaProductId: m.mediaProductId,
        materialId: m.selection!.materialId,
        mediaName: m.mediaName,
        expectedAppearance: m.expectedAppearance
      }));
      if (rows.length > 0) {
        setReadoutMedia(rows);
        setReadoutUncertain(false);
      }
    }
  }, [phase, permitted, readoutMedia.length]);

  const toggleChecked = (stepMediaId: number, checked: boolean) =>
    setSetupRows((prev) => ({ ...prev, [stepMediaId]: { ...prev[stepMediaId], checked } }));
  const setLot = (stepMediaId: number, mediaLotId: number) =>
    setSetupRows((prev) => ({ ...prev, [stepMediaId]: { ...prev[stepMediaId], mediaLotId } }));
  const setEquipment = (stepMediaId: number, equipmentId: number) =>
    setSetupRows((prev) => ({ ...prev, [stepMediaId]: { ...prev[stepMediaId], equipmentId } }));

  const submitSetup = async () => {
    setError(null);
    const checkedRows = Object.values(setupRows).filter((r) => r.checked);
    if (checkedRows.length === 0) { setError("Check at least one medium that was actually plated."); return; }
    if (checkedRows.some((r) => !r.mediaLotId || !r.equipmentId)) {
      setError("Every checked medium needs a media lot and an incubator.");
      return;
    }
    const startUtc = new Date().toISOString();
    const durationMax = step.incubationMaxHours > 0 ? step.incubationMaxHours : 24;
    const endUtc = new Date(Date.now() + durationMax * 3600 * 1000).toISOString();
    const selections = checkedRows.map((r) => ({
      stepMediaId: r.entry.stepMediaId, mediaLotId: Number(r.mediaLotId), equipmentId: Number(r.equipmentId)
    }));
    setSubmitting(true);
    try {
      await TestWorkflowService.submitConfirmatorySetup(testOrderId, step.stepName, selections, startUtc, endUtc);
      setReadoutMedia(checkedRows.map((r) => ({
        stepMediaId: r.entry.stepMediaId, mediaProductId: r.entry.mediaProductId,
        materialId: r.entry.availableLots.find((l) => l.id === Number(r.mediaLotId))!.materialId,
        mediaName: r.entry.mediaName, expectedAppearance: r.entry.expectedAppearance
      })));
      setReadoutUncertain(false);
      setObservations({});
      setReadoutChecked({});
      onSubmitted();
    } catch (e) {
      const parsed = parseWorkflowError(e);
      if (parsed.code === "CONFIRMATORY_SETUP_ALREADY_SUBMITTED") {
        onSubmitted();
        return;
      }
      if (parsed.code === "CONFIRMATORY_ALREADY_RECORDED") {
        setAlreadyRecordedMessage(workflowErrorDisplayMessage(parsed));
        return;
      }
      setError(workflowErrorDisplayMessage(parsed));
    } finally {
      setSubmitting(false);
    }
  };

  const isReadoutActive = (materialId: number) =>
    readoutUncertain ? (readoutChecked[materialId] ?? false) : true;

  const submitReadout = async () => {
    setError(null);
    const active = readoutMedia.filter((m) => isReadoutActive(m.materialId));
    if (active.length === 0) { setError("At least one medium must be confirmed as plated."); return; }
    const missing = active.filter((m) => !observations[m.materialId]);
    if (missing.length > 0) {
      setError(`Select an observation for: ${missing.map((m) => m.mediaName).join(", ")}.`);
      return;
    }
    const payload = active.map((m) => ({ materialId: m.materialId, observation: observations[m.materialId] as GrowthObservation }));
    setSubmitting(true);
    try {
      const outcome = await TestWorkflowService.submitConfirmatoryObservations(testOrderId, step.stepName, payload);
      if (outcome.analystDecisionRequired) {
        setPhase("decision");
      } else {
        onSubmitted();
      }
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    } finally {
      setSubmitting(false);
    }
  };

  const decide = async (decision: AnalystDecision) => {
    setError(null);
    setSubmitting(true);
    try {
      await TestWorkflowService.recordAnalystDecision(testOrderId, decision);
      onSubmitted();
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <DialogBodySkeleton blocks={3} blockHeight={48} />;
  if (alreadyRecordedMessage) {
    return (
      <Alert severity="info">
        {alreadyRecordedMessage}
      </Alert>
    );
  }

  const checkedSetupRows = Object.values(setupRows).filter((r) => r.checked);
  const setupComplete = checkedSetupRows.length > 0 && checkedSetupRows.every((r) => r.mediaLotId && r.equipmentId);
  const activeReadout = readoutMedia.filter((m) => isReadoutActive(m.materialId));
  const readoutComplete = activeReadout.length > 0 && activeReadout.every((m) => observations[m.materialId]);

  return (
    <Stack spacing={2}>
      {error && <Alert severity="error">{error}</Alert>}
      {organism && <FixedMediumRow label="Target organism" mediaName={organism.name} />}

      {phase === "setup" && (
        <>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Tick every medium you actually plated, then choose its lot and incubator. Only media the step template
            permits are listed.
          </Typography>
          {permitted.map((m) => {
            const row = setupRows[m.stepMediaId] ?? {
              entry: m, checked: false, mediaLotId: "", equipmentId: "", incubators: []
            };
            return (
              <Box
                key={m.stepMediaId}
                sx={{
                  border: "1px solid", borderColor: row.checked ? "primary.main" : "divider", borderRadius: 2,
                  p: 1.5, display: "grid", gap: 1.5
                }}
              >
                <FormControlLabel
                  sx={{ m: 0 }}
                  control={<Checkbox checked={row.checked} onChange={(e) => toggleChecked(m.stepMediaId, e.target.checked)} />}
                  label={<Typography sx={{ fontWeight: 600 }}>{m.mediaName}</Typography>}
                />
                <ExpectedAppearanceNote mediaName={m.mediaName} expectedAppearance={m.expectedAppearance} />
                {row.checked && (
                  <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" } }}>
                    <MediaLotIncubatorFields
                      idPrefix={`confirmatory-${m.stepMediaId}`}
                      lots={m.availableLots}
                      incubators={row.incubators}
                      tempMin={m.tempMin}
                      tempMax={m.tempMax}
                      mediaLotId={row.mediaLotId}
                      equipmentId={row.equipmentId}
                      onLotChange={(id) => setLot(m.stepMediaId, id)}
                      onIncubatorChange={(id) => setEquipment(m.stepMediaId, id)}
                    />
                  </Box>
                )}
              </Box>
            );
          })}
          {step.incubationMaxHours > 0 ? (
            <IncubationTimeline
              points={[
                { label: "Start", value: "0 h", active: true },
                { label: "Ready from", value: `${step.incubationMinHours} h` },
                { label: "Read by", value: `${step.incubationMaxHours} h` }
              ]}
            />
          ) : (
            <Alert severity="warning">Incubation duration is not configured in Test Master.</Alert>
          )}
          <Typography variant="caption" sx={{ color: "text.secondary", mt: -1 }}>
            Each medium keeps its own window; the shared incubation runs to the longest one selected.
          </Typography>
          <StepActionBar
            note={setupComplete
              ? "Start time, media lots and incubators are recorded in the audit trail."
              : "Tick at least one medium and choose its lot and incubator to continue."}
          >
            <Button
              variant="contained"
              onClick={submitSetup}
              disabled={submitting || !setupComplete}
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              {submitting ? "Starting..." : "Start Incubation"}
            </Button>
          </StepActionBar>
        </>
      )}

      {phase === "waiting" && (
        <IncubationWaitingView
          testOrderId={testOrderId}
          step={step}
          current={current}
          onSubmitted={onSubmitted}
          nextLabel="plate reading"
          onReady={handleReady}
          renderAction={() => (
            <Button variant="contained" disabled>Submit Observations</Button>
          )}
        />
      )}

      {phase === "readout" && (
        <>
          {readoutUncertain ? (
            <Alert severity="warning">
              Confirmatory media for this step were already selected in a previous session. Confirm
              each medium before recording its observation.
            </Alert>
          ) : (
            <Alert severity="success">Incubation complete. Read each plate against its expected appearance.</Alert>
          )}
          {readoutMedia.map((m) => {
            const checked = isReadoutActive(m.materialId);
            const value = observations[m.materialId] ?? "";
            return (
              <Box key={m.materialId} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, p: 1.5, display: "grid", gap: 1.5 }}>
                <Typography sx={{ fontWeight: 600 }}>{m.mediaName}</Typography>
                {readoutUncertain && (
                  <FormControlLabel
                    sx={{ m: 0 }}
                    control={<Checkbox checked={checked} onChange={(e) =>
                      setReadoutChecked((prev) => ({ ...prev, [m.materialId]: e.target.checked }))} />}
                    label="This medium was actually plated"
                  />
                )}
                <ExpectedAppearanceNote mediaName={m.mediaName} expectedAppearance={m.expectedAppearance} />
                <ObservationOptions
                  name={`confirmatory-observation-${m.materialId}`}
                  value={value}
                  disabled={!checked}
                  onChange={(v) => setObservations((prev) => ({ ...prev, [m.materialId]: v }))}
                  options={OBSERVATION_OPTIONS}
                />
              </Box>
            );
          })}
          <StepActionBar note={readoutComplete ? undefined : "Record an observation for every plated medium to continue."}>
            <Button
              variant="contained"
              onClick={submitReadout}
              disabled={submitting || !readoutComplete}
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              {submitting ? "Submitting..." : "Submit Observations"}
            </Button>
          </StepActionBar>
        </>
      )}

      {phase === "decision" && (
        <>
          <Alert severity="success">
            Every confirmatory medium showed growth matching the target organism's expected appearance.
          </Alert>
          <Typography variant="body2">
            Per SOP, choose whether to conclude detection from this conforming confirmatory result alone,
            or proceed with biochemical confirmation.
          </Typography>
          <StepActionBar>
            <Button
              variant="outlined" color="error" onClick={() => decide("SubmitAsDetected")}
              disabled={submitting}
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              Conclude as Detected
            </Button>
            <Button
              variant="contained" onClick={() => decide("ProceedToBiochemical")}
              disabled={submitting}
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              Proceed to Biochemical Confirmation
            </Button>
          </StepActionBar>
        </>
      )}
    </Stack>
  );
}
