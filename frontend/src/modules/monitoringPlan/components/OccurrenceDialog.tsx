import { useCallback, useEffect, useState } from "react";
import { Alert, Box, Button, Chip, Divider, Stack, TextField, Typography } from "@mui/material";
import { Trans } from "react-i18next";
import { DateField } from "../../../components/DateField";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { Ltr } from "../../../components/Ltr";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { useAuth, useHasPermission } from "../../../contexts/AuthContext";
import { useTranslation } from "../../../i18n";
import { PERMISSIONS } from "../../../routes/routes";
import { serverMessage } from "../../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { MonitoringPlanService } from "../services/MonitoringPlanService";
import { CATEGORY_KEY, ITEM_PROGRESS_KEY, periodLabel } from "../statusDisplay";
import type { OccurrenceDetail } from "../types";
import { OccurrenceStatusChip } from "./OccurrenceStatusChip";

type Mode = "view" | "skip" | "window";

const MIN_REASON = 5;

const formatReason = (r: string | null) => (r ? ` · "${r}"` : "");

// One occurrence of the plan: what it needs, how far it has got, and its
// history. Skipping it is signed once, by a Section Head; changing its window
// is a change two people sign. The form collects the details, then the shared
// signature dialog asks for the signature(s).
export function OccurrenceDialog({
  occurrenceId, onClose, onChanged
}: {
  occurrenceId: number | null;
  onClose: () => void;
  onChanged: () => void;
}) {
  const { t } = useTranslation("monitoring");
  const canManage = useHasPermission(PERMISSIONS.MONITORING_PLAN_MANAGE);
  const { role } = useAuth();
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
    } catch (e: unknown) {
      setError(serverMessage(e) ?? t("errors.loadOccurrence"));
    }
  }, [t]);

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

  // Recorded statement stays English; Arabic translation is shown alongside it in SignatureDialog.
  const meaning = summary
    ? mode === "skip"
      ? `I am skipping ${summary.target} (${periodLabel(summary.periodKey)}). It will be removed from the work due and cannot be undone. Reason: ${trimmedReason}`
      : `I am changing the window of ${summary.target} (${periodLabel(summary.periodKey)})${newTarget ? ` so that it is due on ${newTarget}` : ""} and ends on ${newEnd}. Reason: ${trimmedReason}`
    : "";

  const meaningTranslation = summary
    ? mode === "skip"
      ? t("occurrence.skipSignatureMeaning", {
          target: summary.target,
          period: periodLabel(summary.periodKey, t),
          reason: trimmedReason
        })
      : t("occurrence.windowSignatureMeaning", {
          target: summary.target,
          period: periodLabel(summary.periodKey, t),
          targetClause: newTarget ? t("occurrence.windowTargetClause", { date: newTarget }) : "",
          end: newEnd,
          reason: trimmedReason
        })
    : undefined;

  // Errors propagate to SignatureDialog, which shows them and stays open.
  const sign = async (password: string, second?: { username: string; password: string }) => {
    if (!summary) return;
    if (mode === "skip") {
      await MonitoringPlanService.skip(summary.id, summary.version, { reason: trimmedReason, password });
    } else {
      if (!second) return;
      await MonitoringPlanService.changeWindow(summary.id, summary.version, {
        reason: trimmedReason, password, secondSignerUsername: second.username, secondSignerPassword: second.password,
        newTargetDate: newTarget || null, newWindowEnd: newEnd
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
        title={summary ? <><Ltr>{summary.target}</Ltr> – {periodLabel(summary.periodKey, t)}</> : t("occurrence.title")}
        onClose={onClose}
        maxWidth="sm"
        actions={
          mode === "view" ? (
            <>
              {actionable && (
                <Button onClick={() => { setNewTarget(summary.targetDate ?? ""); setNewEnd(summary.windowEnd); setMode("window"); }}>
                  {t("occurrence.changeWindow")}
                </Button>
              )}
              {actionable && role === "SectionHead" && <Button color="error" onClick={() => setMode("skip")}>{t("occurrence.skip")}</Button>}
              <Button onClick={onClose}>{t("commonFields.close")}</Button>
            </>
          ) : (
            <>
              <Button onClick={() => setMode("view")}>{t("commonFields.back")}</Button>
              <Button variant="contained" disabled={!formComplete} onClick={() => setSigning(true)}>{t("commonFields.continueToSign")}</Button>
            </>
          )
        }
      >
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        {!summary && !error && <Typography>{t("occurrence.loading")}</Typography>}
        {summary && detail && mode === "view" && (
          <Stack spacing={1.5}>
            <Box sx={{ display: "flex", gap: 1, alignItems: "center", flexWrap: "wrap" }}>
              <OccurrenceStatusChip status={summary.status} late={summary.late} />
              {summary.category === "Water" && (detail.labSectionCode || summary.labSectionCode) && (
                <Chip
                  size="small"
                  label={<Ltr>{detail.labSectionCode || summary.labSectionCode}</Ltr>}
                  sx={{ height: 20, fontSize: 11, fontWeight: 600 }}
                />
              )}
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                {t(CATEGORY_KEY[summary.category])}{summary.group ? ` · ${summary.group}` : ""}{summary.manual ? ` · ${t("occurrence.manualEntry")}` : ""}
              </Typography>
            </Box>
            <Typography sx={{ fontSize: 14 }}>
              <Trans
                i18nKey="occurrence.windowRange"
                ns="monitoring"
                values={{ start: summary.windowStart, end: summary.windowEnd }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
              {summary.targetDate && (
                <Trans
                  i18nKey="occurrence.targetSuffix"
                  ns="monitoring"
                  values={{ date: summary.targetDate }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              )}
              {summary.windowEnd !== summary.originalWindowEnd && (
                <Trans
                  i18nKey="occurrence.originallyEndedSuffix"
                  ns="monitoring"
                  values={{ date: summary.originalWindowEnd }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              )}
            </Typography>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              {t("occurrence.frequency", { frequency: detail.frequencySnapshot })}
            </Typography>
            <Divider />
            <Typography sx={{ fontWeight: 600 }}>
              <Trans
                i18nKey="occurrence.testsHeading"
                ns="monitoring"
                values={{ performed: summary.itemsPerformed, total: summary.itemsTotal }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
            {detail.items.map((i) => (
              <Typography key={i.itemId} sx={{ fontSize: 14 }}>
                {i.label} – {t(ITEM_PROGRESS_KEY[i.progress])}
                {i.sampleReference ? <> (<Ltr>{i.sampleReference}</Ltr>)</> : ""}
              </Typography>
            ))}
            <Divider />
            <Typography sx={{ fontWeight: 600 }}>{t("occurrence.history")}</Typography>
            {detail.events.length === 0 && <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{t("occurrence.noChanges")}</Typography>}
            {detail.events.map((ev) => (
              <Typography key={ev.id} sx={{ fontSize: 13 }}>
                <Ltr>{formatDateTimeWithTimeZone(ev.performedAt)}</Ltr> · {ev.eventType} · {ev.performedBy}
                {ev.secondSigner ? ` + ${ev.secondSigner}` : ""}
                {ev.oldValue || ev.newValue ? <> · <Ltr>{ev.oldValue ?? ""}</Ltr> → <Ltr>{ev.newValue ?? ""}</Ltr></> : ""}
                {formatReason(ev.reason)}
              </Typography>
            ))}
          </Stack>
        )}
        {summary && mode !== "view" && (
          <Stack spacing={2} sx={{ mt: 1 }}>
            {mode === "window" && (
              <>
                <DateField label={t("occurrence.newTargetDate")} value={newTarget} onChange={(v) => setNewTarget(v)} size="medium" />
                <DateField
                  label={t("occurrence.newWindowEnd")}
                  required
                  value={newEnd}
                  onChange={(v) => setNewEnd(v)}
                  helperText={t("occurrence.chooseWorkingDay")}
                  size="medium"
                />
              </>
            )}
            {mode === "skip" && <Alert severity="warning">{t("occurrence.skipWarning")}</Alert>}
            <TextField
              label={t("commonFields.reason")} required multiline minRows={2} value={reason}
              onChange={(e) => setReason(e.target.value)} helperText={t("commonFields.minChars", { min: MIN_REASON })}
            />
          </Stack>
        )}
      </FloatingDialog>

      <SignatureDialog
        open={signing}
        meaningStatement={meaning}
        meaningStatementTranslation={meaningTranslation}
        requireSecondSigner={mode === "window"}
        secondSignerHint={t("commonFields.secondSignerHint")}
        onCancel={() => setSigning(false)}
        onConfirm={sign}
      />
    </>
  );
}
