import { useState } from "react";
import {
  Box,
  Typography,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Stack,
  Button,
  IconButton,
  Tooltip,
  Paper,
  Collapse,
  FormHelperText
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import { HplcTechnique, HplcResultMode } from "../services/HplcMethodService";
import { MaterialMasterEntry } from "../services/MaterialMasterService";
import { AnalyteRowState } from "./hplcMethodForm";
import { HplcMethodErrors } from "./hplcMethodValidation";
import { useTranslation } from "../../../../i18n";

export interface HplcAnalytesSectionProps {
  technique?: HplcTechnique;
  resultMode?: HplcResultMode;
  sampleSolutionVolumeMl?: string | number;
  onSampleSolutionVolumeMlChange?: (val: string) => void;
  swabAreaCm2?: string | number;
  onSwabAreaCm2Change?: (val: string) => void;
  analytes: AnalyteRowState[];
  availableStandards: MaterialMasterEntry[];
  errors: HplcMethodErrors;
  onAnalyteChange: (index: number, field: keyof AnalyteRowState, val: string | number) => void;
  onAddAnalyte: () => void;
  onRemoveAnalyte: (index: number) => void;
  onMoveAnalyte: (index: number, direction: "up" | "down") => void;
}

export function HplcAnalytesSection({
  technique = "Hplc",
  resultMode = "Assay",
  sampleSolutionVolumeMl = "",
  onSampleSolutionVolumeMlChange,
  swabAreaCm2 = "",
  onSwabAreaCm2Change,
  analytes,
  availableStandards,
  errors,
  onAnalyteChange,
  onAddAnalyte,
  onRemoveAnalyte,
  onMoveAnalyte
}: HplcAnalytesSectionProps) {
  const { t } = useTranslation("masterData");
  const isGc = technique === "Gc";
  const isResidual = isGc && resultMode === "ResidualSolvents";
  const isRelatedSubstances = !isGc && resultMode === "RelatedSubstances";
  const isCleaningResidue = !isGc && resultMode === "CleaningResidue";
  const [expandedSstIndices, setExpandedSstIndices] = useState<Record<number, boolean>>({});

  const toggleSstExpand = (index: number) => {
    setExpandedSstIndices((prev) => ({ ...prev, [index]: !prev[index] }));
  };

  return (
    <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
        <div>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5, color: "text.primary" }}>
            {isRelatedSubstances
              ? t("hplcMethod.analytes.titleRelated")
              : isResidual
              ? t("hplcMethod.analytes.titleResidual")
              : isCleaningResidue
              ? t("hplcMethod.analytes.titleCleaning")
              : t("hplcMethod.analytes.titleDefault")}
          </Typography>
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {isRelatedSubstances
              ? t("hplcMethod.analytes.subtitleRelated")
              : isResidual
              ? t("hplcMethod.analytes.subtitleResidual")
              : isCleaningResidue
              ? t("hplcMethod.analytes.subtitleCleaning")
              : isGc
              ? t("hplcMethod.analytes.subtitleGc")
              : t("hplcMethod.analytes.subtitleHplc")}
          </Typography>
        </div>
        {!isRelatedSubstances && (
          <Button
            size="small"
            variant="contained"
            startIcon={<AddIcon />}
            onClick={onAddAnalyte}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            {t("hplcMethod.analytes.addAnalyte")}
          </Button>
        )}
      </Stack>

      {/* Residual Solvents method-level preparation volume */}
      {isResidual && (
        <Box
          sx={{
            p: 2,
            mb: 2,
            bgcolor: "action.hover",
            borderRadius: 1.5,
            border: "1px solid",
            borderColor: "divider"
          }}
        >
          <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 0.5 }}>
            {t("hplcMethod.analytes.sampleVolTitle")}
          </Typography>
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 1.5 }}>
            {t("hplcMethod.analytes.sampleVolSubtitle")}
          </Typography>
          <TextField
            size="small"
            label={t("hplcMethod.analytes.sampleVolLabel")}
            type="number"
            value={sampleSolutionVolumeMl}
            onChange={(e) => onSampleSolutionVolumeMlChange?.(e.target.value)}
            required
            placeholder={t("hplcMethod.analytes.sampleVolPlaceholder")}
            error={Boolean(errors["sampleSolutionVolumeMl"])}
            helperText={
              errors["sampleSolutionVolumeMl"] ?? t("hplcMethod.analytes.sampleVolHelper")
            }
            slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
            sx={{ maxWidth: 320 }}
          />
        </Box>
      )}

      {/* Cleaning Residue method-level extraction volume and swabbed area */}
      {isCleaningResidue && (
        <Box
          sx={{
            p: 2,
            mb: 2,
            bgcolor: "action.hover",
            borderRadius: 1.5,
            border: "1px solid",
            borderColor: "divider"
          }}
        >
          <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 0.5 }}>
            {t("hplcMethod.analytes.cleaningTitle")}
          </Typography>
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 1.5 }}>
            {t("hplcMethod.analytes.cleaningSubtitle")}
          </Typography>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              size="small"
              label={t("hplcMethod.analytes.extractionVolLabel")}
              type="number"
              value={sampleSolutionVolumeMl}
              onChange={(e) => onSampleSolutionVolumeMlChange?.(e.target.value)}
              required
              placeholder={t("hplcMethod.analytes.extractionVolPlaceholder")}
              error={Boolean(errors["sampleSolutionVolumeMl"])}
              helperText={
                errors["sampleSolutionVolumeMl"] ?? t("hplcMethod.analytes.extractionVolHelper")
              }
              slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
              sx={{ maxWidth: 320 }}
            />
            <TextField
              size="small"
              label={t("hplcMethod.analytes.swabAreaLabel")}
              type="number"
              value={swabAreaCm2}
              onChange={(e) => onSwabAreaCm2Change?.(e.target.value)}
              placeholder={t("hplcMethod.analytes.swabAreaPlaceholder")}
              error={Boolean(errors["swabAreaCm2"])}
              helperText={
                errors["swabAreaCm2"] ?? t("hplcMethod.analytes.swabAreaHelper")
              }
              slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
              sx={{ maxWidth: 320 }}
            />
          </Stack>
        </Box>
      )}

      <Stack spacing={2}>
        {(isRelatedSubstances ? analytes.slice(0, 1) : analytes).map((a, idx) => {
          const err = (field: string) => errors[`analytes.${idx}.${field}`];
          const hasSstError = Object.keys(errors).some((k) => k.startsWith(`analytes.${idx}.sst`));
          const isSstExpanded = Boolean(expandedSstIndices[idx]) || hasSstError;
          const hasSstConfigured = Boolean(
            a.sstMaxRsdPercent !== "" ||
            a.sstMinResolution !== "" ||
            a.sstMaxTailingFactor !== "" ||
            a.sstMinTheoreticalPlates !== "" ||
            a.sstMinRetentionFactor !== "" ||
            a.sstMinSignalToNoise !== "" ||
            a.sstMinPeakToValley !== ""
          );

          return (
            <Paper
              key={idx}
              elevation={0}
              sx={{
                p: 2,
                border: "1px solid",
                borderColor: "divider",
                borderRadius: 1.5,
                bgcolor: "background.paper"
              }}
            >
              {/* Header row: Index/Order, Move up/down, Delete */}
              <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
                <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                  <ScienceOutlinedIcon fontSize="small" color="primary" />
                  <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                    {a.name.trim() ? a.name.trim() : isRelatedSubstances ? t("hplcMethod.analytes.principalPeak") : t("hplcMethod.analytes.analyteFallback", { index: idx + 1 })}
                  </Typography>
                  {a.id != null && (
                    <Typography variant="caption" sx={{ color: "text.disabled" }}>
                      {t("hplcMethod.analytes.idPrefix", { id: a.id })}
                    </Typography>
                  )}
                </Stack>

                {!isRelatedSubstances && (
                <Stack direction="row" spacing={0.5}>
                  <Tooltip title={t("hplcMethod.analytes.moveUp")}>
                    <span>
                      <IconButton
                        aria-label={t("hplcMethod.analytes.moveUp")}
                        size="small"
                        onClick={() => onMoveAnalyte(idx, "up")}
                        disabled={idx === 0}
                      >
                        <ArrowUpwardIcon fontSize="small" />
                      </IconButton>
                    </span>
                  </Tooltip>
                  <Tooltip title={t("hplcMethod.analytes.moveDown")}>
                    <span>
                      <IconButton
                        aria-label={t("hplcMethod.analytes.moveDown")}
                        size="small"
                        onClick={() => onMoveAnalyte(idx, "down")}
                        disabled={idx === analytes.length - 1}
                      >
                        <ArrowDownwardIcon fontSize="small" />
                      </IconButton>
                    </span>
                  </Tooltip>
                  <Tooltip title={t("hplcMethod.analytes.removeAnalyte")}>
                    <span>
                      <IconButton
                        aria-label={t("hplcMethod.analytes.removeAnalyte")}
                        size="small"
                        onClick={() => onRemoveAnalyte(idx)}
                        disabled={analytes.length <= 1}
                        color="error"
                      >
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </span>
                  </Tooltip>
                </Stack>
                )}
              </Stack>

              {/* Main Fields: Name, Wavelength (if HPLC), Reference Standard */}
              <Box
                sx={{
                  display: "grid",
                  gridTemplateColumns: isGc
                    ? { xs: "1fr", sm: "1.2fr 1.8fr" }
                    : { xs: "1fr", sm: "1fr 120px 1.5fr" },
                  gap: 2,
                  mb: 2
                }}
              >
                <TextField
                  size="small"
                  label={t("hplcMethod.analytes.nameLabel")}
                  placeholder={isRelatedSubstances ? t("hplcMethod.analytes.namePlaceholderRelated") : isResidual ? t("hplcMethod.analytes.namePlaceholderResidual") : t("hplcMethod.analytes.namePlaceholderDefault")}
                  value={a.name}
                  onChange={(e) => onAnalyteChange(idx, "name", e.target.value)}
                  required
                  error={Boolean(err("name"))}
                  helperText={err("name")}
                  fullWidth
                />
                {!isGc && (
                  <TextField
                    size="small"
                    label={t("hplcMethod.analytes.wavelengthLabel")}
                    type="number"
                    value={a.wavelengthNm}
                    onChange={(e) => onAnalyteChange(idx, "wavelengthNm", e.target.value)}
                    required
                    error={Boolean(err("wavelengthNm"))}
                    helperText={err("wavelengthNm") ?? t("hplcMethod.analytes.wavelengthHelper")}
                    slotProps={{ htmlInput: { min: 190, max: 900, step: "any" } }}
                    fullWidth
                  />
                )}
                <FormControl size="small" fullWidth required error={Boolean(err("standardEntryId"))}>
                  <InputLabel id={`analyte-std-label-${idx}`}>{t("hplcMethod.analytes.stdLabel")}</InputLabel>
                  <Select
                    labelId={`analyte-std-label-${idx}`}
                    label={t("hplcMethod.analytes.stdLabel")}
                    value={a.standardEntryId}
                    onChange={(e) => onAnalyteChange(idx, "standardEntryId", Number(e.target.value) || "")}
                  >
                    {availableStandards.length === 0 ? (
                      <MenuItem disabled value="">
                        <em>{t("hplcMethod.analytes.noStds")}</em>
                      </MenuItem>
                    ) : (
                      availableStandards.map((s) => (
                        <MenuItem key={s.id} value={s.id}>
                          {s.code} &middot; {s.name} {!s.isActive && t("hplcMethod.analytes.inactiveSuffix")}
                        </MenuItem>
                      ))
                    )}
                  </Select>
                  {err("standardEntryId") && <FormHelperText>{err("standardEntryId")}</FormHelperText>}
                </FormControl>
              </Box>

              {/* Mode-specific Fields */}
              {isRelatedSubstances ? (
                // Related Substances mode: Standard Injections only (no theoretical weights, no standard dilution)
                <Box
                  sx={{
                    display: "grid",
                    gridTemplateColumns: { xs: "1fr", sm: "1fr" },
                    maxWidth: 320,
                    gap: 2,
                    mb: 1.5
                  }}
                >
                  <TextField
                    size="small"
                    label={t("hplcMethod.analytes.stdInjectionsLabel")}
                    type="number"
                    value={a.standardInjections}
                    onChange={(e) => onAnalyteChange(idx, "standardInjections", e.target.value)}
                    required
                    error={Boolean(err("standardInjections"))}
                    helperText={err("standardInjections") ?? t("hplcMethod.analytes.stdInjectionsHelper")}
                    slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    fullWidth
                  />
                </Box>
              ) : isResidual ? (
                // Residual Solvents mode: Standard concentration & Injections
                <Box
                  sx={{
                    display: "grid",
                    gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" },
                    gap: 2,
                    mb: 1.5
                  }}
                >
                  <TextField
                    size="small"
                    label={t("hplcMethod.analytes.stdConcLabel")}
                    type="number"
                    value={a.standardConcentrationUgPerMl}
                    onChange={(e) =>
                      onAnalyteChange(idx, "standardConcentrationUgPerMl", e.target.value)
                    }
                    required
                    placeholder={t("hplcMethod.analytes.stdConcPlaceholderResidual")}
                    error={Boolean(err("standardConcentrationUgPerMl"))}
                    helperText={
                      err("standardConcentrationUgPerMl") ??
                      t("hplcMethod.analytes.stdConcHelper")
                    }
                    slotProps={{ htmlInput: { min: 0.0001, step: "any" } }}
                    fullWidth
                  />
                  <TextField
                    size="small"
                    label={t("hplcMethod.analytes.stdInjectionsLabel")}
                    type="number"
                    value={a.standardInjections}
                    onChange={(e) => onAnalyteChange(idx, "standardInjections", e.target.value)}
                    required
                    error={Boolean(err("standardInjections"))}
                    helperText={err("standardInjections") ?? t("hplcMethod.analytes.stdInjectionsHelper")}
                    slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    fullWidth
                  />
                </Box>
              ) : isCleaningResidue ? (
                // Cleaning Residue mode: Standard concentration, MACO limit + unit, and Standard Injections
                <Box
                  sx={{
                    display: "grid",
                    gridTemplateColumns: { xs: "1fr", sm: "1.2fr 1fr 1fr 1fr" },
                    gap: 2,
                    mb: 1.5
                  }}
                >
                  <TextField
                    size="small"
                    label={t("hplcMethod.analytes.stdConcLabel")}
                    type="number"
                    value={a.standardConcentrationUgPerMl}
                    onChange={(e) =>
                      onAnalyteChange(idx, "standardConcentrationUgPerMl", e.target.value)
                    }
                    required
                    placeholder={t("hplcMethod.analytes.stdConcPlaceholderCleaning")}
                    error={Boolean(err("standardConcentrationUgPerMl"))}
                    helperText={
                      err("standardConcentrationUgPerMl") ??
                      t("hplcMethod.analytes.stdConcHelper")
                    }
                    slotProps={{ htmlInput: { min: 0.0001, step: "any" } }}
                    fullWidth
                  />
                  <TextField
                    size="small"
                    label={t("hplcMethod.analytes.macoLimitLabel")}
                    type="number"
                    value={a.macoLimit}
                    onChange={(e) => onAnalyteChange(idx, "macoLimit", e.target.value)}
                    required
                    placeholder={t("hplcMethod.analytes.macoLimitPlaceholder")}
                    error={Boolean(err("macoLimit"))}
                    helperText={err("macoLimit") ?? t("hplcMethod.analytes.macoLimitHelper")}
                    slotProps={{ htmlInput: { min: 0.0001, step: "any" } }}
                    fullWidth
                  />
                  <FormControl size="small" fullWidth required error={Boolean(err("macoUnit"))}>
                    <InputLabel id={`analyte-maco-unit-label-${idx}`}>{t("hplcMethod.analytes.macoUnitLabel")}</InputLabel>
                    <Select
                      labelId={`analyte-maco-unit-label-${idx}`}
                      label={t("hplcMethod.analytes.macoUnitLabel")}
                      value={a.macoUnit || "UgPerSwab"}
                      onChange={(e) => onAnalyteChange(idx, "macoUnit", e.target.value)}
                    >
                      <MenuItem value="UgPerSwab">µg/swab</MenuItem>
                      <MenuItem value="UgPerCm2">µg/cm²</MenuItem>
                    </Select>
                    {err("macoUnit") && <FormHelperText>{err("macoUnit")}</FormHelperText>}
                  </FormControl>
                  <TextField
                    size="small"
                    label={t("hplcMethod.analytes.stdInjectionsLabel")}
                    type="number"
                    value={a.standardInjections}
                    onChange={(e) => onAnalyteChange(idx, "standardInjections", e.target.value)}
                    required
                    error={Boolean(err("standardInjections"))}
                    helperText={err("standardInjections") ?? t("hplcMethod.analytes.stdInjectionsHelper")}
                    slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    fullWidth
                  />
                </Box>
              ) : (
                // Assay mode (HPLC or GC Assay): Weights, standard dilution, and Injections
                <Box
                  sx={{
                    display: "grid",
                    gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr 1fr" },
                    gap: 2,
                    mb: 1.5
                  }}
                >
                  <TextField
                    size="small"
                    label={t("hplcMethod.analytes.thWtStdLabel")}
                    type="number"
                    value={a.theoreticalWeightStdMg}
                    onChange={(e) => onAnalyteChange(idx, "theoreticalWeightStdMg", e.target.value)}
                    required
                    error={Boolean(err("theoreticalWeightStdMg"))}
                    helperText={err("theoreticalWeightStdMg") ?? t("hplcMethod.analytes.thWtStdHelper")}
                    slotProps={{ htmlInput: { min: 0.0001, step: "any" } }}
                    fullWidth
                  />
                  <TextField
                    size="small"
                    label={t("hplcMethod.analytes.thWtTestLabel")}
                    type="number"
                    value={a.theoreticalWeightTestMg}
                    onChange={(e) => onAnalyteChange(idx, "theoreticalWeightTestMg", e.target.value)}
                    required
                    error={Boolean(err("theoreticalWeightTestMg"))}
                    helperText={err("theoreticalWeightTestMg") ?? t("hplcMethod.analytes.thWtTestHelper")}
                    slotProps={{ htmlInput: { min: 0.0001, step: "any" } }}
                    fullWidth
                  />
                  <TextField
                    size="small"
                    label={t("hplcMethod.analytes.stdDilutionLabel")}
                    type="number"
                    value={a.standardDilution}
                    onChange={(e) => onAnalyteChange(idx, "standardDilution", e.target.value)}
                    error={Boolean(err("standardDilution"))}
                    helperText={err("standardDilution") ?? t("hplcMethod.analytes.stdDilutionHelper")}
                    slotProps={{ htmlInput: { min: 0.0001, step: "any" } }}
                    fullWidth
                  />
                  <TextField
                    size="small"
                    label={t("hplcMethod.analytes.stdInjectionsLabel")}
                    type="number"
                    value={a.standardInjections}
                    onChange={(e) => onAnalyteChange(idx, "standardInjections", e.target.value)}
                    required
                    error={Boolean(err("standardInjections"))}
                    helperText={err("standardInjections") ?? t("hplcMethod.analytes.stdInjectionsHelper")}
                    slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    fullWidth
                  />
                </Box>
              )}

              {/* Toggle SST Acceptance Criteria */}
              <Button
                size="small"
                onClick={() => toggleSstExpand(idx)}
                disabled={hasSstError}
                endIcon={isSstExpanded ? <ExpandLessIcon /> : <ExpandMoreIcon />}
                sx={{ textTransform: "none", fontWeight: 600, px: 0.5 }}
              >
                {isSstExpanded ? t("hplcMethod.analytes.hideSst") : t("hplcMethod.analytes.showSst")}
                {hasSstConfigured && !isSstExpanded && t("hplcMethod.analytes.configuredSuffix")}
              </Button>

              <Collapse in={isSstExpanded}>
                <Box
                  sx={{
                    mt: 1.5,
                    p: 1.5,
                    bgcolor: "action.hover",
                    borderRadius: 1,
                    border: "1px dashed",
                    borderColor: "divider"
                  }}
                >
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 1.5 }}>
                    {t("hplcMethod.analytes.sstHelp")}
                  </Typography>
                  <Box
                    sx={{
                      display: "grid",
                      gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr 1fr" },
                      gap: 1.5
                    }}
                  >
                    <TextField
                      size="small"
                      label={t("hplcMethod.analytes.sstMaxRsd")}
                      type="number"
                      placeholder={t("hplcMethod.analytes.example", { value: "2.0" })}
                      value={a.sstMaxRsdPercent}
                      onChange={(e) => onAnalyteChange(idx, "sstMaxRsdPercent", e.target.value)}
                      error={Boolean(err("sstMaxRsdPercent"))}
                      helperText={err("sstMaxRsdPercent")}
                      slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                    />
                    <TextField
                      size="small"
                      label={t("hplcMethod.analytes.sstMinResolution")}
                      type="number"
                      placeholder={t("hplcMethod.analytes.example", { value: "1.5" })}
                      value={a.sstMinResolution}
                      onChange={(e) => onAnalyteChange(idx, "sstMinResolution", e.target.value)}
                      error={Boolean(err("sstMinResolution"))}
                      helperText={err("sstMinResolution")}
                      slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                    />
                    <TextField
                      size="small"
                      label={t("hplcMethod.analytes.sstMaxTailing")}
                      type="number"
                      placeholder={t("hplcMethod.analytes.example", { value: "2.0" })}
                      value={a.sstMaxTailingFactor}
                      onChange={(e) => onAnalyteChange(idx, "sstMaxTailingFactor", e.target.value)}
                      error={Boolean(err("sstMaxTailingFactor"))}
                      helperText={err("sstMaxTailingFactor")}
                      slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                    />
                    <TextField
                      size="small"
                      label={t("hplcMethod.analytes.sstMinPlates")}
                      type="number"
                      placeholder={t("hplcMethod.analytes.example", { value: "2000" })}
                      value={a.sstMinTheoreticalPlates}
                      onChange={(e) => onAnalyteChange(idx, "sstMinTheoreticalPlates", e.target.value)}
                      error={Boolean(err("sstMinTheoreticalPlates"))}
                      helperText={err("sstMinTheoreticalPlates")}
                      slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    />
                    <TextField
                      size="small"
                      label={t("hplcMethod.analytes.sstMinRetention")}
                      type="number"
                      placeholder={t("hplcMethod.analytes.example", { value: "2.0" })}
                      value={a.sstMinRetentionFactor}
                      onChange={(e) => onAnalyteChange(idx, "sstMinRetentionFactor", e.target.value)}
                      error={Boolean(err("sstMinRetentionFactor"))}
                      helperText={err("sstMinRetentionFactor")}
                      slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                    />
                    <TextField
                      size="small"
                      label={t("hplcMethod.analytes.sstMinSn")}
                      type="number"
                      placeholder={t("hplcMethod.analytes.example", { value: "10" })}
                      value={a.sstMinSignalToNoise}
                      onChange={(e) => onAnalyteChange(idx, "sstMinSignalToNoise", e.target.value)}
                      error={Boolean(err("sstMinSignalToNoise"))}
                      helperText={err("sstMinSignalToNoise")}
                      slotProps={{ htmlInput: { min: 1, step: "any" } }}
                    />
                    <TextField
                      size="small"
                      label={t("hplcMethod.analytes.sstMinPv")}
                      type="number"
                      placeholder={t("hplcMethod.analytes.example", { value: "1.2" })}
                      value={a.sstMinPeakToValley}
                      onChange={(e) => onAnalyteChange(idx, "sstMinPeakToValley", e.target.value)}
                      error={Boolean(err("sstMinPeakToValley"))}
                      helperText={err("sstMinPeakToValley")}
                      slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                    />
                  </Box>
                </Box>
              </Collapse>
            </Paper>
          );
        })}
      </Stack>
    </Box>
  );
}
