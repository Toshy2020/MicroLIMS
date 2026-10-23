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
  const [calValue, setCalValue] = useState<string>(
    initialSchedule?.calibrationIntervalValue?.toString() ??
      (equipment as any).calibrationIntervalValue?.toString() ??
      ""
  );
  const [calUnit, setCalUnit] = useState<string>(
    initialSchedule?.calibrationIntervalUnit ??
      (equipment as any).calibrationIntervalUnit ??
      ""
  );
  const [lastCalibratedOn, setLastCalibratedOn] = useState<string>(
    initialSchedule?.lastCalibratedOn ??
      (equipment as any).lastCalibratedOn ??
      ""
  );

  const [pmValue, setPmValue] = useState<string>(
    initialSchedule?.pmIntervalValue?.toString() ??
      (equipment as any).pmIntervalValue?.toString() ??
      ""
  );
  const [pmUnit, setPmUnit] = useState<string>(
    initialSchedule?.pmIntervalUnit ??
      (equipment as any).pmIntervalUnit ??
      ""
  );
  const [lastPmOn, setLastPmOn] = useState<string>(
    initialSchedule?.lastPmOn ??
      (equipment as any).lastPmOn ??
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
    }
  }, [initialSchedule]);

  const handleSave = async () => {
    setError(null);
    setSuccess(null);

    const hasCalVal = calValue.trim() !== "";
    const hasCalUnit = calUnit !== "";
    if (hasCalVal && !hasCalUnit) {
      setError("Calibration interval unit is required when value is specified.");
      return;
    }
    if (!hasCalVal && hasCalUnit) {
      setError("Calibration interval value is required when unit is specified.");
      return;
    }

    const hasPmVal = pmValue.trim() !== "";
    const hasPmUnit = pmUnit !== "";
    if (hasPmVal && !hasPmUnit) {
      setError("Preventive maintenance interval unit is required when value is specified.");
      return;
    }
    if (!hasPmVal && hasPmUnit) {
      setError("Preventive maintenance interval value is required when unit is specified.");
      return;
    }

    if (hasCalVal && hasCalUnit) {
      const num = Number(calValue);
      if (calUnit === "Weeks" && (num < 1 || num > 104)) {
        setError("Calibration interval in weeks must be between 1 and 104.");
        return;
      }
      if (calUnit === "Months" && (num < 1 || num > 60)) {
        setError("Calibration interval in months must be between 1 and 60.");
        return;
      }
    }

    if (hasPmVal && hasPmUnit) {
      const num = Number(pmValue);
      if (pmUnit === "Weeks" && (num < 1 || num > 104)) {
        setError("Maintenance interval in weeks must be between 1 and 104.");
        return;
      }
      if (pmUnit === "Months" && (num < 1 || num > 60)) {
        setError("Maintenance interval in months must be between 1 and 60.");
        return;
      }
    }

    const today = new Date().toISOString().slice(0, 10);
    if (lastCalibratedOn && lastCalibratedOn > today) {
      setError("Last calibration date cannot be in the future.");
      return;
    }
    if (lastPmOn && lastPmOn > today) {
      setError("Last maintenance date cannot be in the future.");
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
      setSuccess("Schedule saved successfully.");
      onSaved?.(res);
    } catch (err: any) {
      setError(
        err?.response?.data?.message ??
          err?.message ??
          "Failed to save equipment schedule."
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
            Calibration & Maintenance Schedule
          </Typography>
          <Typography sx={{ fontSize: 13, color: "text.secondary", mt: 0.25 }}>
            Configure calibration and preventive maintenance intervals and initial baseline dates for planning.
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
            Record Calibration
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
            Calibration Configuration
          </Typography>
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <TextField
              id="equip-cal-interval-val"
              label="Calibration Interval"
              type="number"
              size="small"
              value={calValue}
              onChange={(e) => setCalValue(e.target.value)}
              placeholder="e.g. 6 or 12"
              helperText="Frequency value (1-104 weeks, 1-60 months)"
              slotProps={{ htmlInput: { min: 1 } }}
            />
            <FormControl size="small" fullWidth>
              <InputLabel id="cal-unit-label">Calibration Unit</InputLabel>
              <Select
                labelId="cal-unit-label"
                id="equip-cal-interval-unit"
                value={calUnit}
                label="Calibration Unit"
                onChange={(e) => setCalUnit(e.target.value)}
              >
                <MenuItem value="">
                  <em>None</em>
                </MenuItem>
                <MenuItem value="Weeks">Weeks</MenuItem>
                <MenuItem value="Months">Months</MenuItem>
              </Select>
            </FormControl>
          </Box>
          <Box sx={{ mt: 2 }}>
            <TextField
              label="Last Calibration Date (Baseline)"
              type="date"
              size="small"
              fullWidth
              slotProps={{ inputLabel: { shrink: true } }}
              value={lastCalibratedOn}
              onChange={(e) => setLastCalibratedOn(e.target.value)}
              helperText="Baseline date. Once calibrations are completed, future dates update from records."
            />
          </Box>
        </Box>

        {/* Preventive Maintenance Section */}
        <Box sx={{ bgcolor: "background.default", p: 2, borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 1.5 }}>
            Preventive Maintenance Configuration
          </Typography>
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <TextField
              id="equip-pm-interval-val"
              label="Preventive Maintenance Interval"
              type="number"
              size="small"
              value={pmValue}
              onChange={(e) => setPmValue(e.target.value)}
              placeholder="e.g. 1 or 3"
              helperText="Frequency value (1-104 weeks, 1-60 months)"
              slotProps={{ htmlInput: { min: 1 } }}
            />
            <FormControl size="small" fullWidth>
              <InputLabel id="pm-unit-label">Maintenance Unit</InputLabel>
              <Select
                labelId="pm-unit-label"
                id="equip-pm-interval-unit"
                value={pmUnit}
                label="Maintenance Unit"
                onChange={(e) => setPmUnit(e.target.value)}
              >
                <MenuItem value="">
                  <em>None</em>
                </MenuItem>
                <MenuItem value="Weeks">Weeks</MenuItem>
                <MenuItem value="Months">Months</MenuItem>
              </Select>
            </FormControl>
          </Box>
          <Box sx={{ mt: 2 }}>
            <TextField
              label="Last Maintenance Date (Baseline)"
              type="date"
              size="small"
              fullWidth
              slotProps={{ inputLabel: { shrink: true } }}
              value={lastPmOn}
              onChange={(e) => setLastPmOn(e.target.value)}
              helperText="Audited baseline date from existing maintenance log records."
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
            {saving ? "Saving..." : "Save Schedule"}
          </Button>
        </Box>
      </Stack>
    </Box>
  );
}
