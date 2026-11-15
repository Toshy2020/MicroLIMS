import {
  Box,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  TextField
} from "@mui/material";
import type { ExpectedPresence } from "../../../specifications/services/SpecificationService";
import type { LimitGroupProps } from "./NumericLimits";

/**
 * Qualitative and Presence/Absence limit fields.
 */
export function QualitativeLimits({ model, set, errors }: LimitGroupProps) {
  if (model.limitType === "Qualitative") {
    return (
      <TextField
        size="small"
        name="expectedResultText"
        label="Expected Result Text *"
        multiline
        minRows={2}
        placeholder="e.g. White to off-white, round biconvex effervescent tablets, characteristic citrus odor"
        value={model.expectedResultText}
        onChange={(e) => set({ expectedResultText: e.target.value })}
        error={Boolean(errors.expectedResultText)}
        helperText={errors.expectedResultText}
        fullWidth
      />
    );
  }

  if (model.limitType === "PresenceAbsence") {
    return (
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" },
          gap: 2
        }}
      >
        <FormControl size="small" fullWidth error={Boolean(errors.expectedState)}>
          <InputLabel id="expected-state-label">Expected State *</InputLabel>
          <Select
            name="expectedState"
            labelId="expected-state-label"
            label="Expected State *"
            value={model.expectedState}
            onChange={(e) => set({ expectedState: e.target.value as ExpectedPresence })}
          >
            <MenuItem value="Absence">Absence (Absent)</MenuItem>
            <MenuItem value="Presence">Presence (Present)</MenuItem>
          </Select>
          {errors.expectedState && <FormHelperText>{errors.expectedState}</FormHelperText>}
        </FormControl>
        <TextField
          size="small"
          name="sampleQuantity"
          label="Sample Quantity"
          type="number"
          placeholder="e.g. 1"
          value={model.sampleQuantity}
          onChange={(e) => set({ sampleQuantity: e.target.value })}
          error={Boolean(errors.sampleQuantity)}
          helperText={errors.sampleQuantity}
          slotProps={{ htmlInput: { step: "any" } }}
          fullWidth
        />
        <TextField
          size="small"
          name="sampleQuantityUnit"
          label="Quantity Unit"
          placeholder="e.g. g or mL"
          value={model.sampleQuantityUnit}
          onChange={(e) => set({ sampleQuantityUnit: e.target.value })}
          error={Boolean(errors.sampleQuantityUnit)}
          helperText={errors.sampleQuantityUnit}
          fullWidth
        />
      </Box>
    );
  }

  return null;
}
