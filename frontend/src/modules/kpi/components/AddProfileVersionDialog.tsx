import { useEffect, useState } from "react";
import { Button, Stack, TextField, Typography } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { workflowProfileService } from "../services/workflowProfileService";
import { workflowTypeLabel, type LiveWorkflowType, type WorkflowProfileVersion } from "../types/workflowProfileTypes";
import { DateField } from "../../../components/DateField";

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
  const t = parse(target);
  const valuesOk =
    (u !== null || t !== null) &&
    (u === null || (Number.isFinite(u) && u > 0)) &&
    (t === null || (Number.isFinite(t) && t > 0));
  const canContinue = valuesOk && reason.trim() !== "" && /^\d{4}-\d{2}-\d{2}$/.test(effectiveFrom);

  const scope = testCode ? `test ${testCode}` : `${workflowTypeLabel(workflowType)} type default`;
  const meaningStatement =
    `I am ${draft ? "signing" : "adding"} a workflow profile version for ${scope}: ` +
    `${u ?? "inherited"} work units (1 unit = 1 standard analyst-hour), ${t ?? "inherited"} target analyst days, ` +
    `effective ${effectiveFrom}. Reason: ${reason.trim()}`;

  const handleConfirm = async (password: string) => {
    if (draft) {
      await workflowProfileService.signDraft(draft.id, draft.version, {
        workUnits: u,
        targetAnalystDays: t,
        effectiveFrom,
        reasonForChange: reason.trim(),
        password
      });
    } else {
      await workflowProfileService.create({
        workflowType,
        testCode,
        workUnits: u,
        targetAnalystDays: t,
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
        title={`${draft ? "Review and sign" : "Add version"}: ${testCode ?? workflowTypeLabel(workflowType)}`}
        onClose={onClose}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={onClose}>Cancel</Button>
            <Button variant="contained" disabled={!canContinue} onClick={() => setSignatureOpen(true)}>
              Continue to sign
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2" color="text.secondary">
            {testCode
              ? "Leave a field empty to inherit it from the workflow type default."
              : "Applies to every test of this workflow type that has no override."}
          </Typography>
          <TextField
            label="Work units"
            type="number"
            size="small"
            value={units}
            onChange={(e) => setUnits(e.target.value)}
            helperText="1 unit = 1 standard analyst-hour of hands-on work"
            slotProps={{ htmlInput: { min: 0, step: 0.01 } }}
            autoFocus
          />
          <TextField
            label="Target analyst days"
            type="number"
            size="small"
            value={target}
            onChange={(e) => setTarget(e.target.value)}
            helperText="Analyst-stage deadline, from assignment to submission for review"
            slotProps={{ htmlInput: { min: 0, step: 0.01 } }}
          />
          <DateField
            label="Effective from"
            value={effectiveFrom}
            onChange={(v) => setEffectiveFrom(v)}
            helperText="Cannot be in the past, except for the first version of this profile"
          />
          <TextField
            label="Reason for change"
            required
            multiline
            rows={3}
            size="small"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="e.g. SOP time study 2026"
          />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={open && signatureOpen}
        meaningStatement={meaningStatement}
        onCancel={() => setSignatureOpen(false)}
        onConfirm={handleConfirm}
      />
    </>
  );
}
