import { useState } from "react";
import { Box, TextField, Button, Stack, Alert, Typography } from "@mui/material";
import { apiClient } from "../../../services/apiClient";

interface Props {
  testOrderId: number;
}

// Individual TAMC (or collective pathogen) result entry for an After
// Cleaning test order, created by AfterCleaningWorkflowEngine.ReceiveAsync.
export function AfterCleaningResultDialog({ testOrderId }: Props) {
  const [value, setValue] = useState("");
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    setError(null);
    try {
      await apiClient.post("/results", { testOrderId, rawValue: value });
      setSaved(true);
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not save result.");
    }
  };

  if (saved) return <Alert severity="success">Result saved. Ready for review.</Alert>;

  return (
    <Box>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
        Machine → Selected Parts → collective pathogen sample + individual TAMC per part.
      </Typography>
      <TextField size="small" fullWidth label="Colony Count" value={value} onChange={(e) => setValue(e.target.value)} />
      <Stack direction="row" justifyContent="flex-end" sx={{ mt: 2 }}>
        <Button variant="contained" onClick={submit} disabled={!value}>Save Result</Button>
      </Stack>
    </Box>
  );
}
