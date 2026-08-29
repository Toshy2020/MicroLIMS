import { useEffect, useState, useMemo } from "react";
import { Box, Typography, TextField, Button, Stack, Alert, Select, MenuItem, IconButton, useTheme } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import CloseIcon from "@mui/icons-material/Close";
import { StatusBadge } from "../../components/StatusBadge";
import { masterDataOptions } from "../../services/masterDataOptions";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { LocationResultGridDialog } from "./LocationResultGridDialog";
import { PathogenLocationResultGridDialog } from "./PathogenLocationResultGridDialog";
import { WaterLocationResultGridDialog } from "./WaterLocationResultGridDialog";
import { PathogenStepDialog } from "./PathogenStepDialog";
import { useAuth } from "../../contexts/AuthContext";
import { ConfirmationDialog } from "../../components/ConfirmationDialog";

interface Props { testOrderId: number; testCode: string; category: string; displayName: string; onClose?: () => void; }

type Phase = "loading" | "select-media" | "awaiting-result" | "transfer-stage-2" | "enter-result" | "step-complete" | "all-complete";

// Read-only progress strip above the phase content - one chip per step
// in the template, sourced from current-step's allSteps/completedSteps/
// step fields. No click actions.
function StepChainStrip({ current }: { current: any }) {
  const theme = useTheme();
  const completedByOrder = new Map<number, any>((current.completedSteps ?? []).map((s: any) => [s.stepOrder, s]));
  const currentOrder = current.step?.stepOrder ?? null;

  return (
    <Stack direction="row" spacing={1} sx={{ mb: 2, flexWrap: "wrap" }}>
      {(current.allSteps ?? []).map((s: any) => {
        const done = completedByOrder.get(s.stepOrder);
        const isCurrent = s.stepOrder === currentOrder;
        const isInconclusive = done?.outcome?.startsWith("Inconclusive");

        let tone = theme.custom.status.pending;
        let label = s.stepName;
        if (done) {
          label = `${s.stepName}: ${done.outcome}`;
          tone = isInconclusive ? theme.custom.status.detected : theme.custom.status.notDetected;
        } else if (isCurrent) {
          label = `${s.stepName}: In progress`;
          tone = theme.custom.status.info;
        }

        return (
          <Box key={s.stepOrder} sx={{ px: 1.25, py: 0.5, borderRadius: 999, fontSize: 12, fontWeight: 600, bgcolor: tone.bg, color: tone.text, border: `1px solid ${tone.border}` }}>
            {done ? (isInconclusive ? "✗ " : "✓ ") : ""}{label}
          </Box>
        );
      })}
    </Stack>
  );
}

// Universal workflow dialog for any TestDefinition with a configured
// step template (WorkflowType + TestWorkflowStep) - reads
// GET current-step to find out what phase to render, instead of
// routing by test code the way the old Pathogen/CountTest dialogs did.
// Nothing here is specific to any test - the step template drives the
// media-class filter and locked temperature/duration.
//
// CountTest keeps its own select-media/awaiting-result/enter-result
// phases below. Any other workflow (the five-stage pathogen chain) is
// handed off entirely to PathogenStepDialog, except for EM/AfterCleaning
// samples, which still incubate through this component's phases and
// only hand off to LocationResultGridDialog/PathogenLocationResultGrid-
// Dialog for their per-location batch result entry, exactly as today.
export function TestWorkflowDialog({ testOrderId, testCode, category, displayName, onClose }: Props) {
  const theme = useTheme();
  const isEmOrAfterCleaning = category === "EnvironmentalMonitoring" || category === "AfterCleaning";
  const [phase, setPhase] = useState<Phase>("loading");
  const [current, setCurrent] = useState<any | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showLocationGrid, setShowLocationGrid] = useState(false);

  const [mediaId, setMediaId] = useState<number | "">("");
  const [incubatorId, setIncubatorId] = useState<number | "">("");
  const [stage2IncubatorId, setStage2IncubatorId] = useState<number | "">("");
  const [releasedMedia, setReleasedMedia] = useState<any[]>([]);
  const [incubators, setIncubators] = useState<any[]>([]);

  const [readings, setReadings] = useState<string[]>(["", ""]);
  const [dilutionFactor, setDilutionFactor] = useState("1");

  const [lastOutcome, setLastOutcome] = useState<any | null>(null);

  const load = async () => {
    setError(null);
    try {
      const data = await TestWorkflowService.getCurrentStep(testOrderId);
      setCurrent(data);
      setMediaId(""); setIncubatorId(""); setStage2IncubatorId("");
      setReadings(["", ""]); setDilutionFactor("1");
      setPhase(data.allStepsComplete ? "all-complete" : data.incubationLock != null ? "awaiting-result" : "select-media");
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not load this test's workflow.");
    }
  };

  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [testOrderId]);

  useEffect(() => {
    if (phase !== "select-media" && phase !== "transfer-stage-2") return;
    if (phase === "select-media") {
      masterDataOptions.getReleasedMedia().then(setReleasedMedia);
    }
    masterDataOptions.getEquipment("Incubator").then(setIncubators);
  }, [phase]);

  const step = current?.step;
  const isTwoStageTransfer = !!step?.requiresIncubationTransfer;
  const stage2Config = (step?.incubationStages ?? []).find((x: any) => x.stageNumber === 2);

  // Permitted materials come directly from the step's own configured
  // StepMedia (Test Master) - the same set TestWorkflowEngine.SelectMediaAsync
  // enforces server-side, so the picker can't offer a lot the backend
  // would reject anyway. Replaces the old MediaType-class filter plus a
  // step-name-matching fallback, both of which relied on fields that no
  // longer exist on the step/lot after the Media Configuration Migration.
  const permittedMaterialIds = new Set((step?.stepMedia ?? []).map((m: any) => m.materialId));
  const matchingMedia = releasedMedia.filter((m) => permittedMaterialIds.has(m.materialId));
  const permittedMaterialNames = (step?.stepMedia ?? []).map((m: any) => m.materialName).join(" or ");

  const selectedMediaLot = releasedMedia.find((m) => m.id === mediaId);
  const selectedStepMedium = (step?.stepMedia ?? []).find((m: any) => m.materialId === selectedMediaLot?.materialId);
  const activeStage1Medium = selectedStepMedium ?? ((step?.stepMedia?.length === 1) ? step.stepMedia[0] : null);

  const stage1TempMin = activeStage1Medium?.tempMin ?? (step?.stepMedia?.length > 0 ? Math.min(...step.stepMedia.map((m: any) => m.tempMin)) : (step?.temperatureMin ?? 0));
  const stage1TempMax = activeStage1Medium?.tempMax ?? (step?.stepMedia?.length > 0 ? Math.max(...step.stepMedia.map((m: any) => m.tempMax)) : (step?.temperatureMax ?? 0));
  const stage1IncMinHours = activeStage1Medium?.incubationMinHours ?? (step?.stepMedia?.length > 0 ? Math.min(...step.stepMedia.map((m: any) => m.incubationMinHours)) : (step?.incubationMinHours ?? 0));
  const stage1IncMaxHours = activeStage1Medium?.incubationMaxHours ?? (step?.stepMedia?.length > 0 ? Math.max(...step.stepMedia.map((m: any) => m.incubationMaxHours)) : (step?.incubationMaxHours ?? 0));

  // Only offer incubators whose set point actually falls within this
  // step's required temperature range - one out of calibration/set to
  // the wrong temperature for this test shouldn't even be selectable.
  const matchingIncubators = incubators.filter((i) =>
    i.setPointTemperature != null && i.setPointTemperature >= stage1TempMin && i.setPointTemperature <= stage1TempMax
  );

  const matchingStage2Incubators = incubators.filter((i) =>
    i.setPointTemperature != null && stage2Config && i.setPointTemperature >= stage2Config.tempMin && i.setPointTemperature <= stage2Config.tempMax
  );

  // current-step never serialized a top-level "incubation" object - only
  // incubationLock (isLocked/incubationEndUtc/remainingSeconds/stageNumber)
  // and previousSteps (one row per Incubation, including the still-open
  // one, tagged status: "Incubating"). The open row's own start time comes
  // from there instead.
  const openIncubationRow = step
    ? (current?.previousSteps ?? []).find((p: any) => p.stepName === step.stepName && p.status === "Incubating")
    : null;

  const currentStageNumber = current?.incubationLock?.stageNumber ?? openIncubationRow?.stageNumber ?? 1;
  const isStage2 = currentStageNumber === 2;

  const activeTempMin = isStage2 && stage2Config ? stage2Config.tempMin : stage1TempMin;
  const activeTempMax = isStage2 && stage2Config ? stage2Config.tempMax : stage1TempMax;
  const activeIncMinHours = isStage2 && stage2Config ? stage2Config.incubationMinHours : stage1IncMinHours;
  const activeIncMaxHours = isStage2 && stage2Config ? stage2Config.incubationMaxHours : stage1IncMaxHours;

  // Minimum-duration gate, mirrored from the server (TestWorkflowEngine.
  // RequireMinimumDurationElapsed) so the button disables itself instead
  // of just bouncing off a server error - the server still enforces it
  // as the source of truth.
  const minReadyAt = openIncubationRow && activeIncMinHours != null
    ? new Date(new Date(openIncubationRow.incubationStartUtc).getTime() + activeIncMinHours * 3600 * 1000)
    : null;
  const minimumDurationOverridden = current?.incubationLock?.minimumDurationOverridden ?? false;
  const isTimeReady = !minReadyAt || new Date() >= minReadyAt || minimumDurationOverridden;

  const { role } = useAuth();
  const canOverrideWait = role === "SectionHead" || role === "SystemAdministrator";
  const [skipDialogOpen, setSkipDialogOpen] = useState(false);
  const [skipping, setSkipping] = useState(false);
  const confirmSkipWait = async () => {
    setError(null);
    setSkipping(true);
    try {
      await TestWorkflowService.overrideMinimumDuration(testOrderId);
      setSkipDialogOpen(false);
      await load();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not skip the wait.");
    } finally {
      setSkipping(false);
    }
  };

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

  const startStage2Incubation = async () => {
    setError(null);
    if (!stage2IncubatorId) return;
    try {
      await TestWorkflowService.startStage2Incubation(testOrderId, step.stepName, Number(stage2IncubatorId));
      setStage2IncubatorId("");
      await load();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not start stage 2 incubation.");
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

  const isNonNumeric = (val: string) =>
    ["tntc", "uncountable"].includes(val.toLowerCase().trim());

  const isDirectCount = ["Water", "EnvironmentalMonitoring", "AfterCleaning"]
    .includes(current?.sampleContext?.sampleType ?? "");

  const cfuUnit = current?.sampleContext?.cfuUnit ?? "CFU/mL";

  const liveResult = useMemo(() => {
    const hasNonNumeric = readings.some((r) => isNonNumeric(r));
    if (hasNonNumeric) {
      const val = readings.find((r) => isNonNumeric(r))?.toUpperCase();
      return { display: val, isNonNumeric: true };
    }
    const numericVals = readings
      .filter((r) => r.trim() !== "")
      .map((r) => parseFloat(r))
      .filter((n) => !isNaN(n));
    if (numericVals.length === 0) return null;
    const df = parseFloat(dilutionFactor) || 1;
    const avg = numericVals.reduce((a, b) => a + b, 0) / numericVals.length;
    const finalCfu = avg * df;
    const lowerLimit = df;
    const formatted = finalCfu % 1 === 0 ? finalCfu.toFixed(0) : finalCfu.toFixed(1);
    return {
      display: finalCfu < lowerLimit ? `< ${lowerLimit} ${cfuUnit}` : `${formatted} ${cfuUnit}`,
      isNonNumeric: false
    };
  }, [readings, dilutionFactor, cfuUnit]);

  const submitResult = async () => {
    setError(null);
    try {
      if (readings.every((r) => r.trim() === "")) {
        setError("Enter at least one plate reading.");
        return;
      }
      const payload = {
        stepName: step.stepName,
        rawPlateReadings: readings.filter((r) => r.trim() !== ""),
        dilutionFactor: Number(dilutionFactor) || 1
      };

      const result = await TestWorkflowService.recordCountResult(testOrderId, payload);
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

  // Water's TAMC-Water TestOrder is also SampleLocation-batched (Phase 1
  // PrepareAsync), but its result computation is the per-location
  // plate-reading average (WaterLocationResultGridDialog), not EM/AC's
  // CFU x shared-dilution-factor grid.
  const isWaterCountBatch = category === "Water" && current.workflowType === "CountTest";
  const isBatchOrder = isEmOrAfterCleaning || isWaterCountBatch;

  // Five-stage pathogen chain, except EM/AfterCleaning (which still uses
  // this component's own incubation phases below, only handing off to
  // the location-grid dialogs for its final per-location result entry).
  if (current.workflowType !== "CountTest" && !isEmOrAfterCleaning) {
    return <PathogenStepDialog testOrderId={testOrderId} testCode={testCode} displayName={displayName} onClose={onClose} />;
  }

  if (phase === "all-complete") {
    const outcome = lastOutcome?.finalResult ?? current.finalResult;
    return (
      <Box>
        <StepChainStrip current={current} />
        <Alert severity={outcome === "Detected" ? "error" : "success"} sx={{ mb: 2 }}>
          Final result: <strong>{outcome}</strong>
        </Alert>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mt: 2 }}>
          <StatusBadge status="ResultRecorded" label="Result Recorded — Pending Review" />
          {onClose && (
            <Button variant="contained" onClick={onClose} sx={{ fontWeight: 600, textTransform: "none" }}>
              Done / Close
            </Button>
          )}
        </Box>
      </Box>
    );
  }

  return (
    <Box>
      <StepChainStrip current={current} />
      {current?.returnInfo && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          {current.returnInfo.reason
            ? `Returned for revision: ${current.returnInfo.reason}`
            : "Returned by reviewer for revision"}
        </Alert>
      )}
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Typography sx={{ fontWeight: 700, mb: 0.5 }}>
        Step {step.stepOrder}: {step.stepName}
        {isTwoStageTransfer && <Typography component="span" variant="caption" color="text.secondary"> (Two-stage transfer: Stage {currentStageNumber} of 2)</Typography>}
        {step.isFinalStep && <Typography component="span" variant="caption" color="text.secondary"> — determines the final result</Typography>}
      </Typography>

      {phase === "select-media" && (
        <Stack spacing={1.5}>
          <Typography variant="body2" color="text.secondary">
            Requires {permittedMaterialNames || "an approved"} media.
          </Typography>

          <Select
            displayEmpty
            size="small"
            value={mediaId}
            onChange={(e) => {
              const val = e.target.value === "" ? "" : Number(e.target.value);
              setMediaId(val);
              setIncubatorId("");
            }}
          >
            <MenuItem value=""><em>Media Batch ({permittedMaterialNames || "approved"} lots only)</em></MenuItem>
            {matchingMedia.map((m) => <MenuItem key={m.id} value={m.id}>{m.lotNumber} — expires {new Date(m.expiryDate).toLocaleDateString()}</MenuItem>)}
          </Select>

          <Select displayEmpty size="small" value={incubatorId} onChange={(e) => setIncubatorId(Number(e.target.value))}>
            <MenuItem value=""><em>Stage 1 Incubator ({stage1TempMin}-{stage1TempMax} °C)</em></MenuItem>
            {matchingIncubators.map((i) => <MenuItem key={i.id} value={i.id}>{i.name} ({i.code}) — {i.setPointTemperature}°C</MenuItem>)}
          </Select>
          {matchingIncubators.length === 0 && (
            <Alert severity="warning">No incubator is set to {stage1TempMin}-{stage1TempMax} °C for this step.</Alert>
          )}
          {mediaId && incubatorId && (
            <Typography variant="body2" color="text.secondary">
              Required Temperature (Stage 1): <strong>{stage1TempMin}-{stage1TempMax} °C</strong>
              {" — "}Incubation Period: <strong>{stage1IncMinHours}-{stage1IncMaxHours} hours</strong>
            </Typography>
          )}
          <Stack direction="row" justifyContent="flex-end">
            <Button
              variant="contained"
              disabled={!mediaId || !incubatorId}
              onClick={startIncubation}
            >
              Start Incubation
            </Button>
          </Stack>
        </Stack>
      )}

      {phase === "transfer-stage-2" && stage2Config && (
        <Stack spacing={1.5}>
          <Alert severity="info">
            Stage 1 incubation is complete. Transfer plates to Stage 2 incubator ({stage2Config.tempMin}-{stage2Config.tempMax} °C) to start the second stage.
          </Alert>

          <Box sx={{ p: 1.5, bgcolor: "background.default", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
            <Typography variant="body2" color="text.secondary">
              Media Lot (inherited): <strong>{openIncubationRow?.lotNumber ?? "Selected in Stage 1"}</strong>
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Stage 2 Required Temperature: <strong>{stage2Config.tempMin}-{stage2Config.tempMax} °C</strong>
              {" — "}Incubation Period: <strong>{stage2Config.incubationMinHours}-{stage2Config.incubationMaxHours} hours</strong>
            </Typography>
          </Box>

          <Select
            displayEmpty
            size="small"
            value={stage2IncubatorId}
            onChange={(e) => setStage2IncubatorId(Number(e.target.value))}
          >
            <MenuItem value="">
              <em>Select Stage 2 Incubator ({stage2Config.tempMin}-{stage2Config.tempMax} °C)</em>
            </MenuItem>
            {matchingStage2Incubators.map((i) => (
              <MenuItem key={i.id} value={i.id}>
                {i.name} ({i.code}) — {i.setPointTemperature}°C
              </MenuItem>
            ))}
          </Select>
          {matchingStage2Incubators.length === 0 && (
            <Alert severity="warning">No incubator is set to {stage2Config.tempMin}-{stage2Config.tempMax} °C for Stage 2.</Alert>
          )}

          <Stack direction="row" spacing={1} justifyContent="flex-end">
            <Button variant="outlined" onClick={() => setPhase("awaiting-result")}>Back</Button>
            <Button
              variant="contained"
              disabled={!stage2IncubatorId}
              onClick={startStage2Incubation}
            >
              Start Stage 2 Incubation
            </Button>
          </Stack>
        </Stack>
      )}

      {phase === "awaiting-result" && current.incubationLock && (
        <Stack spacing={1.5}>
          <Alert severity="info">
            Incubation in progress {isTwoStageTransfer ? `(Stage ${currentStageNumber} of 2)` : ""}.
          </Alert>
          <Typography variant="body2">Temperature: <strong>{activeTempMin}-{activeTempMax} °C</strong></Typography>
          <Typography variant="body2">Duration: <strong>{activeIncMinHours}-{activeIncMaxHours} hours</strong></Typography>
          <Typography variant="body2">Expected reading: <strong>{new Date(current.incubationLock.incubationEndUtc).toLocaleString()}</strong></Typography>
          {!isTimeReady && minReadyAt && (
            <Alert severity="warning">Not ready yet - available from {minReadyAt.toLocaleString()}.</Alert>
          )}
          {minimumDurationOverridden && (
            <Alert severity="info">Minimum wait time was skipped by a Section Head/System Administrator.</Alert>
          )}
          <Stack direction="row" justifyContent="space-between">
            {canOverrideWait && !isTimeReady ? (
              <Button variant="outlined" color="warning" onClick={() => setSkipDialogOpen(true)} disabled={skipping}>
                Skip Wait
              </Button>
            ) : <span />}
            {isTwoStageTransfer && !isStage2 ? (
              <Button variant="contained" disabled={!isTimeReady} onClick={() => setPhase("transfer-stage-2")}>
                Transfer to Stage 2 Incubation
              </Button>
            ) : isTwoStageTransfer && isStage2 ? (
              isBatchOrder ? (
                <Button variant="contained" disabled={!isTimeReady} onClick={() => setShowLocationGrid(true)}>Record Results</Button>
              ) : (
                <Button variant="contained" disabled={!isTimeReady} onClick={() => setPhase("enter-result")}>Record Result</Button>
              )
            ) : isEmOrAfterCleaning && step && !step.isFinalStep ? (
              <Button variant="contained" disabled={!isTimeReady} onClick={advanceIncubationWindow}>Advance to Next Incubation Window</Button>
            ) : isBatchOrder ? (
              <Button variant="contained" disabled={!isTimeReady} onClick={() => setShowLocationGrid(true)}>Record Results</Button>
            ) : (
              <Button variant="contained" disabled={!isTimeReady} onClick={() => setPhase("enter-result")}>Record Result</Button>
            )}
          </Stack>
        </Stack>
      )}

      <ConfirmationDialog
        open={skipDialogOpen}
        message="Skip the remaining minimum incubation wait time for this step? This bypasses the wait only — the recorded incubation window is not changed."
        onConfirm={confirmSkipWait}
        onCancel={() => setSkipDialogOpen(false)}
      />

      {current.workflowType === "CountTest" && isWaterCountBatch ? (
        <WaterLocationResultGridDialog
          open={showLocationGrid}
          testOrderId={testOrderId}
          testCode={testCode}
          displayName={displayName}
          minReadyAt={minReadyAt}
          minimumDurationOverridden={minimumDurationOverridden}
          onClose={() => setShowLocationGrid(false)}
          onSubmitted={() => { setShowLocationGrid(false); load(); }}
        />
      ) : current.workflowType === "CountTest" ? (
        <LocationResultGridDialog
          open={showLocationGrid}
          testOrderId={testOrderId}
          testCode={testCode}
          displayName={displayName}
          minReadyAt={minReadyAt}
          minimumDurationOverridden={minimumDurationOverridden}
          onClose={() => setShowLocationGrid(false)}
          onSubmitted={() => { setShowLocationGrid(false); load(); }}
        />
      ) : (
        <PathogenLocationResultGridDialog
          open={showLocationGrid}
          testOrderId={testOrderId}
          testCode={testCode}
          displayName={displayName}
          minReadyAt={minReadyAt}
          minimumDurationOverridden={minimumDurationOverridden}
          onClose={() => setShowLocationGrid(false)}
          onSubmitted={() => { setShowLocationGrid(false); load(); }}
        />
      )}

      {phase === "enter-result" && (
        <Stack spacing={1.5}>
          {current.workflowType === "CountTest" && (
            <>
              <TextField
                label="Dilution Factor"
                type="number"
                value={dilutionFactor}
                onChange={(e) => setDilutionFactor(e.target.value)}
                disabled={isDirectCount}
                helperText={
                  isDirectCount
                    ? "Direct count — dilution factor fixed at 1"
                    : "Enter multiplier: 10 for 1:10 dilution, 100 for 1:100"
                }
                inputProps={{ step: "1", min: "1" }}
                sx={{ maxWidth: 300, mb: 1 }}
              />

              <Stack spacing={1}>
                {readings.map((r, i) => (
                  <Stack direction="row" spacing={1} key={i} alignItems="center">
                    <TextField
                      size="small"
                      label={`Plate ${i + 1}`}
                      value={r}
                      onChange={(e) => updateReading(i, e.target.value)}
                      placeholder="Colony count or TNTC"
                      helperText={
                        isNonNumeric(r)
                          ? "⚠ Non-numeric — will be flagged for reviewer decision"
                          : ""
                      }
                      sx={{
                        flex: 1,
                        "& .MuiOutlinedInput-root fieldset": {
                          borderColor: isNonNumeric(r) ? theme.custom.status.inconclusive.text : undefined
                        }
                      }}
                    />
                    <IconButton size="small" onClick={() => removeReading(i)}><CloseIcon fontSize="small" /></IconButton>
                  </Stack>
                ))}
              </Stack>
              <Button startIcon={<AddIcon />} onClick={addReading} sx={{ alignSelf: "flex-start" }}>Add Plate</Button>

              {liveResult && (
                <Box
                  sx={{
                    p: 1.5,
                    mt: 1,
                    mb: 1,
                    borderRadius: 1,
                    backgroundColor: liveResult.isNonNumeric ? theme.custom.status.inconclusive.bg : theme.custom.status.notDetected.bg,
                    border: `1px solid ${liveResult.isNonNumeric ? theme.custom.status.inconclusive.border : theme.custom.status.notDetected.border}`
                  }}
                >
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    {liveResult.isNonNumeric ? "⚠ " : "✓ "}
                    Calculated result: {liveResult.display}
                  </Typography>
                  {liveResult.isNonNumeric && (
                    <Typography variant="caption" sx={{ color: theme.custom.status.inconclusive.text, display: "block", mt: 0.5 }}>
                      Non-numeric result — reviewer will decide accept or retest
                    </Typography>
                  )}
                </Box>
              )}
            </>
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
