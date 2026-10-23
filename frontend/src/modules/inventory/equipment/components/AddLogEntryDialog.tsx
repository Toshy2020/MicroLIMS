import { useEffect, useState } from "react";
import {
  Button,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  TextField,
  Typography,
  Alert,
  Stack
} from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { SignatureDialog } from "../../../../components/SignatureDialog";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import type {
  AddLogEntryRequest,
  MaintenanceEntryType,
  MaintenanceLogEntryResponse
} from "../types/equipmentTypes";
import { MAINTENANCE_ENTRY_TYPE_LABELS } from "../types/equipmentTypes";

interface Props {
  open: boolean;
  equipmentId: number;
  equipmentCode?: string;
  onClose: () => void;
  onSuccess: (entry: MaintenanceLogEntryResponse) => void;
}

export function AddLogEntryDialog({
  open,
  equipmentId,
  equipmentCode = "",
  onClose,
  onSuccess
}: Props) {
  const [entryType, setEntryType] = useState<MaintenanceEntryType>("PreventiveMaintenance");
  const [performedOn, setPerformedOn] = useState<string>("");
  const [workDone, setWorkDone] = useState<string>("");
  const [partsReplaced, setPartsReplaced] = useState<string>("");
  const [findings, setFindings] = useState<string>("");
  const [signing, setSigning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setEntryType("PreventiveMaintenance");
      setPerformedOn(new Date().toISOString().slice(0, 10));
      setWorkDone("");
      setPartsReplaced("");
      setFindings("");
      setSigning(false);
      setError(null);
    }
  }, [open, equipmentId]);

  const canContinue = Boolean(workDone.trim() && performedOn);

  const handleContinueToSign = () => {
    setError(null);

    if (!workDone.trim()) {
      setError("Work done is required.");
      return;
    }
    if (!performedOn) {
      setError("Date performed is required.");
      return;
    }

    const today = new Date().toISOString().slice(0, 10);
    if (performedOn > today) {
      setError("Date performed cannot be in the future.");
      return;
    }

    setSigning(true);
  };

  const typeLabel = MAINTENANCE_ENTRY_TYPE_LABELS[entryType];
  const meaningStatement = `I confirm this ${typeLabel.toLowerCase()} entry for ${equipmentCode}, performed on ${performedOn}, is accurate.`;

  const handleSignConfirm = async (password: string) => {
    const payload: AddLogEntryRequest = {
      entryType,
      performedOn,
      workDone: workDone.trim(),
      partsReplaced: partsReplaced.trim() ? partsReplaced.trim() : null,
      findings: findings.trim() ? findings.trim() : null,
      calendarEventId: null,
      password
    };
    const entry = await EquipmentPlanService.addLogEntry(equipmentId, payload);
    setSigning(false);
    onSuccess(entry);
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open && !signing}
        title="Add Maintenance Log Entry"
        onClose={onClose}
        maxWidth="sm"
        actions={
          <>
            <Button onClick={onClose} color="inherit">
              Cancel
            </Button>
            <Button
              id="add-log-entry-submit"
              variant="contained"
              onClick={handleContinueToSign}
              disabled={!canContinue}
            >
              Continue to sign
            </Button>
          </>
        }
      >
        <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 2 }}>
          Record preventive maintenance, breakdown repairs, or other service activities. All entries are electronically signed and append-only.
        </Typography>

        {error && (
          <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <Stack spacing={2}>
          <FormControl size="small" fullWidth>
            <InputLabel id="entry-type-label">Entry Type</InputLabel>
            <Select
              labelId="entry-type-label"
              id="log-entry-type-select"
              value={entryType}
              label="Entry Type"
              onChange={(e) => setEntryType(e.target.value as MaintenanceEntryType)}
            >
              <MenuItem value="PreventiveMaintenance">Preventive Maintenance</MenuItem>
              <MenuItem value="BreakdownRepair">Breakdown Repair</MenuItem>
              <MenuItem value="Other">Other</MenuItem>
            </Select>
          </FormControl>

          <TextField
            id="log-entry-date"
            label="Date Performed"
            type="date"
            size="small"
            required
            fullWidth
            slotProps={{ inputLabel: { shrink: true } }}
            value={performedOn}
            onChange={(e) => setPerformedOn(e.target.value)}
            helperText="Date when work was performed"
          />

          <TextField
            id="log-entry-work-done"
            label="Work Done"
            multiline
            rows={3}
            size="small"
            required
            fullWidth
            value={workDone}
            onChange={(e) => setWorkDone(e.target.value)}
            placeholder="Describe what work or servicing was performed"
          />

          <TextField
            id="log-entry-parts-replaced"
            label="Parts Replaced (Optional)"
            multiline
            rows={2}
            size="small"
            fullWidth
            value={partsReplaced}
            onChange={(e) => setPartsReplaced(e.target.value)}
            placeholder="List any components, filters, or parts replaced"
          />

          <TextField
            id="log-entry-findings"
            label="Findings (Optional)"
            multiline
            rows={2}
            size="small"
            fullWidth
            value={findings}
            onChange={(e) => setFindings(e.target.value)}
            placeholder="Note any observations, issues found, or follow-up recommendations"
          />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={open && signing}
        meaningStatement={meaningStatement}
        onCancel={() => setSigning(false)}
        onConfirm={handleSignConfirm}
      />
    </>
  );
}
