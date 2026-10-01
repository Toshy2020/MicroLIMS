import { Box, TextField, FormControl, InputLabel, Select, MenuItem, FormHelperText } from "@mui/material";
import { LaboratorySection } from "../../../../services/laboratorySectionService";
import { HplcMethodErrors } from "./hplcMethodValidation";

export interface HplcGeneralSectionProps {
  name: string;
  abbreviation: string;
  effectiveDate: string;
  sectionId: number | "";
  errors: HplcMethodErrors;
  editingId: number | null;
  mySections: LaboratorySection[];
  sections: LaboratorySection[];
  onNameChange: (val: string) => void;
  onAbbreviationChange: (val: string) => void;
  onEffectiveDateChange: (val: string) => void;
  onSectionIdChange: (val: number | "") => void;
}

export function HplcGeneralSection({
  name,
  abbreviation,
  effectiveDate,
  sectionId,
  errors,
  editingId,
  mySections,
  sections,
  onNameChange,
  onAbbreviationChange,
  onEffectiveDateChange,
  onSectionIdChange
}: HplcGeneralSectionProps) {
  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1.5fr 1fr 1fr" }, gap: 2, mb: (!editingId && mySections.length > 1) ? 2 : 0 }}>
        <TextField
          size="small"
          label="Method Name *"
          placeholder="e.g. Paracetamol Assay by HPLC"
          value={name}
          onChange={(e) => onNameChange(e.target.value)}
          required
          error={Boolean(errors.name)}
          helperText={errors.name}
          fullWidth
        />
        <TextField
          size="small"
          label="Abbreviation *"
          placeholder="e.g. PARA-ASSAY"
          value={abbreviation}
          onChange={(e) => onAbbreviationChange(e.target.value.toUpperCase())}
          required
          error={Boolean(errors.abbreviation)}
          helperText={errors.abbreviation ?? "2–20 characters (A–Z, 0–9, -)"}
          slotProps={{ htmlInput: { maxLength: 20 } }}
          fullWidth
        />
        <TextField
          size="small"
          label="Effective Date *"
          type="date"
          value={effectiveDate}
          onChange={(e) => onEffectiveDateChange(e.target.value)}
          required
          error={Boolean(errors.effectiveDate)}
          helperText={errors.effectiveDate}
          slotProps={{ inputLabel: { shrink: true } }}
          fullWidth
        />
      </Box>

      {!editingId && mySections.length > 1 && (
        <FormControl size="small" fullWidth required error={Boolean(errors.sectionId)}>
          <InputLabel id="method-section-label">Laboratory Section *</InputLabel>
          <Select
            labelId="method-section-label"
            label="Laboratory Section *"
            value={sectionId}
            onChange={(e) => onSectionIdChange(Number(e.target.value) || "")}
          >
            {sections.map((sec) => (
              <MenuItem key={sec.sectionId} value={sec.sectionId}>
                {sec.sectionName}
              </MenuItem>
            ))}
          </Select>
          {errors.sectionId && <FormHelperText>{errors.sectionId}</FormHelperText>}
        </FormControl>
      )}
    </Box>
  );
}
