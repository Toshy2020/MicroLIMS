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
import type { IcpMethodMode } from "../services/IcpMethodService";
import type { LaboratorySection } from "../../../../services/laboratorySectionService";
import { ICP_MODE_OPTIONS } from "./icpMethodForm";
import type { IcpMethodErrors } from "./icpMethodValidation";

export interface IcpGeneralSectionProps {
  name: string;
  abbreviation: string;
  effectiveDate: string;
  sectionId: number | "";
  mode: IcpMethodMode;
  errors: IcpMethodErrors;
  editingId: number | null;
  mySections: LaboratorySection[];
  sections: LaboratorySection[];
  onNameChange: (val: string) => void;
  onAbbreviationChange: (val: string) => void;
  onEffectiveDateChange: (val: string) => void;
  onSectionIdChange: (val: number | "") => void;
  onModeChange: (val: IcpMethodMode) => void;
}

export function IcpGeneralSection({
  name,
  abbreviation,
  effectiveDate,
  sectionId,
  mode,
  errors,
  editingId,
  mySections,
  sections,
  onNameChange,
  onAbbreviationChange,
  onEffectiveDateChange,
  onSectionIdChange,
  onModeChange
}: IcpGeneralSectionProps) {
  const isEditing = Boolean(editingId);
  const activeModeOption = ICP_MODE_OPTIONS.find((o) => o.value === mode);

  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
      {/* Mode selection row */}
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 2, alignItems: { sm: "center" } }}>
        {isEditing ? (
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <Typography variant="body2" sx={{ fontWeight: 600, color: "text.secondary" }}>
              Method Mode:
            </Typography>
            <Chip
              label={activeModeOption?.label ?? mode}
              size="small"
              color={mode === "MineralAssay" ? "primary" : "secondary"}
              sx={{ fontWeight: 700 }}
            />
            <Typography variant="caption" sx={{ color: "text.secondary", ml: 1 }}>
              (Mode is locked after creation)
            </Typography>
          </Stack>
        ) : (
          <FormControl size="small" sx={{ minWidth: 260 }} error={Boolean(errors.mode)}>
            <InputLabel id="icp-method-mode-label">Method Mode *</InputLabel>
            <Select
              labelId="icp-method-mode-label"
              label="Method Mode *"
              value={mode}
              onChange={(e) => onModeChange(e.target.value as IcpMethodMode)}
            >
              {ICP_MODE_OPTIONS.map((opt) => (
                <MenuItem key={opt.value} value={opt.value}>
                  <Box>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {opt.label}
                    </Typography>
                    <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                      {opt.description}
                    </Typography>
                  </Box>
                </MenuItem>
              ))}
            </Select>
            {errors.mode && <FormHelperText>{errors.mode}</FormHelperText>}
          </FormControl>
        )}
      </Stack>

      {/* Main form grid: Name, Abbreviation, Effective Date */}
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "1.5fr 1fr 1fr" },
          gap: 2,
          mb: !isEditing && mySections.length > 1 ? 2 : 0
        }}
      >
        <TextField
          size="small"
          label="Method Name *"
          placeholder="e.g. Zinc & Minerals Assay by ICP-OES"
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
          placeholder="e.g. ICP-MIN-01"
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

      {/* Section Picker (if user belongs to multiple sections or editing) */}
      {(!isEditing && mySections.length > 1) && (
        <Box sx={{ mt: 2 }}>
          <FormControl size="small" fullWidth error={Boolean(errors.sectionId)}>
            <InputLabel id="icp-method-section-label">Laboratory Section *</InputLabel>
            <Select
              labelId="icp-method-section-label"
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
        </Box>
      )}
    </Box>
  );
}
