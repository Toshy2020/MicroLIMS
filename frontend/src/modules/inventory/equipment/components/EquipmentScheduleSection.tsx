import { useEffect, useState } from "react";
import {
  Box,
  Typography,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Button,
  Alert,
  Divider,
  CircularProgress,
  Stack
} from "@mui/material";
import SaveIcon from "@mui/icons-material/Save";
import VerifiedIcon from "@mui/icons-material/Verified";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import type {
  EquipmentItem,
  EquipmentScheduleResponse,
  IntervalUnit,
  SaveEquipmentScheduleRequest
} from "../types/equipmentTypes";
import { DateField } from "../../../../components/DateField";
import { useTranslation } from "../../../../i18n";

interface Props {
  equipment: EquipmentItem;
  initialSchedule?: EquipmentScheduleResponse | null;
  onSaved?: (schedule: EquipmentScheduleResponse) => void;
  onRecordCalibrationClick?: () => void;
}

export function EquipmentScheduleSection({
  equipment,
  initialSchedule,
  onSaved,
  onRecordCalibrationClick
}: Props) {
  const { t } = useTranslation("inventory");
  const [calValue, setCalValue] = useState<string>(
    initialSchedule?.calibrationIntervalValue?.toString() ??
      equipment.calibrationIntervalValue?.toString() ??
      ""
  );
  const [calUnit, setCalUnit] = useState<string>(
    initialSchedule?.calibrationIntervalUnit ??
      equipment.calibrationIntervalUnit ??
      ""
  );
  const [lastCalibratedOn, setLastCalibratedOn] = useState<string>(
    initialSchedule?.lastCalibratedOn ??
      equipment.lastCalibratedOn ??
      ""
  );

  const [pmValue, setPmValue] = useState<string>(
    initialSchedule?.pmIntervalValue?.toString() ??
      equipment.pmIntervalValue?.toString() ??
      ""
  );
  const [pmUnit, setPmUnit] = useState<string>(
    initialSchedule?.pmIntervalUnit ??
      equipment.pmIntervalUnit ??
      ""
  );
  const [lastPmOn, setLastPmOn] = useState<string>(
    initialSchedule?.lastPmOn ??
      equipment.lastPmOn ??
      ""
  );

  const [version, setVersion] = useState<number | undefined>(
    initialSchedule?.version ?? equipment.version
  );
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  useEffect(() => {
    if (initialSchedule) {
      setCalValue(initialSchedule.calibrationIntervalValue?.toString() ?? "");
      setCalUnit(initialSchedule.calibrationIntervalUnit ?? "");
      setLastCalibratedOn(initialSchedule.lastCalibratedOn ?? "");
      setPmValue(initialSchedule.pmIntervalValue?.toString() ?? "");
      setPmUnit(initialSchedule.pmIntervalUnit ?? "");
      setLastPmOn(initialSchedule.lastPmOn ?? "");
      setVersion(initialSchedule.version);
    } else if (equipment) {
      setCalValue(equipment.calibrationIntervalValue?.toString() ?? "");
      setCalUnit(equipment.calibrationIntervalUnit ?? "");
      setLastCalibratedOn(equipment.lastCalibratedOn ?? "");
      setPmValue(equipment.pmIntervalValue?.toString() ?? "");
      setPmUnit(equipment.pmIntervalUnit ?? "");
      setLastPmOn(equipment.lastPmOn ?? "");
      setVersion(equipment.version);
    }
  }, [initialSchedule, equipment]);

  const handleSave = async () => {
    setError(null);
    setSuccess(null);

    const hasCalVal = calValue.trim() !== "";
    const hasCalUnit = calUnit !== "";
    if (hasCalVal && !hasCalUnit) {
      setError(t("equipment.schedule.errors.calUnitRequired"));
      return;
    }
    if (!hasCalVal && hasCalUnit) {
      setError(t("equipment.schedule.errors.calValueRequired"));
      return;
    }

    const hasPmVal = pmValue.trim() !== "";
    const hasPmUnit = pmUnit !== "";
    if (hasPmVal && !hasPmUnit) {
      setError(t("equipment.schedule.errors.pmUnitRequired"));
      return;
    }
    if (!hasPmVal && hasPmUnit) {
      setError(t("equipment.schedule.errors.pmValueRequired"));
      return;
    }

    if (hasCalVal && hasCalUnit) {
      const num = Number(calValue);
      if (calUnit === "Weeks" && (num < 1 || num > 104)) {
        setError(t("equipment.schedule.errors.calWeeksRange"));
        return;
      }
      if (calUnit === "Months" && (num < 1 || num > 60)) {
        setError(t("equipment.schedule.errors.calMonthsRange"));
        return;
      }
      if (calUnit === "Years" && (num < 1 || num > 5)) {
        setError(t("equipment.schedule.errors.calYearsRange"));
        return;
      }
    }

    if (hasPmVal && hasPmUnit) {
      const num = Number(pmValue);
      if (pmUnit === "Weeks" && (num < 1 || num > 104)) {
        setError(t("equipment.schedule.errors.pmWeeksRange"));
        return;
      }
      if (pmUnit === "Months" && (num < 1 || num > 60)) {
        setError(t("equipment.schedule.errors.pmMonthsRange"));
        return;
      }
      if (pmUnit === "Years" && (num < 1 || num > 5)) {
        setError(t("equipment.schedule.errors.pmYearsRange"));
        return;
      }
    }

    const today = new Date().toISOString().slice(0, 10);
    if (lastCalibratedOn && lastCalibratedOn > today) {
      setError(t("equipment.schedule.errors.lastCalFuture"));
      return;
    }
    if (lastPmOn && lastPmOn > today) {
      setError(t("equipment.schedule.errors.lastPmFuture"));
      return;
    }

    const payload: SaveEquipmentScheduleRequest = {
      calibrationIntervalValue: hasCalVal ? Number(calValue) : null,
      calibrationIntervalUnit: hasCalUnit ? (calUnit as IntervalUnit) : null,
      lastCalibratedOn: lastCalibratedOn ? lastCalibratedOn : null,
      pmIntervalValue: hasPmVal ? Number(pmValue) : null,
      pmIntervalUnit: hasPmUnit ? (pmUnit as IntervalUnit) : null,
      lastPmOn: lastPmOn ? lastPmOn : null
    };

    setSaving(true);
    try {
      const res = await EquipmentPlanService.saveSchedule(equipment.id, payload, version);
      setVersion(res.version);
      if (!onSaved) {
        setSuccess(t("equipment.schedule.savedSuccess"));
      }
      onSaved?.(res);
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { message?: string } }; message?: string };
      setError(
        apiErr?.response?.data?.message ??
          apiErr?.message ??
          t("equipment.schedule.saveError")
      );
    } finally {
      setSaving(false);
    }
  };

  return (
    <Box sx={{ p: 2 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
        <Box>
          <Typography variant="h6" sx={{ fontSize: 16, fontWeight: 700, color: "text.primary" }}>
            {t("equipment.schedule.title")}
          </Typography>
          <Typography sx={{ fontSize: 13, color: "text.secondary", mt: 0.25 }}>
            {t("equipment.schedule.subtitle")}
          </Typography>
        </Box>
        {onRecordCalibrationClick && (
          <Button
            variant="outlined"
            size="small"
            startIcon={<VerifiedIcon fontSize="small" />}
            onClick={onRecordCalibrationClick}
            sx={{ textTransform: "none", fontSize: 12 }}
          >
            {t("equipment.schedule.recordCalibration")}
          </Button>
        )}
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {success && (
        <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSuccess(null)}>
          {success}
        </Alert>
      )}

      <Stack spacing={3}>
        {/* Calibration Section */}
        <Box sx={{ bgcolor: "background.default", p: 2, borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 1.5 }}>
            {t("equipment.schedule.calSectionTitle")}
          </Typography>
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <TextField
              id="equip-cal-interval-val"
              label={t("equipment.schedule.calIntervalLabel")}
              type="number"
              size="small"
              value={calValue}
              onChange={(e) => setCalValue(e.target.value)}
              placeholder={t("equipment.schedule.calIntervalPlaceholder")}
              helperText={t("equipment.schedule.intervalHelper")}
              slotProps={{ htmlInput: { min: 1 } }}
            />
            <FormControl size="small" fullWidth>
              <InputLabel id="cal-unit-label">{t("equipment.schedule.calUnitLabel")}</InputLabel>
              <Select
                labelId="cal-unit-label"
                id="equip-cal-interval-unit"
                value={calUnit}
                label={t("equipment.schedule.calUnitLabel")}
                onChange={(e) => setCalUnit(e.target.value)}
              >
                <MenuItem value="">
                  <em>{t("equipment.schedule.none")}</em>
                </MenuItem>
                <MenuItem value="Weeks">{t("equipment.schedule.units.Weeks")}</MenuItem>
                <MenuItem value="Months">{t("equipment.schedule.units.Months")}</MenuItem>
                <MenuItem value="Years">{t("equipment.schedule.units.Years")}</MenuItem>
              </Select>
            </FormControl>
          </Box>
          <Box sx={{ mt: 2 }}>
            <DateField
              label={t("equipment.schedule.lastCalDateLabel")}
              fullWidth
              value={lastCalibratedOn}
              onChange={(v) => setLastCalibratedOn(v)}
              helperText={t("equipment.schedule.lastCalDateHelper")}
            />
          </Box>
        </Box>

        {/* Preventive Maintenance Section */}
        <Box sx={{ bgcolor: "background.default", p: 2, borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 1.5 }}>
            {t("equipment.schedule.pmSectionTitle")}
          </Typography>
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <TextField
              id="equip-pm-interval-val"
              label={t("equipment.schedule.pmIntervalLabel")}
              type="number"
              size="small"
              value={pmValue}
              onChange={(e) => setPmValue(e.target.value)}
              placeholder={t("equipment.schedule.pmIntervalPlaceholder")}
              helperText={t("equipment.schedule.intervalHelper")}
              slotProps={{ htmlInput: { min: 1 } }}
            />
            <FormControl size="small" fullWidth>
              <InputLabel id="pm-unit-label">{t("equipment.schedule.pmUnitLabel")}</InputLabel>
              <Select
                labelId="pm-unit-label"
                id="equip-pm-interval-unit"
                value={pmUnit}
                label={t("equipment.schedule.pmUnitLabel")}
                onChange={(e) => setPmUnit(e.target.value)}
              >
                <MenuItem value="">
                  <em>{t("equipment.schedule.none")}</em>
                </MenuItem>
                <MenuItem value="Weeks">{t("equipment.schedule.units.Weeks")}</MenuItem>
                <MenuItem value="Months">{t("equipment.schedule.units.Months")}</MenuItem>
                <MenuItem value="Years">{t("equipment.schedule.units.Years")}</MenuItem>
              </Select>
            </FormControl>
          </Box>
          <Box sx={{ mt: 2 }}>
            <DateField
              label={t("equipment.schedule.lastPmDateLabel")}
              fullWidth
              value={lastPmOn}
              onChange={(v) => setLastPmOn(v)}
              helperText={t("equipment.schedule.lastPmDateHelper")}
            />
          </Box>
        </Box>

        <Divider />

        <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
          <Button
            id="equip-save-schedule-btn"
            variant="contained"
            startIcon={saving ? <CircularProgress size={16} color="inherit" /> : <SaveIcon />}
            disabled={saving}
            onClick={handleSave}
            sx={{ textTransform: "none", px: 3 }}
          >
            {saving ? t("equipment.schedule.saving") : t("equipment.schedule.saveButton")}
          </Button>
        </Box>
      </Stack>
    </Box>
  );
}
