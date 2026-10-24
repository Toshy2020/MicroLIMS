import { useCallback, useEffect, useState } from "react";
import { Alert, Box, Button, Divider, Stack, TextField, Typography } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { useHasPermission } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { MonitoringPlanService } from "../services/MonitoringPlanService";
import { CATEGORY_LABEL, periodLabel } from "../statusDisplay";
import type { OccurrenceDetail } from "../types";
import { OccurrenceStatusChip } from "./OccurrenceStatusChip";
import { DateField } from "../../../components/DateField";

type Mode = "view" | "skip" | "window";

const MIN_REASON = 5;

// One occurrence of the plan: what it needs, how far it has got, and its
// history. Skipping it or changing its window is a change two people sign:
// the form collects the details, then the shared signature dialog asks for
// both signatures.
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
  const [reason, setReason] = useState("");
  const [newTarget, setNewTarget] = useState("");
  const [newEnd, setNewEnd] = useState("");
  const [signing, setSigning] = useState(false);
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
    setReason("");
    setNewTarget("");
    setNewEnd("");
    setSigning(false);
    setError(null);
    if (occurrenceId !== null) void load(occurrenceId);
  }, [occurrenceId, load]);

  const summary = detail?.summary;
  const actionable = !!summary && canManage && !["Skipped", "Retired", "Performed", "PerformedExcursion"].includes(summary.status);
  const trimmedReason = reason.trim();
  const formComplete = trimmedReason.length >= MIN_REASON && (mode !== "window" || !!newEnd);

  const meaning = summary
    ? mode === "skip"
      ? `I am skipping ${summary.target} (${periodLabel(summary.periodKey)}). It will be removed from the work due and cannot be undone. Reason: ${trimmedReason}`
      : `I am changing the window of ${summary.target} (${periodLabel(summary.periodKey)})${newTarget ? ` so that it is due on ${newTarget}` : ""} and ends on ${newEnd}. Reason: ${trimmedReason}`
    : "";

  // Errors propagate to SignatureDialog, which shows them and stays open.
  const sign = async (password: string, second?: { username: string; password: string }) => {
    if (!summary || !second) return;
    const signed = {
      reason: trimmedReason, password, secondSignerUsername: second.username, secondSignerPassword: second.password
    };
    if (mode === "skip") {
      await MonitoringPlanService.skip(summary.id, summary.version, signed);
    } else {
      await MonitoringPlanService.changeWindow(summary.id, summary.version, {
        ...signed, newTargetDate: newTarget || null, newWindowEnd: newEnd
      });
    }
    setSigning(false);
    onChanged();
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={occurrenceId !== null && !signing}
        title={summary ? `${summary.target} – ${periodLabel(summary.periodKey)}` : "Occurrence"}
        onClose={onClose}
        maxWidth="sm"
        actions={
          mode === "view" ? (
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
              <Button onClick={() => setMode("view")}>Back</Button>
              <Button variant="contained" disabled={!formComplete} onClick={() => setSigning(true)}>Continue to sign</Button>
            </>
          )
        }
      >
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
          <Stack spacing={2} sx={{ mt: 1 }}>
            {mode === "window" && (
              <>
                <DateField label="New target date" value={newTarget} onChange={(v) => setNewTarget(v)} size="medium" />
                <DateField
                  label="New window end"
                  required
                  value={newEnd}
                  onChange={(v) => setNewEnd(v)}
                  helperText="Choose a working day."
                  size="medium"
                />
              </>
            )}
            {mode === "skip" && <Alert severity="warning">Skipping removes this occurrence from the work due. It cannot be undone.</Alert>}
            <TextField
              label="Reason" required multiline minRows={2} value={reason}
              onChange={(e) => setReason(e.target.value)} helperText={`At least ${MIN_REASON} characters.`}
            />
          </Stack>
        )}
      </FloatingDialog>

      <SignatureDialog
        open={signing}
        meaningStatement={meaning}
        requireSecondSigner
        secondSignerHint="A different person who is allowed to approve monitoring plan changes signs as well."
        onCancel={() => setSigning(false)}
        onConfirm={sign}
      />
    </>
  );
}
