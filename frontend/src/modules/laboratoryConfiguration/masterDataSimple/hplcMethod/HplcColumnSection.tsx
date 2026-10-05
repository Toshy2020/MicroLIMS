import { Box, Typography, TextField, Stack } from "@mui/material";
import { HplcTechnique } from "../services/HplcMethodService";
import { HplcMethodErrors } from "./hplcMethodValidation";

export interface HplcColumnSectionProps {
  technique?: HplcTechnique;
  columnDesignation: string;
  columnLength?: string | number;
  columnInternalDiameterMm: string | number;
  particleSizeUm?: string | number;
  filmThicknessUm?: string | number;
  columnBrand: string;
  columnPartNumber: string;
  columnTemperatureC?: string | number;
  errors: HplcMethodErrors;
  onColumnDesignationChange: (val: string) => void;
  onColumnLengthChange?: (val: string) => void;
  onColumnLengthMmChange?: (val: string) => void;
  onColumnInternalDiameterMmChange: (val: string) => void;
  onParticleSizeUmChange?: (val: string) => void;
  onFilmThicknessUmChange?: (val: string) => void;
  onColumnBrandChange: (val: string) => void;
  onColumnPartNumberChange: (val: string) => void;
  onColumnTemperatureCChange?: (val: string) => void;
}

export function HplcColumnSection({
  technique = "Hplc",
  columnDesignation,
  columnLength,
  columnInternalDiameterMm,
  particleSizeUm = "",
  filmThicknessUm = "",
  columnBrand,
  columnPartNumber,
  columnTemperatureC = "",
  errors,
  onColumnDesignationChange,
  onColumnLengthChange,
  onColumnLengthMmChange,
  onColumnInternalDiameterMmChange,
  onParticleSizeUmChange,
  onFilmThicknessUmChange,
  onColumnBrandChange,
  onColumnPartNumberChange,
  onColumnTemperatureCChange
}: HplcColumnSectionProps) {
  const isGc = technique === "Gc";
  const lengthVal = columnLength ?? "";
  const handleLengthChange = (val: string) => {
    onColumnLengthChange?.(val);
    onColumnLengthMmChange?.(val);
  };

  const lengthError = errors["column.length"] ?? errors["column.lengthMm"];

  return (
    <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5, color: "text.primary" }}>
        {isGc ? "Column Specifications (USP <621> / Capillary GC)" : "Column Specifications (USP <621>)"}
      </Typography>
      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
        {isGc
          ? "Specify stationary phase designation (e.g. G43), capillary dimensions (length in metres, ID, film thickness), and brand/part number."
          : "Specify stationary phase designation, column dimensions, brand/part number, and oven temperature."}
      </Typography>

      <Stack spacing={2}>
        <Box sx={{ display: "grid", gridTemplateColumns: isGc ? "1fr" : { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
          <TextField
            size="small"
            label="Column Designation *"
            placeholder={isGc ? "e.g. G43 (USP G43 - 6% cyanopropylphenyl / 94% dimethylpolysiloxane)" : "e.g. L1 (C18), L7 (C8), L11"}
            value={columnDesignation}
            onChange={(e) => onColumnDesignationChange(e.target.value)}
            required
            error={Boolean(errors["column.designation"])}
            helperText={errors["column.designation"] ?? (isGc ? "USP designation (e.g. G43, G1, G27)" : "USP designation (e.g. L1)")}
            fullWidth
          />
          {!isGc && (
            <TextField
              size="small"
              label="Column Temperature (°C) *"
              type="number"
              value={columnTemperatureC}
              onChange={(e) => onColumnTemperatureCChange?.(e.target.value)}
              required
              error={Boolean(errors["column.temperatureC"])}
              helperText={errors["column.temperatureC"] ?? "Oven temperature in Celsius"}
              slotProps={{ htmlInput: { min: 0, step: "any" } }}
              fullWidth
            />
          )}
        </Box>

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" }, gap: 2 }}>
          <TextField
            size="small"
            label={isGc ? "Length (m) *" : "Length (mm) *"}
            type="number"
            value={lengthVal}
            onChange={(e) => handleLengthChange(e.target.value)}
            required
            placeholder={isGc ? "e.g. 30" : "e.g. 150"}
            error={Boolean(lengthError)}
            helperText={lengthError ?? (isGc ? "Column length in metres (e.g. 30 m)" : "Column length in mm")}
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
            placeholder={isGc ? "e.g. 0.32 or 0.53" : "e.g. 4.6"}
            error={Boolean(errors["column.internalDiameterMm"])}
            helperText={errors["column.internalDiameterMm"] ?? "Internal diameter in mm"}
            slotProps={{ htmlInput: { min: 0, step: "any" } }}
            fullWidth
          />
          {isGc ? (
            <TextField
              size="small"
              label="Film Thickness (µm) *"
              type="number"
              value={filmThicknessUm}
              onChange={(e) => onFilmThicknessUmChange?.(e.target.value)}
              required
              placeholder="e.g. 1.8 or 0.25"
              error={Boolean(errors["column.filmThicknessUm"])}
              helperText={errors["column.filmThicknessUm"] ?? "Stationary phase film thickness (df)"}
              slotProps={{ htmlInput: { min: 0, step: "any" } }}
              fullWidth
            />
          ) : (
            <TextField
              size="small"
              label="Particle Size (µm) *"
              type="number"
              value={particleSizeUm}
              onChange={(e) => onParticleSizeUmChange?.(e.target.value)}
              required
              placeholder="e.g. 5"
              error={Boolean(errors["column.particleSizeUm"])}
              helperText={errors["column.particleSizeUm"] ?? "Packing particle size"}
              slotProps={{ htmlInput: { min: 0, step: "any" } }}
              fullWidth
            />
          )}
        </Box>

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
          <TextField
            size="small"
            label="Column Brand (Optional)"
            placeholder={isGc ? "e.g. DB-624, HP-5, Rtx-Volatiles" : "e.g. Waters Symmetry, Agilent Zorbax"}
            value={columnBrand}
            onChange={(e) => onColumnBrandChange(e.target.value)}
            fullWidth
          />
          <TextField
            size="small"
            label="Column Part Number (Optional)"
            placeholder={isGc ? "e.g. 123-1334" : "e.g. WAT045905"}
            value={columnPartNumber}
            onChange={(e) => onColumnPartNumberChange(e.target.value)}
            fullWidth
          />
        </Box>
      </Stack>
    </Box>
  );
}
