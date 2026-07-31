import { useState } from "react";
import { Box, Typography, TextField, Button, Stack, Alert, Chip } from "@mui/material";
import { apiClient } from "../../../services/apiClient";

interface Props { testOrderId: number; }

type Phase = "step1-not-started" | "step1-running" | "step2-not-started" | "step2-running" | "done";

// Step 1 and Step 2 are sequential incubation TIME WINDOWS, not two
// separate counts - only ONE final colony count is entered, after both
// windows have elapsed. Mirrors EMWorkflowEngine exactly.
export function EMIncubationDialog({ testOrderId }: Props) {
  const [phase, setPhase] = useState<Phase>("step1-not-started");
  const [finalCount, setFinalCount] = useState("");
  const [actionLimit, setActionLimit] = useState("10");
  const [error, setError] = useState<string | null>(null);
  const [outcome, setOutcome] = useState<{ step2Count: number; isOutOfTrend: boolean } | null>(null);

  const run = async (fn: () => Promise<void>) => {
    setError(null);
    try { await fn(); } catch (e: any) { setError(e?.response?.data?.message ?? "Action failed."); }
  };

  if (phase === "done" && outcome) {
    return (
      <Box>
        <Alert severity={outcome.isOutOfTrend ? "error" : "success"}>
          Final count: {outcome.step2Count} {outcome.isOutOfTrend ? "— OUT OF TREND" : "— Within trend"}
        </Alert>
      </Box>
    );
  }

  return (
    <Box>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
        <Chip label="Step 1 window" color={phase.startsWith("step1") ? "primary" : "default"} size="small" />
        <Chip label="Step 2 window" color={phase.startsWith("step2") ? "primary" : "default"} size="small" />
      </Stack>

      {phase === "step1-not-started" && (
        <Button variant="contained" onClick={() => run(async () => {
          await apiClient.post(`/em/step1/start/${testOrderId}`);
          setPhase("step1-running");
        })}>Start Step 1 Window</Button>
      )}

      {phase === "step1-running" && (
        <Stack spacing={1.5}>
          <Typography variant="body2" color="text.secondary">No count is entered yet — Step 1 is just an incubation window.</Typography>
          <Button variant="contained" onClick={() => run(async () => {
            await apiClient.post(`/em/step2/start/${testOrderId}`);
            setPhase("step2-running");
          })}>Close Step 1, Start Step 2 Window</Button>
        </Stack>
      )}

      {phase === "step2-running" && (
        <Stack spacing={1.5}>
          <Typography variant="body2" color="text.secondary">Enter the final colony count after both windows have elapsed.</Typography>
          <TextField size="small" type="number" label="Final Count" value={finalCount} onChange={(e) => setFinalCount(e.target.value)} />
          <TextField size="small" type="number" label="Action Limit (from Room config)" value={actionLimit} onChange={(e) => setActionLimit(e.target.value)} />
          <Button variant="contained" onClick={() => run(async () => {
            const res = await apiClient.post("/em/complete", { testOrderId, finalCount: Number(finalCount), actionLimit: Number(actionLimit) });
            setOutcome(res.data.data);
            setPhase("done");
          })}>Complete</Button>
        </Stack>
      )}
    </Box>
  );
}
