import {
  Box,
  Paper,
  Typography,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  RadioGroup,
  Radio,
  FormControlLabel,
  FormLabel,
  Switch,
  useTheme,
  FormHelperText
} from "@mui/material";
import {
  SolutionMaster,
  TitrantStrengthUnit,
  StandardizationMode
} from "../services/SolutionMasterService";
import { MaterialMasterEntry } from "../services/MaterialMasterService";
import { useTranslation } from "../../../../i18n";
import { STRENGTH_UNIT_OPTIONS } from "./solutionForm";

export interface SolutionTitrantSectionProps {
  nominalStrength: string | number;
  strengthUnit: TitrantStrengthUnit;
  standardizationMode: StandardizationMode;
  standardEntryId: string | number;
  equivalenceMgPerMl: string | number;
  referenceSolutionId: string | number;
  blankRequired: boolean;
  replicateCount: string | number;
  factorMin: string | number;
  factorMax: string | number;
  maxRsdPercent: string | number;
  validityDays: string | number;
  availableTitrantStandardEntries: MaterialMasterEntry[];
  availableReferenceTitrants: SolutionMaster[];
  onNominalStrengthChange: (value: string) => void;
  onStrengthUnitChange: (value: TitrantStrengthUnit) => void;
  onStandardizationModeChange: (value: StandardizationMode) => void;
  onStandardEntryIdChange: (value: string | number) => void;
  onEquivalenceMgPerMlChange: (value: string) => void;
  onReferenceSolutionIdChange: (value: string | number) => void;
  onBlankRequiredChange: (value: boolean) => void;
  onReplicateCountChange: (value: string) => void;
  onFactorMinChange: (value: string) => void;
  onFactorMaxChange: (value: string) => void;
  onMaxRsdPercentChange: (value: string) => void;
  onValidityDaysChange: (value: string) => void;
}

export function SolutionTitrantSection({
  nominalStrength,
  strengthUnit,
  standardizationMode,
  standardEntryId,
  equivalenceMgPerMl,
  referenceSolutionId,
  blankRequired,
  replicateCount,
  factorMin,
  factorMax,
  maxRsdPercent,
  validityDays,
  availableTitrantStandardEntries,
  availableReferenceTitrants,
  onNominalStrengthChange,
  onStrengthUnitChange,
  onStandardizationModeChange,
  onStandardEntryIdChange,
  onEquivalenceMgPerMlChange,
  onReferenceSolutionIdChange,
  onBlankRequiredChange,
  onReplicateCountChange,
  onFactorMinChange,
  onFactorMaxChange,
  onMaxRsdPercentChange,
  onValidityDaysChange
}: SolutionTitrantSectionProps) {
  const { t } = useTranslation("masterData");
  const theme = useTheme();

  return (
    <Paper
      variant="outlined"
      sx={{
        p: 2,
        borderRadius: 1.5,
        bgcolor: "background.default",
        border: "1px solid",
        borderColor: theme.palette.info.light
      }}
    >
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5, color: "text.primary" }}>
        {t("solutionMaster.titrant.title")}
      </Typography>
      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
        {t("solutionMaster.titrant.subtitle")}
      </Typography>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
        <TextField
          label={t("solutionMaster.titrant.nominalStrengthLabel")}
          type="number"
          size="small"
          required
          value={nominalStrength}
          onChange={(e) => onNominalStrengthChange(e.target.value)}
          placeholder={t("solutionMaster.titrant.nominalStrengthPlaceholder")}
          slotProps={{ htmlInput: { min: "0.00001", step: "any" } }}
        />

        <FormControl size="small" required>
          <InputLabel id="titrant-strength-unit-select-label">{t("solutionMaster.titrant.strengthUnitLabel")}</InputLabel>
          <Select
            labelId="titrant-strength-unit-select-label"
            label={t("solutionMaster.titrant.strengthUnitLabel")}
            value={strengthUnit}
            onChange={(e) => onStrengthUnitChange(e.target.value as TitrantStrengthUnit)}
          >
            {STRENGTH_UNIT_OPTIONS.map((u) => (
              <MenuItem key={u.value} value={u.value}>
                {t(`solutionMaster.titrant.strengthUnits.${u.value}`, { defaultValue: u.label })}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        {/* Standardization Mode */}
        <FormControl component="fieldset" sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
          <FormLabel component="legend" sx={{ fontSize: 13, fontWeight: 600 }}>
            {t("solutionMaster.titrant.standardizationModeLabel")}
          </FormLabel>
          <RadioGroup
            row
            value={standardizationMode}
            onChange={(e) => onStandardizationModeChange(e.target.value as StandardizationMode)}
          >
            <FormControlLabel
              value="PrimaryStandard"
              control={<Radio size="small" />}
              label={t("solutionMaster.titrant.primaryStandardMode")}
            />
            <FormControlLabel
              value="AgainstVolumetricSolution"
              control={<Radio size="small" />}
              label={t("solutionMaster.titrant.againstVolumetricMode")}
            />
          </RadioGroup>
        </FormControl>

        {/* Primary Standard Mode Inputs */}
        {standardizationMode === "PrimaryStandard" ? (
          <>
            <FormControl fullWidth size="small" required>
              <InputLabel id="primary-standard-entry-label">{t("solutionMaster.titrant.primaryStandardLabel")}</InputLabel>
              <Select
                labelId="primary-standard-entry-label"
                label={t("solutionMaster.titrant.primaryStandardLabel")}
                value={standardEntryId}
                onChange={(e) => onStandardEntryIdChange(e.target.value)}
              >
                {availableTitrantStandardEntries
                  .filter((m) => m.category === "PrimaryStandard")
                  .map((m) => (
                    <MenuItem key={m.id} value={m.id}>
                      {m.code} - {m.name}
                    </MenuItem>
                  ))}
              </Select>
              <FormHelperText>{t("solutionMaster.titrant.primaryStandardHelper")}</FormHelperText>
            </FormControl>

            <TextField
              label={t("solutionMaster.titrant.equivalenceLabel")}
              type="number"
              size="small"
              required
              value={equivalenceMgPerMl}
              onChange={(e) => onEquivalenceMgPerMlChange(e.target.value)}
              placeholder={t("solutionMaster.titrant.equivalencePlaceholder")}
              slotProps={{ htmlInput: { min: "0.0001", step: "any" } }}
              helperText={t("solutionMaster.titrant.equivalenceHelper")}
            />
          </>
        ) : (
          /* Against Volumetric Solution Mode Inputs */
          <FormControl fullWidth size="small" required sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
            <InputLabel id="reference-titrant-select-label">{t("solutionMaster.titrant.referenceTitrantLabel")}</InputLabel>
            <Select
              labelId="reference-titrant-select-label"
              label={t("solutionMaster.titrant.referenceTitrantLabel")}
              value={referenceSolutionId}
              onChange={(e) => onReferenceSolutionIdChange(e.target.value)}
            >
              {availableReferenceTitrants.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.name} ({s.nominalStrength} {s.strengthUnit})
                </MenuItem>
              ))}
            </Select>
            <FormHelperText>{t("solutionMaster.titrant.referenceTitrantHelper")}</FormHelperText>
          </FormControl>
        )}

        <FormControlLabel
          control={
            <Switch
              checked={blankRequired}
              onChange={(e) => onBlankRequiredChange(e.target.checked)}
              size="small"
            />
          }
          label={t("solutionMaster.titrant.blankRequiredLabel")}
          sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
        />

        <TextField
          label={t("solutionMaster.titrant.replicateCountLabel")}
          type="number"
          size="small"
          required
          value={replicateCount}
          onChange={(e) => onReplicateCountChange(e.target.value)}
          placeholder={t("solutionMaster.titrant.replicateCountPlaceholder")}
          slotProps={{ htmlInput: { min: "1", step: "1" } }}
          helperText={t("solutionMaster.titrant.replicateCountHelper")}
        />

        <TextField
          label={t("solutionMaster.titrant.maxRsdLabel")}
          type="number"
          size="small"
          required
          value={maxRsdPercent}
          onChange={(e) => onMaxRsdPercentChange(e.target.value)}
          placeholder={t("solutionMaster.titrant.maxRsdPlaceholder")}
          slotProps={{ htmlInput: { min: "0.01", step: "0.01" } }}
          helperText={t("solutionMaster.titrant.maxRsdHelper")}
        />

        <TextField
          label={t("solutionMaster.titrant.factorMinLabel")}
          type="number"
          size="small"
          required
          value={factorMin}
          onChange={(e) => onFactorMinChange(e.target.value)}
          placeholder={t("solutionMaster.titrant.factorMinPlaceholder")}
          slotProps={{ htmlInput: { min: "0.00001", step: "any" } }}
          helperText={t("solutionMaster.titrant.factorMinHelper")}
        />

        <TextField
          label={t("solutionMaster.titrant.factorMaxLabel")}
          type="number"
          size="small"
          required
          value={factorMax}
          onChange={(e) => onFactorMaxChange(e.target.value)}
          placeholder={t("solutionMaster.titrant.factorMaxPlaceholder")}
          slotProps={{ htmlInput: { min: "0.00001", step: "any" } }}
          helperText={t("solutionMaster.titrant.factorMaxHelper")}
        />

        <TextField
          label={t("solutionMaster.titrant.validityDaysLabel")}
          type="number"
          size="small"
          required
          value={validityDays}
          onChange={(e) => onValidityDaysChange(e.target.value)}
          placeholder={t("solutionMaster.titrant.validityDaysPlaceholder")}
          slotProps={{ htmlInput: { min: "0", step: "1" } }}
          helperText={t("solutionMaster.titrant.validityDaysHelper")}
          sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
        />
      </Box>
    </Paper>
  );
}
