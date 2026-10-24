import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Chip, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography
} from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { SignatureDialog } from "../../../../components/SignatureDialog";
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
        title="Apply trend limits to configuration"
        onClose={onCancel}
        maxWidth="lg"
        actions={
          <>
            <Button onClick={onCancel}>Cancel</Button>
            <Button variant="contained" disabled={!allValid || !reason.trim()} onClick={() => setSigning(true)}>
              Sign & apply ({items.length})
            </Button>
          </>
        }
      >
        <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 1.5 }}>
          Basis: {basisSummary}. New limits apply to results entered from now on; results already recorded keep the limits they were evaluated against.
        </Typography>
        {anyBelowMinimum && (
          <Alert severity="warning" sx={{ mb: 1.5 }}>
            Some locations have fewer results than the minimum for a statistical limit. Justify this in the reason for change.
          </Alert>
        )}
        <Box sx={{ overflowX: "auto" }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Location</TableCell>
                <TableCell>Configuration</TableCell>
                <TableCell align="right">n</TableCell>
                <TableCell align="right">Current alert / action</TableCell>
                <TableCell align="right">Calculated alert / action</TableCell>
                <TableCell align="right">New alert</TableCell>
                <TableCell align="right">New action</TableCell>
                <TableCell align="right">Specification</TableCell>
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
                        slotProps={{ htmlInput: { inputMode: "decimal", "aria-label": `New alert limit for ${c.subjectName}`, style: { textAlign: "right", width: 64 } } }} />
                    </TableCell>
                    <TableCell align="right">
                      <TextField size="small" value={d.action} onChange={(e) => setDraft(key(c), { action: e.target.value })}
                        slotProps={{ htmlInput: { inputMode: "decimal", "aria-label": `New action limit for ${c.subjectName}`, style: { textAlign: "right", width: 64 } } }} />
                      {overridden && <Chip size="small" color="info" label="Override" sx={{ ml: 1 }} />}
                    </TableCell>
                    <TableCell align="right">{c.target.specLimit || "—"} {c.target.unit}</TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </Box>
        <TextField
          label="Reason for change"
          required
          fullWidth
          multiline
          minRows={2}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder="e.g. Annual alert/action limit review 2026 per SOP QC-EM-012"
          sx={{ mt: 2 }}
        />
      </FloatingDialog>

      <SignatureDialog
        open={open && signing}
        meaningStatement={`I approve replacing the alert and action limits of ${items.length} configuration(s) with the values shown, derived from the trend analysis.`}
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
