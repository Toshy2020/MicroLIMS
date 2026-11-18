import {
  Box,
  Paper,
  Typography,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  FormHelperText
} from "@mui/material";
import { MaterialMasterEntry } from "../services/MaterialMasterService";
import { useTranslation } from "../../../../i18n";
import type { SolutionFieldKey } from "./solutionForm";

export interface SolutionPhSectionProps {
  phTarget: string | number;
  phTolerance: string | number;
  phAdjustingEntryId: string | number;
  availablePhAdjustingEntries: MaterialMasterEntry[];
  // Client-side validation messages, shown on the field.
  errors?: Partial<Record<SolutionFieldKey, string>>;
  onPhTargetChange: (value: string) => void;
  onPhToleranceChange: (value: string) => void;
  onPhAdjustingEntryIdChange: (value: string | number) => void;
}

export function SolutionPhSection({
  phTarget,
  phTolerance,
  phAdjustingEntryId,
  availablePhAdjustingEntries,
  errors = {},
  onPhTargetChange,
  onPhToleranceChange,
  onPhAdjustingEntryIdChange
}: SolutionPhSectionProps) {
  const { t } = useTranslation("masterData");
  const hasPhTarget = phTarget !== "";

  return (
    <Paper variant="outlined" sx={{ p: 2, borderRadius: 1.5, bgcolor: "background.default" }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.primary" }}>
        {t("solutionMaster.ph.title")}
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" }, gap: 2 }}>
        <TextField
          label={t("solutionMaster.ph.targetLabel")}
          type="number"
          value={phTarget}
          onChange={(e) => onPhTargetChange(e.target.value)}
          size="small"
          placeholder={t("solutionMaster.ph.targetPlaceholder")}
          slotProps={{ htmlInput: { min: "0", max: "14", step: "0.01" } }}
          error={!!errors.phTarget}
          helperText={errors.phTarget ?? t("solutionMaster.ph.targetHelper")}
        />

        <TextField
          label={t("solutionMaster.ph.toleranceLabel")}
          type="number"
          value={phTolerance}
          onChange={(e) => onPhToleranceChange(e.target.value)}
          size="small"
          disabled={!hasPhTarget}
          placeholder={t("solutionMaster.ph.tolerancePlaceholder")}
          slotProps={{ htmlInput: { min: "0.01", step: "0.01" } }}
          error={!!errors.phTolerance}
          helperText={errors.phTolerance ?? (!hasPhTarget ? t("solutionMaster.ph.toleranceHelperNoTarget") : t("solutionMaster.ph.toleranceHelper"))}
        />

        <FormControl size="small" disabled={!hasPhTarget} error={!!errors.phAdjuster}>
          <InputLabel id="ph-adjusting-reagent-select-label">{t("solutionMaster.ph.adjusterLabel")}</InputLabel>
          <Select
            labelId="ph-adjusting-reagent-select-label"
            label={t("solutionMaster.ph.adjusterLabel")}
            value={phAdjustingEntryId}
            onChange={(e) => onPhAdjustingEntryIdChange(e.target.value)}
          >
            <MenuItem value="">
              <em>{t("solutionMaster.ph.noneOption")}</em>
            </MenuItem>
            {availablePhAdjustingEntries.map((m) => (
              <MenuItem key={m.id} value={m.id}>
                {m.code} - {m.name} {!m.isActive ? t("solutionMaster.ph.inactiveTag") : ""}
              </MenuItem>
            ))}
          </Select>
          <FormHelperText>{errors.phAdjuster ?? t("solutionMaster.ph.adjusterHelper")}</FormHelperText>
        </FormControl>
      </Box>
    </Paper>
  );
}
