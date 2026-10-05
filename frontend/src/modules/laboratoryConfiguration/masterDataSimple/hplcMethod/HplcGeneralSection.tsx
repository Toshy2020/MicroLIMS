import {
  Box,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  FormHelperText,
  Chip,
  Typography,
  Stack
} from "@mui/material";
import { HplcTechnique, HplcResultMode } from "../services/HplcMethodService";
import { LaboratorySection } from "../../../../services/laboratorySectionService";
import { HplcMethodErrors } from "./hplcMethodValidation";

export interface HplcGeneralSectionProps {
  technique?: HplcTechnique;
  resultMode?: HplcResultMode;
  onTechniqueChange?: (val: HplcTechnique) => void;
  onResultModeChange?: (val: HplcResultMode) => void;
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
  technique = "Hplc",
  resultMode = "Assay",
  onTechniqueChange,
  onResultModeChange,
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
  const isEditing = Boolean(editingId);
  const isGc = technique === "Gc";

  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
      {/* Technique & Result Mode Pickers */}
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 2, alignItems: { sm: "center" } }}>
        {isEditing ? (
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <Typography variant="body2" sx={{ fontWeight: 600, color: "text.secondary" }}>
              Technique:
            </Typography>
            <Chip
              label={technique.toUpperCase()}
              size="small"
              color={isGc ? "secondary" : "primary"}
              sx={{ fontWeight: 700 }}
            />
          </Stack>
        ) : (
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel id="method-technique-picker-label">Technique *</InputLabel>
            <Select
              labelId="method-technique-picker-label"
              label="Technique *"
              value={technique}
              onChange={(e) => onTechniqueChange?.(e.target.value as HplcTechnique)}
            >
              <MenuItem value="Hplc">HPLC (Liquid)</MenuItem>
              <MenuItem value="Gc">GC (Gas)</MenuItem>
            </Select>
          </FormControl>
        )}

        {isGc && (
          isEditing ? (
            <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
              <Typography variant="body2" sx={{ fontWeight: 600, color: "text.secondary" }}>
                Result Mode:
              </Typography>
              <Chip
                label={resultMode === "ResidualSolvents" ? "Residual Solvents" : "Assay"}
                size="small"
                variant="outlined"
                color={resultMode === "ResidualSolvents" ? "warning" : "default"}
                sx={{ fontWeight: 600 }}
              />
            </Stack>
          ) : (
            <FormControl size="small" sx={{ minWidth: 200 }}>
              <InputLabel id="method-result-mode-picker-label">Result Mode *</InputLabel>
              <Select
                labelId="method-result-mode-picker-label"
                label="Result Mode *"
                value={resultMode}
                onChange={(e) => onResultModeChange?.(e.target.value as HplcResultMode)}
              >
                <MenuItem value="Assay">Assay</MenuItem>
                <MenuItem value="ResidualSolvents">Residual Solvents</MenuItem>
              </Select>
            </FormControl>
          )
        )}
      </Stack>

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "1.5fr 1fr 1fr" },
          gap: 2,
          mb: !editingId && mySections.length > 1 ? 2 : 0
        }}
      >
        <TextField
          size="small"
          label="Method Name *"
          placeholder={isGc ? "e.g. Residual Solvents by Headspace GC" : "e.g. Paracetamol Assay by HPLC"}
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
          placeholder={isGc ? "e.g. RS-GC-01" : "e.g. PARA-ASSAY"}
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
