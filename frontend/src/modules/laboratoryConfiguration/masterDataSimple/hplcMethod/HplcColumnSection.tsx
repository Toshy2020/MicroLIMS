import { Box, Typography, TextField, Stack } from "@mui/material";
import { HplcMethodErrors } from "./hplcMethodValidation";

export interface HplcColumnSectionProps {
  columnDesignation: string;
  columnLengthMm: string | number;
  columnInternalDiameterMm: string | number;
  particleSizeUm: string | number;
  columnBrand: string;
  columnPartNumber: string;
  columnTemperatureC: string | number;
  errors: HplcMethodErrors;
  onColumnDesignationChange: (val: string) => void;
  onColumnLengthMmChange: (val: string) => void;
  onColumnInternalDiameterMmChange: (val: string) => void;
  onParticleSizeUmChange: (val: string) => void;
  onColumnBrandChange: (val: string) => void;
  onColumnPartNumberChange: (val: string) => void;
  onColumnTemperatureCChange: (val: string) => void;
}

export function HplcColumnSection({
  columnDesignation,
  columnLengthMm,
  columnInternalDiameterMm,
  particleSizeUm,
  columnBrand,
  columnPartNumber,
  columnTemperatureC,
  errors,
  onColumnDesignationChange,
  onColumnLengthMmChange,
  onColumnInternalDiameterMmChange,
  onParticleSizeUmChange,
  onColumnBrandChange,
  onColumnPartNumberChange,
  onColumnTemperatureCChange
}: HplcColumnSectionProps) {
  return (
    <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5, color: "text.primary" }}>
        Column Specifications (USP &lt;621&gt;)
      </Typography>
      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
        Specify stationary phase designation, column dimensions, brand/part number, and oven temperature.
      </Typography>

      <Stack spacing={2}>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
          <TextField
            size="small"
            label="Column Designation *"
            placeholder="e.g. L1 (C18), L7 (C8), L11"
            value={columnDesignation}
            onChange={(e) => onColumnDesignationChange(e.target.value)}
            required
            error={Boolean(errors["column.designation"])}
            helperText={errors["column.designation"] ?? "USP designation (e.g. L1)"}
            fullWidth
          />
          <TextField
            size="small"
            label="Column Temperature (°C) *"
            type="number"
            value={columnTemperatureC}
            onChange={(e) => onColumnTemperatureCChange(e.target.value)}
            required
            error={Boolean(errors["column.temperatureC"])}
            helperText={errors["column.temperatureC"] ?? "Oven temperature in Celsius"}
            slotProps={{ htmlInput: { min: 0, step: "any" } }}
            fullWidth
          />
        </Box>

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" }, gap: 2 }}>
          <TextField
            size="small"
            label="Length (mm) *"
            type="number"
            value={columnLengthMm}
            onChange={(e) => onColumnLengthMmChange(e.target.value)}
            required
            placeholder="e.g. 150"
            error={Boolean(errors["column.lengthMm"])}
            helperText={errors["column.lengthMm"] ?? "Column length in mm"}
            slotProps={{ htmlInput: { min: 0, step: "any" } }}
            fullWidth
          />
          <TextField
            size="small"
            label="Internal Diameter (mm) *"
            type="number"
            value={columnInternalDiameterMm}
            onChange={(e) => onColumnInternalDiameterMmChange(e.target.value)}
            required
            placeholder="e.g. 4.6"
            error={Boolean(errors["column.internalDiameterMm"])}
            helperText={errors["column.internalDiameterMm"] ?? "Internal diameter in mm"}
            slotProps={{ htmlInput: { min: 0, step: "any" } }}
            fullWidth
          />
          <TextField
            size="small"
            label="Particle Size (µm) *"
            type="number"
            value={particleSizeUm}
            onChange={(e) => onParticleSizeUmChange(e.target.value)}
            required
            placeholder="e.g. 5"
            error={Boolean(errors["column.particleSizeUm"])}
            helperText={errors["column.particleSizeUm"] ?? "Packing particle size"}
            slotProps={{ htmlInput: { min: 0, step: "any" } }}
            fullWidth
          />
        </Box>

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
          <TextField
            size="small"
            label="Column Brand (Optional)"
            placeholder="e.g. Waters Symmetry, Agilent Zorbax"
            value={columnBrand}
            onChange={(e) => onColumnBrandChange(e.target.value)}
            fullWidth
          />
          <TextField
            size="small"
            label="Column Part Number (Optional)"
            placeholder="e.g. WAT045905"
            value={columnPartNumber}
            onChange={(e) => onColumnPartNumberChange(e.target.value)}
            fullWidth
          />
        </Box>
      </Stack>
    </Box>
  );
}
