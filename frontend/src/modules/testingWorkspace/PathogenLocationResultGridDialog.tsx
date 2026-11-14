import { useEffect, useState } from "react";
import { Box, Table, TableHead, TableRow, TableCell, TableBody, Button, Stack, Alert, Typography, RadioGroup, FormControlLabel, Radio } from "@mui/material";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { useTheme } from "@mui/material/styles";
import { toast } from "sonner";
import { FloatingDialog } from "../../components/FloatingDialog";
import { StatusBadge } from "../../components/StatusBadge";
import { TableSkeleton } from "../../components/TableSkeleton";
import { BenchDialog } from "../../components/bench/BenchDialog";
import { DraftRestoreNotice } from "../../components/bench/DraftRestoreNotice";
import { useBenchDraft } from "../../hooks/useBenchDraft";
import { useBenchMode } from "../../theme/BenchModeContext";
import { BENCH_MIN_TARGET } from "../../theme/benchTheme";
import { OfflineDraftNotice, classifySaveFailure, draftHeaderArgs, serverMessage, useKeptOffline } from "./benchSave";
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
  minimumDurationOverridden?: boolean;
  onClose: () => void;
  onSubmitted: () => void;
}

interface PathogenGridDraftValues {
  growth: Record<number, "yes" | "no" | "">;
}

function pickPathogenGridDraft(values: PathogenGridDraftValues): Partial<PathogenGridDraftValues> {
  return { growth: values.growth };
}

// EM/After Cleaning batch pathogen result entry - the final step's per-
// location Detected/Absent call. Sibling to LocationResultGridDialog,
// which handles the CFU/Count case instead.
export function PathogenLocationResultGridDialog({ open, testOrderId, testCode, displayName, minReadyAt, minimumDurationOverridden, onClose, onSubmitted }: Props) {
  const theme = useTheme();
  const { benchMode } = useBenchMode();
  const isTimeReady = !minReadyAt || new Date() >= minReadyAt || !!minimumDurationOverridden;

  const [rows, setRows] = useState<LocationRow[] | null>(null);
  const [growth, setGrowth] = useState<Record<number, "yes" | "no" | "">>({});
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [keptOffline, setKeptOffline] = useKeptOffline();

  const draft = useBenchDraft<PathogenGridDraftValues>({
    formKey: "pathogen-grid",
    recordKey: String(testOrderId),
    recordVersion: null,
    title: `${testCode} Results: ${displayName}`,
    values: { growth },
    pick: pickPathogenGridDraft,
    enabled: benchMode && open && rows !== null
  });

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setRows(null);
    setError(null);
    setGrowth({});
    setKeptOffline(false);
    TestWorkflowService.getLocations(testOrderId).then((data) => {
      if (!cancelled) setRows(data);
    });
    return () => {
      cancelled = true;
    };
  }, [open, testOrderId, setKeptOffline]);

  const restoreDraft = () => {
    const saved = draft.acceptRestore();
    if (!saved?.growth) return;
    setGrowth((prev) => {
      const next = { ...prev };
      for (const [idStr, val] of Object.entries(saved.growth ?? {})) {
        const id = Number(idStr);
        if (!Number.isNaN(id) && (val === "yes" || val === "no" || val === "")) {
          next[id] = val;
        }
      }
      return next;
    });
  };

  const enteredCount = rows === null ? 0 : rows.filter((r) => Boolean(growth[r.id])).length;
  const allEntered = rows !== null && rows.length > 0 && enteredCount === rows.length;
  const dirty = rows !== null && Object.values(growth).some((v) => v === "yes" || v === "no");

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
      await TestWorkflowService.recordBatchPathogenResults(
        testOrderId,
        locations,
        ...(benchMode ? draftHeaderArgs(draft.submitHeaders()) : [])
      );
      if (benchMode) await draft.onSubmitted();
      setKeptOffline(false);
      onSubmitted();
    } catch (e) {
      if (!benchMode) {
        setError(serverMessage(e) ?? "Could not record batch pathogen results.");
        return;
      }
      const failure = classifySaveFailure(e);
      if (failure.kind === "network") {
        setKeptOffline(true);
      } else if (failure.kind === "conflict") {
        await draft.onRejected(failure.message);
        setError(failure.message);
      } else {
        setError(failure.message ?? "Could not record batch pathogen results.");
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
      ? "Select a result for every location."
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
                  <TableCell sx={{ position: "sticky", left: 0, zIndex: 2, bgcolor: "background.paper" }}>Location</TableCell>
                  <TableCell>Growth Observed?</TableCell>
                  <TableCell>Status</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.map((r) => {
                  const status = liveStatus(r);
                  return (
                    <TableRow
                      key={r.id}
                      onFocus={(e) => e.currentTarget.scrollIntoView?.({ block: "nearest" })}
                    >
                      <TableCell sx={{ position: "sticky", left: 0, zIndex: 1, bgcolor: "background.paper" }}>
                        {r.locationName}{r.gradeClassification ? ` (${r.gradeClassification})` : ""}
                      </TableCell>
                      <TableCell>
                        {benchMode ? (
                          <Stack direction="row" spacing={1} role="group" aria-label={`Growth observed for ${r.locationName}`}>
                            <Button
                              variant={growth[r.id] === "yes" ? "contained" : "outlined"}
                              aria-pressed={growth[r.id] === "yes"}
                              startIcon={<WarningAmberIcon aria-hidden />}
                              onClick={() => setGrowth((g) => ({ ...g, [r.id]: "yes" }))}
                              sx={{
                                minHeight: BENCH_MIN_TARGET,
                                minWidth: 136,
                                fontWeight: growth[r.id] === "yes" ? 700 : 500,
                                ...(growth[r.id] === "yes"
                                  ? {
                                      bgcolor: theme.custom.status.detected.bg,
                                      color: theme.custom.status.detected.text,
                                      borderColor: theme.custom.status.detected.border,
                                      "&:hover": { bgcolor: theme.custom.status.detected.bg }
                                    }
                                  : {})
                              }}
                            >
                              Yes (Detected)
                            </Button>
                            <Button
                              variant={growth[r.id] === "no" ? "contained" : "outlined"}
                              aria-pressed={growth[r.id] === "no"}
                              startIcon={<CheckCircleOutlineIcon aria-hidden />}
                              onClick={() => setGrowth((g) => ({ ...g, [r.id]: "no" }))}
                              sx={{
                                minHeight: BENCH_MIN_TARGET,
                                minWidth: 136,
                                fontWeight: growth[r.id] === "no" ? 700 : 500,
                                ...(growth[r.id] === "no"
                                  ? {
                                      bgcolor: theme.custom.status.notDetected.bg,
                                      color: theme.custom.status.notDetected.text,
                                      borderColor: theme.custom.status.notDetected.border,
                                      "&:hover": { bgcolor: theme.custom.status.notDetected.bg }
                                    }
                                  : {})
                              }}
                            >
                              No (Absent)
                            </Button>
                          </Stack>
                        ) : (
                          <RadioGroup row value={growth[r.id] ?? ""} onChange={(e) => setGrowth((g) => ({ ...g, [r.id]: e.target.value as "yes" | "no" }))}>
                            <FormControlLabel value="yes" control={<Radio size="small" />} label="Yes" />
                            <FormControlLabel value="no" control={<Radio size="small" />} label="No" />
                          </RadioGroup>
                        )}
                      </TableCell>
                      <TableCell>{status ? <StatusBadge status={status} /> : "—"}</TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </Box>
          <Typography sx={{ fontSize: 13 }}>
            {detectedCount}/{rows.length} locations detected; overall: <StatusBadge status={detectedCount > 0 ? "Detected" : "Absent"} />
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
