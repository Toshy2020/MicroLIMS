import {
  Box,
  Paper,
  Typography,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  FormHelperText,
  FormControlLabel,
  Switch,
  Stack
} from "@mui/material";
import { SolutionType, ShelfLifeUnit, isGeneralSolutionType } from "../services/SolutionMasterService";
import { LaboratorySection } from "../../../../services/laboratorySectionService";
import { useTranslation } from "../../../../i18n";
import { SOLUTION_TYPE_OPTIONS, SHELF_LIFE_UNIT_OPTIONS, SolutionFieldKey } from "./solutionForm";

export interface SolutionGeneralSectionProps {
  name: string;
  type: SolutionType;
  shelfLifeValue: string | number;
  shelfLifeUnit: ShelfLifeUnit;
  storageCondition: string;
  finalVolumeMl: string | number;
  instructions: string;
  sectionId: string | number;
  sterilizationRequired?: boolean;
  releaseRequired?: boolean;
  onSterilizationRequiredChange?: (value: boolean) => void;
  onReleaseRequiredChange?: (value: boolean) => void;
  isEditing: boolean;
  sections: LaboratorySection[];
  mySections: LaboratorySection[];
  // Client-side validation messages, shown on the field.
  errors?: Partial<Record<SolutionFieldKey, string>>;
  onNameChange: (value: string) => void;
  onTypeChange: (value: SolutionType) => void;
  onShelfLifeValueChange: (value: string) => void;
  onShelfLifeUnitChange: (value: ShelfLifeUnit) => void;
  onStorageConditionChange: (value: string) => void;
  onFinalVolumeMlChange: (value: string) => void;
  onInstructionsChange: (value: string) => void;
  onSectionIdChange: (value: string | number) => void;
}

export function SolutionGeneralSection({
  name,
  type,
  shelfLifeValue,
  shelfLifeUnit,
  storageCondition,
  finalVolumeMl,
  instructions,
  sectionId,
  sterilizationRequired = false,
  releaseRequired = false,
  onSterilizationRequiredChange,
  onReleaseRequiredChange,
  isEditing,
  sections,
  mySections,
  errors = {},
  onNameChange,
  onTypeChange,
  onShelfLifeValueChange,
  onShelfLifeUnitChange,
  onStorageConditionChange,
  onFinalVolumeMlChange,
  onInstructionsChange,
  onSectionIdChange
}: SolutionGeneralSectionProps) {
  const { t } = useTranslation("masterData");
  const showSectionSelect = (!isEditing && mySections.length > 1) || (isEditing && sections.length > 0);

  return (
    <Paper variant="outlined" sx={{ p: 2, borderRadius: 1.5, bgcolor: "background.default" }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.primary" }}>
        {t("solutionMaster.general.title")}
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
        <TextField
          label={t("solutionMaster.general.nameLabel")}
          value={name}
          onChange={(e) => onNameChange(e.target.value)}
          required
          fullWidth
          size="small"
          placeholder={t("solutionMaster.general.namePlaceholder")}
          error={!!errors.name}
          helperText={errors.name ?? t("solutionMaster.general.nameHelper")}
          sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
        />

        <FormControl fullWidth size="small" required>
          <InputLabel id="solution-type-select-label">{t("solutionMaster.general.typeLabel")}</InputLabel>
          <Select
            labelId="solution-type-select-label"
            label={t("solutionMaster.general.typeLabel")}
            value={type}
            onChange={(e) => onTypeChange(e.target.value as SolutionType)}
          >
            {SOLUTION_TYPE_OPTIONS.map((opt) => (
              <MenuItem key={opt.value} value={opt.value}>
                {t(`solutionMasterPage.types.${opt.value}`, { defaultValue: opt.label })}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        {/* Laboratory Section Select (if creating and user has multiple sections, or display current) */}
        {showSectionSelect ? (
          <FormControl fullWidth size="small" required={!isEditing && mySections.length > 1} error={!!errors.section}>
            <InputLabel id="solution-section-select-label">{t("solutionMaster.general.sectionLabel")}</InputLabel>
            <Select
              labelId="solution-section-select-label"
              label={t("solutionMaster.general.sectionLabel")}
              value={sectionId}
              onChange={(e) => onSectionIdChange(e.target.value)}
              disabled={isEditing}
            >
              {(!isEditing && mySections.length > 1 ? mySections : sections).map((sec) => (
                <MenuItem key={sec.sectionId} value={sec.sectionId}>
                  {sec.sectionName}
                </MenuItem>
              ))}
            </Select>
            {errors.section && <FormHelperText>{errors.section}</FormHelperText>}
          </FormControl>
        ) : null}

        {isGeneralSolutionType(type) && (
          <Stack sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
            <FormControlLabel
              control={
                <Switch
                  checked={sterilizationRequired}
                  onChange={(e) => onSterilizationRequiredChange?.(e.target.checked)}
                />
              }
              label={t("solutionMaster.general.sterilizationLabel")}
            />
            <FormControlLabel
              control={
                <Switch
                  checked={releaseRequired}
                  onChange={(e) => onReleaseRequiredChange?.(e.target.checked)}
                />
              }
              label={t("solutionMaster.general.releaseLabel")}
            />
          </Stack>
        )}

        <Stack direction="row" spacing={1} sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
          <TextField
            label={t("solutionMaster.general.shelfLifeLabel")}
            type="number"
            value={shelfLifeValue}
            onChange={(e) => onShelfLifeValueChange(e.target.value)}
            required
            size="small"
            error={!!errors.shelfLife}
            helperText={errors.shelfLife}
            sx={{ width: errors.shelfLife ? 200 : 140 }}
            slotProps={{ htmlInput: { min: "1", step: "1" } }}
          />
          <FormControl size="small" sx={{ width: 130 }} required>
            <InputLabel id="shelf-life-unit-select-label">{t("solutionMaster.general.unitLabel")}</InputLabel>
            <Select
              labelId="shelf-life-unit-select-label"
              label={t("solutionMaster.general.unitLabel")}
              value={shelfLifeUnit}
              onChange={(e) => onShelfLifeUnitChange(e.target.value as ShelfLifeUnit)}
            >
              {SHELF_LIFE_UNIT_OPTIONS.map((u) => (
                <MenuItem key={u.value} value={u.value}>
                  {t(`solutionMaster.general.shelfLifeUnits.${u.value}`, { defaultValue: u.label })}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <TextField
            label={t("solutionMaster.general.finalVolumeLabel")}
            type="number"
            value={finalVolumeMl}
            onChange={(e) => onFinalVolumeMlChange(e.target.value)}
            required
            size="small"
            fullWidth
            placeholder={t("solutionMaster.general.finalVolumePlaceholder")}
            slotProps={{ htmlInput: { min: "0.1", step: "any" } }}
            error={!!errors.finalVolume}
            helperText={errors.finalVolume ?? t("solutionMaster.general.finalVolumeHelper")}
          />
        </Stack>

        <TextField
          label={t("solutionMaster.general.storageLabel")}
          value={storageCondition}
          onChange={(e) => onStorageConditionChange(e.target.value)}
          required
          fullWidth
          size="small"
          placeholder={t("solutionMaster.general.storagePlaceholder")}
          error={!!errors.storage}
          helperText={errors.storage ?? t("solutionMaster.general.storageHelper")}
          sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
        />

        <TextField
          label={t("solutionMaster.general.instructionsLabel")}
          value={instructions}
          onChange={(e) => onInstructionsChange(e.target.value)}
          required
          fullWidth
          multiline
          rows={3}
          size="small"
          placeholder={t("solutionMaster.general.instructionsPlaceholder")}
          error={!!errors.instructions}
          helperText={errors.instructions ?? t("solutionMaster.general.instructionsHelper")}
          sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
        />
      </Box>
    </Paper>
  );
}
