import {
  Box,
  TextField,
  Typography
} from "@mui/material";
import type { IcpMethodErrors } from "./icpMethodValidation";

export interface IcpSamplePrepSectionProps {
  sampleVolumeMl: string | number;
  dilutionFactor: string | number;
  errors: IcpMethodErrors;
  onSampleVolumeChange: (val: string) => void;
  onDilutionFactorChange: (val: string) => void;
}

export function IcpSamplePrepSection({
  sampleVolumeMl,
  dilutionFactor,
  errors,
  onSampleVolumeChange,
  onDilutionFactorChange
}: IcpSamplePrepSectionProps) {
  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1.5 }}>
        Default Sample Preparation Parameters
      </Typography>

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" },
          gap: 2
        }}
      >
        <TextField
          size="small"
          label="Sample Volume (mL) *"
          placeholder="10"
          type="number"
          slotProps={{ htmlInput: { step: "any", min: "0.001" } }}
          value={sampleVolumeMl}
          onChange={(e) => onSampleVolumeChange(e.target.value)}
          required
          error={Boolean(errors.sampleVolumeMl)}
          helperText={errors.sampleVolumeMl ?? "Default nominal volume of test aliquot (mL > 0)"}
          fullWidth
        />

        <TextField
          size="small"
          label="Dilution Factor *"
          placeholder="1"
          type="number"
          slotProps={{ htmlInput: { step: "any", min: "1" } }}
          value={dilutionFactor}
          onChange={(e) => onDilutionFactorChange(e.target.value)}
          required
          error={Boolean(errors.dilutionFactor)}
          helperText={errors.dilutionFactor ?? "Default dilution ratio multiplier (≥ 1)"}
          fullWidth
        />
      </Box>
    </Box>
  );
}
