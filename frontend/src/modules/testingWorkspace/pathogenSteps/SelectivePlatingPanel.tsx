import { useEffect, useState } from "react";
import {
  Typography, Select, MenuItem, TextField, Button, Stack, Alert,
  RadioGroup, FormControlLabel, Radio
} from "@mui/material";
import { TestWorkflowService } from "../services/TestWorkflowService";
import { TestWorkflowStepDto, PermittedConfirmatoryMediaEntry, GrowthObservation } from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";

interface Props {
  testOrderId: number;
  step: TestWorkflowStepDto;
  onSubmitted: () => void;
}

// SelectivePlating: this IS the growth-interpretation decision point (unlike
// BrothStepPanel's preparation-only steps). The analyst reads the plate
// against the medium's expected appearance and records one of three
// GrowthObservation states. No option is pre-selected - matches the
// pre-refactor TestWorkflowDialog.tsx convention for this kind of GMP
// pass/fail radio (useState<"yes"|"no"|"">("")  plus a submit-time guard)
// so an analyst can't submit a growth call by leaving a default untouched.
//
// Known API gap: the original spec called for a required observed-appearance
// note whenever growth is recorded, but submit-selective-plating's request
// body has no free-text field - only the `observation` enum. The note below
// is kept as a client-side confirmation gate only (it blocks submission if
// blank, forcing the analyst to actually describe what they saw) and is
// never sent to the server - only the enum value is submitted.
export function SelectivePlatingPanel({ testOrderId, step, onSubmitted }: Props) {
  const [medium, setMedium] = useState<PermittedConfirmatoryMediaEntry | null>(null);
  const [incubators, setIncubators] = useState<{ id: number; name: string; code: string; setTemperature: number }[]>([]);
  const [mediaLotId, setMediaLotId] = useState<number | "">("");
  const [equipmentId, setEquipmentId] = useState<number | "">("");
  const [durationHours, setDurationHours] = useState(String(step.incubationMinHours));
  const [observation, setObservation] = useState<GrowthObservation | "">("");
  const [appearanceNote, setAppearanceNote] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

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

  const submit = async () => {
    setError(null);
    if (!medium || !mediaLotId || !equipmentId) { setError("Select a media lot and an incubator."); return; }
    if (observation === "") { setError("Select an observation."); return; }
    if (observation !== "NoGrowth" && !appearanceNote.trim()) {
      setError("Describe the observed appearance before submitting a growth result.");
      return;
    }
    const startUtc = new Date().toISOString();
    const endUtc = new Date(Date.now() + Number(durationHours) * 3600 * 1000).toISOString();
    try {
      await TestWorkflowService.submitSelectivePlating(
        testOrderId, step.stepName, Number(mediaLotId), Number(equipmentId),
        startUtc, endUtc, observation
      );
      onSubmitted();
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
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
      <Select displayEmpty size="small" value={mediaLotId} onChange={(e) => setMediaLotId(Number(e.target.value))}>
        <MenuItem value=""><em>Media Lot</em></MenuItem>
        {medium.availableLots.map((l) => (
          <MenuItem key={l.id} value={l.id}>{l.lotNumber} — expires {new Date(l.expiryDate).toLocaleDateString()}</MenuItem>
        ))}
      </Select>
      <Select displayEmpty size="small" value={equipmentId} onChange={(e) => setEquipmentId(Number(e.target.value))}>
        <MenuItem value=""><em>Incubator ({medium.tempMin}-{medium.tempMax} °C)</em></MenuItem>
        {incubators.map((i) => <MenuItem key={i.id} value={i.id}>{i.name} ({i.code}) — {i.setTemperature}°C</MenuItem>)}
      </Select>
      <TextField
        size="small" type="number" label="Incubation Duration (hours)" value={durationHours}
        onChange={(e) => setDurationHours(e.target.value)} sx={{ maxWidth: 220 }}
        helperText={`Template range: ${step.incubationMinHours}-${step.incubationMaxHours}h`}
      />
      <Typography variant="subtitle2">Observation</Typography>
      <RadioGroup value={observation} onChange={(e) => setObservation(e.target.value as GrowthObservation)}>
        <FormControlLabel value="NoGrowth" control={<Radio />} label="No growth — target absent" />
        <FormControlLabel value="GrowthNonConforming" control={<Radio />} label="Growth present, does not match expected appearance — target absent" />
        <FormControlLabel value="GrowthConforming" control={<Radio />} label="Growth matching expected appearance — presumptive positive" />
      </RadioGroup>
      {(observation === "GrowthNonConforming" || observation === "GrowthConforming") && (
        <TextField
          size="small" multiline minRows={2} required
          label="Observed Appearance Note"
          value={appearanceNote} onChange={(e) => setAppearanceNote(e.target.value)}
          helperText="Describe what you observed. This confirms you inspected the plate before recording growth - it is not sent to the server (the API has no field for it); only the selected observation category above is submitted."
        />
      )}
      <Stack direction="row" justifyContent="flex-end">
        <Button variant="contained" onClick={submit}>Submit</Button>
      </Stack>
    </Stack>
  );
}
