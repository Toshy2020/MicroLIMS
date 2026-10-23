import { useCallback, useEffect, useState } from "react";
import {
  Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Divider, Stack, TextField, Typography
} from "@mui/material";
import { useHasPermission } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { MonitoringPlanService } from "../services/MonitoringPlanService";
import { CATEGORY_LABEL, periodLabel } from "../statusDisplay";
import type { OccurrenceDetail, SignedChange } from "../types";
import { OccurrenceStatusChip } from "./OccurrenceStatusChip";
import { EMPTY_SIGNED_CHANGE, SignedChangeFields, isSignedChangeComplete } from "./SignedChangeFields";

type Mode = "view" | "skip" | "window";

export function OccurrenceDialog({
  occurrenceId, onClose, onChanged
}: {
  occurrenceId: number | null;
  onClose: () => void;
  onChanged: () => void;
}) {
  const canManage = useHasPermission(PERMISSIONS.MONITORING_PLAN_MANAGE);
  const [detail, setDetail] = useState<OccurrenceDetail | null>(null);
  const [mode, setMode] = useState<Mode>("view");
  const [signed, setSigned] = useState<SignedChange>(EMPTY_SIGNED_CHANGE);
  const [newTarget, setNewTarget] = useState("");
  const [newEnd, setNewEnd] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (id: number) => {
    try {
      setDetail(await MonitoringPlanService.getDetail(id));
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not load the occurrence.");
    }
  }, []);

  useEffect(() => {
    setDetail(null);
    setMode("view");
    setSigned(EMPTY_SIGNED_CHANGE);
    setNewTarget("");
    setNewEnd("");
    setError(null);
    if (occurrenceId !== null) void load(occurrenceId);
  }, [occurrenceId, load]);

  const summary = detail?.summary;
  const actionable = !!summary && canManage && !["Skipped", "Retired", "Performed", "PerformedExcursion"].includes(summary.status);

  const submit = async () => {
    if (!summary) return;
    setBusy(true);
    setError(null);
    try {
      if (mode === "skip") {
        await MonitoringPlanService.skip(summary.id, summary.version, signed);
      } else {
        await MonitoringPlanService.changeWindow(summary.id, summary.version, {
          ...signed, newTargetDate: newTarget || null, newWindowEnd: newEnd
        });
      }
      onChanged();
      onClose();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "The change was not saved.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={occurrenceId !== null} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>{summary ? `${summary.target} – ${periodLabel(summary.periodKey)}` : "Occurrence"}</DialogTitle>
      <DialogContent dividers>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        {!summary && !error && <Typography>Loading…</Typography>}
        {summary && detail && mode === "view" && (
          <Stack spacing={1.5}>
            <Box sx={{ display: "flex", gap: 1, alignItems: "center", flexWrap: "wrap" }}>
              <OccurrenceStatusChip status={summary.status} late={summary.late} />
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                {CATEGORY_LABEL[summary.category]}{summary.group ? ` · ${summary.group}` : ""}{summary.manual ? " · manual entry" : ""}
              </Typography>
            </Box>
            <Typography sx={{ fontSize: 14 }}>
              Window {summary.windowStart} to {summary.windowEnd}
              {summary.targetDate ? ` · target ${summary.targetDate}` : ""}
              {summary.windowEnd !== summary.originalWindowEnd ? ` (originally ended ${summary.originalWindowEnd})` : ""}
            </Typography>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>Frequency: {detail.frequencySnapshot}</Typography>
            <Divider />
            <Typography sx={{ fontWeight: 600 }}>Tests ({summary.itemsPerformed} of {summary.itemsTotal} performed)</Typography>
            {detail.items.map((i) => (
              <Typography key={i.itemId} sx={{ fontSize: 14 }}>
                {i.label} – {i.progress === "None" ? "not started" : i.progress}
                {i.sampleReference ? ` (${i.sampleReference})` : ""}
              </Typography>
            ))}
            <Divider />
            <Typography sx={{ fontWeight: 600 }}>History</Typography>
            {detail.events.length === 0 && <Typography sx={{ fontSize: 13, color: "text.secondary" }}>No changes yet.</Typography>}
            {detail.events.map((ev) => (
              <Typography key={ev.id} sx={{ fontSize: 13 }}>
                {new Date(ev.performedAt).toLocaleString()} · {ev.eventType} · {ev.performedBy}
                {ev.secondSigner ? ` + ${ev.secondSigner}` : ""}
                {ev.oldValue || ev.newValue ? ` · ${ev.oldValue ?? ""} → ${ev.newValue ?? ""}` : ""}
                {ev.reason ? ` · "${ev.reason}"` : ""}
              </Typography>
            ))}
          </Stack>
        )}
        {summary && mode !== "view" && (
          <Stack spacing={2}>
            {mode === "window" && (
              <>
                <TextField label="New target date" type="date" slotProps={{ inputLabel: { shrink: true } }} value={newTarget} onChange={(e) => setNewTarget(e.target.value)} />
                <TextField label="New window end" type="date" required slotProps={{ inputLabel: { shrink: true } }} value={newEnd} onChange={(e) => setNewEnd(e.target.value)} />
              </>
            )}
            {mode === "skip" && <Alert severity="warning">Skipping removes this occurrence from the work due. It cannot be undone.</Alert>}
            <SignedChangeFields value={signed} onChange={setSigned} />
          </Stack>
        )}
      </DialogContent>
      <DialogActions>
        {mode === "view" ? (
          <>
            {actionable && (
              <Button onClick={() => { setNewTarget(summary.targetDate ?? ""); setNewEnd(summary.windowEnd); setMode("window"); }}>
                Change window
              </Button>
            )}
            {actionable && <Button color="error" onClick={() => setMode("skip")}>Skip</Button>}
            <Button onClick={onClose}>Close</Button>
          </>
        ) : (
          <>
            <Button onClick={() => setMode("view")} disabled={busy}>Back</Button>
            <Button
              variant="contained" onClick={submit}
              disabled={busy || !isSignedChangeComplete(signed) || (mode === "window" && !newEnd)}
            >
              {mode === "skip" ? "Sign and skip" : "Sign and change window"}
            </Button>
          </>
        )}
      </DialogActions>
    </Dialog>
  );
}
