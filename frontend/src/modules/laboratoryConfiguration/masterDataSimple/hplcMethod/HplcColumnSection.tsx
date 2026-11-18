import { Box, Typography, TextField, Stack } from "@mui/material";
import { useTranslation } from "react-i18next";
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
  const { t } = useTranslation("masterData");
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
        {isGc ? t("hplcMethod.column.titleGc") : t("hplcMethod.column.titleHplc")}
      </Typography>
      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
        {isGc
          ? t("hplcMethod.column.subtitleGc")
          : t("hplcMethod.column.subtitleHplc")}
      </Typography>

      <Stack spacing={2}>
        <Box sx={{ display: "grid", gridTemplateColumns: isGc ? "1fr" : { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
          <TextField
            size="small"
            label={t("hplcMethod.column.designationLabel")}
            placeholder={isGc ? t("hplcMethod.column.designationPlaceholderGc") : t("hplcMethod.column.designationPlaceholderHplc")}
            value={columnDesignation}
            onChange={(e) => onColumnDesignationChange(e.target.value)}
            required
            error={Boolean(errors["column.designation"])}
            helperText={errors["column.designation"] ?? (isGc ? t("hplcMethod.column.designationHelperGc") : t("hplcMethod.column.designationHelperHplc"))}
            fullWidth
          />
          {!isGc && (
            <TextField
              size="small"
              label={t("hplcMethod.column.temperatureLabel")}
              type="number"
              value={columnTemperatureC}
              onChange={(e) => onColumnTemperatureCChange?.(e.target.value)}
              required
              error={Boolean(errors["column.temperatureC"])}
              helperText={errors["column.temperatureC"] ?? t("hplcMethod.column.temperatureHelper")}
              slotProps={{ htmlInput: { min: 0, step: "any" } }}
              fullWidth
            />
          )}
        </Box>

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" }, gap: 2 }}>
          <TextField
            size="small"
            label={isGc ? t("hplcMethod.column.lengthLabelGc") : t("hplcMethod.column.lengthLabelHplc")}
            type="number"
            value={lengthVal}
            onChange={(e) => handleLengthChange(e.target.value)}
            required
            placeholder={isGc ? t("hplcMethod.column.lengthPlaceholderGc") : t("hplcMethod.column.lengthPlaceholderHplc")}
            error={Boolean(lengthError)}
            helperText={lengthError ?? (isGc ? t("hplcMethod.column.lengthHelperGc") : t("hplcMethod.column.lengthHelperHplc"))}
            slotProps={{ htmlInput: { min: 0, step: "any" } }}
            fullWidth
          />
          <TextField
            size="small"
            label={t("hplcMethod.column.idLabel")}
            type="number"
            value={columnInternalDiameterMm}
            onChange={(e) => onColumnInternalDiameterMmChange(e.target.value)}
            required
            placeholder={isGc ? t("hplcMethod.column.idPlaceholderGc") : t("hplcMethod.column.idPlaceholderHplc")}
            error={Boolean(errors["column.internalDiameterMm"])}
            helperText={errors["column.internalDiameterMm"] ?? t("hplcMethod.column.idHelper")}
            slotProps={{ htmlInput: { min: 0, step: "any" } }}
            fullWidth
          />
          {isGc ? (
            <TextField
              size="small"
              label={t("hplcMethod.column.filmThicknessLabel")}
              type="number"
              value={filmThicknessUm}
              onChange={(e) => onFilmThicknessUmChange?.(e.target.value)}
              required
              placeholder={t("hplcMethod.column.filmThicknessPlaceholder")}
              error={Boolean(errors["column.filmThicknessUm"])}
              helperText={errors["column.filmThicknessUm"] ?? t("hplcMethod.column.filmThicknessHelper")}
              slotProps={{ htmlInput: { min: 0, step: "any" } }}
              fullWidth
            />
          ) : (
            <TextField
              size="small"
              label={t("hplcMethod.column.particleSizeLabel")}
              type="number"
              value={particleSizeUm}
              onChange={(e) => onParticleSizeUmChange?.(e.target.value)}
              required
              placeholder={t("hplcMethod.column.particleSizePlaceholder")}
              error={Boolean(errors["column.particleSizeUm"])}
              helperText={errors["column.particleSizeUm"] ?? t("hplcMethod.column.particleSizeHelper")}
              slotProps={{ htmlInput: { min: 0, step: "any" } }}
              fullWidth
            />
          )}
        </Box>

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
          <TextField
            size="small"
            label={t("hplcMethod.column.brandLabel")}
            placeholder={isGc ? t("hplcMethod.column.brandPlaceholderGc") : t("hplcMethod.column.brandPlaceholderHplc")}
            value={columnBrand}
            onChange={(e) => onColumnBrandChange(e.target.value)}
            fullWidth
          />
          <TextField
            size="small"
            label={t("hplcMethod.column.partNumberLabel")}
            placeholder={isGc ? t("hplcMethod.column.partNumberPlaceholderGc") : t("hplcMethod.column.partNumberPlaceholderHplc")}
            value={columnPartNumber}
            onChange={(e) => onColumnPartNumberChange(e.target.value)}
            fullWidth
          />
        </Box>
      </Stack>
    </Box>
  );
}
