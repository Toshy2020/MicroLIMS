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
  const hasPhTarget = phTarget !== "";

  return (
    <Paper variant="outlined" sx={{ p: 2, borderRadius: 1.5, bgcolor: "background.default" }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.primary" }}>
        pH Specifications (Optional)
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" }, gap: 2 }}>
        <TextField
          label="pH Target"
          type="number"
          value={phTarget}
          onChange={(e) => onPhTargetChange(e.target.value)}
          size="small"
          placeholder="e.g. 7.00"
          slotProps={{ htmlInput: { min: "0", max: "14", step: "0.01" } }}
          error={!!errors.phTarget}
          helperText={errors.phTarget ?? "Range: 0.00 – 14.00"}
        />

        <TextField
          label="pH Tolerance (±)"
          type="number"
          value={phTolerance}
          onChange={(e) => onPhToleranceChange(e.target.value)}
          size="small"
          disabled={!hasPhTarget}
          placeholder="e.g. 0.05"
          slotProps={{ htmlInput: { min: "0.01", step: "0.01" } }}
          error={!!errors.phTolerance}
          helperText={errors.phTolerance ?? (!hasPhTarget ? "Requires target pH" : "Acceptable ± deviation")}
        />

        <FormControl size="small" disabled={!hasPhTarget} error={!!errors.phAdjuster}>
          <InputLabel id="ph-adjusting-reagent-select-label">pH Adjusting Reagent</InputLabel>
          <Select
            labelId="ph-adjusting-reagent-select-label"
            label="pH Adjusting Reagent"
            value={phAdjustingEntryId}
            onChange={(e) => onPhAdjustingEntryIdChange(e.target.value)}
          >
            <MenuItem value="">
              <em>(None)</em>
            </MenuItem>
            {availablePhAdjustingEntries.map((m) => (
              <MenuItem key={m.id} value={m.id}>
                {m.code} - {m.name} {!m.isActive ? "(Inactive)" : ""}
              </MenuItem>
            ))}
          </Select>
          <FormHelperText>{errors.phAdjuster ?? "Reagents in current section"}</FormHelperText>
        </FormControl>
      </Box>
    </Paper>
  );
}
