import {
  Box,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  FormHelperText,
  Typography
} from "@mui/material";
import type { MaterialMasterEntry } from "../services/MaterialMasterService";
import type { IcpMethodErrors } from "./icpMethodValidation";

export interface IcpCalibrationSectionProps {
  standardLevelsMgPerL: string;
  calibrationStandardEntryId: number | "";
  minCorrelation: string | number;
  maxCalibrationAgeHours: string | number;
  availableStandards: MaterialMasterEntry[];
  errors: IcpMethodErrors;
  onStandardLevelsChange: (val: string) => void;
  onCalibrationStandardChange: (val: number | "") => void;
  onMinCorrelationChange: (val: string) => void;
  onMaxCalibrationAgeHoursChange: (val: string) => void;
}

export function IcpCalibrationSection({
  standardLevelsMgPerL,
  calibrationStandardEntryId,
  minCorrelation,
  maxCalibrationAgeHours,
  availableStandards,
  errors,
  onStandardLevelsChange,
  onCalibrationStandardChange,
  onMinCorrelationChange,
  onMaxCalibrationAgeHoursChange
}: IcpCalibrationSectionProps) {
  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1.5 }}>
        Calibration Curve & Standard Configuration
      </Typography>

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" },
          gap: 2,
          mb: 2
        }}
      >
        <TextField
          size="small"
          label="Standard Levels (mg/L) *"
          placeholder="0.1, 0.5, 1, 3, 6"
          value={standardLevelsMgPerL}
          onChange={(e) => onStandardLevelsChange(e.target.value)}
          required
          error={Boolean(errors.standardLevelsMgPerL)}
          helperText={
            errors.standardLevelsMgPerL ??
            "Comma-separated list of ≥ 2 distinct standard concentrations (mg/L)"
          }
          fullWidth
        />

        <FormControl size="small" fullWidth error={Boolean(errors.calibrationStandardEntryId)}>
          <InputLabel id="cal-std-picker-label">Calibration Reference Standard *</InputLabel>
          <Select
            labelId="cal-std-picker-label"
            label="Calibration Reference Standard *"
            value={calibrationStandardEntryId}
            onChange={(e) => onCalibrationStandardChange(Number(e.target.value) || "")}
          >
            {availableStandards.length === 0 ? (
              <MenuItem disabled value="">
                <em>No reference standards available in this section</em>
              </MenuItem>
            ) : (
              availableStandards.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.code} &middot; {s.name} {!s.isActive && "(Inactive)"}
                </MenuItem>
              ))
            )}
          </Select>
          {errors.calibrationStandardEntryId && (
            <FormHelperText>{errors.calibrationStandardEntryId}</FormHelperText>
          )}
        </FormControl>
      </Box>

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" },
          gap: 2
        }}
      >
        <TextField
          size="small"
          label="Minimum Correlation (r) *"
          placeholder="0.995"
          value={minCorrelation}
          onChange={(e) => onMinCorrelationChange(e.target.value)}
          required
          type="number"
          slotProps={{ htmlInput: { step: "0.001", min: "0.001", max: "1" } }}
          error={Boolean(errors.minCorrelation)}
          helperText={errors.minCorrelation ?? "Linear correlation limit (0 < r ≤ 1, typically ≥ 0.995)"}
          fullWidth
        />

        <TextField
          size="small"
          label="Max Calibration Age (Hours) *"
          placeholder="24"
          value={maxCalibrationAgeHours}
          onChange={(e) => onMaxCalibrationAgeHoursChange(e.target.value)}
          required
          type="number"
          slotProps={{ htmlInput: { step: "1", min: "1", max: "168" } }}
          error={Boolean(errors.maxCalibrationAgeHours)}
          helperText={errors.maxCalibrationAgeHours ?? "Valid time window for calibration curve (1–168 hours)"}
          fullWidth
        />
      </Box>
    </Box>
  );
}
