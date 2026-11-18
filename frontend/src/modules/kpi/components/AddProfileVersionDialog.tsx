import { useEffect, useState } from "react";
import { Button, Stack, TextField, Typography } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { workflowProfileService } from "../services/workflowProfileService";
import { workflowTypeLabel, type LiveWorkflowType, type WorkflowProfileVersion } from "../types/workflowProfileTypes";
import { DateField } from "../../../components/DateField";
import { i18n, useTranslation } from "../../../i18n";

interface AddProfileVersionDialogProps {
  open: boolean;
  onClose: () => void;
  onDone: () => void;
  workflowType: LiveWorkflowType;
  testCode: string | null;
  // Present when a Draft is being reviewed and signed instead of a new version being added.
  draft?: WorkflowProfileVersion;
}

// The server compares against today in UTC, so the form does too.
const todayUtc = () => new Date().toISOString().slice(0, 10);
const parse = (v: string): number | null => (v.trim() === "" ? null : Number(v));

// Step 1: the values and a reason. Step 2: SignatureDialog. Same two-step flow as
// ChangeMediaProductCodeDialog. All rules (dates, ranges, retired types) are enforced by the server.
export function AddProfileVersionDialog({ open, onClose, onDone, workflowType, testCode, draft }: AddProfileVersionDialogProps) {
  const { t } = useTranslation("kpi");
  const [units, setUnits] = useState("");
  const [target, setTarget] = useState("");
  const [effectiveFrom, setEffectiveFrom] = useState(todayUtc());
  const [reason, setReason] = useState("");
  const [signatureOpen, setSignatureOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    setUnits(draft?.workUnits?.toString() ?? "");
    setTarget(draft?.targetAnalystDays?.toString() ?? "");
    // An imported Draft carries the migration day; a past date would be refused, so offer today.
    const today = todayUtc();
    setEffectiveFrom(draft && draft.effectiveFrom >= today ? draft.effectiveFrom : today);
    setReason("");
    setSignatureOpen(false);
  }, [open, draft]);

  const u = parse(units);
  const targetValue = parse(target);
  const valuesOk =
    (u !== null || targetValue !== null) &&
    (u === null || (Number.isFinite(u) && u > 0)) &&
    (targetValue === null || (Number.isFinite(targetValue) && targetValue > 0));
  const canContinue = valuesOk && reason.trim() !== "" && /^\d{4}-\d{2}-\d{2}$/.test(effectiveFrom);

  // The recorded statement is the English text; the Arabic one is shown beside it when the user works in Arabic.
  const englishT = i18n.getFixedT("en", "kpi");
  const meaningValues = (tf: (key: string, options?: Record<string, string | number>) => string) => ({
    action: tf(draft ? "signature.signing" : "signature.adding"),
    scope: testCode ? tf("signature.scopeTest", { code: testCode }) : tf("signature.scopeType", { type: workflowTypeLabel(workflowType) }),
    units: u ?? tf("signature.inherited"),
    target: targetValue ?? tf("signature.inherited"),
    from: effectiveFrom,
    reason: reason.trim()
  });
  const meaningStatement = englishT("signature.meaning", meaningValues(englishT));
  const meaningStatementTranslation = t("signature.meaning", meaningValues(t));

  const handleConfirm = async (password: string) => {
    if (draft) {
      await workflowProfileService.signDraft(draft.id, draft.version, {
        workUnits: u,
        targetAnalystDays: targetValue,
        effectiveFrom,
        reasonForChange: reason.trim(),
        password
      });
    } else {
      await workflowProfileService.create({
        workflowType,
        testCode,
        workUnits: u,
        targetAnalystDays: targetValue,
        effectiveFrom,
        reasonForChange: reason.trim(),
        password
      });
    }
    setSignatureOpen(false);
    onDone();
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open && !signatureOpen}
        title={`${draft ? t("dialog.reviewAndSign") : t("dialog.addVersion")}: ${testCode ?? workflowTypeLabel(workflowType)}`}
        onClose={onClose}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={onClose}>{t("dialog.cancel")}</Button>
            <Button variant="contained" disabled={!canContinue} onClick={() => setSignatureOpen(true)}>
              {t("dialog.continueToSign")}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2" color="text.secondary">
            {testCode
              ? t("dialog.inheritHint")
              : t("dialog.appliesToAll")}
          </Typography>
          <TextField
            label={t("dialog.workUnits")}
            type="number"
            size="small"
            value={units}
            onChange={(e) => setUnits(e.target.value)}
            helperText={t("dialog.unitHelp")}
            slotProps={{ htmlInput: { min: 0, step: 0.01 } }}
            autoFocus
          />
          <TextField
            label={t("dialog.targetDays")}
            type="number"
            size="small"
            value={target}
            onChange={(e) => setTarget(e.target.value)}
            helperText={t("dialog.targetHelp")}
            slotProps={{ htmlInput: { min: 0, step: 0.01 } }}
          />
          <DateField
            label={t("dialog.effectiveFrom")}
            value={effectiveFrom}
            onChange={(v) => setEffectiveFrom(v)}
            helperText={t("dialog.effectiveHelp")}
          />
          <TextField
            label={t("dialog.reason")}
            required
            multiline
            rows={3}
            size="small"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder={t("dialog.reasonPlaceholder")}
          />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={open && signatureOpen}
        meaningStatement={meaningStatement}
        meaningStatementTranslation={meaningStatementTranslation}
        onCancel={() => setSignatureOpen(false)}
        onConfirm={handleConfirm}
      />
    </>
  );
}
