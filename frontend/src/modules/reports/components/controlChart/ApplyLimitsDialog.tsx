import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Chip, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography
} from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { SignatureDialog } from "../../../../components/SignatureDialog";
import { i18n, useTranslation } from "../../../../i18n";
import { AppliedLimit, ApplyLimitItem, LimitTarget } from "../../types/controlChartTypes";
import { fmt } from "./SpcCharts";

export interface ApplyCandidate {
  target: LimitTarget;
  subjectName: string;
  proposedAlert: number | null;
  proposedAction: number | null;
  count: number;
  belowMinimumPoints: boolean;
}

interface ApplyLimitsDialogProps {
  open: boolean;
  candidates: ApplyCandidate[];
  basisSummary: string;
  onCancel: () => void;
  onSubmit: (items: ApplyLimitItem[], reason: string, password: string) => Promise<AppliedLimit[]>;
  onApplied: (applied: AppliedLimit[]) => void;
}

interface Draft {
  alert: string;
  action: string;
}

const toText = (v: number | null) => (v == null ? "" : String(v));

// Reviews the trend-derived limits before they replace the configured
// ones. Values can be overridden; the server recomputes the calculation,
// records any difference as an override, and validates every pair - this
// dialog only collects the signer's choice, reason and signature.
export function ApplyLimitsDialog({ open, candidates, basisSummary, onCancel, onSubmit, onApplied }: ApplyLimitsDialogProps) {
  const { t } = useTranslation("reports");
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const [reason, setReason] = useState("");
  const [signing, setSigning] = useState(false);

  const key = (c: ApplyCandidate) => `${c.target.kind}:${c.target.configurationId}`;

  useEffect(() => {
    if (!open) return;
    setDrafts(Object.fromEntries(candidates.map((c) => [key(c), { alert: toText(c.proposedAlert), action: toText(c.proposedAction) }])));
    setReason("");
  }, [open, candidates]);

  const parsed = candidates.map((c) => {
    const d = drafts[key(c)] ?? { alert: "", action: "" };
    const alert = d.alert.trim() === "" ? NaN : Number(d.alert);
    const action = d.action.trim() === "" ? NaN : Number(d.action);
    return { c, d, alert, action, valid: Number.isFinite(alert) && Number.isFinite(action) };
  });
  const allValid = parsed.length > 0 && parsed.every((p) => p.valid);
  const anyBelowMinimum = candidates.some((c) => c.belowMinimumPoints);

  const items: ApplyLimitItem[] = parsed.filter((p) => p.valid).map((p) => ({
    kind: p.c.target.kind,
    configurationId: p.c.target.configurationId,
    version: p.c.target.version,
    subjectName: p.c.subjectName,
    alertLimit: p.alert,
    actionLimit: p.action
  }));

  const setDraft = (k: string, patch: Partial<Draft>) =>
    setDrafts((prev) => ({ ...prev, [k]: { ...(prev[k] ?? { alert: "", action: "" }), ...patch } }));

  return (
    <>
      <FloatingDialog
        open={open && !signing}
        title={t("controlChart.applyLimits.title")}
        onClose={onCancel}
        maxWidth="lg"
        actions={
          <>
            <Button onClick={onCancel}>{t("controlChart.applyLimits.cancel")}</Button>
            <Button variant="contained" disabled={!allValid || !reason.trim()} onClick={() => setSigning(true)}>
              {t("controlChart.applyLimits.signAndApply", { count: items.length })}
            </Button>
          </>
        }
      >
        <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 1.5 }}>
          {t("controlChart.applyLimits.basis", { basisSummary })}
        </Typography>
        {anyBelowMinimum && (
          <Alert severity="warning" sx={{ mb: 1.5 }}>
            {t("controlChart.applyLimits.belowMinimumWarning")}
          </Alert>
        )}
        <Box sx={{ overflowX: "auto" }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>{t("controlChart.applyLimits.location")}</TableCell>
                <TableCell>{t("controlChart.applyLimits.configuration")}</TableCell>
                <TableCell align="right">n</TableCell>
                <TableCell align="right">{t("controlChart.applyLimits.currentAlertAction")}</TableCell>
                <TableCell align="right">{t("controlChart.applyLimits.calculatedAlertAction")}</TableCell>
                <TableCell align="right">{t("controlChart.applyLimits.newAlert")}</TableCell>
                <TableCell align="right">{t("controlChart.applyLimits.newAction")}</TableCell>
                <TableCell align="right">{t("controlChart.applyLimits.specification")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {parsed.map(({ c, d, alert, action }) => {
                const overridden =
                  (Number.isFinite(alert) && alert !== c.proposedAlert) || (Number.isFinite(action) && action !== c.proposedAction);
                return (
                  <TableRow key={key(c)}>
                    <TableCell>
                      <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{c.subjectName}</Typography>
                      {c.target.detail && <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{c.target.detail}</Typography>}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12 }}>{c.target.testType ?? c.target.testCode}</TableCell>
                    <TableCell align="right" sx={{ color: c.belowMinimumPoints ? "error.main" : undefined, fontWeight: c.belowMinimumPoints ? 700 : undefined }}>
                      {c.count}
                    </TableCell>
                    <TableCell align="right">{c.target.alertLimit || "—"} / {c.target.actionLimit || "—"}</TableCell>
                    <TableCell align="right">{fmt(c.proposedAlert)} / {fmt(c.proposedAction)}</TableCell>
                    <TableCell align="right">
                      <TextField size="small" value={d.alert} onChange={(e) => setDraft(key(c), { alert: e.target.value })}
                        slotProps={{ htmlInput: { inputMode: "decimal", "aria-label": t("controlChart.applyLimits.newAlertAria", { subject: c.subjectName }), style: { textAlign: "right", width: 64 } } }} />
                    </TableCell>
                    <TableCell align="right">
                      <TextField size="small" value={d.action} onChange={(e) => setDraft(key(c), { action: e.target.value })}
                        slotProps={{ htmlInput: { inputMode: "decimal", "aria-label": t("controlChart.applyLimits.newActionAria", { subject: c.subjectName }), style: { textAlign: "right", width: 64 } } }} />
                      {overridden && <Chip size="small" color="info" label={t("controlChart.applyLimits.override")} sx={{ ml: 1 }} />}
                    </TableCell>
                    <TableCell align="right">{c.target.specLimit || "—"} {c.target.unit}</TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </Box>
        <TextField
          label={t("controlChart.applyLimits.reasonLabel")}
          required
          fullWidth
          multiline
          minRows={2}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder={t("controlChart.applyLimits.reasonPlaceholder")}
          sx={{ mt: 2 }}
        />
      </FloatingDialog>

      <SignatureDialog
        open={open && signing}
        meaningStatement={i18n.getFixedT("en", "reports")("controlChart.applyLimits.signatureMeaning", { count: items.length })}
        meaningStatementTranslation={t("controlChart.applyLimits.signatureMeaning", { count: items.length })}
        onCancel={() => setSigning(false)}
        onConfirm={async (password) => {
          const applied = await onSubmit(items, reason.trim(), password);
          setSigning(false);
          onApplied(applied);
        }}
      />
    </>
  );
}
