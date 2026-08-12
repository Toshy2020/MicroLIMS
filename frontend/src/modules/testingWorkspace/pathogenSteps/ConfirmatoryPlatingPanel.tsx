import { useEffect, useState } from "react";
import {
  Box, Typography, Select, MenuItem, TextField, Button, Stack, Alert,
  Checkbox, FormControlLabel, RadioGroup, Radio, Divider
} from "@mui/material";
import { TestWorkflowService } from "../services/TestWorkflowService";
import {
  TestWorkflowStepDto, PermittedConfirmatoryMediaEntry, StepResultDto,
  GrowthObservation, ConfirmatoryOutcomeDto, AnalystDecision
} from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";

interface Props {
  testOrderId: number;
  step: TestWorkflowStepDto;
  onSubmitted: (result: StepResultDto) => void;
}

type Phase = "setup" | "readout" | "decision";

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

// submit-confirmatory-observations returns a ConfirmatoryOutcomeDto, and
// analyst-decision's server call is what actually completes the panel in
// the decision-required path - but PathogenStepDialog's onSubmitted only
// ever uses its argument to trigger a reload (handleSubmitted ignores the
// value: `(_result) => load()`). This stub carries the one real field we
// have (stepInstanceId, flags) and inert defaults for the rest, purely to
// satisfy the shared StepResultDto prop type without inventing data the
// server never sent.
function toStepResultStub(outcome: ConfirmatoryOutcomeDto): StepResultDto {
  return {
    stepInstanceId: outcome.stepInstanceId,
    stepType: "ConfirmatoryPlating",
    status: "Complete",
    submittedByUserId: 0,
    submittedAtUtc: new Date().toISOString(),
    nextStepUnlocked: false,
    workflowFinalResult: null,
    flags: outcome.flags
  };
}

// ConfirmatoryPlating: the most complex pathogen step. One template step
// covers three real-world moments - the analyst first sets up the panel
// (which permitted media they're plating, on what lot/incubator, for how
// long), later comes back once incubation finishes to read each plate
// against its expected appearance, and - only when every reading came
// back conforming - makes a GMP-significant decision about whether to
// skip biochemical confirmation. There is no server-side signal for
// "which of these three moments is this analyst in right now": the
// current-step endpoint only ever reports this step as a whole as
// "current" or "done" (see TestWorkflowEngine.IsStepDoneAsync - it only
// flips to done once a ConfirmatoryResult exists, i.e. after read-out).
// So setup vs. read-out is resolved the only way the API allows: default
// to "setup", attempt the setup call, and let its error code redirect us:
//   - CONFIRMATORY_SETUP_ALREADY_SUBMITTED -> media were already chosen
//     in an earlier session and are incubating; jump straight to read-out.
//   - CONFIRMATORY_ALREADY_RECORDED -> this step was already read out
//     entirely (a stale/second tab racing an already-finished run); show
//     a read-only notice instead of any form.
// Read-out vs. decision needs no such trick: submit-confirmatory-
// observations' own response (`analystDecisionRequired`) says which one
// comes next, and per SubmitConfirmatoryObservationsAsync (backend/
// MicroLIMS.Application/Workflows/TestWorkflowEngine.cs ~1205-1222)
// `AnalystDecisionRequired` is set to exactly `allConforming`, so it is
// never true for an Inconclusive result - a plain if/else fully covers
// both outcomes.
export function ConfirmatoryPlatingPanel({ testOrderId, step, onSubmitted }: Props) {
  const [permitted, setPermitted] = useState<PermittedConfirmatoryMediaEntry[]>([]);
  const [organism, setOrganism] = useState<{ id: number; name: string } | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [alreadyRecordedMessage, setAlreadyRecordedMessage] = useState<string | null>(null);

  const [phase, setPhase] = useState<Phase>("setup");

  // Setup phase state, keyed by stepMediaId.
  const [setupRows, setSetupRows] = useState<Record<number, SetupRow>>({});
  const [durationHours, setDurationHours] = useState(String(step.incubationMinHours));

  // Read-out phase state. `readoutMedia` is populated either from the
  // media the analyst just checked+submitted in this session (the normal
  // path) or, when redirected here by CONFIRMATORY_SETUP_ALREADY_SUBMITTED,
  // from a best-effort guess (see submitSetup below) - `readoutUncertain`
  // flags that second case so the UI can warn the analyst and leave every
  // row unchecked-by-default-but-editable rather than presenting a guess
  // as fact.
  const [readoutMedia, setReadoutMedia] = useState<ReadoutMedium[]>([]);
  const [readoutUncertain, setReadoutUncertain] = useState(false);
  const [readoutChecked, setReadoutChecked] = useState<Record<number, boolean>>({});
  const [observations, setObservations] = useState<Record<number, GrowthObservation | "">>({});

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
    const endUtc = new Date(Date.now() + Number(durationHours) * 3600 * 1000).toISOString();
    const selections = checkedRows.map((r) => ({
      stepMediaId: r.entry.stepMediaId, mediaLotId: Number(r.mediaLotId), equipmentId: Number(r.equipmentId)
    }));
    try {
      await TestWorkflowService.submitConfirmatorySetup(testOrderId, step.stepName, selections, startUtc, endUtc);
      // Setup succeeded but does NOT complete the step - do not call
      // onSubmitted here, only transition phase locally.
      setReadoutMedia(checkedRows.map((r) => ({
        stepMediaId: r.entry.stepMediaId, materialId: r.entry.materialId,
        mediaName: r.entry.mediaName, expectedAppearance: r.entry.expectedAppearance
      })));
      setReadoutUncertain(false);
      setObservations({});
      setReadoutChecked({});
      setPhase("readout");
    } catch (e) {
      const parsed = parseWorkflowError(e);
      if (parsed.code === "CONFIRMATORY_SETUP_ALREADY_SUBMITTED") {
        // Media were already selected in an earlier session. There is no
        // read endpoint that reports which ones - the best available
        // signal is whatever the analyst just checked in this attempt; if
        // they hadn't checked anything yet, fall back to every permitted
        // medium and let them uncheck what wasn't actually plated. Either
        // way this is a guess, flagged via readoutUncertain, and the
        // server still validates the real selection on submit.
        const guessSource = checkedRows.length > 0 ? checkedRows.map((r) => r.entry) : permitted;
        setReadoutMedia(guessSource.map((m) => ({
          stepMediaId: m.stepMediaId, materialId: m.materialId, mediaName: m.mediaName, expectedAppearance: m.expectedAppearance
        })));
        setReadoutUncertain(true);
        setObservations({});
        setReadoutChecked({});
        setPhase("readout");
        return;
      }
      if (parsed.code === "CONFIRMATORY_ALREADY_RECORDED") {
        setAlreadyRecordedMessage(workflowErrorDisplayMessage(parsed));
        return;
      }
      setError(workflowErrorDisplayMessage(parsed));
    }
  };

  const submitReadout = async () => {
    setError(null);
    const active = readoutMedia.filter((m) => readoutChecked[m.materialId] ?? true);
    if (active.length === 0) { setError("At least one medium must be recorded."); return; }
    const missing = active.filter((m) => !observations[m.materialId]);
    if (missing.length > 0) {
      setError(`Select an observation for: ${missing.map((m) => m.mediaName).join(", ")}.`);
      return;
    }
    const payload = active.map((m) => ({ materialId: m.materialId, observation: observations[m.materialId] as GrowthObservation }));
    try {
      const outcome = await TestWorkflowService.submitConfirmatoryObservations(testOrderId, step.stepName, payload);
      // AnalystDecisionRequired is exactly `allConforming` server-side, so
      // an Inconclusive result never reaches the decision phase - this
      // if/else fully covers both outcomes.
      if (outcome.analystDecisionRequired) {
        setPhase("decision");
      } else {
        onSubmitted(toStepResultStub(outcome));
      }
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    }
  };

  const decide = async (decision: AnalystDecision) => {
    setError(null);
    try {
      const result = await TestWorkflowService.recordAnalystDecision(testOrderId, decision);
      onSubmitted(result);
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    }
  };

  const triggerRefresh = () => onSubmitted({
    stepInstanceId: 0, stepType: "ConfirmatoryPlating", status: "Complete",
    submittedByUserId: 0, submittedAtUtc: new Date().toISOString(),
    nextStepUnlocked: true, workflowFinalResult: null, flags: []
  });

  if (loading) return <Typography variant="body2">Loading step configuration…</Typography>;

  if (alreadyRecordedMessage) {
    return (
      <Stack spacing={1.5}>
        <Alert severity="info">{alreadyRecordedMessage}</Alert>
        <Stack direction="row" justifyContent="flex-end">
          <Button variant="outlined" onClick={triggerRefresh}>Refresh</Button>
        </Stack>
      </Stack>
    );
  }

  if (permitted.length === 0) {
    return <Alert severity="error">This step has no permitted confirmatory media configured in Test Master.</Alert>;
  }

  return (
    <Stack spacing={1.5}>
      {error && <Alert severity="error">{error}</Alert>}
      {organism && <Typography variant="body2">Target organism: <strong>{organism.name}</strong></Typography>}

      {phase === "setup" && (
        <Stack spacing={1.5}>
          <Alert severity="info">
            Check every medium you actually plated for this confirmatory run, choose its lot and incubator, then
            set the shared incubation duration below. Only what the step template permits can be selected - there
            is no way to add another medium here.
          </Alert>
          {permitted.map((m) => {
            const row = setupRows[m.stepMediaId];
            if (!row) return null;
            return (
              <Box key={m.stepMediaId} sx={{ border: "1px solid #e0e0e0", borderRadius: 1, p: 1.5 }}>
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
          <TextField
            size="small" type="number" label="Incubation Duration (hours)" value={durationHours}
            onChange={(e) => setDurationHours(e.target.value)} sx={{ maxWidth: 220 }}
            helperText={`Shared by every selected medium. Template range: ${step.incubationMinHours}-${step.incubationMaxHours}h`}
          />
          <Stack direction="row" justifyContent="flex-end">
            <Button variant="contained" onClick={submitSetup}>Submit Setup</Button>
          </Stack>
        </Stack>
      )}

      {phase === "readout" && (
        <Stack spacing={1.5}>
          {readoutUncertain ? (
            <Alert severity="warning">
              Confirmatory media for this step were already selected in a previous session, and there is no way to
              retrieve exactly which ones from here. The media below are a best guess - uncheck anything that was
              not actually plated before recording observations; the server will still reject an incorrect selection.
            </Alert>
          ) : (
            <Alert severity="info">Read each plate and record what you observe against the expected appearance.</Alert>
          )}
          {readoutMedia.map((m) => {
            const checked = readoutChecked[m.materialId] ?? true;
            const value = observations[m.materialId] ?? "";
            return (
              <Box key={m.materialId} sx={{ border: "1px solid #e0e0e0", borderRadius: 1, p: 1.5 }}>
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
            <Button variant="contained" onClick={submitReadout}>Submit Observations</Button>
          </Stack>
        </Stack>
      )}

      {phase === "decision" && (
        <Stack spacing={1.5}>
          <Alert severity="success">
            Confirmatory result: <strong>All Conforming</strong>. An analyst decision is required before this
            workflow proceeds.
          </Alert>
          <Divider />
          <Alert severity="warning">
            Submitting as Detected without biochemical confirmation will be flagged for the reviewer.
          </Alert>
          <Stack direction="row" spacing={1.5} justifyContent="flex-end">
            <Button variant="outlined" color="warning" onClick={() => decide("SubmitAsDetected")}>
              Submit as Detected (skip biochemical)
            </Button>
            <Button variant="contained" onClick={() => decide("ProceedToBiochemical")}>
              Proceed to Biochemical Test
            </Button>
          </Stack>
        </Stack>
      )}
    </Stack>
  );
}
