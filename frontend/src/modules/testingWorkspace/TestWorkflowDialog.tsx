import { useEffect, useState } from "react";
import { Box, Typography, TextField, Button, Stack, Alert, Select, MenuItem, IconButton, RadioGroup, FormControlLabel, Radio } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import CloseIcon from "@mui/icons-material/Close";
import { StatusBadge } from "../../components/StatusBadge";
import { masterDataOptions, mediaClassLabel } from "../../services/masterDataOptions";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { LocationResultGridDialog } from "./LocationResultGridDialog";
import { PathogenLocationResultGridDialog } from "./PathogenLocationResultGridDialog";

interface Props { testOrderId: number; testCode: string; category: string; displayName: string; }

type Phase = "loading" | "select-media" | "awaiting-result" | "enter-result" | "step-complete" | "all-complete";

// Universal workflow dialog for any TestDefinition with a configured
// step template (WorkflowType + TestWorkflowStep) - reads
// GET current-step to find out what phase to render, instead of
// routing by test code the way the old Pathogen/CountTest dialogs did.
// Nothing here is specific to any test - the step template drives the
// media-class filter, locked temperature/duration, and which result
// shape (CountTest/Observation/DualPlate) to collect.
export function TestWorkflowDialog({ testOrderId, testCode, category, displayName }: Props) {
  const isEmOrAfterCleaning = category === "EnvironmentalMonitoring" || category === "AfterCleaning";
  const [phase, setPhase] = useState<Phase>("loading");
  const [current, setCurrent] = useState<any | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showLocationGrid, setShowLocationGrid] = useState(false);

  const [mediaId, setMediaId] = useState<number | "">("");
  const [incubatorId, setIncubatorId] = useState<number | "">("");
  const [releasedMedia, setReleasedMedia] = useState<any[]>([]);
  const [incubators, setIncubators] = useState<any[]>([]);

  const [readings, setReadings] = useState<string[]>([""]);
  const [dilutionFactor, setDilutionFactor] = useState("1");
  const [growthObserved, setGrowthObserved] = useState<"yes" | "no" | "">("");
  const [plate1Growth, setPlate1Growth] = useState<"yes" | "no" | "">("");
  const [plate2Growth, setPlate2Growth] = useState<"yes" | "no" | "">("");

  const [lastOutcome, setLastOutcome] = useState<any | null>(null);

  const load = async () => {
    setError(null);
    try {
      const data = await TestWorkflowService.getCurrentStep(testOrderId);
      setCurrent(data);
      setMediaId(""); setIncubatorId("");
      setReadings([""]); setDilutionFactor("1");
      setGrowthObserved(""); setPlate1Growth(""); setPlate2Growth("");
      setPhase(data.allStepsComplete ? "all-complete" : data.incubation ? "awaiting-result" : "select-media");
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not load this test's workflow.");
    }
  };

  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [testOrderId]);

  useEffect(() => {
    if (phase !== "select-media") return;
    masterDataOptions.getReleasedMedia().then(setReleasedMedia);
    masterDataOptions.getEquipment("Incubator").then(setIncubators);
  }, [phase]);

  const step = current?.step;
  const classMedia = releasedMedia.filter((m) => m.mediaTypeId === step?.mediaTypeId || m.mediaType?.id === step?.mediaTypeId);
  // Some steps are named after the exact material they require (e.g.
  // "MSA", "TSB", "RVS") - when that's the case, narrow down to just
  // that material instead of every lot of the step's (coarse, 4-value)
  // MediaType class. Other steps are named after a procedure instead
  // ("Detection", "XLD_TSI", "CountIncubation") with no single matching
  // material, so there's nothing to narrow - fall back to the full
  // class list exactly as before.
  const materialMedia = step ? classMedia.filter((m) => (m.material?.code ?? "").toUpperCase() === step.stepName.toUpperCase()) : [];
  const matchingMedia = materialMedia.length > 0 ? materialMedia : classMedia;
  // Only offer incubators whose set point actually falls within this
  // step's required temperature range - one out of calibration/set to
  // the wrong temperature for this test shouldn't even be selectable.
  const matchingIncubators = incubators.filter((i) =>
    i.setPointTemperature != null && step && i.setPointTemperature >= step.temperatureMin && i.setPointTemperature <= step.temperatureMax
  );

  // Minimum-duration gate, mirrored from the server (TestWorkflowEngine.
  // RequireMinimumDurationElapsed) so the button disables itself instead
  // of just bouncing off a server error - the server still enforces it
  // as the source of truth.
  const minReadyAt = current?.incubation && step
    ? new Date(new Date(current.incubation.startedAt).getTime() + step.incubationMinHours * 3600 * 1000)
    : null;
  const isTimeReady = !minReadyAt || new Date() >= minReadyAt;

  const startIncubation = async () => {
    setError(null);
    if (!mediaId || !incubatorId) return;
    try {
      await TestWorkflowService.selectMedia(testOrderId, step.stepName, Number(mediaId), Number(incubatorId));
      await load();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not start incubation for this step.");
    }
  };

  const advanceIncubationWindow = async () => {
    setError(null);
    try {
      await TestWorkflowService.closeIncubationWindow(testOrderId);
      await load();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not close this incubation window.");
    }
  };

  const updateReading = (i: number, value: string) => setReadings((r) => r.map((v, idx) => (idx === i ? value : v)));
  const addReading = () => setReadings((r) => [...r, ""]);
  const removeReading = (i: number) => setReadings((r) => (r.length > 1 ? r.filter((_, idx) => idx !== i) : r));

  const submitResult = async () => {
    setError(null);
    try {
      let payload: Record<string, unknown>;
      if (current.workflowType === "CountTest") {
        const parsed = readings.map(Number).filter((n) => !Number.isNaN(n));
        if (parsed.length === 0) { setError("Enter at least one plate reading."); return; }
        payload = { stepName: step.stepName, plateReadings: parsed, dilutionFactor: Number(dilutionFactor) };
      } else if (step.isDualPlate) {
        if (!plate1Growth || !plate2Growth) { setError("Record both plates' growth observation."); return; }
        payload = { stepName: step.stepName, plate1GrowthObserved: plate1Growth === "yes", plate2GrowthObserved: plate2Growth === "yes" };
      } else {
        if (!growthObserved) { setError("Record whether growth was observed."); return; }
        payload = { stepName: step.stepName, growthObserved: growthObserved === "yes" };
      }

      const result = await TestWorkflowService.recordResult(testOrderId, payload);
      setLastOutcome(result);
      if (result.allStepsComplete) {
        setPhase("all-complete");
      } else {
        setPhase("step-complete");
      }
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not record this result.");
    }
  };

  if (phase === "loading" || !current) {
    return (
      <Box sx={{ py: 4 }}>
        {error ? <Alert severity="error">{error}</Alert> : <LoadingSpinner />}
      </Box>
    );
  }

  if (phase === "all-complete") {
    const outcome = lastOutcome?.finalResult ?? current.finalResult;
    return (
      <Box>
        <Alert severity={outcome === "Detected" ? "error" : "success"} sx={{ mb: 1 }}>
          Final result: <strong>{outcome}</strong>
        </Alert>
        <StatusBadge status="Ready" />
      </Box>
    );
  }

  return (
    <Box>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Typography sx={{ fontWeight: 700, mb: 0.5 }}>
        Step {step.stepOrder}: {step.stepName}
        {step.isFinalStep && <Typography component="span" variant="caption" color="text.secondary"> — determines the final result</Typography>}
      </Typography>

      {phase === "select-media" && (
        <Stack spacing={1.5}>
          <Typography variant="body2" color="text.secondary">
            Requires {mediaClassLabel(step.mediaType?.class)} media.
          </Typography>
          <Select displayEmpty size="small" value={mediaId} onChange={(e) => setMediaId(Number(e.target.value))}>
            <MenuItem value=""><em>Media Batch ({mediaClassLabel(step.mediaType?.class)} lots only)</em></MenuItem>
            {matchingMedia.map((m) => <MenuItem key={m.id} value={m.id}>{m.lotNumber} — expires {new Date(m.expiryDate).toLocaleDateString()}</MenuItem>)}
          </Select>
          <Select displayEmpty size="small" value={incubatorId} onChange={(e) => setIncubatorId(Number(e.target.value))}>
            <MenuItem value=""><em>Incubator ({step.temperatureMin}-{step.temperatureMax} °C)</em></MenuItem>
            {matchingIncubators.map((i) => <MenuItem key={i.id} value={i.id}>{i.name} ({i.code}) — {i.setPointTemperature}°C</MenuItem>)}
          </Select>
          {matchingIncubators.length === 0 && (
            <Alert severity="warning">No incubator is set to {step.temperatureMin}-{step.temperatureMax} °C for this step.</Alert>
          )}
          {mediaId && incubatorId && (
            <Typography variant="body2" color="text.secondary">
              Required Temperature: <strong>{step.temperatureMin}-{step.temperatureMax} °C</strong>
              {" — "}Incubation Period: <strong>{step.incubationMinHours}-{step.incubationMaxHours} hours</strong>
            </Typography>
          )}
          <Stack direction="row" justifyContent="flex-end">
            <Button variant="contained" disabled={!mediaId || !incubatorId} onClick={startIncubation}>Start Incubation</Button>
          </Stack>
        </Stack>
      )}

      {phase === "awaiting-result" && current.incubation && (
        <Stack spacing={1.5}>
          <Alert severity="info">Incubation in progress.</Alert>
          <Typography variant="body2">Temperature: <strong>{current.incubation.temperature}</strong></Typography>
          <Typography variant="body2">Duration: <strong>{current.incubation.duration}</strong></Typography>
          <Typography variant="body2">Expected reading: <strong>{new Date(current.incubation.expectedReadingAt).toLocaleString()}</strong></Typography>
          {!isTimeReady && minReadyAt && (
            <Alert severity="warning">Not ready yet - available from {minReadyAt.toLocaleString()}.</Alert>
          )}
          <Stack direction="row" justifyContent="flex-end">
            {isEmOrAfterCleaning && step && !step.isFinalStep ? (
              <Button variant="contained" disabled={!isTimeReady} onClick={advanceIncubationWindow}>Advance to Next Incubation Window</Button>
            ) : isEmOrAfterCleaning ? (
              <Button variant="contained" disabled={!isTimeReady} onClick={() => setShowLocationGrid(true)}>Record Results</Button>
            ) : (
              <Button variant="contained" disabled={!isTimeReady} onClick={() => setPhase("enter-result")}>Record Result</Button>
            )}
          </Stack>
        </Stack>
      )}

      {current.workflowType === "CountTest" ? (
        <LocationResultGridDialog
          open={showLocationGrid}
          testOrderId={testOrderId}
          testCode={testCode}
          displayName={displayName}
          minReadyAt={minReadyAt}
          onClose={() => setShowLocationGrid(false)}
          onSubmitted={() => { setShowLocationGrid(false); load(); }}
        />
      ) : (
        <PathogenLocationResultGridDialog
          open={showLocationGrid}
          testOrderId={testOrderId}
          testCode={testCode}
          displayName={displayName}
          isDualPlate={!!step?.isDualPlate}
          minReadyAt={minReadyAt}
          onClose={() => setShowLocationGrid(false)}
          onSubmitted={() => { setShowLocationGrid(false); load(); }}
        />
      )}

      {phase === "enter-result" && (
        <Stack spacing={1.5}>
          {current.workflowType === "CountTest" && (
            <>
              <Stack spacing={1}>
                {readings.map((r, i) => (
                  <Stack direction="row" spacing={1} key={i} alignItems="center">
                    <TextField size="small" type="number" label={`Plate ${i + 1}`} value={r} onChange={(e) => updateReading(i, e.target.value)} />
                    <IconButton size="small" onClick={() => removeReading(i)}><CloseIcon fontSize="small" /></IconButton>
                  </Stack>
                ))}
              </Stack>
              <Button startIcon={<AddIcon />} onClick={addReading} sx={{ alignSelf: "flex-start" }}>Add Plate</Button>
              <TextField size="small" type="number" label="Dilution Factor" value={dilutionFactor} onChange={(e) => setDilutionFactor(e.target.value)} sx={{ maxWidth: 200 }} />
            </>
          )}

          {current.workflowType !== "CountTest" && !step.isDualPlate && (
            <RadioGroup value={growthObserved} onChange={(e) => setGrowthObserved(e.target.value as "yes" | "no")}>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>Growth Observed?</Typography>
              <FormControlLabel value="yes" control={<Radio />} label="Yes" />
              <FormControlLabel value="no" control={<Radio />} label="No" />
            </RadioGroup>
          )}

          {step.isDualPlate && (
            <Stack spacing={2}>
              <Box>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>Plate 1 (e.g. XLD)</Typography>
                <RadioGroup row value={plate1Growth} onChange={(e) => setPlate1Growth(e.target.value as "yes" | "no")}>
                  <FormControlLabel value="yes" control={<Radio />} label="Growth" />
                  <FormControlLabel value="no" control={<Radio />} label="No Growth" />
                </RadioGroup>
              </Box>
              <Box>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>Plate 2 (e.g. TSI)</Typography>
                <RadioGroup row value={plate2Growth} onChange={(e) => setPlate2Growth(e.target.value as "yes" | "no")}>
                  <FormControlLabel value="yes" control={<Radio />} label="Growth" />
                  <FormControlLabel value="no" control={<Radio />} label="No Growth" />
                </RadioGroup>
              </Box>
              {plate1Growth && plate2Growth && plate1Growth !== plate2Growth && (
                <Alert severity="warning">Inconclusive - the two plates disagree. Recording this will require a retest.</Alert>
              )}
            </Stack>
          )}

          {!isTimeReady && minReadyAt && (
            <Alert severity="warning">Results cannot be submitted before {minReadyAt.toLocaleString()}.</Alert>
          )}
          <Stack direction="row" justifyContent="flex-end">
            <Button variant="contained" disabled={!isTimeReady} onClick={submitResult}>
              {current.workflowType === "CountTest" ? "Calculate" : "Submit"}
            </Button>
          </Stack>
        </Stack>
      )}

      {phase === "step-complete" && lastOutcome && (
        <Stack spacing={1.5}>
          {lastOutcome.isDefinitive ? (
            <Alert severity="success">
              Step recorded: <strong>{lastOutcome.outcomeSummary}</strong>
              {lastOutcome.average != null && ` — Average ${lastOutcome.average}, Calculated ${lastOutcome.calculatedResult}, Status ${lastOutcome.status}`}
            </Alert>
          ) : (
            <Alert severity="warning">
              {lastOutcome.outcomeSummary} - this step must be repeated with a fresh media lot.
            </Alert>
          )}
          <Stack direction="row" justifyContent="flex-end">
            <Button variant="contained" onClick={load}>{lastOutcome.isDefinitive ? "Proceed to Next Step" : "Retry This Step"}</Button>
          </Stack>
        </Stack>
      )}
    </Box>
  );
}
