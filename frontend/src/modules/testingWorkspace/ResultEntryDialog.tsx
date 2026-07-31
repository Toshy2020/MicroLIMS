import { useState } from "react";
import { Box, TextField, Button, Stack, Alert, Typography } from "@mui/material";
import { apiClient } from "../../services/apiClient";

interface Props {
  testOrderId: number;
  testCode: string;
}

// Plain result entry for simple, single-value tests (e.g. TAMC/TYMC
// on a Product sample) that don't need a multi-step workflow dialog.
export function ResultEntryDialog({ testOrderId, testCode }: Props) {
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

  if (saved) {
    return <Alert severity="success">Result saved for {testCode}. Ready for review.</Alert>;
  }

  return (
    <Box>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
        Enter the result for {testCode}. TAMC/TYMC counts are rounded to whole numbers by the backend.
      </Typography>
      <TextField size="small" fullWidth label="Result" value={value} onChange={(e) => setValue(e.target.value)} />
      <Stack direction="row" justifyContent="flex-end" sx={{ mt: 2 }}>
        <Button variant="contained" onClick={submit} disabled={!value}>Save Result</Button>
      </Stack>
    </Box>
  );
}
