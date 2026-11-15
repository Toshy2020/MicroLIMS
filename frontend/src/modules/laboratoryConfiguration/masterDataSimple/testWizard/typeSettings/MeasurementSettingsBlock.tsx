import {
  Box,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import type { MeasurementEvaluationBasis, TestFormModel } from "../testFormModel";

interface Props {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
  errors: Record<string, string>;
}

export function MeasurementSettingsBlock({ model, set, errors }: Props) {
  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
        Measurement Settings
      </Typography>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
        <TextField
          size="small"
          type="number"
          name="replicateCount"
          label="Replicate Count *"
          value={model.replicateCount}
          onChange={(e) => set({ replicateCount: e.target.value })}
          slotProps={{ htmlInput: { min: 1, max: 30, step: 1 } }}
          error={Boolean(errors.replicateCount)}
          helperText={errors.replicateCount || "Replicates per parameter (1–30)"}
          required
          sx={{ flex: 1 }}
        />
        <FormControl size="small" sx={{ flex: 1 }} required error={Boolean(errors.evaluationBasis)}>
          <InputLabel id="dialog-eval-basis-label">Evaluation Basis *</InputLabel>
          <Select
            labelId="dialog-eval-basis-label"
            name="evaluationBasis"
            label="Evaluation Basis *"
            value={model.evaluationBasis}
            onChange={(e) => set({ evaluationBasis: e.target.value as MeasurementEvaluationBasis })}
          >
            <MenuItem value="Mean">Mean</MenuItem>
            <MenuItem value="EachValue">Each Value</MenuItem>
            <MenuItem value="Min">Minimum</MenuItem>
            <MenuItem value="Max">Maximum</MenuItem>
          </Select>
        </FormControl>
      </Stack>
    </Box>
  );
}
