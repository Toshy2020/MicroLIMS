import { useMemo } from "react";
import {
  Box,
  Paper,
  Typography,
  Grid,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Stack,
  Chip,
  Divider,
  Alert,
  useTheme
} from "@mui/material";
import ScienceIcon from "@mui/icons-material/Science";
import type { HplcSstAnalyteDto, SaveSstAnalyteInput } from "../types";
import type { HplcMethodAnalyteResponse } from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";

export interface StandardMaterialOption {
  id: number;
  materialName: string;
  batchNumber: string;
  purity?: number | null;
  moisturePercent?: number | null;
  materialMasterEntryId?: number | null;
  materialMasterEntryCode?: string | null;
}

export interface StandardEntryFormProps {
  analyte: HplcSstAnalyteDto;
  methodAnalyte?: HplcMethodAnalyteResponse | null;
  standardLots: StandardMaterialOption[];
  formValue: SaveSstAnalyteInput;
  onChange: (patch: Partial<SaveSstAnalyteInput>) => void;
  disabled?: boolean;
  // Outline blank required fields red after a save attempt found gaps.
  showMissing?: boolean;
}

export function StandardEntryForm({
  analyte,
  methodAnalyte,
  standardLots,
  formValue,
  onChange,
  disabled = false,
  showMissing = false
}: StandardEntryFormProps) {
  const theme = useTheme();

  // Filter options per analyte to lots whose materialMasterEntryId === analyte.standardEntryId
  const matchingLots = useMemo(() => {
    return standardLots.filter((lot) => lot.materialMasterEntryId === analyte.standardEntryId);
  }, [standardLots, analyte.standardEntryId]);

  // Keep a previously saved lot visible even if not in current usable matching lots
  const selectableLots = useMemo(() => {
    const list = [...matchingLots];
    if (analyte.standardMaterialId && !list.some((l) => l.id === analyte.standardMaterialId)) {
      const foundInAll = standardLots.find((l) => l.id === analyte.standardMaterialId);
      if (foundInAll) {
        list.unshift(foundInAll);
      } else if (analyte.standardMaterialBatch) {
        list.unshift({
          id: analyte.standardMaterialId,
          materialName: analyte.analyteName,
          batchNumber: analyte.standardMaterialBatch,
          purity: analyte.standardPurityPercent,
          moisturePercent: analyte.standardMoisturePercent,
          materialMasterEntryId: analyte.standardEntryId
        });
      }
    }
    return list;
  }, [matchingLots, analyte, standardLots]);

  // Selected lot details
  const selectedLot = useMemo(() => {
    return selectableLots.find((l) => l.id === formValue.standardMaterialId);
  }, [selectableLots, formValue.standardMaterialId]);

  // Target injection count from method (defaults to 5 if not configured)
  const injectionCount = methodAnalyte?.standardInjections ?? 5;

  // Responses array
  const responses = useMemo(() => formValue.responses ?? [], [formValue.responses]);

  const handleResponseChange = (idx: number, rawVal: string) => {
    const val = rawVal === "" ? 0 : Number(rawVal);
    const next = [...responses];
    // Ensure array is sized to injectionCount
    while (next.length < injectionCount) next.push(0);
    next[idx] = val;
    onChange({ responses: next });
  };

  const handleNumChange = (field: keyof SaveSstAnalyteInput, rawVal: string) => {
    const val = rawVal.trim() === "" ? null : Number(rawVal);
    onChange({ [field]: val } as unknown as Partial<SaveSstAnalyteInput>);
  };

  return (
    <Paper
      elevation={0}
      sx={{
        p: 2.5,
        borderRadius: 2,
        border: `1px solid ${theme.palette.divider}`,
        backgroundColor: theme.palette.background.paper
      }}
    >
      {/* Header */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <ScienceIcon color="primary" />
          <Typography variant="h6" sx={{ fontWeight: 700, fontSize: 16 }}>
            Analyte: {analyte.analyteName}
          </Typography>
          {methodAnalyte && (
            <Chip
              size="small"
              label={`${methodAnalyte.wavelengthNm} nm`}
              variant="outlined"
              sx={{ fontWeight: 600 }}
            />
          )}
        </Box>
      </Box>

      <Divider sx={{ mb: 2.5 }} />

      <Stack spacing={3}>
        {/* Row 1: Reference Standard Lot & Weights */}
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, md: 6 }}>
            <FormControl fullWidth size="small" required disabled={disabled} error={showMissing && !formValue.standardMaterialId}>
              <InputLabel id={`std-lot-label-${analyte.id}`}>Reference Standard Lot</InputLabel>
              <Select
                labelId={`std-lot-label-${analyte.id}`}
                label="Reference Standard Lot"
                value={formValue.standardMaterialId || ""}
                onChange={(e) => onChange({ standardMaterialId: Number(e.target.value) })}
              >
                {selectableLots.map((lot) => (
                  <MenuItem key={lot.id} value={lot.id}>
                    {lot.materialName}, Lot {lot.batchNumber} (Purity: {lot.purity ?? "—"}% | MC: {lot.moisturePercent ?? "—"}%)
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            {matchingLots.length === 0 && (
              <Alert severity="info" sx={{ mt: 1, py: 0.5, px: 1.5, fontSize: 13 }}>
                No usable lot of this analyte's reference standard in stock.
              </Alert>
            )}

            {selectedLot ? (
              <Box sx={{ mt: 1, p: 1, borderRadius: 1, backgroundColor: theme.palette.action.hover }}>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  <strong>Purity (P):</strong> {selectedLot.purity ?? "—"}% ·{" "}
                  <strong>Moisture Content (MC):</strong> {selectedLot.moisturePercent ?? "—"}%
                </Typography>
              </Box>
            ) : (
              analyte.standardMaterialBatch && (
                <Box sx={{ mt: 1, p: 1, borderRadius: 1, backgroundColor: theme.palette.action.hover }}>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                    Previously saved: Lot {analyte.standardMaterialBatch} (P: {analyte.standardPurityPercent ?? "—"}% | MC: {analyte.standardMoisturePercent ?? "—"}%)
                  </Typography>
                </Box>
              )
            )}
          </Grid>

          <Grid size={{ xs: 12, sm: 6, md: 3 }}>
            <TextField
              fullWidth
              size="small"
              label="Theoretical Std Wt (mg)"
              value={methodAnalyte?.theoreticalWeightStdMg ?? "—"}
              disabled
              helperText="Method constant (read-only)"
            />
          </Grid>

          <Grid size={{ xs: 12, sm: 6, md: 3 }}>
            <TextField
              fullWidth
              size="small"
              label="Actual Std Weight (mg)"
              type="number"
              required
              disabled={disabled}
              error={showMissing && !(formValue.standardWeightMg > 0)}
              value={formValue.standardWeightMg === 0 ? "" : formValue.standardWeightMg}
              onChange={(e) => onChange({ standardWeightMg: Number(e.target.value) })}
              helperText="Weighed on analytical balance"
            />
          </Grid>
        </Grid>

        {/* Row 2: Standard Injections Responses */}
        <Box>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              Standard Injections (n = {injectionCount}):
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              Mean and %RSD are calculated on save (see the values table).
            </Typography>
          </Box>

          <Grid container spacing={1.5}>
            {Array.from({ length: injectionCount }).map((_, i) => (
              <Grid size={{ xs: 6, sm: 4, md: 2.4 }} key={i}>
                <TextField
                  fullWidth
                  size="small"
                  label={`Injection #${i + 1}`}
                  type="number"
                  disabled={disabled}
                  error={showMissing && !((responses[i] ?? 0) > 0)}
                  value={responses[i] !== undefined && responses[i] !== 0 ? responses[i] : ""}
                  onChange={(e) => handleResponseChange(i, e.target.value)}
                  placeholder="Peak area"
                />
              </Grid>
            ))}
          </Grid>
        </Box>

        {/* Row 3: Transcribed CDS Report Criteria Values */}
        <Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
            Transcribed CDS Suitability Values:
          </Typography>

          <Grid container spacing={2}>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <TextField
                fullWidth
                size="small"
                label="Reported %RSD"
                type="number"
                disabled={disabled}
                value={formValue.reportedRsdPercent ?? ""}
                onChange={(e) => handleNumChange("reportedRsdPercent", e.target.value)}
                helperText={methodAnalyte?.sstMaxRsdPercent != null ? `Criterion: ≤ ${methodAnalyte.sstMaxRsdPercent}%` : undefined}
              />
            </Grid>

            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <TextField
                fullWidth
                size="small"
                label="Resolution"
                type="number"
                disabled={disabled}
                error={showMissing && methodAnalyte?.sstMinResolution != null && formValue.resolution == null}
                value={formValue.resolution ?? ""}
                onChange={(e) => handleNumChange("resolution", e.target.value)}
                helperText={methodAnalyte?.sstMinResolution != null ? `Criterion: ≥ ${methodAnalyte.sstMinResolution}` : undefined}
              />
            </Grid>

            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <TextField
                fullWidth
                size="small"
                label="Tailing Factor"
                type="number"
                disabled={disabled}
                error={showMissing && methodAnalyte?.sstMaxTailingFactor != null && formValue.tailingFactor == null}
                value={formValue.tailingFactor ?? ""}
                onChange={(e) => handleNumChange("tailingFactor", e.target.value)}
                helperText={methodAnalyte?.sstMaxTailingFactor != null ? `Criterion: ≤ ${methodAnalyte.sstMaxTailingFactor}` : undefined}
              />
            </Grid>

            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <TextField
                fullWidth
                size="small"
                label="Theoretical Plates"
                type="number"
                disabled={disabled}
                error={showMissing && methodAnalyte?.sstMinTheoreticalPlates != null && formValue.theoreticalPlates == null}
                value={formValue.theoreticalPlates ?? ""}
                onChange={(e) => handleNumChange("theoreticalPlates", e.target.value)}
                helperText={methodAnalyte?.sstMinTheoreticalPlates != null ? `Criterion: ≥ ${methodAnalyte.sstMinTheoreticalPlates}` : undefined}
              />
            </Grid>

            {/* Optional criteria if method or value has them */}
            {(methodAnalyte?.sstMinRetentionFactor != null || formValue.retentionFactor != null) && (
              <Grid size={{ xs: 12, sm: 6, md: 4 }}>
                <TextField
                  fullWidth
                  size="small"
                  label="Retention Factor (k')"
                  type="number"
                  disabled={disabled}
                  error={showMissing && methodAnalyte?.sstMinRetentionFactor != null && formValue.retentionFactor == null}
                  value={formValue.retentionFactor ?? ""}
                  onChange={(e) => handleNumChange("retentionFactor", e.target.value)}
                  helperText={methodAnalyte?.sstMinRetentionFactor != null ? `Criterion: ≥ ${methodAnalyte.sstMinRetentionFactor}` : undefined}
                />
              </Grid>
            )}

            {(methodAnalyte?.sstMinSignalToNoise != null || formValue.signalToNoise != null) && (
              <Grid size={{ xs: 12, sm: 6, md: 4 }}>
                <TextField
                  fullWidth
                  size="small"
                  label="Signal-to-Noise (S/N)"
                  type="number"
                  disabled={disabled}
                  error={showMissing && methodAnalyte?.sstMinSignalToNoise != null && formValue.signalToNoise == null}
                  value={formValue.signalToNoise ?? ""}
                  onChange={(e) => handleNumChange("signalToNoise", e.target.value)}
                  helperText={methodAnalyte?.sstMinSignalToNoise != null ? `Criterion: ≥ ${methodAnalyte.sstMinSignalToNoise}` : undefined}
                />
              </Grid>
            )}

            {(methodAnalyte?.sstMinPeakToValley != null || formValue.peakToValley != null) && (
              <Grid size={{ xs: 12, sm: 6, md: 4 }}>
                <TextField
                  fullWidth
                  size="small"
                  label="Peak-to-Valley (P/V)"
                  type="number"
                  disabled={disabled}
                  error={showMissing && methodAnalyte?.sstMinPeakToValley != null && formValue.peakToValley == null}
                  value={formValue.peakToValley ?? ""}
                  onChange={(e) => handleNumChange("peakToValley", e.target.value)}
                  helperText={methodAnalyte?.sstMinPeakToValley != null ? `Criterion: ≥ ${methodAnalyte.sstMinPeakToValley}` : undefined}
                />
              </Grid>
            )}
          </Grid>
        </Box>
      </Stack>
    </Paper>
  );
}
