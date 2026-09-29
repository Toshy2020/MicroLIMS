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
        Titrant Standardization Settings (USP Volumetric Solutions)
      </Typography>
      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
        Configure primary standard titration or secondary volumetric standardization criteria.
      </Typography>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
        <TextField
          label="Nominal Strength"
          type="number"
          size="small"
          required
          value={nominalStrength}
          onChange={(e) => onNominalStrengthChange(e.target.value)}
          placeholder="e.g. 0.1, 0.05"
          slotProps={{ htmlInput: { min: "0.00001", step: "any" } }}
        />

        <FormControl size="small" required>
          <InputLabel id="titrant-strength-unit-select-label">Strength Unit</InputLabel>
          <Select
            labelId="titrant-strength-unit-select-label"
            label="Strength Unit"
            value={strengthUnit}
            onChange={(e) => onStrengthUnitChange(e.target.value as TitrantStrengthUnit)}
          >
            {STRENGTH_UNIT_OPTIONS.map((u) => (
              <MenuItem key={u.value} value={u.value}>
                {u.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        {/* Standardization Mode */}
        <FormControl component="fieldset" sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
          <FormLabel component="legend" sx={{ fontSize: 13, fontWeight: 600 }}>
            Standardization Mode *
          </FormLabel>
          <RadioGroup
            row
            value={standardizationMode}
            onChange={(e) => onStandardizationModeChange(e.target.value as StandardizationMode)}
          >
            <FormControlLabel
              value="PrimaryStandard"
              control={<Radio size="small" />}
              label="Primary Standard (Direct Weighing)"
            />
            <FormControlLabel
              value="AgainstVolumetricSolution"
              control={<Radio size="small" />}
              label="Against Volumetric Solution (Secondary Titrant)"
            />
          </RadioGroup>
        </FormControl>

        {/* Primary Standard Mode Inputs */}
        {standardizationMode === "PrimaryStandard" ? (
          <>
            <FormControl fullWidth size="small" required>
              <InputLabel id="primary-standard-entry-label">Primary Standard</InputLabel>
              <Select
                labelId="primary-standard-entry-label"
                label="Primary Standard"
                value={standardEntryId}
                onChange={(e) => onStandardEntryIdChange(e.target.value)}
              >
                {availableTitrantStandardEntries.map((m) => (
                  <MenuItem key={m.id} value={m.id}>
                    {m.code} - {m.name} ({m.category})
                  </MenuItem>
                ))}
              </Select>
              <FormHelperText>Reference Standards or Reagents in section</FormHelperText>
            </FormControl>

            <TextField
              label="Equivalence (mg/mL)"
              type="number"
              size="small"
              required
              value={equivalenceMgPerMl}
              onChange={(e) => onEquivalenceMgPerMlChange(e.target.value)}
              placeholder="e.g. 20.422"
              slotProps={{ htmlInput: { min: "0.0001", step: "any" } }}
              helperText="mg of standard reacting per mL of nominal titrant"
            />
          </>
        ) : (
          /* Against Volumetric Solution Mode Inputs */
          <FormControl fullWidth size="small" required sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
            <InputLabel id="reference-titrant-select-label">Reference Titrant</InputLabel>
            <Select
              labelId="reference-titrant-select-label"
              label="Reference Titrant"
              value={referenceSolutionId}
              onChange={(e) => onReferenceSolutionIdChange(e.target.value)}
            >
              {availableReferenceTitrants.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.name} ({s.nominalStrength} {s.strengthUnit})
                </MenuItem>
              ))}
            </Select>
            <FormHelperText>Active titrant in current section (excluding self)</FormHelperText>
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
          label="Blank Titration Required"
          sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
        />

        <TextField
          label="Replicate Count"
          type="number"
          size="small"
          required
          value={replicateCount}
          onChange={(e) => onReplicateCountChange(e.target.value)}
          placeholder="e.g. 3 or 6"
          slotProps={{ htmlInput: { min: "1", step: "1" } }}
          helperText="Minimum runs required (e.g. 3)"
        />

        <TextField
          label="Max RSD %"
          type="number"
          size="small"
          required
          value={maxRsdPercent}
          onChange={(e) => onMaxRsdPercentChange(e.target.value)}
          placeholder="e.g. 0.20"
          slotProps={{ htmlInput: { min: "0.01", step: "0.01" } }}
          helperText="Acceptable relative standard deviation %"
        />

        <TextField
          label="Factor Min"
          type="number"
          size="small"
          required
          value={factorMin}
          onChange={(e) => onFactorMinChange(e.target.value)}
          placeholder="e.g. 0.97000"
          slotProps={{ htmlInput: { min: "0.00001", step: "any" } }}
          helperText="Lower acceptance factor limit"
        />

        <TextField
          label="Factor Max"
          type="number"
          size="small"
          required
          value={factorMax}
          onChange={(e) => onFactorMaxChange(e.target.value)}
          placeholder="e.g. 1.03000"
          slotProps={{ htmlInput: { min: "0.00001", step: "any" } }}
          helperText="Upper acceptance factor limit"
        />

        <TextField
          label="Standardization Validity (Days)"
          type="number"
          size="small"
          required
          value={validityDays}
          onChange={(e) => onValidityDaysChange(e.target.value)}
          placeholder="e.g. 30"
          slotProps={{ htmlInput: { min: "0", step: "1" } }}
          helperText="0 = restandardize before each use"
          sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
        />
      </Box>
    </Paper>
  );
}
