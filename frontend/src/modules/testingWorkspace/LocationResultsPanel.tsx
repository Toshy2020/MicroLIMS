import { useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography
} from "@mui/material";
import { TestWorkflowService } from "./services/TestWorkflowService";
import type { CurrentStepResponse, CurrentStepLocationItem } from "./types/testWorkflowTypes";

export interface LocationResultsPanelProps {
  testOrderId: number;
  displayName?: string;
  testCode?: string;
  current: CurrentStepResponse;
  onRecorded?: () => Promise<void> | void;
  onDone?: () => Promise<void> | void;
  onClose?: () => void;
}

export function formatLocationLimit(loc: CurrentStepLocationItem): string {
  if (loc.specLimit) return loc.specLimit;
  if (loc.limitType === "Qualitative") return loc.expectedResultText ?? "—";
  if (loc.limitType === "Range") {
    if (loc.lowerLimit != null && loc.upperLimit != null) return `${loc.lowerLimit} \u2013 ${loc.upperLimit}`;
    if (loc.lowerLimit != null) return `NLT ${loc.lowerLimit}`;
    if (loc.upperLimit != null) return `NMT ${loc.upperLimit}`;
    return "—";
  }
  if (loc.limitType === "NotMoreThan") {
    return loc.upperLimit != null ? `NMT ${loc.upperLimit}` : "—";
  }
  if (loc.limitType === "NotLessThan") {
    return loc.lowerLimit != null ? `NLT ${loc.lowerLimit}` : "—";
  }
  return loc.expectedResultText ?? "—";
}

export function LocationResultsPanel({
  testOrderId,
  displayName,
  testCode,
  current,
  onRecorded,
  onDone,
  onClose
}: LocationResultsPanelProps) {
  const locations = current.locations ?? [];
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [values, setValues] = useState<Record<number, { value: string; text: string }>>(() => {
    const init: Record<number, { value: string; text: string }> = {};
    for (const loc of locations) {
      init[loc.sampleLocationId] = {
        value: loc.value != null ? String(loc.value) : "",
        text: loc.text ?? ""
      };
    }
    return init;
  });

  const updateValue = (id: number, val: string) => {
    setValues((prev) => ({
      ...prev,
      [id]: { value: val, text: "" }
    }));
  };

  const updateText = (id: number, text: string) => {
    setValues((prev) => ({
      ...prev,
      [id]: { value: "", text }
    }));
  };

  const handleSubmit = async () => {
    setError(null);
    for (const loc of locations) {
      const entry = values[loc.sampleLocationId];
      if (loc.limitType === "Qualitative") {
        if (!entry?.text || entry.text.trim() === "") {
          setError(`Please enter result text for ${loc.name}.`);
          return;
        }
      } else {
        if (!entry?.value || entry.value.trim() === "" || Number.isNaN(Number(entry.value))) {
          setError(`Please enter a valid numeric result for ${loc.name}.`);
          return;
        }
      }
    }

    setSubmitting(true);
    try {
      const payload = locations.map((loc) => {
        const entry = values[loc.sampleLocationId];
        return {
          sampleLocationId: loc.sampleLocationId,
          value: loc.limitType === "Qualitative" ? null : Number(entry.value),
          text: loc.limitType === "Qualitative" ? entry.text.trim() : null
        };
      });

      await TestWorkflowService.recordLocationResults(testOrderId, payload);
      if (onRecorded) {
        await onRecorded();
      } else if (onDone) {
        await onDone();
      }
    } catch (err: any) {
      setError(err?.response?.data?.message ?? "Failed to record location results.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Stack spacing={2.5} sx={{ p: 2 }}>
      <Box>
        <Typography variant="h6" sx={{ fontWeight: 600 }}>
          {displayName || testCode || current.testName || "Location Results"}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Enter results for each sampling location.
        </Typography>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 1.5 }}>
        <Table size="small">
          <TableHead>
            <TableRow sx={{ bgcolor: "action.hover" }}>
              <TableCell sx={{ fontWeight: 600 }}>Location</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Limit</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Unit</TableCell>
              <TableCell sx={{ fontWeight: 600, width: 220 }}>Result</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {locations.length === 0 ? (
              <TableRow>
                <TableCell colSpan={4} align="center" sx={{ py: 3, color: "text.secondary" }}>
                  No locations configured for this test.
                </TableCell>
              </TableRow>
            ) : (
              locations.map((loc) => {
                const isQualitative = loc.limitType === "Qualitative";
                return (
                  <TableRow key={loc.sampleLocationId}>
                    <TableCell sx={{ fontWeight: 600 }}>{loc.name}</TableCell>
                    <TableCell>{formatLocationLimit(loc)}</TableCell>
                    <TableCell>{loc.unit || "—"}</TableCell>
                    <TableCell>
                      {isQualitative ? (
                        <TextField
                          size="small"
                          fullWidth
                          placeholder="Result text"
                          value={values[loc.sampleLocationId]?.text ?? ""}
                          onChange={(e) => updateText(loc.sampleLocationId, e.target.value)}
                          slotProps={{
                            htmlInput: {
                              "aria-label": `Result for ${loc.name}`
                            }
                          }}
                        />
                      ) : (
                        <TextField
                          size="small"
                          fullWidth
                          type="number"
                          placeholder="Value"
                          value={values[loc.sampleLocationId]?.value ?? ""}
                          onChange={(e) => updateValue(loc.sampleLocationId, e.target.value)}
                          slotProps={{
                            htmlInput: {
                              "aria-label": `Result for ${loc.name}`,
                              step: "any"
                            }
                          }}
                        />
                      )}
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <Stack direction="row" spacing={1.5} sx={{ justifyContent: "flex-end" }}>
        {onClose && (
          <Button variant="outlined" onClick={onClose} disabled={submitting}>
            Cancel
          </Button>
        )}
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={submitting || locations.length === 0}
          startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : null}
        >
          {submitting ? "Submitting..." : "Submit"}
        </Button>
      </Stack>
    </Stack>
  );
}

export default LocationResultsPanel;
