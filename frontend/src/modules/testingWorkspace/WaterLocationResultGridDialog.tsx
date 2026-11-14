import { useEffect, useMemo, useState } from "react";
import { Box, Table, TableHead, TableRow, TableCell, TableBody, IconButton, Button, Stack, Alert, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import CloseIcon from "@mui/icons-material/Close";
import { toast } from "sonner";
import { FloatingDialog } from "../../components/FloatingDialog";
import { StatusBadge } from "../../components/StatusBadge";
import { TableSkeleton } from "../../components/TableSkeleton";
import { BenchDialog } from "../../components/bench/BenchDialog";
import { DraftRestoreNotice } from "../../components/bench/DraftRestoreNotice";
import { NumericEntryField } from "../../components/bench/NumericEntryField";
import { useBenchDraft } from "../../hooks/useBenchDraft";
import { useBenchMode } from "../../theme/BenchModeContext";
import { OfflineDraftNotice, classifySaveFailure, draftHeaderArgs, serverMessage, useKeptOffline } from "./benchSave";
import { TestWorkflowService } from "./services/TestWorkflowService";

interface LocationRow {
  id: number;
  locationType: string;
  locationName: string;
  alertLimit: string | null;
  actionLimit: string | null;
  specLimit: string | null;
  rawReadings: string | null;
  calculatedResult: number | null;
  status: string | null;
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

interface WaterGridDraftValues {
  readingsByLocation: Record<number, string[]>;
}

function pickWaterGridDraft(values: WaterGridDraftValues): Partial<WaterGridDraftValues> {
  return { readingsByLocation: values.readingsByLocation };
}

// Same Spec -> Action -> Alert precedence as the backend's
// TestWorkflowEngine.Compare - mirrored here only for the live preview
// as the analyst types; the server recomputes and is the authority on
// the persisted Status.
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

function isRowEntered(readings: string[] | undefined): boolean {
  const values = (readings ?? []).filter((v) => v !== "").map(Number);
  return values.length > 0 && values.every((v) => !Number.isNaN(v));
}

export function WaterLocationResultGridDialog({ open, testOrderId, testCode, displayName, minReadyAt, minimumDurationOverridden, onClose, onSubmitted }: Props) {
  const { benchMode } = useBenchMode();
  const isTimeReady = !minReadyAt || new Date() >= minReadyAt || !!minimumDurationOverridden;
  const [rows, setRows] = useState<LocationRow[] | null>(null);
  const [readingsByLocation, setReadingsByLocation] = useState<Record<number, string[]>>({});
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [keptOffline, setKeptOffline] = useKeptOffline();

  const draft = useBenchDraft<WaterGridDraftValues>({
    formKey: "water-grid",
    recordKey: String(testOrderId),
    recordVersion: null,
    title: `${testCode} Results: ${displayName}`,
    values: { readingsByLocation },
    pick: pickWaterGridDraft,
    enabled: benchMode && open && rows !== null
  });

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setRows(null);
    setError(null);
    setReadingsByLocation({});
    setKeptOffline(false);
    TestWorkflowService.getLocations(testOrderId).then((data) => {
      if (cancelled) return;
      setRows(data);
      const initial: Record<number, string[]> = {};
      data.forEach((r: LocationRow) => {
        initial[r.id] = r.rawReadings ? r.rawReadings.split(",") : [""];
      });
      setReadingsByLocation(initial);
    });
    return () => {
      cancelled = true;
    };
  }, [open, testOrderId, setKeptOffline]);

  const restoreDraft = () => {
    const saved = draft.acceptRestore();
    if (!saved?.readingsByLocation) return;
    setReadingsByLocation((prev) => {
      const next: Record<number, string[]> = { ...prev };
      for (const [idStr, arr] of Object.entries(saved.readingsByLocation ?? {})) {
        const id = Number(idStr);
        if (!Number.isNaN(id) && Array.isArray(arr) && arr.every((v) => typeof v === "string")) {
          next[id] = arr.length > 0 ? arr : [""];
        }
      }
      return next;
    });
  };

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
        return { ...r, liveAverage: null as number | null, liveStatus: null as string | null };
      }
      const average = values.reduce((a, b) => a + b, 0) / values.length;
      const status = compareStatus(average, r.alertLimit, r.actionLimit, r.specLimit);
      return { ...r, liveAverage: average, liveStatus: status };
    });
  }, [rows, readingsByLocation]);

  const enteredCount = rows ? rows.filter((r) => isRowEntered(readingsByLocation[r.id])).length : 0;
  const allEntered = rows !== null && rows.length > 0 && enteredCount === rows.length;
  const dirty = rows !== null && Object.values(readingsByLocation).some((arr) => arr.some((v) => v.trim() !== ""));

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
      await TestWorkflowService.recordWaterBatchReadings(
        testOrderId,
        rows.map((r) => ({
          sampleLocationId: r.id,
          readings: (readingsByLocation[r.id] ?? []).filter((v) => v !== "").map(Number)
        })),
        ...(benchMode ? draftHeaderArgs(draft.submitHeaders()) : [])
      );
      if (benchMode) await draft.onSubmitted();
      setKeptOffline(false);
      onSubmitted();
    } catch (e) {
      if (!benchMode) {
        setError(serverMessage(e) ?? "Could not record batch readings.");
        return;
      }
      const failure = classifySaveFailure(e);
      if (failure.kind === "network") {
        setKeptOffline(true);
      } else if (failure.kind === "conflict") {
        await draft.onRejected(failure.message);
        setError(failure.message);
      } else {
        setError(failure.message ?? "Could not record batch readings.");
      }
    } finally {
      setSubmitting(false);
    }
  };

  const closeGrid = async () => {
    if (benchMode && dirty && !draft.restored) await draft.discard();
    setKeptOffline(false);
    setError(null);
    onClose();
  };

  const keepDraft = () => {
    toast.info("Draft kept on this tablet. It is not saved to MicroLIMS.");
    onClose();
  };

  const submitDisabledReason = !rows
    ? "Loading locations…"
    : !allEntered
      ? "Enter at least one reading for every location."
      : !isTimeReady && minReadyAt
        ? `Results cannot be submitted before ${minReadyAt.toLocaleString()}.`
        : undefined;

  const content = (
    <>
      {!rows && !error && <TableSkeleton rows={5} cols={5} hasContainer={false} />}
      {error && !rows && <Alert severity="error">{error}</Alert>}
      {rows && (
        <Stack spacing={2}>
          {benchMode && <DraftRestoreNotice startedAt={draft.restoredStartedAt} onRestore={restoreDraft} onDiscard={() => void draft.discard()} />}
          {benchMode && keptOffline && <OfflineDraftNotice onTryAgain={() => setKeptOffline(false)} />}
          {error && <Alert severity="error">{error}</Alert>}
          <Typography variant="body2" aria-live="polite" role="status" sx={{ fontVariantNumeric: "tabular-nums" }}>
            {enteredCount} of {rows.length} entered
          </Typography>
          <Box sx={{ overflowX: "auto" }}>
            <Table size={benchMode ? "medium" : "small"}>
              <TableHead>
                <TableRow>
                  <TableCell sx={{ position: "sticky", left: 0, zIndex: 2, bgcolor: "background.paper" }}>Sampling Point</TableCell>
                  <TableCell>Limits (Alert / Action / Spec)</TableCell>
                  <TableCell>Plate Readings</TableCell>
                  <TableCell>Average</TableCell>
                  <TableCell>Status</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {computed.map((r) => (
                  <TableRow
                    key={r.id}
                    onFocus={(e) => e.currentTarget.scrollIntoView?.({ block: "nearest" })}
                  >
                    <TableCell sx={{ position: "sticky", left: 0, zIndex: 1, bgcolor: "background.paper" }}>
                      {r.locationName}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                      {r.alertLimit ?? "—"} / {r.actionLimit ?? "—"} / {r.specLimit ?? "—"}
                    </TableCell>
                    <TableCell>
                      <Stack spacing={1}>
                        {(readingsByLocation[r.id] ?? [""]).map((v, i) => (
                          <Stack direction="row" spacing={1} key={i} sx={{ alignItems: "center", width: "100%" }}>
                            <Box sx={{ flex: 1, minWidth: 120 }}>
                              <NumericEntryField
                                label={`${r.locationName} reading ${i + 1}`}
                                aria-label={`${r.locationName} reading ${i + 1}`}
                                kind="count"
                                value={v}
                                onChange={(val) => updateReading(r.id, i, val)}
                              />
                            </Box>
                            <IconButton
                              aria-label={`Remove reading ${i + 1} for ${r.locationName}`}
                              size={benchMode ? "large" : "small"}
                              onClick={() => removeReading(r.id, i)}
                            >
                              <CloseIcon fontSize="small" />
                            </IconButton>
                          </Stack>
                        ))}
                        <Button
                          size={benchMode ? "large" : "small"}
                          startIcon={<AddIcon />}
                          aria-label={`Add plate reading for ${r.locationName}`}
                          onClick={() => addReading(r.id)}
                          sx={{ alignSelf: "flex-start" }}
                        >
                          Add Plate
                        </Button>
                      </Stack>
                    </TableCell>
                    <TableCell sx={{ fontVariantNumeric: "tabular-nums" }}>{r.liveAverage != null ? Math.round(r.liveAverage) : "—"}</TableCell>
                    <TableCell>{r.liveStatus ? <StatusBadge status={r.liveStatus} /> : "—"}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
          <Typography sx={{ fontSize: 13 }}>
            {conformCount}/{rows.length} locations within spec; worst status: <StatusBadge status={worstStatus} />
          </Typography>
          {!isTimeReady && minReadyAt && (
            <Alert severity="warning">Results cannot be submitted before {minReadyAt.toLocaleString()}.</Alert>
          )}
          {!benchMode && (
            <Stack direction="row" sx={{ justifyContent: "flex-end" }}>
              <Button variant="contained" disabled={!allEntered || !isTimeReady || submitting} onClick={submit}>
                {submitting ? "Submitting…" : "Submit Results"}
              </Button>
            </Stack>
          )}
        </Stack>
      )}
    </>
  );

  if (benchMode) {
    return (
      <BenchDialog
        open={open}
        title={`${testCode} Results: ${displayName}`}
        onClose={() => void closeGrid()}
        onSave={keptOffline ? keepDraft : () => void submit()}
        saving={submitting}
        saveLabel={keptOffline ? "Keep draft" : "Submit Results"}
        saveDisabledReason={keptOffline ? undefined : submitDisabledReason}
        dirty={dirty}
        maxWidth="lg"
      >
        {content}
      </BenchDialog>
    );
  }

  return (
    <FloatingDialog open={open} title={`${testCode} Results: ${displayName}`} onClose={onClose}>
      {content}
    </FloatingDialog>
  );
}
