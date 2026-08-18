import { useEffect, useState } from "react";
import {
  Typography, Select, MenuItem, TextField, Button, Stack, Alert,
  RadioGroup, FormControlLabel, Radio, Box, CircularProgress
} from "@mui/material";
import { TestWorkflowService } from "../services/TestWorkflowService";
import {
  TestWorkflowStepDto, CurrentStepResponse, PermittedConfirmatoryMediaEntry, GrowthObservation
} from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";

interface Props {
  testOrderId: number;
  step: TestWorkflowStepDto;
  current: CurrentStepResponse;
  onSubmitted: () => void;
}

export function SelectivePlatingPanel({ testOrderId, step, current, onSubmitted }: Props) {
  const [medium, setMedium] = useState<PermittedConfirmatoryMediaEntry | null>(null);
  const [incubators, setIncubators] = useState<{ id: number; name: string; code: string; setTemperature: number }[]>([]);
  const [mediaLotId, setMediaLotId] = useState<number | "">("");
  const [equipmentId, setEquipmentId] = useState<number | "">("");
  const [observation, setObservation] = useState<GrowthObservation | "">("");
  const [appearanceNote, setAppearanceNote] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);

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

  // Derive timestamps from incubationStartUtc
  const incubationStartUtc = openIncubationRow?.incubationStartUtc
    ? new Date(openIncubationRow.incubationStartUtc)
    : null;

  // Available from: start + minHours
  const minReadyAt = incubationStartUtc && step.incubationMinHours != null
    ? new Date(incubationStartUtc.getTime() + step.incubationMinHours * 3600 * 1000)
    : null;

  // Expected reading end: from incubationLock or start + maxHours
  const expectedEndAt = current?.incubationLock?.incubationEndUtc
    ? new Date(current.incubationLock.incubationEndUtc)
    : (incubationStartUtc && step.incubationMaxHours != null
        ? new Date(incubationStartUtc.getTime() + step.incubationMaxHours * 3600 * 1000)
        : null);

  const isTimeReady = minReadyAt != null && new Date() >= minReadyAt;

  const phase: "setup" | "waiting" | "readout" =
    !isStepIncubating ? "setup" :
    !isTimeReady ? "waiting" :
    "readout";

  // Phase 1: Start incubation
  const handleStartIncubation = async () => {
    setError(null);
    if (!medium || !mediaLotId || !equipmentId) {
      setError("Select a media lot and an incubator.");
      return;
    }
    setSubmitting(true);
    try {
      await TestWorkflowService.startSelectivePlatingIncubation(
        testOrderId, step.stepName, Number(mediaLotId), Number(equipmentId)
      );
      onSubmitted();
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
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
        appearanceNote.trim() || undefined
      );
      onSubmitted();
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <Typography variant="body2">Loading step configuration…</Typography>;
  if (error && !medium) return <Alert severity="error">{error}</Alert>;
  if (!medium) return <Alert severity="error">This step has no assigned medium configured in Test Master.</Alert>;

  return (
    <Stack spacing={1.5}>
      {error && <Alert severity="error">{error}</Alert>}

      <Typography variant="body2">Medium: <strong>{medium.mediaName}</strong></Typography>
      {medium.expectedAppearance === null ? (
        <Alert severity="warning">
          Expected appearance not configured in Test Master. Growth vs. no-growth can still be recorded,
          but a conforming/non-conforming judgment is unreliable until Test Master is fixed.
        </Alert>
      ) : (
        <Alert severity="info">
          Expected appearance of a target-positive colony: <strong>{medium.expectedAppearance}</strong>
        </Alert>
      )}

      {/* Phase 1: Setup (Start Incubation) */}
      {phase === "setup" && (
        <>
          <Select
            displayEmpty
            size="small"
            value={mediaLotId}
            onChange={(e) => setMediaLotId(Number(e.target.value))}
          >
            <MenuItem value=""><em>Media Lot</em></MenuItem>
            {medium.availableLots.map((l) => (
              <MenuItem key={l.id} value={l.id}>
                {l.lotNumber} — expires {new Date(l.expiryDate).toLocaleDateString()}
              </MenuItem>
            ))}
          </Select>

          <Select
            displayEmpty
            size="small"
            value={equipmentId}
            onChange={(e) => setEquipmentId(Number(e.target.value))}
          >
            <MenuItem value=""><em>Incubator ({medium.tempMin}-{medium.tempMax} °C)</em></MenuItem>
            {incubators.map((i) => (
              <MenuItem key={i.id} value={i.id}>
                {i.name} ({i.code}) — {i.setTemperature}°C
              </MenuItem>
            ))}
          </Select>

          {/* Read-only template duration */}
          <Box sx={{ p: 1.5, backgroundColor: "#f3f4f6", borderRadius: 1 }}>
            <Typography variant="caption" sx={{ color: "#6b7280", display: "block" }}>
              Incubation Duration (from Test Master)
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {step.incubationMinHours}–{step.incubationMaxHours} hours
            </Typography>
          </Box>

          <Stack direction="row" justifyContent="flex-end">
            <Button
              variant="contained"
              onClick={handleStartIncubation}
              disabled={!mediaLotId || !equipmentId || submitting}
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              {submitting ? "Starting..." : "Start Incubation"}
            </Button>
          </Stack>
        </>
      )}

      {/* Phase 2: Waiting (Incubation in Progress, not ready yet) */}
      {phase === "waiting" && (
        <>
          <Alert severity="info">
            <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.5 }}>
              Incubation in progress.
            </Typography>
            {step.temperatureMin != null && step.temperatureMax != null && (
              <Typography variant="body2">
                Temperature: <strong>{step.temperatureMin}–{step.temperatureMax} °C</strong>
              </Typography>
            )}
            <Typography variant="body2">
              Duration: <strong>{step.incubationMinHours}–{step.incubationMaxHours} hours</strong>
            </Typography>
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
              Not ready yet — available from {minReadyAt.toLocaleString()}.
            </Alert>
          )}

          <Stack direction="row" justifyContent="flex-end">
            <Button variant="contained" disabled>
              Record Observation
            </Button>
          </Stack>
        </>
      )}

      {/* Phase 3: Readout (Ready for observation) */}
      {phase === "readout" && (
        <>
          <Alert severity="success">
            <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.5 }}>
              ✓ Incubation complete — ready for observation.
            </Typography>
            {incubationStartUtc && (
              <Typography variant="body2">
                Started: <strong>{incubationStartUtc.toLocaleString()}</strong>
              </Typography>
            )}
            {minReadyAt && (
              <Typography variant="body2">
                Available from: <strong>{minReadyAt.toLocaleString()}</strong>
              </Typography>
            )}
          </Alert>

          <Typography variant="subtitle2">Observation</Typography>
          <RadioGroup
            value={observation}
            onChange={(e) => setObservation(e.target.value as GrowthObservation)}
          >
            <FormControlLabel
              value="NoGrowth"
              control={<Radio />}
              label="No growth — target absent"
            />
            <FormControlLabel
              value="GrowthNonConforming"
              control={<Radio />}
              label="Growth present, does not match expected appearance — target absent"
            />
            <FormControlLabel
              value="GrowthConforming"
              control={<Radio />}
              label="Growth matching expected appearance — presumptive positive"
            />
          </RadioGroup>

          <TextField
            size="small"
            multiline
            minRows={2}
            label="Observed Appearance Note (optional)"
            value={appearanceNote}
            onChange={(e) => setAppearanceNote(e.target.value)}
            helperText="Optional notes on colony characteristics observed on the plate."
          />

          <Stack direction="row" justifyContent="flex-end">
            <Button
              variant="contained"
              onClick={handleSubmitObservation}
              disabled={!observation || submitting}
              startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              {submitting ? "Recording..." : "Record Observation"}
            </Button>
          </Stack>
        </>
      )}
    </Stack>
  );
}
