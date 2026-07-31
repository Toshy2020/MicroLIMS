import { useState } from "react";
import { Box, TextField, Button, Stack, Alert, IconButton, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import CloseIcon from "@mui/icons-material/Close";
import { WaterService, WaterComparisonResult } from "./services/WaterService";
import { StatusBadge } from "../../../components/StatusBadge";

interface Props {
  testOrderId: number;
}

const statusSeverity: Record<string, "success" | "warning" | "error"> = {
  WithinLimits: "success",
  AlertLimitExceeded: "warning",
  ActionLimitExceeded: "warning",
  OutOfSpecification: "error"
};

// Calculation engine: enter raw readings -> average -> Alert/Action/Spec
// comparison, exactly mirroring WaterWorkflowEngine.CalculateAndCompareAsync.
export function WaterWorkflowDialog({ testOrderId }: Props) {
  const [readings, setReadings] = useState<string[]>([""]);
  const [result, setResult] = useState<WaterComparisonResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  const updateReading = (i: number, value: string) => setReadings((r) => r.map((v, idx) => (idx === i ? value : v)));
  const addReading = () => setReadings((r) => [...r, ""]);
  const removeReading = (i: number) => setReadings((r) => (r.length > 1 ? r.filter((_, idx) => idx !== i) : r));

  const submit = async () => {
    setError(null);
    const parsed = readings.map(Number).filter((n) => !Number.isNaN(n));
    if (parsed.length === 0) {
      setError("Enter at least one numeric reading.");
      return;
    }
    try {
      const res = await WaterService.calculate(testOrderId, parsed);
      setResult(res);
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not calculate result.");
    }
  };

  if (result) {
    return (
      <Box>
        <Alert severity={statusSeverity[result.status] ?? "info"} sx={{ mb: 2 }}>
          Average: <strong>{result.average}</strong> — <StatusBadge status={result.status} />
        </Alert>
        {result.exceededLimit && (
          <Typography variant="body2" color="text.secondary">
            Exceeded the {result.exceededLimit} limit.
          </Typography>
        )}
      </Box>
    );
  }

  return (
    <Box>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
        Enter raw plate readings for this sampling point.
      </Typography>
      <Stack spacing={1}>
        {readings.map((r, i) => (
          <Stack direction="row" spacing={1} key={i} alignItems="center">
            <TextField size="small" type="number" label={`Reading ${i + 1}`} value={r} onChange={(e) => updateReading(i, e.target.value)} />
            <IconButton size="small" onClick={() => removeReading(i)}><CloseIcon fontSize="small" /></IconButton>
          </Stack>
        ))}
      </Stack>
      <Button startIcon={<AddIcon />} onClick={addReading} sx={{ mt: 1 }}>Add Reading</Button>
      <Stack direction="row" justifyContent="flex-end" sx={{ mt: 2 }}>
        <Button variant="contained" onClick={submit}>Calculate & Compare</Button>
      </Stack>
    </Box>
  );
}
