import { useEffect, useState } from "react";
import { Box, Table, TableHead, TableRow, TableCell, TableBody, Button, Stack, Alert, Typography, RadioGroup, FormControlLabel, Radio } from "@mui/material";
import { FloatingDialog } from "../../components/FloatingDialog";
import { StatusBadge } from "../../components/StatusBadge";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { TestWorkflowService } from "./services/TestWorkflowService";

interface LocationRow {
  id: number;
  locationType: string;
  locationName: string;
  gradeClassification: string | null;
  status: string | null;
  enteredAt: string | null;
}

interface Props {
  open: boolean;
  testOrderId: number;
  testCode: string;
  displayName: string;
  minReadyAt: Date | null;
  onClose: () => void;
  onSubmitted: () => void;
}

// EM/After Cleaning batch pathogen result entry - the final step's per-
// location Detected/Absent call. Sibling to LocationResultGridDialog,
// which handles the CFU/Count case instead.
export function PathogenLocationResultGridDialog({ open, testOrderId, testCode, displayName, minReadyAt, onClose, onSubmitted }: Props) {
  const isTimeReady = !minReadyAt || new Date() >= minReadyAt;

  const [rows, setRows] = useState<LocationRow[] | null>(null);
  const [growth, setGrowth] = useState<Record<number, "yes" | "no" | "">>({});
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    setRows(null);
    setError(null);
    setGrowth({});
    TestWorkflowService.getLocations(testOrderId).then(setRows);
  }, [open, testOrderId]);

  const allEntered = rows !== null && rows.length > 0 && rows.every((r) => growth[r.id]);

  const liveStatus = (r: LocationRow): string | null => {
    if (!growth[r.id]) return null;
    return growth[r.id] === "yes" ? "Detected" : "Absent";
  };

  const detectedCount = rows === null ? 0 : rows.filter((r) => liveStatus(r) === "Detected").length;

  const submit = async () => {
    if (!rows) return;
    setError(null);
    setSubmitting(true);
    try {
      const locations = rows.map((r) => ({ sampleLocationId: r.id, growthObserved: growth[r.id] === "yes" }));
      await TestWorkflowService.recordBatchPathogenResults(testOrderId, locations);
      onSubmitted();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not record batch pathogen results.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FloatingDialog open={open} title={`${testCode} Results — ${displayName}`} onClose={onClose}>
      {!rows && !error && <LoadingSpinner />}
      {error && !rows && <Alert severity="error">{error}</Alert>}
      {rows && (
        <Stack spacing={2}>
          {error && <Alert severity="error">{error}</Alert>}
          <Box sx={{ overflowX: "auto" }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Location</TableCell>
                  <TableCell>Growth Observed?</TableCell>
                  <TableCell>Status</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell>{r.locationName}{r.gradeClassification ? ` (${r.gradeClassification})` : ""}</TableCell>
                    <TableCell>
                      <RadioGroup row value={growth[r.id] ?? ""} onChange={(e) => setGrowth((g) => ({ ...g, [r.id]: e.target.value as "yes" | "no" }))}>
                        <FormControlLabel value="yes" control={<Radio size="small" />} label="Yes" />
                        <FormControlLabel value="no" control={<Radio size="small" />} label="No" />
                      </RadioGroup>
                    </TableCell>
                    <TableCell>{liveStatus(r) ? <StatusBadge status={liveStatus(r)!} /> : "—"}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
          <Typography sx={{ fontSize: 13 }}>
            {detectedCount}/{rows.length} locations detected — overall: <StatusBadge status={detectedCount > 0 ? "Detected" : "Absent"} />
          </Typography>
          {!isTimeReady && minReadyAt && (
            <Alert severity="warning">Results cannot be submitted before {minReadyAt.toLocaleString()}.</Alert>
          )}
          <Stack direction="row" justifyContent="flex-end">
            <Button variant="contained" disabled={!allEntered || !isTimeReady || submitting} onClick={submit}>
              {submitting ? "Submitting…" : "Submit Results"}
            </Button>
          </Stack>
        </Stack>
      )}
    </FloatingDialog>
  );
}
