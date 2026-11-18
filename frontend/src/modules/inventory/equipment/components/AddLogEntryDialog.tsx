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
import { DateField } from "../../../../components/DateField";
import { useTranslation } from "../../../../i18n";

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
  const { t, i18n } = useTranslation("inventory");
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
      setError(t("equipment.addLogDialog.errors.workDoneRequired"));
      return;
    }
    if (!performedOn) {
      setError(t("equipment.addLogDialog.errors.dateRequired"));
      return;
    }

    const today = new Date().toISOString().slice(0, 10);
    if (performedOn > today) {
      setError(t("equipment.addLogDialog.errors.dateFuture"));
      return;
    }

    setSigning(true);
  };

  const enT = i18n.getFixedT("en", "inventory");
  const enTypeLabel = enT(`equipment.maintenance.entryTypes.${entryType}`);
  const meaningStatement = enT("equipment.addLogDialog.meaningStatement", {
    type: enTypeLabel.toLowerCase(),
    code: equipmentCode,
    date: performedOn
  });
  const meaningStatementTranslation = t("equipment.addLogDialog.meaningStatement", {
    type: t(`equipment.maintenance.entryTypes.${entryType}`),
    code: equipmentCode,
    date: performedOn
  });

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
        title={t("equipment.addLogDialog.title")}
        onClose={onClose}
        maxWidth="sm"
        actions={
          <>
            <Button onClick={onClose} color="inherit">
              {t("common.cancel")}
            </Button>
            <Button
              id="add-log-entry-submit"
              variant="contained"
              onClick={handleContinueToSign}
              disabled={!canContinue}
            >
              {t("equipment.addLogDialog.continueToSign")}
            </Button>
          </>
        }
      >
        <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 2 }}>
          {t("equipment.addLogDialog.subtitle")}
        </Typography>

        {error && (
          <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <Stack spacing={2}>
          <FormControl size="small" fullWidth>
            <InputLabel id="entry-type-label">{t("equipment.addLogDialog.entryType")}</InputLabel>
            <Select
              labelId="entry-type-label"
              id="log-entry-type-select"
              value={entryType}
              label={t("equipment.addLogDialog.entryType")}
              onChange={(e) => setEntryType(e.target.value as MaintenanceEntryType)}
            >
              <MenuItem value="PreventiveMaintenance">{t("equipment.maintenance.entryTypes.PreventiveMaintenance")}</MenuItem>
              <MenuItem value="BreakdownRepair">{t("equipment.maintenance.entryTypes.BreakdownRepair")}</MenuItem>
              <MenuItem value="Other">{t("equipment.maintenance.entryTypes.Other")}</MenuItem>
            </Select>
          </FormControl>

          <DateField
            label={t("equipment.addLogDialog.datePerformed")}
            required
            fullWidth
            value={performedOn}
            onChange={(v) => setPerformedOn(v)}
            helperText={t("equipment.addLogDialog.datePerformedHelper")}
          />

          <TextField
            id="log-entry-work-done"
            label={t("equipment.addLogDialog.workDone")}
            multiline
            rows={3}
            size="small"
            required
            fullWidth
            value={workDone}
            onChange={(e) => setWorkDone(e.target.value)}
            placeholder={t("equipment.addLogDialog.workDonePlaceholder")}
          />

          <TextField
            id="log-entry-parts-replaced"
            label={t("equipment.addLogDialog.partsReplaced")}
            multiline
            rows={2}
            size="small"
            fullWidth
            value={partsReplaced}
            onChange={(e) => setPartsReplaced(e.target.value)}
            placeholder={t("equipment.addLogDialog.partsReplacedPlaceholder")}
          />

          <TextField
            id="log-entry-findings"
            label={t("equipment.addLogDialog.findings")}
            multiline
            rows={2}
            size="small"
            fullWidth
            value={findings}
            onChange={(e) => setFindings(e.target.value)}
            placeholder={t("equipment.addLogDialog.findingsPlaceholder")}
          />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={open && signing}
        meaningStatement={meaningStatement}
        meaningStatementTranslation={meaningStatementTranslation}
        onCancel={() => setSigning(false)}
        onConfirm={handleSignConfirm}
      />
    </>
  );
}
