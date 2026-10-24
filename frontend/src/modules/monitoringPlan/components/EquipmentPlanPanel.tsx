import { useCallback, useEffect, useState } from "react";
import { Alert, Box, Button, Paper, Stack, TextField, Typography } from "@mui/material";
import { useAuth, useHasPermission } from "../../../contexts/AuthContext";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { PERMISSIONS } from "../../../routes/routes";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import type { EquipmentPlanPreview, EquipmentPlanRevision } from "../types";
import { DateField } from "../../../components/DateField";

export function EquipmentPlanPanel({ year, onApproved }: { year: number; onApproved: () => void }) {
  const { userId } = useAuth();
  const hasEqManage = useHasPermission("EquipmentPlan.Manage");
  const hasMonManage = useHasPermission(PERMISSIONS.MONITORING_PLAN_MANAGE);
  const canManage = hasEqManage || hasMonManage;

  const hasEqApprove = useHasPermission("EquipmentPlan.Approve");
  const hasMonApprove = useHasPermission(PERMISSIONS.MONITORING_PLAN_APPROVE);
  const canApprove = hasEqApprove || hasMonApprove;

  const [revisions, setRevisions] = useState<EquipmentPlanRevision[]>([]);
  const [preview, setPreview] = useState<EquipmentPlanPreview | null>(null);
  const [showNoInterval, setShowNoInterval] = useState(false);
  const [showPossibleDuplicates, setShowPossibleDuplicates] = useState(true);
  const [signing, setSigning] = useState(false);
  const [reason, setReason] = useState("");
  const [goLive, setGoLive] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    try {
      const list = await EquipmentPlanService.getRevisions(year);
      setRevisions(list);
      const draft = list.find((r) => r.status === "Draft");
      setPreview(draft ? await EquipmentPlanService.getPreview(draft.id) : null);
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not load the equipment plan revisions.");
    }
  }, [year]);

  useEffect(() => {
    void load();
  }, [load]);

  const run = async (action: () => Promise<unknown>, failure: string) => {
    setBusy(true);
    setError(null);
    try {
      await action();
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
  const isGeneratingUser = Boolean(draft && userId != null && draft.generatedByUserId === userId);

  return (
    <Stack spacing={2}>
      <Typography variant="h6" sx={{ fontWeight: 600, mt: 2 }}>
        Equipment Calibration & Maintenance Plan
      </Typography>

      {error && <Alert severity="error">{error}</Alert>}

      <Alert severity={approved ? "success" : "warning"}>
        {approved
          ? `The ${year} equipment plan is approved (revision ${approved.revisionNumber}, ${approved.taskCount} tasks).`
          : `There is no approved equipment plan for ${year} yet. Generate a draft, check it, then approve it.`}
      </Alert>

      {canManage && (
        <Box sx={{ display: "flex", gap: 2, alignItems: "flex-start", flexWrap: "wrap" }}>
          <DateField
            label="Go-live date (optional)"
            value={goLive}
            onChange={(v) => setGoLive(v)}
            helperText="The equipment plan starts on this date. Empty means today."
            sx={{ minWidth: 260 }}
          />
          <Button
            variant="outlined"
            disabled={busy}
            sx={{ mt: 0.25 }}
            onClick={() =>
              void run(
                () => EquipmentPlanService.generateDraft(year, goLive || undefined),
                "Could not generate the draft."
              )
            }
          >
            {draft ? "Regenerate draft" : approved ? "Generate revision" : "Generate draft"}
          </Button>
        </Box>
      )}

      {preview && draft && (
        <Paper variant="outlined" sx={{ p: 2 }}>
          <Typography sx={{ fontWeight: 600, mb: 1 }}>
            Draft revision {draft.revisionNumber}: {draft.taskCount} tasks from {draft.effectiveFrom}
          </Typography>

          {preview.totals.map((t) => (
            <Typography key={t.taskType} sx={{ fontSize: 14 }}>
              {t.taskType === "Calibration" ? "Calibration" : "Preventive maintenance"}: {t.instruments} instruments, {t.tasks} tasks
            </Typography>
          ))}

          {preview.toReschedule.length > 0 && (
            <Box sx={{ mt: 1 }}>
              <Typography sx={{ fontSize: 14 }}>
                Approving moves {preview.toReschedule.length} task(s) to new dates.
              </Typography>
              {preview.toReschedule.slice(0, 8).map((m) => (
                <Typography key={m.taskId} sx={{ fontSize: 13, color: "text.secondary" }}>
                  {m.equipmentCode} · {m.taskType}: {m.oldDueDate} → {m.newDueDate}
                </Typography>
              ))}
              {preview.toReschedule.length > 8 && (
                <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                  …and {preview.toReschedule.length - 8} more.
                </Typography>
              )}
            </Box>
          )}

          {preview.toRetire.length > 0 && (
            <Typography sx={{ fontSize: 14, mt: 1 }}>
              Approving retires {preview.toRetire.length} existing task(s) that no longer match the master data.
            </Typography>
          )}

          {preview.exceptions.length > 0 && (() => {
            const possibleDuplicates = preview.exceptions.filter(
              (x) => x.kind === "PossibleDuplicate"
            );
            const noIntervals = preview.exceptions.filter(
              (x) => x.kind === "NoInterval" || x.message?.toLowerCase().includes("no calibration or maintenance interval")
            );
            const otherExceptions = preview.exceptions.filter(
              (x) =>
                x.kind !== "PossibleDuplicate" &&
                x.kind !== "NoInterval" &&
                !x.message?.toLowerCase().includes("no calibration or maintenance interval")
            );

            return (
              <Box sx={{ mt: 1.5 }}>
                <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
                  Not planned, or adjusted ({preview.exceptions.length})
                </Typography>

                {possibleDuplicates.length > 0 && (
                  <Box sx={{ mt: 0.5 }}>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                      <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                        Possible duplicate instruments (not planned)
                      </Typography>
                      <Button
                        size="small"
                        variant="text"
                        onClick={() => setShowPossibleDuplicates((prev) => !prev)}
                        sx={{ textTransform: "none", fontSize: 12, p: 0, minWidth: "auto" }}
                      >
                        {showPossibleDuplicates ? "Hide" : "Show"}
                      </Button>
                    </Box>
                    {showPossibleDuplicates && (
                      <Box sx={{ mt: 0.5 }}>
                        {possibleDuplicates.map((x, i) => (
                          <Typography key={i} sx={{ fontSize: 13, color: "text.secondary", pl: 2 }}>
                            {x.equipmentCode} · {x.taskType ?? "Instrument"}: {x.message}
                          </Typography>
                        ))}
                      </Box>
                    )}
                  </Box>
                )}

                {noIntervals.length > 0 && (
                  <Box sx={{ mt: 0.5 }}>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                      <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                        {noIntervals.length} {noIntervals.length === 1 ? "instrument has" : "instruments have"} no calibration or maintenance interval
                      </Typography>
                      <Button
                        size="small"
                        variant="text"
                        onClick={() => setShowNoInterval((prev) => !prev)}
                        sx={{ textTransform: "none", fontSize: 12, p: 0, minWidth: "auto" }}
                      >
                        {showNoInterval ? "Hide" : "Show"}
                      </Button>
                    </Box>
                    {showNoInterval && (
                      <Box sx={{ mt: 0.5 }}>
                        {noIntervals.map((x, i) => (
                          <Typography key={i} sx={{ fontSize: 13, color: "text.secondary", pl: 2 }}>
                            {x.equipmentCode} · {x.taskType ?? "Instrument"}: {x.message}
                          </Typography>
                        ))}
                      </Box>
                    )}
                  </Box>
                )}

                {otherExceptions.map((x, i) => (
                  <Typography key={i} sx={{ fontSize: 13, color: "text.secondary", mt: 0.5 }}>
                    {x.equipmentCode} · {x.taskType ?? "Instrument"}: {x.message}
                  </Typography>
                ))}
              </Box>
            );
          })()}

          {isGeneratingUser && (
            <Alert severity="warning" sx={{ mt: 2 }}>
              The plan must be approved by someone other than the person who generated it.
            </Alert>
          )}

          {canApprove && (
            <Stack spacing={1.5} sx={{ mt: 2, maxWidth: 420 }}>
              {approved && (
                <TextField
                  label="Reason for the revision"
                  required
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                />
              )}
              <Box>
                <Button
                  variant="contained"
                  disabled={busy || isGeneratingUser || (!!approved && reason.trim().length < 5)}
                  onClick={() => setSigning(true)}
                >
                  Continue to sign
                </Button>
              </Box>
            </Stack>
          )}
        </Paper>
      )}

      {draft && (
        <SignatureDialog
          open={signing}
          meaningStatement={`I am approving the ${year} equipment calibration and maintenance plan (revision ${draft.revisionNumber}), which starts on ${draft.effectiveFrom} with ${draft.taskCount} planned task(s).${reason.trim() ? ` Reason: ${reason.trim()}` : ""}`}
          onCancel={() => setSigning(false)}
          onConfirm={async (password) => {
            await EquipmentPlanService.approve(draft.id, password, reason.trim() || undefined);
            setSigning(false);
            setReason("");
            onApproved();
            await load();
          }}
        />
      )}
    </Stack>
  );
}
