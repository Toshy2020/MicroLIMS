import { useEffect, useState } from "react";
import { Typography, Select, MenuItem, TextField, Button, Stack, Alert } from "@mui/material";
import { TestWorkflowService } from "../services/TestWorkflowService";
import { TestWorkflowStepDto, PermittedConfirmatoryMediaEntry, StepResultDto } from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";

interface Props {
  testOrderId: number;
  step: TestWorkflowStepDto;
  onSubmitted: (result: StepResultDto) => void;
}

// BrothEnrichment/SelectiveBroth: preparation only. There is deliberately
// no result-interpretation UI here - the chain runs to completion
// regardless of what the analyst observes here (the method requires it),
// so this must never be framed as a pass/fail decision point.
export function BrothStepPanel({ testOrderId, step, onSubmitted }: Props) {
  const [medium, setMedium] = useState<PermittedConfirmatoryMediaEntry | null>(null);
  const [incubators, setIncubators] = useState<{ id: number; name: string; code: string; setTemperature: number }[]>([]);
  const [mediaLotId, setMediaLotId] = useState<number | "">("");
  const [equipmentId, setEquipmentId] = useState<number | "">("");
  const [durationHours, setDurationHours] = useState(String(step.incubationMinHours));
  const [observation, setObservation] = useState("");
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
    const startUtc = new Date().toISOString();
    const endUtc = new Date(Date.now() + Number(durationHours) * 3600 * 1000).toISOString();
    try {
      const result = await TestWorkflowService.submitBroth(
        testOrderId, step.stepName, Number(mediaLotId), Number(equipmentId),
        startUtc, endUtc, observation.trim() || null
      );
      onSubmitted(result);
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    }
  };

  if (loading) return <Typography variant="body2">Loading step configuration…</Typography>;
  if (!medium) return <Alert severity="error">This step has no assigned medium configured in Test Master.</Alert>;

  return (
    <Stack spacing={1.5}>
      {error && <Alert severity="error">{error}</Alert>}
      <Alert severity="info">
        This step is preparation only - it is not a pass/fail result. The observation below (if any) is recorded
        for the record; the workflow proceeds to the next step regardless of what is observed here.
      </Alert>
      <Typography variant="body2">Medium: <strong>{medium.mediaName}</strong></Typography>
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
      <TextField
        size="small" multiline minRows={2} label="Observation (optional)" value={observation}
        onChange={(e) => setObservation(e.target.value)}
      />
      <Stack direction="row" justifyContent="flex-end">
        <Button variant="contained" onClick={submit}>Start Incubation</Button>
      </Stack>
    </Stack>
  );
}
