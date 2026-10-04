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

export interface HplcAnalytesSectionProps {
  technique?: HplcTechnique;
  resultMode?: HplcResultMode;
  sampleSolutionVolumeMl?: string | number;
  onSampleSolutionVolumeMlChange?: (val: string) => void;
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
  analytes,
  availableStandards,
  errors,
  onAnalyteChange,
  onAddAnalyte,
  onRemoveAnalyte,
  onMoveAnalyte
}: HplcAnalytesSectionProps) {
  const isGc = technique === "Gc";
  const isResidual = isGc && resultMode === "ResidualSolvents";
  const [expandedSstIndices, setExpandedSstIndices] = useState<Record<number, boolean>>({});

  const toggleSstExpand = (index: number) => {
    setExpandedSstIndices((prev) => ({ ...prev, [index]: !prev[index] }));
  };

  return (
    <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
        <div>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5, color: "text.primary" }}>
            {isResidual
              ? "Residual Solvents Analytes & SST Acceptance Criteria (USP <467>)"
              : "Method Analytes & System Suitability Criteria"}
          </Typography>
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {isResidual
              ? "Specify reference standards, calibration standard concentrations (µg/mL), injections, and SST limits for volatile solvent impurities."
              : isGc
              ? "Define active analytes, reference standards, theoretical weights, and SST criteria."
              : "Define active analytes, detection wavelengths, reference standards, theoretical weights, and SST criteria."}
          </Typography>
        </div>
        <Button
          size="small"
          variant="contained"
          startIcon={<AddIcon />}
          onClick={onAddAnalyte}
          sx={{ textTransform: "none", fontWeight: 600 }}
        >
          Add Analyte
        </Button>
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
            Sample Solution Volume (USP &lt;467&gt;)
          </Typography>
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 1.5 }}>
            Method-level test solution volume (mL) used for dissolving the drug substance/product sample.
          </Typography>
          <TextField
            size="small"
            label="Sample Solution Volume (mL) *"
            type="number"
            value={sampleSolutionVolumeMl}
            onChange={(e) => onSampleSolutionVolumeMlChange?.(e.target.value)}
            required
            placeholder="e.g. 5.0"
            error={Boolean(errors["sampleSolutionVolumeMl"])}
            helperText={
              errors["sampleSolutionVolumeMl"] ?? "Volume of diluent in mL used to prepare test sample"
            }
            slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
            sx={{ maxWidth: 320 }}
          />
        </Box>
      )}

      <Stack spacing={2}>
        {analytes.map((a, idx) => {
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
                    {a.name.trim() ? a.name.trim() : `Analyte #${idx + 1}`}
                  </Typography>
                  {a.id != null && (
                    <Typography variant="caption" sx={{ color: "text.disabled" }}>
                      (ID: {a.id})
                    </Typography>
                  )}
                </Stack>

                <Stack direction="row" spacing={0.5}>
                  <Tooltip title="Move up">
                    <span>
                      <IconButton
                        aria-label="Move up"
                        size="small"
                        onClick={() => onMoveAnalyte(idx, "up")}
                        disabled={idx === 0}
                      >
                        <ArrowUpwardIcon fontSize="small" />
                      </IconButton>
                    </span>
                  </Tooltip>
                  <Tooltip title="Move down">
                    <span>
                      <IconButton
                        aria-label="Move down"
                        size="small"
                        onClick={() => onMoveAnalyte(idx, "down")}
                        disabled={idx === analytes.length - 1}
                      >
                        <ArrowDownwardIcon fontSize="small" />
                      </IconButton>
                    </span>
                  </Tooltip>
                  <Tooltip title="Remove analyte">
                    <span>
                      <IconButton
                        aria-label="Remove analyte"
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
                  label="Analyte Name *"
                  placeholder={isResidual ? "e.g. Methanol, Acetone, DCM" : "e.g. Paracetamol, Caffeine"}
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
                    label="λ (nm) *"
                    type="number"
                    value={a.wavelengthNm}
                    onChange={(e) => onAnalyteChange(idx, "wavelengthNm", e.target.value)}
                    required
                    error={Boolean(err("wavelengthNm"))}
                    helperText={err("wavelengthNm") ?? "190–900"}
                    slotProps={{ htmlInput: { min: 190, max: 900, step: "any" } }}
                    fullWidth
                  />
                )}
                <FormControl size="small" fullWidth required error={Boolean(err("standardEntryId"))}>
                  <InputLabel id={`analyte-std-label-${idx}`}>Reference Standard *</InputLabel>
                  <Select
                    labelId={`analyte-std-label-${idx}`}
                    label="Reference Standard *"
                    value={a.standardEntryId}
                    onChange={(e) => onAnalyteChange(idx, "standardEntryId", Number(e.target.value) || "")}
                  >
                    {availableStandards.length === 0 ? (
                      <MenuItem disabled value="">
                        <em>No reference standards available in this section</em>
                      </MenuItem>
                    ) : (
                      availableStandards.map((s) => (
                        <MenuItem key={s.id} value={s.id}>
                          {s.code} &middot; {s.name} {!s.isActive && "(Inactive)"}
                        </MenuItem>
                      ))
                    )}
                  </Select>
                  {err("standardEntryId") && <FormHelperText>{err("standardEntryId")}</FormHelperText>}
                </FormControl>
              </Box>

              {/* Mode-specific Fields */}
              {isResidual ? (
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
                    label="Standard Concentration (µg/mL) *"
                    type="number"
                    value={a.standardConcentrationUgPerMl}
                    onChange={(e) =>
                      onAnalyteChange(idx, "standardConcentrationUgPerMl", e.target.value)
                    }
                    required
                    placeholder="e.g. 50.0"
                    error={Boolean(err("standardConcentrationUgPerMl"))}
                    helperText={
                      err("standardConcentrationUgPerMl") ??
                      "Standard concentration in µg/mL (C_std)"
                    }
                    slotProps={{ htmlInput: { min: 0.0001, step: "any" } }}
                    fullWidth
                  />
                  <TextField
                    size="small"
                    label="Standard Injections *"
                    type="number"
                    value={a.standardInjections}
                    onChange={(e) => onAnalyteChange(idx, "standardInjections", e.target.value)}
                    required
                    error={Boolean(err("standardInjections"))}
                    helperText={err("standardInjections") ?? "e.g. 5 or 6 replicate injections"}
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
                    label="Th. Wt. Std (mg) *"
                    type="number"
                    value={a.theoreticalWeightStdMg}
                    onChange={(e) => onAnalyteChange(idx, "theoreticalWeightStdMg", e.target.value)}
                    required
                    error={Boolean(err("theoreticalWeightStdMg"))}
                    helperText={err("theoreticalWeightStdMg") ?? "Theoretical standard weight in mg"}
                    slotProps={{ htmlInput: { min: 0.0001, step: "any" } }}
                    fullWidth
                  />
                  <TextField
                    size="small"
                    label="Th. Wt. Test (mg) *"
                    type="number"
                    value={a.theoreticalWeightTestMg}
                    onChange={(e) => onAnalyteChange(idx, "theoreticalWeightTestMg", e.target.value)}
                    required
                    error={Boolean(err("theoreticalWeightTestMg"))}
                    helperText={err("theoreticalWeightTestMg") ?? "Theoretical sample weight in mg"}
                    slotProps={{ htmlInput: { min: 0.0001, step: "any" } }}
                    fullWidth
                  />
                  <TextField
                    size="small"
                    label="Standard Dilution (mL)"
                    type="number"
                    value={a.standardDilution}
                    onChange={(e) => onAnalyteChange(idx, "standardDilution", e.target.value)}
                    error={Boolean(err("standardDilution"))}
                    helperText={err("standardDilution") ?? "Volume standard is diluted to"}
                    slotProps={{ htmlInput: { min: 0.0001, step: "any" } }}
                    fullWidth
                  />
                  <TextField
                    size="small"
                    label="Standard Injections *"
                    type="number"
                    value={a.standardInjections}
                    onChange={(e) => onAnalyteChange(idx, "standardInjections", e.target.value)}
                    required
                    error={Boolean(err("standardInjections"))}
                    helperText={err("standardInjections") ?? "e.g. 5 or 6 replicate injections"}
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
                {isSstExpanded ? "Hide SST Acceptance Criteria" : "System Suitability Acceptance Criteria (Optional)"}
                {hasSstConfigured && !isSstExpanded && " (Configured)"}
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
                    Criteria checked during system suitability runs. Blank fields are not enforced.
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
                      label="Max RSD (%)"
                      type="number"
                      placeholder="e.g. 2.0"
                      value={a.sstMaxRsdPercent}
                      onChange={(e) => onAnalyteChange(idx, "sstMaxRsdPercent", e.target.value)}
                      error={Boolean(err("sstMaxRsdPercent"))}
                      helperText={err("sstMaxRsdPercent")}
                      slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                    />
                    <TextField
                      size="small"
                      label="Min Resolution"
                      type="number"
                      placeholder="e.g. 1.5"
                      value={a.sstMinResolution}
                      onChange={(e) => onAnalyteChange(idx, "sstMinResolution", e.target.value)}
                      error={Boolean(err("sstMinResolution"))}
                      helperText={err("sstMinResolution")}
                      slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                    />
                    <TextField
                      size="small"
                      label="Max Tailing Factor"
                      type="number"
                      placeholder="e.g. 2.0"
                      value={a.sstMaxTailingFactor}
                      onChange={(e) => onAnalyteChange(idx, "sstMaxTailingFactor", e.target.value)}
                      error={Boolean(err("sstMaxTailingFactor"))}
                      helperText={err("sstMaxTailingFactor")}
                      slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                    />
                    <TextField
                      size="small"
                      label="Min Theoretical Plates"
                      type="number"
                      placeholder="e.g. 2000"
                      value={a.sstMinTheoreticalPlates}
                      onChange={(e) => onAnalyteChange(idx, "sstMinTheoreticalPlates", e.target.value)}
                      error={Boolean(err("sstMinTheoreticalPlates"))}
                      helperText={err("sstMinTheoreticalPlates")}
                      slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    />
                    <TextField
                      size="small"
                      label="Min Retention Factor (k')"
                      type="number"
                      placeholder="e.g. 2.0"
                      value={a.sstMinRetentionFactor}
                      onChange={(e) => onAnalyteChange(idx, "sstMinRetentionFactor", e.target.value)}
                      error={Boolean(err("sstMinRetentionFactor"))}
                      helperText={err("sstMinRetentionFactor")}
                      slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                    />
                    <TextField
                      size="small"
                      label="Min Signal-to-Noise"
                      type="number"
                      placeholder="e.g. 10"
                      value={a.sstMinSignalToNoise}
                      onChange={(e) => onAnalyteChange(idx, "sstMinSignalToNoise", e.target.value)}
                      error={Boolean(err("sstMinSignalToNoise"))}
                      helperText={err("sstMinSignalToNoise")}
                      slotProps={{ htmlInput: { min: 1, step: "any" } }}
                    />
                    <TextField
                      size="small"
                      label="Min Peak-to-Valley"
                      type="number"
                      placeholder="e.g. 1.2"
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
