import { useState } from "react";
import { Alert, Box, Button, MenuItem, Stack, TextField, Typography } from "@mui/material";
import AutoFixHighOutlinedIcon from "@mui/icons-material/AutoFixHighOutlined";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import type { IcpReportValuesDto } from "../types";

export interface IcpReportAutofillProps {
  runSampleId: number;
  hasReport: boolean;
  disabled?: boolean;
  // Receives the proposed readings; the parent only puts them into the draft, nothing is saved.
  onUse: (values: IcpReportValuesDto["elements"]) => void;
}

// Reads the uploaded sample report on the server and proposes the ppm per element. The analyst picks the
// solution when the report does not identify it, reviews the values, and still saves the entry themselves.
export function IcpReportAutofill({ runSampleId, hasReport, disabled = false, onUse }: IcpReportAutofillProps) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [values, setValues] = useState<IcpReportValuesDto | null>(null);

  const read = async (solutionIndex?: number | null) => {
    setBusy(true);
    setError(null);
    try {
      setValues(await IcpWorkspaceService.getReportValues(runSampleId, solutionIndex));
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setValues(null);
      setError(e.response?.data?.message ?? e.message ?? "Could not read the report.");
    } finally {
      setBusy(false);
    }
  };

  const usable = values?.elements.filter((e) => e.resultPpm != null && e.resultPpm >= 0) ?? [];

  return (
    <Box sx={{ mb: 2 }}>
      <Stack direction="row" spacing={1.5} sx={{ alignItems: "center", flexWrap: "wrap" }}>
        <Button
          variant="outlined"
          startIcon={<AutoFixHighOutlinedIcon />}
          onClick={() => read(values?.selectedIndex)}
          disabled={disabled || busy || !hasReport}
          sx={{ textTransform: "none", fontWeight: 600 }}
        >
          {busy ? "Reading report..." : "Read values from the uploaded report"}
        </Button>
        {!hasReport && (
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Upload the sample report above first.
          </Typography>
        )}
      </Stack>

      {error && <Alert severity="error" sx={{ mt: 1.5 }}>{error}</Alert>}

      {values && (
        <Box sx={{ mt: 1.5 }}>
          {values.requiresVerification && (
            <Alert severity="warning" sx={{ mb: 1 }}>
              The report was read with some uncertainty ({values.source}). Check every value against the report.
            </Alert>
          )}
          {values.problems.map((p) => (
            <Alert key={p} severity="warning" sx={{ mb: 1 }}>{p}</Alert>
          ))}

          <TextField
            select
            size="small"
            label="Solution in the report"
            value={values.selectedIndex ?? ""}
            onChange={(e) => read(Number(e.target.value))}
            disabled={busy}
            sx={{ minWidth: 320, mb: 1.5 }}
            slotProps={{ htmlInput: { "aria-label": "Solution in the report" } }}
          >
            {values.solutions.map((s) => (
              <MenuItem key={s.index} value={s.index}>
                {s.label} — page {s.page}
                {s.elements ? ` (${s.elements})` : ""}
                {s.matchesSample ? " · matches this sample" : ""}
              </MenuItem>
            ))}
          </TextField>

          {values.selectedIndex != null && (
            <>
              <Stack spacing={0.25} sx={{ mb: 1.5 }}>
                {values.elements.map((e) => (
                  <Typography key={e.icpTestElementId} variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
                    <strong>{e.symbol}</strong>: {e.resultPpm != null ? `${e.resultPpm} ppm` : "—"}
                    {e.wavelengthNm != null ? ` (${e.wavelengthNm} nm)` : ""}
                    {e.note ? ` — ${e.note}` : ""}
                  </Typography>
                ))}
              </Stack>
              <Button
                variant="contained"
                onClick={() => onUse(values.elements)}
                disabled={disabled || usable.length === 0}
                sx={{ textTransform: "none", fontWeight: 600 }}
              >
                Use these values
              </Button>
              <Typography variant="caption" sx={{ ml: 1.5, color: "text.secondary" }}>
                Fills the entry below - review, then save.
              </Typography>
            </>
          )}
        </Box>
      )}
    </Box>
  );
}
