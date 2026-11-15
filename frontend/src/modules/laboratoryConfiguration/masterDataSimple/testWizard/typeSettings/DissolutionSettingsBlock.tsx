import {
  Box,
  FormControlLabel,
  Stack,
  Switch,
  TextField,
  Typography
} from "@mui/material";
import type { TestFormModel } from "../testFormModel";

interface Props {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
  errors: Record<string, string>;
}

export function DissolutionSettingsBlock({ model, set, errors }: Props) {
  return (
    <Stack spacing={2}>
      <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
        <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
          Dissolution Acceptance Offsets (USP &lt;711&gt; / EP 2.9.3)
        </Typography>
        <Stack spacing={2}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              size="small"
              type="number"
              name="dissolutionS1Offset"
              label="S1: each unit ≥ Q + "
              value={model.dissolutionS1Offset}
              onChange={(e) => set({ dissolutionS1Offset: e.target.value })}
              slotProps={{ htmlInput: { min: 0, step: "any" } }}
              error={Boolean(errors.dissolutionS1Offset)}
              helperText={errors.dissolutionS1Offset || "Default: 5 (%)"}
              sx={{ flex: 1 }}
            />
            <TextField
              size="small"
              type="number"
              name="dissolutionS2MinOffset"
              label="S2: no unit < Q − "
              value={model.dissolutionS2MinOffset}
              onChange={(e) => set({ dissolutionS2MinOffset: e.target.value })}
              slotProps={{ htmlInput: { min: 0, step: "any" } }}
              error={Boolean(errors.dissolutionS2MinOffset)}
              helperText={errors.dissolutionS2MinOffset || "Default: 15 (%)"}
              sx={{ flex: 1 }}
            />
          </Stack>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              size="small"
              type="number"
              name="dissolutionS3MinOffset"
              label="S3: no unit < Q − "
              value={model.dissolutionS3MinOffset}
              onChange={(e) => set({ dissolutionS3MinOffset: e.target.value })}
              slotProps={{ htmlInput: { min: 0, step: "any" } }}
              error={Boolean(errors.dissolutionS3MinOffset)}
              helperText={errors.dissolutionS3MinOffset || "Default: 25 (%)"}
              sx={{ flex: 1 }}
            />
            <TextField
              size="small"
              type="number"
              name="dissolutionS3MaxBelowS2Min"
              label="S3: max units < Q − S2 value"
              value={model.dissolutionS3MaxBelowS2Min}
              onChange={(e) => set({ dissolutionS3MaxBelowS2Min: e.target.value })}
              slotProps={{ htmlInput: { min: 0, step: 1 } }}
              error={Boolean(errors.dissolutionS3MaxBelowS2Min)}
              helperText={errors.dissolutionS3MaxBelowS2Min || "Default: 2 (units)"}
              sx={{ flex: 1 }}
            />
          </Stack>
          <TextField
            size="small"
            name="conditionFields"
            label="Condition fields"
            placeholder="Apparatus,RPM,Medium,Temperature (°C),Time (min)"
            value={model.conditionFields}
            onChange={(e) => set({ conditionFields: e.target.value })}
            error={Boolean(errors.conditionFields)}
            helperText={errors.conditionFields || "comma-separated, e.g. Apparatus,RPM,Medium,Temperature (°C),Time (min)"}
            slotProps={{ htmlInput: { maxLength: 500 } }}
            fullWidth
          />
        </Stack>
      </Box>

      <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
        <FormControlLabel
          control={<Switch checked={model.requiresSystemSuitability} disabled />}
          label="Requires system suitability (standard from the assigned HPLC run)"
        />
        <Box sx={{ mt: 2, pt: 2, borderTop: "1px dashed", borderTopColor: "divider" }}>
          <TextField
            size="small"
            name="methodAbbreviation"
            label="Method Abbreviation"
            placeholder="e.g. VIT-C"
            value={model.methodAbbreviation}
            onChange={(e) => set({ methodAbbreviation: e.target.value.toUpperCase() })}
            required
            fullWidth
            error={Boolean(errors.methodAbbreviation)}
            slotProps={{ htmlInput: { maxLength: 20 } }}
            helperText={errors.methodAbbreviation || "1–20 uppercase alphanumeric characters or hyphens (auto-uppercased)"}
          />
        </Box>
      </Box>
    </Stack>
  );
}
