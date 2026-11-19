import { useCallback, useEffect, useState } from "react";
import { Alert, Box, Button, Paper, Stack, TextField, Typography } from "@mui/material";
import { Trans } from "react-i18next";
import { DateField } from "../../../components/DateField";
import { Ltr } from "../../../components/Ltr";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { useAuth, useHasPermission } from "../../../contexts/AuthContext";
import { useTranslation } from "../../../i18n";
import { PERMISSIONS } from "../../../routes/routes";
import { serverMessage } from "../../../utils/errorMessage";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import { EQUIPMENT_TASK_KEY } from "../statusDisplay";
import type { EquipmentPlanPreview, EquipmentPlanRevision } from "../types";

export function EquipmentPlanPanel({ year, onApproved }: { year: number; onApproved: () => void }) {
  const { t } = useTranslation("monitoring");
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
    } catch (e: unknown) {
      setError(serverMessage(e) ?? t("errors.loadEquipmentPlan"));
    }
  }, [year, t]);

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
    } catch (e: unknown) {
      setError(serverMessage(e) ?? failure);
    } finally {
      setBusy(false);
    }
  };

  const approved = revisions.find((r) => r.status === "Approved");
  const draft = preview?.revision;
  const isGeneratingUser = Boolean(draft && userId != null && draft.generatedByUserId === userId);
  const trimmedReason = reason.trim();

  return (
    <Stack spacing={2}>
      <Typography variant="h6" sx={{ fontWeight: 600, mt: 2 }}>
        {t("equipmentPlan.title")}
      </Typography>

      {error && <Alert severity="error">{error}</Alert>}

      <Alert severity={approved ? "success" : "warning"}>
        {approved ? (
          <Trans
            i18nKey="equipmentPlan.approvedAlert"
            ns="monitoring"
            values={{ year, rev: approved.revisionNumber, count: approved.taskCount }}
            components={{ ltr: <Ltr>{null}</Ltr> }}
          />
        ) : (
          <Trans
            i18nKey="equipmentPlan.noApprovedAlert"
            ns="monitoring"
            values={{ year }}
            components={{ ltr: <Ltr>{null}</Ltr> }}
          />
        )}
      </Alert>

      {canManage && (
        <Box sx={{ display: "flex", gap: 2, alignItems: "flex-start", flexWrap: "wrap" }}>
          <DateField
            label={t("plan.goLiveLabel")}
            value={goLive}
            onChange={(v) => setGoLive(v)}
            helperText={t("equipmentPlan.goLiveHelper")}
            sx={{ minWidth: 260 }}
          />
          <Button
            variant="outlined"
            disabled={busy}
            sx={{ mt: 0.25 }}
            onClick={() =>
              void run(
                () => EquipmentPlanService.generateDraft(year, goLive || undefined),
                t("errors.generateDraft")
              )
            }
          >
            {draft ? t("plan.regenerateDraft") : approved ? t("plan.generateRevision") : t("plan.generateDraft")}
          </Button>
        </Box>
      )}

      {preview && draft && (
        <Paper variant="outlined" sx={{ p: 2 }}>
          <Typography sx={{ fontWeight: 600, mb: 1 }}>
            <Trans
              i18nKey="equipmentPlan.draftSummary"
              ns="monitoring"
              values={{ rev: draft.revisionNumber, count: draft.taskCount, date: draft.effectiveFrom }}
              components={{ ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>

          {preview.totals.map((tot) => (
            <Typography key={tot.taskType} sx={{ fontSize: 14 }}>
              <Trans
                i18nKey="equipmentPlan.typeTotals"
                ns="monitoring"
                values={{ taskType: t(EQUIPMENT_TASK_KEY[tot.taskType]), instruments: tot.instruments, tasks: tot.tasks }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
          ))}

          {preview.toReschedule.length > 0 && (
            <Box sx={{ mt: 1 }}>
              <Typography sx={{ fontSize: 14 }}>
                <Trans
                  i18nKey="equipmentPlan.rescheduleIntro"
                  ns="monitoring"
                  values={{ count: preview.toReschedule.length }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
              {preview.toReschedule.slice(0, 8).map((m) => (
                <Typography key={m.taskId} sx={{ fontSize: 13, color: "text.secondary" }}>
                  <Ltr>{m.equipmentCode}</Ltr> · {t(EQUIPMENT_TASK_KEY[m.taskType])}: <Ltr>{m.oldDueDate}</Ltr> → <Ltr>{m.newDueDate}</Ltr>
                </Typography>
              ))}
              {preview.toReschedule.length > 8 && (
                <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                  <Trans
                    i18nKey="plan.andMore"
                    ns="monitoring"
                    values={{ count: preview.toReschedule.length - 8 }}
                    components={{ ltr: <Ltr>{null}</Ltr> }}
                  />
                </Typography>
              )}
            </Box>
          )}

          {preview.toRetire.length > 0 && (
            <Typography sx={{ fontSize: 14, mt: 1 }}>
              <Trans
                i18nKey="equipmentPlan.retireIntro"
                ns="monitoring"
                values={{ count: preview.toRetire.length }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
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

            const renderException = (x: typeof preview.exceptions[number], i: number, indented: boolean) => (
              <Typography key={i} sx={{ fontSize: 13, color: "text.secondary", ...(indented ? { pl: 2 } : { mt: 0.5 }) }}>
                <Ltr>{x.equipmentCode}</Ltr> · {x.taskType ? t(EQUIPMENT_TASK_KEY[x.taskType]) : t("equipmentPlan.instrumentFallback")}: {x.message}
              </Typography>
            );

            return (
              <Box sx={{ mt: 1.5 }}>
                <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
                  <Trans
                    i18nKey="plan.exceptionsHeading"
                    ns="monitoring"
                    values={{ count: preview.exceptions.length }}
                    components={{ ltr: <Ltr>{null}</Ltr> }}
                  />
                </Typography>

                {possibleDuplicates.length > 0 && (
                  <Box sx={{ mt: 0.5 }}>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                      <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                        {t("equipmentPlan.possibleDuplicates")}
                      </Typography>
                      <Button
                        size="small"
                        variant="text"
                        onClick={() => setShowPossibleDuplicates((prev) => !prev)}
                        sx={{ textTransform: "none", fontSize: 12, p: 0, minWidth: "auto" }}
                      >
                        {showPossibleDuplicates ? t("equipmentPlan.hide") : t("equipmentPlan.show")}
                      </Button>
                    </Box>
                    {showPossibleDuplicates && (
                      <Box sx={{ mt: 0.5 }}>
                        {possibleDuplicates.map((x, i) => renderException(x, i, true))}
                      </Box>
                    )}
                  </Box>
                )}

                {noIntervals.length > 0 && (
                  <Box sx={{ mt: 0.5 }}>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                      <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                        <Trans
                          i18nKey="equipmentPlan.noInterval"
                          ns="monitoring"
                          count={noIntervals.length}
                          values={{ count: noIntervals.length }}
                          components={{ ltr: <Ltr>{null}</Ltr> }}
                        />
                      </Typography>
                      <Button
                        size="small"
                        variant="text"
                        onClick={() => setShowNoInterval((prev) => !prev)}
                        sx={{ textTransform: "none", fontSize: 12, p: 0, minWidth: "auto" }}
                      >
                        {showNoInterval ? t("equipmentPlan.hide") : t("equipmentPlan.show")}
                      </Button>
                    </Box>
                    {showNoInterval && (
                      <Box sx={{ mt: 0.5 }}>
                        {noIntervals.map((x, i) => renderException(x, i, true))}
                      </Box>
                    )}
                  </Box>
                )}

                {otherExceptions.map((x, i) => renderException(x, i, false))}
              </Box>
            );
          })()}

          {isGeneratingUser && (
            <Alert severity="warning" sx={{ mt: 2 }}>
              {t("equipmentPlan.sameUserWarning")}
            </Alert>
          )}

          {canApprove && (
            <Stack spacing={1.5} sx={{ mt: 2, maxWidth: 420 }}>
              {approved && (
                <TextField
                  label={t("plan.revisionReasonLabel")}
                  required
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                />
              )}
              <Box>
                <Button
                  variant="contained"
                  disabled={busy || isGeneratingUser || (!!approved && trimmedReason.length < 5)}
                  onClick={() => setSigning(true)}
                >
                  {t("commonFields.continueToSign")}
                </Button>
              </Box>
            </Stack>
          )}
        </Paper>
      )}

      {draft && (
        <SignatureDialog
          open={signing}
          meaningStatement={`I am approving the ${year} equipment calibration and maintenance plan (revision ${draft.revisionNumber}), which starts on ${draft.effectiveFrom} with ${draft.taskCount} planned task(s).${trimmedReason ? ` Reason: ${trimmedReason}` : ""}`}
          meaningStatementTranslation={t("equipmentPlan.signatureMeaning", {
            year,
            rev: draft.revisionNumber,
            date: draft.effectiveFrom,
            count: draft.taskCount,
            reasonSuffix: trimmedReason ? t("plan.reasonSuffix", { reason: trimmedReason }) : ""
          })}
          onCancel={() => setSigning(false)}
          onConfirm={async (password) => {
            await EquipmentPlanService.approve(draft.id, password, trimmedReason || undefined);
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
