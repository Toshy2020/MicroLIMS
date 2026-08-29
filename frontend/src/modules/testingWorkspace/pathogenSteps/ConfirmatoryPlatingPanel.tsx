import { useEffect, useState } from "react";
import {
  Box, Typography, Select, MenuItem, Button, Stack, Alert,
  Checkbox, FormControlLabel, RadioGroup, Radio, CircularProgress
} from "@mui/material";
import { TestWorkflowService } from "../services/TestWorkflowService";
import {
  TestWorkflowStepDto, CurrentStepResponse, PermittedConfirmatoryMediaEntry,
  GrowthObservation, AnalystDecision
} from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";
import { useAuth } from "../../../contexts/AuthContext";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";

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
  const [skipDialogOpen, setSkipDialogOpen] = useState(false);
  const [skipping, setSkipping] = useState(false);
  const { role } = useAuth();
  const canOverride = role === "SectionHead" || role === "SystemAdministrator";

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

  const incubationStartUtc = openIncubationRow?.incubationStartUtc
    ? new Date(openIncubationRow.incubationStartUtc)
    : null;

  // Incubation specifications: stepMedia is authoritative, step fallback
  const firstMedia = step?.stepMedia?.[0];
  const tempMin = (firstMedia && firstMedia.tempMin > 0) ? firstMedia.tempMin : step?.temperatureMin;
  const tempMax = (firstMedia && firstMedia.tempMax > 0) ? firstMedia.tempMax : step?.temperatureMax;
  const incMinHours = (firstMedia && (firstMedia.incubationMinHours ?? 0) > 0) ? firstMedia.incubationMinHours! : step?.incubationMinHours;
  const incMaxHours = (firstMedia && (firstMedia.incubationMaxHours ?? 0) > 0) ? firstMedia.incubationMaxHours! : step?.incubationMaxHours;

  const minReadyAt = incubationStartUtc && incMinHours != null
    ? new Date(incubationStartUtc.getTime() + incMinHours * 3600 * 1000)
    : null;

  const expectedEndAt = current?.incubationLock?.incubationEndUtc
    ? new Date(current.incubationLock.incubationEndUtc)
    : (incubationStartUtc && incMaxHours != null
        ? new Date(incubationStartUtc.getTime() + incMaxHours * 3600 * 1000)
        : null);

  const isTimeReady = (minReadyAt != null
    ? new Date() >= minReadyAt
    : (current?.incubationLock ? current.incubationLock.remainingSeconds <= 0 : true))
    || (current?.incubationLock?.minimumDurationOverridden ?? false);

  const initialPhase: Phase =
    !isStepIncubating ? "setup" :
    !isTimeReady ? "waiting" :
    "readout";

  const [phase, setPhase] = useState<Phase>(initialPhase);

  useEffect(() => {
    setPhase(initialPhase);
  }, [initialPhase]);

  const confirmSkipWait = async () => {
    setError(null);
    setSkipping(true);
    try {
      await TestWorkflowService.overrideMinimumDuration(testOrderId);
      setSkipDialogOpen(false);
      onSubmitted();
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    } finally {
      setSkipping(false);
    }
  };

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
      setReadoutMedia(permitted.map((m) => ({
        stepMediaId: m.stepMediaId,
        materialId: m.materialId,
        mediaName: m.mediaName,
        expectedAppearance: m.expectedAppearance
      })));
      setReadoutUncertain(true);
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
        stepMediaId: r.entry.stepMediaId, materialId: r.entry.materialId,
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

  if (loading) return <Typography variant="body2">Loading step configuration…</Typography>;
  if (alreadyRecordedMessage) {
    return (
      <Alert severity="info">
        {alreadyRecordedMessage}
      </Alert>
    );
  }

  return (
    <Stack spacing={1.5}>
      {error && <Alert severity="error">{error}</Alert>}
      {organism && (
        <Typography variant="body2" sx={{ mb: 1 }}>
          Target organism: <strong>{organism.name}</strong>
        </Typography>
      )}

      {phase === "setup" && (
        <Stack spacing={1.5}>
          <Alert severity="info">
            Check every medium you actually plated for this confirmatory run, choose its lot and incubator, then
            set the shared incubation duration below. Only what the step template permits can be selected - there is
            no way to add another medium here.
          </Alert>
          {permitted.map((m) => {
            const row = setupRows[m.stepMediaId] ?? {
              entry: m, checked: false, mediaLotId: "", equipmentId: "", incubators: []
            };
            return (
              <Box key={m.stepMediaId} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1, p: 1.5 }}>
                <FormControlLabel
                  control={<Checkbox checked={row.checked} onChange={(e) => toggleChecked(m.stepMediaId, e.target.checked)} />}
                  label={<Typography sx={{ fontWeight: 600 }}>{m.mediaName}</Typography>}
                />
                {m.expectedAppearance === null ? (
                  <Alert severity="warning" sx={{ mb: 1 }}>
                    Expected appearance not configured in Test Master for {m.mediaName}. This medium can still be
                    plated and read; a conforming/non-conforming judgment is unreliable until Test Master is fixed.
                  </Alert>
                ) : (
                  <Alert severity="info" sx={{ mb: 1 }}>
                    Expected appearance of a target-positive colony: <strong>{m.expectedAppearance}</strong>
                  </Alert>
                )}
                <Stack direction="row" spacing={1} flexWrap="wrap">
                  <Select
                    displayEmpty size="small" value={row.mediaLotId} disabled={!row.checked}
                    onChange={(e) => setLot(m.stepMediaId, Number(e.target.value))}
                    sx={{ minWidth: 220 }}
                  >
                    <MenuItem value=""><em>Media Lot</em></MenuItem>
                    {m.availableLots.map((l) => (
                      <MenuItem key={l.id} value={l.id}>{l.lotNumber} — expires {new Date(l.expiryDate).toLocaleDateString()}</MenuItem>
                    ))}
                  </Select>
                  <Select
                    displayEmpty size="small" value={row.equipmentId} disabled={!row.checked}
                    onChange={(e) => setEquipment(m.stepMediaId, Number(e.target.value))}
                    sx={{ minWidth: 220 }}
                  >
                    <MenuItem value=""><em>Incubator ({m.tempMin}-{m.tempMax} °C)</em></MenuItem>
                    {row.incubators.map((i) => <MenuItem key={i.id} value={i.id}>{i.name} ({i.code}) — {i.setTemperature}°C</MenuItem>)}
                  </Select>
                </Stack>
              </Box>
            );
          })}
          <Box sx={{ p: 1.5, backgroundColor: "background.default", borderRadius: 1, mb: 1 }}>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Incubation Duration (from Test Master)
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {step.incubationMinHours}–{step.incubationMaxHours} hours
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              Shared by every selected medium
            </Typography>
          </Box>
          <Stack direction="row" justifyContent="flex-end">
            <Button
              variant="contained"
              onClick={submitSetup}
              disabled={submitting}
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              {submitting ? "Starting..." : "Submit Setup"}
            </Button>
          </Stack>
        </Stack>
      )}

      {phase === "waiting" && (
        <Stack spacing={1.5}>
          <Alert severity="info">
            <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.5 }}>
              Confirmatory incubation in progress.
            </Typography>
            {tempMin != null && tempMax != null && tempMin > 0 && (
              <Typography variant="body2">
                Temperature: <strong>{tempMin}–{tempMax} °C</strong>
              </Typography>
            )}
            {incMinHours != null && incMaxHours != null && incMinHours > 0 && (
              <Typography variant="body2">
                Duration: <strong>{incMinHours}–{incMaxHours} hours</strong>
              </Typography>
            )}
            {incubationStartUtc && (
              <Typography variant="body2">
                Started: <strong>{incubationStartUtc.toLocaleString()}</strong>
              </Typography>
            )}
            {expectedEndAt && (
              <Typography variant="body2">
                Expected reading: <strong>{expectedEndAt.toLocaleString()}</strong>
              </Typography>
            )}
          </Alert>

          {minReadyAt && (
            <Alert severity="warning">
              Not ready yet — available from <strong>{minReadyAt.toLocaleString()}</strong>. Confirmatory plate observation entry will unlock once the incubation period has completed.
            </Alert>
          )}

          <Stack direction="row" justifyContent="space-between" alignItems="center">
            {canOverride ? (
              <Button variant="outlined" color="warning" onClick={() => setSkipDialogOpen(true)} disabled={skipping}>
                Skip Wait
              </Button>
            ) : <span />}
            <Button variant="contained" disabled>
              Incubation In Progress
            </Button>
          </Stack>
        </Stack>
      )}

      {phase === "readout" && (
        <Stack spacing={1.5}>
          {readoutUncertain ? (
            <Alert severity="warning">
              Confirmatory media for this step were already selected in a previous session. Confirm
              each medium before recording its observation.
            </Alert>
          ) : (
            <Alert severity="info">Read each plate and record what you observe against the expected appearance.</Alert>
          )}
          {readoutMedia.map((m) => {
            const checked = isReadoutActive(m.materialId);
            const value = observations[m.materialId] ?? "";
            return (
              <Box key={m.materialId} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1, p: 1.5 }}>
                {readoutUncertain && (
                  <FormControlLabel
                    control={<Checkbox checked={checked} onChange={(e) =>
                      setReadoutChecked((prev) => ({ ...prev, [m.materialId]: e.target.checked }))} />}
                    label="This medium was actually plated"
                  />
                )}
                <Typography sx={{ fontWeight: 600 }}>{m.mediaName}</Typography>
                {m.expectedAppearance === null ? (
                  <Alert severity="warning" sx={{ my: 1 }}>
                    Expected appearance not configured in Test Master for {m.mediaName}. An observation can still be
                    recorded, but a conforming/non-conforming judgment is unreliable until Test Master is fixed.
                  </Alert>
                ) : (
                  <Alert severity="info" sx={{ my: 1 }}>
                    Expected appearance of a target-positive colony: <strong>{m.expectedAppearance}</strong>
                  </Alert>
                )}
                <RadioGroup
                  value={value}
                  onChange={(e) => setObservations((prev) => ({ ...prev, [m.materialId]: e.target.value as GrowthObservation }))}
                >
                  <FormControlLabel value="NoGrowth" control={<Radio />} disabled={!checked} label="No growth" />
                  <FormControlLabel
                    value="GrowthNonConforming" control={<Radio />} disabled={!checked}
                    label="Growth present, does not match expected appearance"
                  />
                  <FormControlLabel
                    value="GrowthConforming" control={<Radio />} disabled={!checked}
                    label="Growth matching expected appearance — conforming"
                  />
                </RadioGroup>
              </Box>
            );
          })}
          <Stack direction="row" justifyContent="flex-end">
            <Button
              variant="contained"
              onClick={submitReadout}
              disabled={submitting}
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              {submitting ? "Submitting..." : "Submit Observations"}
            </Button>
          </Stack>
        </Stack>
      )}

      {phase === "decision" && (
        <Stack spacing={1.5}>
          <Alert severity="success">
            Every confirmatory medium showed growth matching the target organism's expected appearance.
          </Alert>
          <Typography variant="body2">
            Per SOP, choose whether to conclude detection from this conforming confirmatory result alone,
            or proceed with biochemical confirmation.
          </Typography>
          <Stack direction="row" spacing={2} justifyContent="flex-end">
            <Button
              variant="contained" color="error" onClick={() => decide("SubmitAsDetected")}
              disabled={submitting}
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              Conclude as Detected
            </Button>
            <Button
              variant="outlined" onClick={() => decide("ProceedToBiochemical")}
              disabled={submitting}
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              Proceed to Biochemical Confirmation
            </Button>
          </Stack>
        </Stack>
      )}

      <ConfirmationDialog
        open={skipDialogOpen}
        message="Skip the remaining minimum incubation wait time for this step? This bypasses the wait only — the recorded incubation window is not changed."
        onConfirm={confirmSkipWait}
        onCancel={() => setSkipDialogOpen(false)}
      />
    </Stack>
  );
}
