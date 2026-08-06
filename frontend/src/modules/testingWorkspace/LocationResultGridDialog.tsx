import { useEffect, useMemo, useState } from "react";
import { Box, Table, TableHead, TableRow, TableCell, TableBody, TextField, Button, Stack, Alert, Typography } from "@mui/material";
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

// Same Spec -> Action -> Alert precedence (most severe first) as the
// backend's TestWorkflowEngine.Compare - mirrored here only for the
// live preview as the analyst types; the server recomputes and is the
// authority on the persisted Status.
function compareStatus(value: number, alert: string | null, action: string | null, spec: string | null): string {
  const specLimit = spec !== null ? Number(spec) : NaN;
  if (!Number.isNaN(specLimit) && value > specLimit) return "OutOfSpecification";
  const actionLimit = action !== null ? Number(action) : NaN;
  if (!Number.isNaN(actionLimit) && value > actionLimit) return "ActionLimitExceeded";
  const alertLimit = alert !== null ? Number(alert) : NaN;
  if (!Number.isNaN(alertLimit) && value > alertLimit) return "AlertLimitExceeded";
  return "WithinLimits";
}

function reportedResult(calculated: number): string {
  return calculated < 1 ? "<1" : Math.round(calculated).toString();
}

export function LocationResultGridDialog({ open, testOrderId, testCode, displayName, minReadyAt, onClose, onSubmitted }: Props) {
  const isTimeReady = !minReadyAt || new Date() >= minReadyAt;
  const [rows, setRows] = useState<LocationRow[] | null>(null);
  const [dilutionFactor, setDilutionFactor] = useState("1");
  const [cfuValues, setCfuValues] = useState<Record<number, string>>({});
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    setRows(null);
    setError(null);
    setCfuValues({});
    setDilutionFactor("1");
    TestWorkflowService.getLocations(testOrderId).then((data) => {
      setRows(data);
      const initial: Record<number, string> = {};
      data.forEach((r: LocationRow) => {
        if (r.cfuResult != null) initial[r.id] = String(r.cfuResult);
      });
      setCfuValues(initial);
    });
  }, [open, testOrderId]);

  const dilution = Number(dilutionFactor) || 0;

  const computed = useMemo(() => {
    if (!rows) return [];
    return rows.map((r) => {
      const cfuText = cfuValues[r.id];
      const cfu = cfuText !== undefined && cfuText !== "" ? Number(cfuText) : null;
      if (cfu === null || Number.isNaN(cfu) || dilution <= 0) {
        return { ...r, liveCalculated: null as number | null, liveReported: null as string | null, liveStatus: null as string | null };
      }
      const calculated = cfu * dilution;
      const status = compareStatus(calculated, r.alertLimit, r.actionLimit, r.specLimit);
      return { ...r, liveCalculated: calculated, liveReported: reportedResult(calculated), liveStatus: status };
    });
  }, [rows, cfuValues, dilution]);

  const allEntered = rows !== null && rows.length > 0 && rows.every((r) => {
    const v = cfuValues[r.id];
    return v !== undefined && v !== "" && !Number.isNaN(Number(v));
  });

  const conformCount = computed.filter((r) => r.liveStatus === "WithinLimits").length;
  const worstStatus = computed.reduce<string>((worst, r) => {
    const order = ["WithinLimits", "AlertLimitExceeded", "ActionLimitExceeded", "OutOfSpecification"];
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
        dilution,
        rows.map((r) => ({ sampleLocationId: r.id, cfuResult: Number(cfuValues[r.id]) }))
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
          <TextField
            size="small" type="number" label="Dilution Factor" value={dilutionFactor}
            onChange={(e) => setDilutionFactor(e.target.value)} sx={{ maxWidth: 200 }}
          />
          <Box sx={{ overflowX: "auto" }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Location</TableCell>
                  <TableCell>Limits (Alert / Action / Spec)</TableCell>
                  <TableCell>CFU Count</TableCell>
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
                      <TextField
                        size="small" type="number" sx={{ width: 100 }}
                        value={cfuValues[r.id] ?? ""}
                        onChange={(e) => setCfuValues((c) => ({ ...c, [r.id]: e.target.value }))}
                      />
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
