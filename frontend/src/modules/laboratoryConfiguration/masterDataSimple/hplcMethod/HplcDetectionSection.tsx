import {
  Box,
  Typography,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem
} from "@mui/material";
import { HplcDetectorType } from "../services/HplcMethodService";
import { DETECTOR_TYPE_OPTIONS } from "./hplcMethodForm";
import { HplcMethodErrors } from "./hplcMethodValidation";

export interface HplcDetectionSectionProps {
  detectorType: HplcDetectorType;
  injectionVolumeUl: string | number;
  runTimeMin: string | number;
  errors: HplcMethodErrors;
  onDetectorTypeChange: (val: HplcDetectorType) => void;
  onInjectionVolumeUlChange: (val: string) => void;
  onRunTimeMinChange: (val: string) => void;
}

export function HplcDetectionSection({
  detectorType,
  injectionVolumeUl,
  runTimeMin,
  errors,
  onDetectorTypeChange,
  onInjectionVolumeUlChange,
  onRunTimeMinChange
}: HplcDetectionSectionProps) {
  return (
    <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5, color: "text.primary" }}>
        Detection &amp; Injection Parameters
      </Typography>
      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
        Specify detector type, autosampler injection volume, and total chromatogram run duration.
      </Typography>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" }, gap: 2 }}>
        <FormControl size="small" fullWidth required>
          <InputLabel id="detector-type-label">Detector Type *</InputLabel>
          <Select
            labelId="detector-type-label"
            label="Detector Type *"
            value={detectorType}
            onChange={(e) => onDetectorTypeChange(e.target.value as HplcDetectorType)}
          >
            {DETECTOR_TYPE_OPTIONS.map((opt) => (
              <MenuItem key={opt.value} value={opt.value}>
                {opt.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <TextField
          size="small"
          label="Injection Volume (µL) *"
          type="number"
          value={injectionVolumeUl}
          onChange={(e) => onInjectionVolumeUlChange(e.target.value)}
          required
          placeholder="e.g. 10 or 20"
          error={Boolean(errors["detection.injectionVolumeUl"])}
          helperText={errors["detection.injectionVolumeUl"] ?? "Autosampler injection volume"}
          slotProps={{ htmlInput: { min: 0.1, step: "any" } }}
          fullWidth
        />

        <TextField
          size="small"
          label="Run Time (min) *"
          type="number"
          value={runTimeMin}
          onChange={(e) => onRunTimeMinChange(e.target.value)}
          required
          placeholder="e.g. 15 or 30"
          error={Boolean(errors["detection.runTimeMin"])}
          helperText={errors["detection.runTimeMin"] ?? "Total run time per injection"}
          slotProps={{ htmlInput: { min: 0.5, step: "any" } }}
          fullWidth
        />
      </Box>
    </Box>
  );
}
