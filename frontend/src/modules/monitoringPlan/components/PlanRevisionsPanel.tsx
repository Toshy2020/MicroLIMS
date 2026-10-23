import { useCallback, useEffect, useState } from "react";
import { Alert, Box, Button, Paper, Stack, TextField, Typography } from "@mui/material";
import { useHasPermission } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { MonitoringPlanService } from "../services/MonitoringPlanService";
import { CATEGORY_LABEL, periodLabel } from "../statusDisplay";
import type { PlanPreview, PlanRevision } from "../types";

const dates = (target: string | null, start: string, end: string) =>
  `${target ? `target ${target}, ` : ""}${start} to ${end}`;

// Generates the year's plan from the master data, shows what it would
// contain and approves it. Occurrences, manual entries and the calendar only
// exist once a plan is approved.
export function PlanRevisionsPanel({ year, onApproved }: { year: number; onApproved: () => void }) {
  const canManage = useHasPermission(PERMISSIONS.MONITORING_PLAN_MANAGE);
  const canApprove = useHasPermission(PERMISSIONS.MONITORING_PLAN_APPROVE);
  const [revisions, setRevisions] = useState<PlanRevision[]>([]);
  const [preview, setPreview] = useState<PlanPreview | null>(null);
  const [password, setPassword] = useState("");
  const [reason, setReason] = useState("");
  const [goLive, setGoLive] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    try {
      const list = await MonitoringPlanService.getRevisions(year);
      setRevisions(list);
      const draft = list.find((r) => r.status === "Draft");
      setPreview(draft ? await MonitoringPlanService.getPreview(draft.id) : null);
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not load the plan revisions.");
    }
  }, [year]);

  useEffect(() => { void load(); }, [load]);

  const run = async (action: () => Promise<unknown>, failure: string) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      setPassword("");
      setReason("");
      await load();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? failure);
    } finally {
      setBusy(false);
    }
  };

  const approved = revisions.find((r) => r.status === "Approved");
  const draft = preview?.revision;

  return (
    <Stack spacing={2}>
      {error && <Alert severity="error">{error}</Alert>}
      <Alert severity={approved ? "success" : "warning"}>
        {approved
          ? `The ${year} plan is approved (revision ${approved.revisionNumber}, ${approved.occurrenceCount} occurrences).`
          : `There is no approved plan for ${year} yet. Generate a draft, check it, then approve it.`}
      </Alert>

      {canManage && (
        <Box sx={{ display: "flex", gap: 2, alignItems: "flex-start", flexWrap: "wrap" }}>
          <TextField
            size="small" type="date" label="Go-live date (optional)" value={goLive}
            onChange={(e) => setGoLive(e.target.value)} slotProps={{ inputLabel: { shrink: true } }}
            helperText="The plan starts on this date. Nothing before it is planned. Empty means today."
            sx={{ minWidth: 260 }}
          />
          <Button
            variant="outlined" disabled={busy} sx={{ mt: 0.25 }}
            onClick={() => void run(() => MonitoringPlanService.generateDraft(year, goLive || undefined), "Could not generate the draft.")}
          >
            {draft ? "Regenerate draft" : approved ? "Generate revision" : "Generate draft"}
          </Button>
        </Box>
      )}

      {preview && draft && (
        <Paper variant="outlined" sx={{ p: 2 }}>
          <Typography sx={{ fontWeight: 600, mb: 1 }}>
            Draft revision {draft.revisionNumber}: {draft.occurrenceCount} occurrences from {draft.effectiveFrom}
          </Typography>
          {preview.totals.map((t) => (
            <Typography key={t.category} sx={{ fontSize: 14 }}>
              {CATEGORY_LABEL[t.category]}: {t.targets} targets, {t.occurrences} occurrences
            </Typography>
          ))}
          {preview.toReschedule.length > 0 && (
            <Box sx={{ mt: 1 }}>
              <Typography sx={{ fontSize: 14 }}>
                Approving moves {preview.toReschedule.length} occurrence(s) that have not started to new dates, for example away from days the lab does not work.
              </Typography>
              {preview.toReschedule.slice(0, 8).map((m) => (
                <Typography key={m.occurrenceId} sx={{ fontSize: 13, color: "text.secondary" }}>
                  {CATEGORY_LABEL[m.category]} · {m.target} · {periodLabel(m.periodKey)}: {dates(m.oldTargetDate, m.oldWindowStart, m.oldWindowEnd)} → {dates(m.newTargetDate, m.newWindowStart, m.newWindowEnd)}
                </Typography>
              ))}
              {preview.toReschedule.length > 8 && (
                <Typography sx={{ fontSize: 13, color: "text.secondary" }}>…and {preview.toReschedule.length - 8} more.</Typography>
              )}
            </Box>
          )}
          {preview.toRetire.length > 0 && (
            <Typography sx={{ fontSize: 14, mt: 1 }}>
              Approving retires {preview.toRetire.length} existing occurrence(s) that no longer match the master data.
            </Typography>
          )}
          {preview.exceptions.length > 0 && (
            <Box sx={{ mt: 1.5 }}>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>Not planned, or adjusted ({preview.exceptions.length})</Typography>
              {preview.exceptions.map((x, i) => (
                <Typography key={i} sx={{ fontSize: 13, color: "text.secondary" }}>
                  {CATEGORY_LABEL[x.category]} · {x.target}: {x.message}
                </Typography>
              ))}
            </Box>
          )}
          {canApprove && (
            <Stack spacing={1.5} sx={{ mt: 2, maxWidth: 420 }}>
              {approved && (
                <TextField label="Reason for the revision" required value={reason} onChange={(e) => setReason(e.target.value)} />
              )}
              <TextField
                label="Your password" type="password" autoComplete="current-password"
                value={password} onChange={(e) => setPassword(e.target.value)}
              />
              <Box>
                <Button
                  variant="contained" disabled={busy || !password || (!!approved && reason.trim().length < 5)}
                  onClick={() => void run(async () => {
                    await MonitoringPlanService.approve(draft.id, password, reason.trim() || undefined);
                    onApproved();
                  }, "Could not approve the plan.")}
                >
                  Approve plan
                </Button>
              </Box>
            </Stack>
          )}
        </Paper>
      )}
    </Stack>
  );
}
