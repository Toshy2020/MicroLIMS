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
  Stack
} from "@mui/material";
import { SolutionType, ShelfLifeUnit } from "../services/SolutionMasterService";
import { LaboratorySection } from "../../../../services/laboratorySectionService";
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
  const showSectionSelect = (!isEditing && mySections.length > 1) || (isEditing && sections.length > 0);

  return (
    <Paper variant="outlined" sx={{ p: 2, borderRadius: 1.5, bgcolor: "background.default" }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.primary" }}>
        General Information
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
        <TextField
          label="Solution Name"
          value={name}
          onChange={(e) => onNameChange(e.target.value)}
          required
          fullWidth
          size="small"
          placeholder="e.g. 0.1M Phosphate Buffer pH 3.0, Mobile Phase A"
          error={!!errors.name}
          helperText={errors.name ?? "Descriptive name of the solution recipe"}
          sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
        />

        <FormControl fullWidth size="small" required>
          <InputLabel id="solution-type-select-label">Solution Type</InputLabel>
          <Select
            labelId="solution-type-select-label"
            label="Solution Type"
            value={type}
            onChange={(e) => onTypeChange(e.target.value as SolutionType)}
          >
            {SOLUTION_TYPE_OPTIONS.map((opt) => (
              <MenuItem key={opt.value} value={opt.value}>
                {opt.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        {/* Laboratory Section Select (if creating and user has multiple sections, or display current) */}
        {showSectionSelect ? (
          <FormControl fullWidth size="small" required={!isEditing && mySections.length > 1} error={!!errors.section}>
            <InputLabel id="solution-section-select-label">Laboratory Section</InputLabel>
            <Select
              labelId="solution-section-select-label"
              label="Laboratory Section"
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

        <Stack direction="row" spacing={1} sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
          <TextField
            label="Shelf Life Value"
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
            <InputLabel id="shelf-life-unit-select-label">Unit</InputLabel>
            <Select
              labelId="shelf-life-unit-select-label"
              label="Unit"
              value={shelfLifeUnit}
              onChange={(e) => onShelfLifeUnitChange(e.target.value as ShelfLifeUnit)}
            >
              {SHELF_LIFE_UNIT_OPTIONS.map((u) => (
                <MenuItem key={u.value} value={u.value}>
                  {u.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <TextField
            label="Final Volume (mL)"
            type="number"
            value={finalVolumeMl}
            onChange={(e) => onFinalVolumeMlChange(e.target.value)}
            required
            size="small"
            fullWidth
            placeholder="e.g. 1000"
            slotProps={{ htmlInput: { min: "0.1", step: "any" } }}
            error={!!errors.finalVolume}
            helperText={errors.finalVolume ?? "Nominal total prepared volume in mL"}
          />
        </Stack>

        <TextField
          label="Storage Condition"
          value={storageCondition}
          onChange={(e) => onStorageConditionChange(e.target.value)}
          required
          fullWidth
          size="small"
          placeholder="e.g. Store at 20-25°C in amber glass bottle"
          error={!!errors.storage}
          helperText={errors.storage ?? "Temperature, container, and light protection requirements"}
          sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
        />

        <TextField
          label="Preparation Instructions"
          value={instructions}
          onChange={(e) => onInstructionsChange(e.target.value)}
          required
          fullWidth
          multiline
          rows={3}
          size="small"
          placeholder="Step-by-step preparation protocol, sonication, degassing, and filtering requirements..."
          error={!!errors.instructions}
          helperText={errors.instructions ?? "Detailed SOP instructions for the laboratory analyst"}
          sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
        />
      </Box>
    </Paper>
  );
}
