import {
  Box,
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Switch,
  TextField,
  Typography
} from "@mui/material";
import type { MoistureRole } from "../../../../../services/masterDataOptions";
import type { TestFormModel } from "../testFormModel";

interface Props {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
  errors: Record<string, string>;
}

export function GravimetricSettingsBlock({ model, set, errors }: Props) {
  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
        Gravimetric Settings
      </Typography>
      <Stack spacing={2}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: "center" }}>
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
          <FormControlLabel
            control={
              <Switch
                checked={model.usesTare}
                onChange={(e) => set({ usesTare: e.target.checked })}
              />
            }
            label="Uses tare (container weight)"
            sx={{ flex: 1 }}
          />
        </Stack>
        <TextField
          size="small"
          name="conditionFields"
          label="Condition fields"
          placeholder="e.g. Temperature (°C),Time (h)"
          value={model.conditionFields}
          onChange={(e) => set({ conditionFields: e.target.value })}
          error={Boolean(errors.conditionFields)}
          helperText={errors.conditionFields || "comma-separated, e.g. Temperature (°C),Time (h)"}
          slotProps={{ htmlInput: { maxLength: 500 } }}
          fullWidth
        />
        {model.equationType === "GravimetricLoss" && (
          <FormControl size="small" sx={{ maxWidth: 300 }}>
            <InputLabel id="gravimetric-moisture-role-label">Moisture role</InputLabel>
            <Select<MoistureRole>
              labelId="gravimetric-moisture-role-label"
              name="moistureRole"
              label="Moisture role"
              value={model.moistureRole === "Water" ? "None" : model.moistureRole}
              onChange={(e) => set({ moistureRole: e.target.value as MoistureRole })}
            >
              <MenuItem value="None">None</MenuItem>
              <MenuItem value="LossOnDrying">Loss on drying</MenuItem>
            </Select>
          </FormControl>
        )}
      </Stack>
    </Box>
  );
}
