import { useEffect, useMemo, useState } from "react";
import { Box, Table, TableHead, TableRow, TableCell, TableBody, TextField, IconButton, Button, Stack, Alert, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import CloseIcon from "@mui/icons-material/Close";
import { FloatingDialog } from "../../components/FloatingDialog";
import { StatusBadge } from "../../components/StatusBadge";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { TestWorkflowService } from "./services/TestWorkflowService";

interface LocationRow {
  id: number;
  locationType: string;
  locationName: string;
  gradeClassification: string | null;
  alertLimit: string | null;
  actionLimit: string | null;
  specLimit: string | null;
  cfuResult: number | null;
  calculatedResult: number | null;
  reportedResult: string | null;
  rawReadings: string | null;
  status: string | null;
  enteredAt: string | null;
}

interface Props {
  open: boolean;
  testOrderId: number;
  testCode: string;
  displayName: string;
  minReadyAt: Date | null;
  minimumDurationOverridden?: boolean;
  onClose: () => void;
  onSubmitted: () => void;
}

// Same Spec -> Action -> Alert precedence (most severe first) as the
// backend's TestWorkflowEngine.Compare - mirrored here only for the
// live preview as the analyst types; the server recomputes and is the
// authority on the persisted Status.
function compareStatus(value: number, alert: string | null, action: string | null, spec: string | null): string {
  const specLimit = spec !== null && spec.trim() !== "" ? Number(spec) : NaN;
  if (!Number.isNaN(specLimit) && value > specLimit) return "OutOfSpecification";
  const actionLimit = action !== null && action.trim() !== "" ? Number(action) : NaN;
  if (!Number.isNaN(actionLimit) && value > actionLimit) return "ActionLimitExceeded";
  const alertLimit = alert !== null && alert.trim() !== "" ? Number(alert) : NaN;
  if (!Number.isNaN(alertLimit) && value > alertLimit) return "AlertLimitExceeded";
  if (Number.isNaN(specLimit) && Number.isNaN(actionLimit) && Number.isNaN(alertLimit)) return "LimitsNotConfigured";
  return "WithinLimits";
}

function reportedResult(calculated: number): string {
  return calculated < 1 ? "<1" : Math.round(calculated).toString();
}

// EM/After Cleaning are direct-count categories - dilution factor is
// always 1 server-side (RecordBatchResultsAsync), never free-typed for
// the whole batch. Each location takes multiple raw plate readings,
// averaged the same way RecordCountTestAsync averages a single order's
// RawPlateReadings - same multi-reading model Water has always used,
// mirrored here (see WaterLocationResultGridDialog.tsx).
export function LocationResultGridDialog({ open, testOrderId, testCode, displayName, minReadyAt, minimumDurationOverridden, onClose, onSubmitted }: Props) {
  const isTimeReady = !minReadyAt || new Date() >= minReadyAt || !!minimumDurationOverridden;
  const [rows, setRows] = useState<LocationRow[] | null>(null);
  const [readingsByLocation, setReadingsByLocation] = useState<Record<number, string[]>>({});
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    setRows(null);
    setError(null);
    setReadingsByLocation({});
    TestWorkflowService.getLocations(testOrderId).then((data) => {
      setRows(data);
      const initial: Record<number, string[]> = {};
      data.forEach((r: LocationRow) => {
        initial[r.id] = r.rawReadings ? r.rawReadings.split(",") : [""];
      });
      setReadingsByLocation(initial);
    });
  }, [open, testOrderId]);

  const updateReading = (locationId: number, i: number, value: string) =>
    setReadingsByLocation((m) => ({ ...m, [locationId]: (m[locationId] ?? [""]).map((v, idx) => (idx === i ? value : v)) }));
  const addReading = (locationId: number) =>
    setReadingsByLocation((m) => ({ ...m, [locationId]: [...(m[locationId] ?? [""]), ""] }));
  const removeReading = (locationId: number, i: number) =>
    setReadingsByLocation((m) => {
      const current = m[locationId] ?? [""];
      return { ...m, [locationId]: current.length > 1 ? current.filter((_, idx) => idx !== i) : current };
    });

  const computed = useMemo(() => {
    if (!rows) return [];
    return rows.map((r) => {
      const raw = readingsByLocation[r.id] ?? [];
      const values = raw.filter((v) => v !== "").map(Number);
      if (values.length === 0 || values.some(Number.isNaN)) {
        return { ...r, liveCalculated: null as number | null, liveReported: null as string | null, liveStatus: null as string | null };
      }
      const average = values.reduce((a, b) => a + b, 0) / values.length;
      // Dilution factor is always 1 - calculated equals the average directly.
      const status = compareStatus(average, r.alertLimit, r.actionLimit, r.specLimit);
      return { ...r, liveCalculated: average, liveReported: reportedResult(average), liveStatus: status };
    });
  }, [rows, readingsByLocation]);

  const allEntered = rows !== null && rows.length > 0 && rows.every((r) => {
    const values = (readingsByLocation[r.id] ?? []).filter((v) => v !== "").map(Number);
    return values.length > 0 && values.every((v) => !Number.isNaN(v));
  });

  const conformCount = computed.filter((r) => r.liveStatus === "WithinLimits").length;
  const worstStatus = computed.reduce<string>((worst, r) => {
    const order = ["WithinLimits", "LimitsNotConfigured", "AlertLimitExceeded", "ActionLimitExceeded", "OutOfSpecification"];
    if (!r.liveStatus) return worst;
    return order.indexOf(r.liveStatus) > order.indexOf(worst) ? r.liveStatus : worst;
  }, "WithinLimits");

  const submit = async () => {
    if (!rows) return;
    setError(null);
    setSubmitting(true);
    try {
      await TestWorkflowService.recordBatchResults(
        testOrderId,
        rows.map((r) => ({
          sampleLocationId: r.id,
          readings: (readingsByLocation[r.id] ?? []).filter((v) => v !== "").map(Number)
        }))
      );
      onSubmitted();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not record batch results.");
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
                  <TableCell>Limits (Alert / Action / Spec)</TableCell>
                  <TableCell>Plate Readings</TableCell>
                  <TableCell>Calculated Result</TableCell>
                  <TableCell>Status</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {computed.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell>
                      {r.locationName}{r.gradeClassification ? ` (${r.gradeClassification})` : ""}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                      {r.alertLimit ?? "—"} / {r.actionLimit ?? "—"} / {r.specLimit ?? "—"}
                    </TableCell>
                    <TableCell>
                      <Stack spacing={0.5}>
                        {(readingsByLocation[r.id] ?? [""]).map((v, i) => (
                          <Stack direction="row" spacing={0.5} key={i} alignItems="center">
                            <TextField
                              size="small" type="number" sx={{ width: 90 }}
                              value={v}
                              onChange={(e) => updateReading(r.id, i, e.target.value)}
                            />
                            <IconButton size="small" onClick={() => removeReading(r.id, i)}><CloseIcon fontSize="small" /></IconButton>
                          </Stack>
                        ))}
                        <Button size="small" startIcon={<AddIcon />} onClick={() => addReading(r.id)} sx={{ alignSelf: "flex-start" }}>
                          Add Plate
                        </Button>
                      </Stack>
                    </TableCell>
                    <TableCell>{r.liveReported ?? "—"}</TableCell>
                    <TableCell>{r.liveStatus ? <StatusBadge status={r.liveStatus} /> : "—"}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
          <Typography sx={{ fontSize: 13 }}>
            {conformCount}/{rows.length} locations within spec — worst status: <StatusBadge status={worstStatus} />
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
