import {
  Box,
  Typography,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  FormHelperText
} from "@mui/material";
import { HplcDetectorType, HplcTechnique } from "../services/HplcMethodService";
import { useTranslation } from "react-i18next";
import { HPLC_DETECTOR_OPTIONS, GC_DETECTOR_OPTIONS } from "./hplcMethodForm";
import { HplcMethodErrors } from "./hplcMethodValidation";

export interface HplcDetectionSectionProps {
  technique?: HplcTechnique;
  detectorType: HplcDetectorType;
  injectionVolumeUl: string | number;
  runTimeMin: string | number;
  errors: HplcMethodErrors;
  onDetectorTypeChange: (val: HplcDetectorType) => void;
  onInjectionVolumeUlChange: (val: string) => void;
  onRunTimeMinChange: (val: string) => void;
}

export function HplcDetectionSection({
  technique = "Hplc",
  detectorType,
  injectionVolumeUl,
  runTimeMin,
  errors,
  onDetectorTypeChange,
  onInjectionVolumeUlChange,
  onRunTimeMinChange
}: HplcDetectionSectionProps) {
  const { t } = useTranslation("masterData");
  const isGc = technique === "Gc";
  const options = isGc ? GC_DETECTOR_OPTIONS : HPLC_DETECTOR_OPTIONS;
  const detectorError = errors["detection.detectorType"];

  return (
    <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5, color: "text.primary" }}>
        {t("hplcMethod.detection.title")}
      </Typography>
      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
        {t("hplcMethod.detection.subtitle")}
      </Typography>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" }, gap: 2 }}>
        <FormControl size="small" fullWidth required error={Boolean(detectorError)}>
          <InputLabel id="detector-type-label">{t("hplcMethod.detection.detectorTypeLabel")}</InputLabel>
          <Select
            labelId="detector-type-label"
            label={t("hplcMethod.detection.detectorTypeLabel")}
            value={detectorType}
            onChange={(e) => onDetectorTypeChange(e.target.value as HplcDetectorType)}
          >
            {options.map((opt) => (
              <MenuItem key={opt.value} value={opt.value}>
                {t(`hplcMethod.options.${isGc ? "gcDetector" : "hplcDetector"}.${opt.value}`, { defaultValue: opt.label })}
              </MenuItem>
            ))}
          </Select>
          {detectorError && <FormHelperText>{detectorError}</FormHelperText>}
        </FormControl>

        <TextField
          size="small"
          label={t("hplcMethod.detection.injectionVolumeLabel")}
          type="number"
          value={injectionVolumeUl}
          onChange={(e) => onInjectionVolumeUlChange(e.target.value)}
          required
          placeholder={t("hplcMethod.detection.injectionVolumePlaceholder")}
          error={Boolean(errors["detection.injectionVolumeUl"])}
          helperText={errors["detection.injectionVolumeUl"] ?? t("hplcMethod.detection.injectionVolumeHelper")}
          slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
          fullWidth
        />

        <TextField
          size="small"
          label={t("hplcMethod.detection.runTimeLabel")}
          type="number"
          value={runTimeMin}
          onChange={(e) => onRunTimeMinChange(e.target.value)}
          required
          placeholder={t("hplcMethod.detection.runTimePlaceholder")}
          error={Boolean(errors["detection.runTimeMin"])}
          helperText={errors["detection.runTimeMin"] ?? t("hplcMethod.detection.runTimeHelper")}
          slotProps={{ htmlInput: { min: 0.5, step: "any" } }}
          fullWidth
        />
      </Box>
    </Box>
  );
}
