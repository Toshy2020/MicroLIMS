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
  isDualPlate: boolean;
  minReadyAt: Date | null;
  onClose: () => void;
  onSubmitted: () => void;
}

// EM/After Cleaning batch pathogen result entry - the final step's per-
// location Detected/Absent call (or, for a dual-plate final step, two
// plates that must agree per location). Sibling to LocationResultGrid-
// Dialog, which handles the CFU/Count case instead.
export function PathogenLocationResultGridDialog({ open, testOrderId, testCode, displayName, isDualPlate, minReadyAt, onClose, onSubmitted }: Props) {
  const isTimeReady = !minReadyAt || new Date() >= minReadyAt;

  const [rows, setRows] = useState<LocationRow[] | null>(null);
  const [growth, setGrowth] = useState<Record<number, "yes" | "no" | "">>({});
  const [plate1, setPlate1] = useState<Record<number, "yes" | "no" | "">>({});
  const [plate2, setPlate2] = useState<Record<number, "yes" | "no" | "">>({});
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    setRows(null);
    setError(null);
    setGrowth({});
    setPlate1({});
    setPlate2({});
    TestWorkflowService.getLocations(testOrderId).then(setRows);
  }, [open, testOrderId]);

  const allEntered = rows !== null && rows.length > 0 && rows.every((r) =>
    isDualPlate ? plate1[r.id] && plate2[r.id] : growth[r.id]
  );

  const inconclusiveCount = rows === null ? 0 : rows.filter((r) => isDualPlate && plate1[r.id] && plate2[r.id] && plate1[r.id] !== plate2[r.id]).length;

  const liveStatus = (r: LocationRow): string | null => {
    if (isDualPlate) {
      if (!plate1[r.id] || !plate2[r.id]) return null;
      if (plate1[r.id] !== plate2[r.id]) return "Inconclusive";
      return plate1[r.id] === "yes" ? "Detected" : "Absent";
    }
    if (!growth[r.id]) return null;
    return growth[r.id] === "yes" ? "Detected" : "Absent";
  };

  const detectedCount = rows === null ? 0 : rows.filter((r) => liveStatus(r) === "Detected").length;

  const submit = async () => {
    if (!rows) return;
    setError(null);
    setSubmitting(true);
    try {
      const locations = rows.map((r) =>
        isDualPlate
          ? { sampleLocationId: r.id, plate1GrowthObserved: plate1[r.id] === "yes", plate2GrowthObserved: plate2[r.id] === "yes" }
          : { sampleLocationId: r.id, growthObserved: growth[r.id] === "yes" }
      );
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
          {inconclusiveCount > 0 && (
            <Alert severity="warning">{inconclusiveCount} location(s) inconclusive - the two plates disagree. All locations must be resubmitted once resolved.</Alert>
          )}
          <Box sx={{ overflowX: "auto" }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Location</TableCell>
                  {isDualPlate ? (
                    <>
                      <TableCell>Plate 1</TableCell>
                      <TableCell>Plate 2</TableCell>
                    </>
                  ) : (
                    <TableCell>Growth Observed?</TableCell>
                  )}
                  <TableCell>Status</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell>{r.locationName}{r.gradeClassification ? ` (${r.gradeClassification})` : ""}</TableCell>
                    {isDualPlate ? (
                      <>
                        <TableCell>
                          <RadioGroup row value={plate1[r.id] ?? ""} onChange={(e) => setPlate1((p) => ({ ...p, [r.id]: e.target.value as "yes" | "no" }))}>
                            <FormControlLabel value="yes" control={<Radio size="small" />} label="Growth" />
                            <FormControlLabel value="no" control={<Radio size="small" />} label="None" />
                          </RadioGroup>
                        </TableCell>
                        <TableCell>
                          <RadioGroup row value={plate2[r.id] ?? ""} onChange={(e) => setPlate2((p) => ({ ...p, [r.id]: e.target.value as "yes" | "no" }))}>
                            <FormControlLabel value="yes" control={<Radio size="small" />} label="Growth" />
                            <FormControlLabel value="no" control={<Radio size="small" />} label="None" />
                          </RadioGroup>
                        </TableCell>
                      </>
                    ) : (
                      <TableCell>
                        <RadioGroup row value={growth[r.id] ?? ""} onChange={(e) => setGrowth((g) => ({ ...g, [r.id]: e.target.value as "yes" | "no" }))}>
                          <FormControlLabel value="yes" control={<Radio size="small" />} label="Yes" />
                          <FormControlLabel value="no" control={<Radio size="small" />} label="No" />
                        </RadioGroup>
                      </TableCell>
                    )}
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
