import { useCallback, useEffect, useState } from "react";
import { Alert, Box, Button, Paper, Stack, TextField, Typography } from "@mui/material";
import { Trans } from "react-i18next";
import { DateField } from "../../../components/DateField";
import { Ltr } from "../../../components/Ltr";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { useHasPermission } from "../../../contexts/AuthContext";
import { useTranslation } from "../../../i18n";
import { PERMISSIONS } from "../../../routes/routes";
import { serverMessage } from "../../../utils/errorMessage";
import { MonitoringPlanService } from "../services/MonitoringPlanService";
import { CATEGORY_KEY, periodLabel } from "../statusDisplay";
import type { PlanPreview, PlanRevision } from "../types";

// Generates the year's plan from the master data, shows what it would
// contain and approves it. Occurrences, manual entries and the calendar only
// exist once a plan is approved.
export function PlanRevisionsPanel({ year, onApproved }: { year: number; onApproved: () => void }) {
  const { t } = useTranslation("monitoring");
  const canManage = useHasPermission(PERMISSIONS.MONITORING_PLAN_MANAGE);
  const canApprove = useHasPermission(PERMISSIONS.MONITORING_PLAN_APPROVE);
  const [revisions, setRevisions] = useState<PlanRevision[]>([]);
  const [preview, setPreview] = useState<PlanPreview | null>(null);
  const [signing, setSigning] = useState(false);
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
    } catch (e: unknown) {
      setError(serverMessage(e) ?? t("errors.loadPlanRevisions"));
    }
  }, [year, t]);

  useEffect(() => { void load(); }, [load]);

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
  const trimmedReason = reason.trim();

  const dates = (target: string | null, start: string, end: string) =>
    t("plan.dateRange", {
      target: target ? t("plan.targetPrefix", { date: target }) : "",
      start,
      end
    });

  return (
    <Stack spacing={2}>
      {error && <Alert severity="error">{error}</Alert>}
      <Alert severity={approved ? "success" : "warning"}>
        {approved ? (
          <Trans
            i18nKey="plan.approvedAlert"
            ns="monitoring"
            values={{ year, rev: approved.revisionNumber, count: approved.occurrenceCount }}
            components={{ ltr: <Ltr>{null}</Ltr> }}
          />
        ) : (
          <Trans
            i18nKey="plan.noApprovedAlert"
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
            helperText={t("plan.goLiveHelper")}
            sx={{ minWidth: 260 }}
          />
          <Button
            variant="outlined" disabled={busy} sx={{ mt: 0.25 }}
            onClick={() => void run(() => MonitoringPlanService.generateDraft(year, goLive || undefined), t("errors.generateDraft"))}
          >
            {draft ? t("plan.regenerateDraft") : approved ? t("plan.generateRevision") : t("plan.generateDraft")}
          </Button>
        </Box>
      )}

      {preview && draft && (
        <Paper variant="outlined" sx={{ p: 2 }}>
          <Typography sx={{ fontWeight: 600, mb: 1 }}>
            <Trans
              i18nKey="plan.draftSummary"
              ns="monitoring"
              values={{ rev: draft.revisionNumber, count: draft.occurrenceCount, date: draft.effectiveFrom }}
              components={{ ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>
          {preview.totals.map((tot) => (
            <Typography key={tot.category} sx={{ fontSize: 14 }}>
              <Trans
                i18nKey="plan.categoryTotals"
                ns="monitoring"
                values={{ category: t(CATEGORY_KEY[tot.category]), targets: tot.targets, occurrences: tot.occurrences }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
          ))}
          {preview.toReschedule.length > 0 && (
            <Box sx={{ mt: 1 }}>
              <Typography sx={{ fontSize: 14 }}>
                <Trans
                  i18nKey="plan.rescheduleIntro"
                  ns="monitoring"
                  values={{ count: preview.toReschedule.length }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
              {preview.toReschedule.slice(0, 8).map((m) => (
                <Typography key={m.occurrenceId} sx={{ fontSize: 13, color: "text.secondary" }}>
                  {t(CATEGORY_KEY[m.category])} · <Ltr>{m.target}</Ltr> · {periodLabel(m.periodKey, t)}:{" "}
                  <Ltr>{dates(m.oldTargetDate, m.oldWindowStart, m.oldWindowEnd)}</Ltr> →{" "}
                  <Ltr>{dates(m.newTargetDate, m.newWindowStart, m.newWindowEnd)}</Ltr>
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
                i18nKey="plan.retireIntro"
                ns="monitoring"
                values={{ count: preview.toRetire.length }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
          )}
          {preview.exceptions.length > 0 && (
            <Box sx={{ mt: 1.5 }}>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
                <Trans
                  i18nKey="plan.exceptionsHeading"
                  ns="monitoring"
                  values={{ count: preview.exceptions.length }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
              {preview.exceptions.map((x, i) => (
                <Typography key={i} sx={{ fontSize: 13, color: "text.secondary" }}>
                  {t(CATEGORY_KEY[x.category])} · <Ltr>{x.target}</Ltr>: {x.message}
                </Typography>
              ))}
            </Box>
          )}
          {canApprove && (
            <Stack spacing={1.5} sx={{ mt: 2, maxWidth: 420 }}>
              {approved && (
                <TextField label={t("plan.revisionReasonLabel")} required value={reason} onChange={(e) => setReason(e.target.value)} />
              )}
              <Box>
                <Button
                  variant="contained" disabled={busy || (!!approved && trimmedReason.length < 5)}
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
          meaningStatement={`I am approving the ${year} monitoring plan (revision ${draft.revisionNumber}), which starts on ${draft.effectiveFrom} with ${draft.occurrenceCount} new occurrence(s).${trimmedReason ? ` Reason: ${trimmedReason}` : ""}`}
          meaningStatementTranslation={t("plan.signatureMeaning", {
            year,
            rev: draft.revisionNumber,
            date: draft.effectiveFrom,
            count: draft.occurrenceCount,
            reasonSuffix: trimmedReason ? t("plan.reasonSuffix", { reason: trimmedReason }) : ""
          })}
          onCancel={() => setSigning(false)}
          onConfirm={async (password) => {
            await MonitoringPlanService.approve(draft.id, password, trimmedReason || undefined);
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
