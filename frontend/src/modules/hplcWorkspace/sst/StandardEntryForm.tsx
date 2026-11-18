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
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import type { TFunction } from "i18next";
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
  materialType?: string | null;
  code?: string | null;
}

// A working standard lot is named by its WS code; its batch number is the
// source raw-material batch.
const lotLabel = (lot: StandardMaterialOption, t: TFunction<"hplcWorkspace">) =>
  lot.materialType === "WorkingStandard" && lot.code
    ? t("sst.entryForm.lotWorkingStandard", { code: lot.code })
    : t("sst.entryForm.lotBatch", { lotNumber: lot.batchNumber });

export interface StandardEntryFormProps {
  analyte: HplcSstAnalyteDto;
  methodAnalyte?: HplcMethodAnalyteResponse | null;
  standardLots: StandardMaterialOption[];
  formValue: SaveSstAnalyteInput;
  onChange: (patch: Partial<SaveSstAnalyteInput>) => void;
  disabled?: boolean;
  // Outline blank required fields red after a save attempt found gaps.
  showMissing?: boolean;
  isResidualSolvents?: boolean;
  // Only an assay weighs its standard; the server stores no weight for related-substances and residual-solvent methods.
  standardIsWeighed?: boolean;
  hasInternalStandard?: boolean;
  internalStandardName?: string | null;
}

export function StandardEntryForm({
  analyte,
  methodAnalyte,
  standardLots,
  formValue,
  onChange,
  disabled = false,
  showMissing = false,
  isResidualSolvents = false,
  standardIsWeighed = true,
  hasInternalStandard = false,
  internalStandardName
}: StandardEntryFormProps) {
  const { t } = useTranslation("hplcWorkspace");
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

  // Internal standard responses array
  const isResponses = useMemo(
    () => formValue.internalStandardResponses ?? [],
    [formValue.internalStandardResponses]
  );

  const handleResponseChange = (idx: number, rawVal: string) => {
    const val = rawVal === "" ? 0 : Number(rawVal);
    const next = [...responses];
    // Ensure array is sized to injectionCount
    while (next.length < injectionCount) next.push(0);
    next[idx] = val;
    onChange({ responses: next });
  };

  const handleIsResponseChange = (idx: number, rawVal: string) => {
    const val = rawVal === "" ? 0 : Number(rawVal);
    const next = [...isResponses];
    while (next.length < injectionCount) next.push(0);
    next[idx] = val;
    onChange({ internalStandardResponses: next });
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
            {t("sst.entryForm.analyteTitle", { name: analyte.analyteName })}
          </Typography>
          {methodAnalyte && methodAnalyte.wavelengthNm != null && (
            <Chip
              size="small"
              label={<Ltr>{`${methodAnalyte.wavelengthNm} nm`}</Ltr>}
              variant="outlined"
              sx={{ fontWeight: 600 }}
            />
          )}
          {hasInternalStandard && (
            <Chip
              size="small"
              label={internalStandardName ? t("sst.entryForm.isChip", { name: internalStandardName }) : t("sst.entryForm.isChipFallback")}
              color="primary"
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
              <InputLabel id={`std-lot-label-${analyte.id}`}>{t("sst.entryForm.refStandardLot")}</InputLabel>
              <Select
                labelId={`std-lot-label-${analyte.id}`}
                label={t("sst.entryForm.refStandardLot")}
                value={formValue.standardMaterialId || ""}
                onChange={(e) => onChange({ standardMaterialId: Number(e.target.value) })}
              >
                {selectableLots.map((lot) => (
                  <MenuItem key={lot.id} value={lot.id}>
                    {isResidualSolvents
                      ? t("sst.entryForm.lotOptionResidual", { name: lot.materialName, lot: lotLabel(lot, t) })
                      : t("sst.entryForm.lotOptionFull", {
                          name: lot.materialName,
                          lot: lotLabel(lot, t),
                          purity: lot.purity ?? "—",
                          mc: lot.moisturePercent ?? "—"
                        })}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            {matchingLots.length === 0 && (
              <Alert severity="info" sx={{ mt: 1, py: 0.5, px: 1.5, fontSize: 13 }}>
                {t("sst.entryForm.noUsableLot")}
              </Alert>
            )}

            {selectedLot ? (
              <Box sx={{ mt: 1, p: 1, borderRadius: 1, backgroundColor: theme.palette.action.hover }}>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  {isResidualSolvents ? (
                    <>
                      <strong>{t("sst.entryForm.lotPrefix")}</strong> {lotLabel(selectedLot, t)}
                      {selectedLot.purity != null && t("sst.entryForm.purityShort", { purity: selectedLot.purity })}
                      {selectedLot.moisturePercent != null && t("sst.entryForm.mcShort", { mc: selectedLot.moisturePercent })}
                    </>
                  ) : (
                    <>
                      <strong>{t("sst.entryForm.purityFull")}</strong> <Ltr>{selectedLot.purity ?? "—"}%</Ltr> ·{" "}
                      <strong>{t("sst.entryForm.mcFull")}</strong> <Ltr>{selectedLot.moisturePercent ?? "—"}%</Ltr>
                    </>
                  )}
                </Typography>
              </Box>
            ) : (
              analyte.standardMaterialBatch && (
                <Box sx={{ mt: 1, p: 1, borderRadius: 1, backgroundColor: theme.palette.action.hover }}>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                    {t("sst.entryForm.previouslySaved", {
                      lotNumber: analyte.standardMaterialBatch,
                      purity: analyte.standardPurityPercent ?? "—",
                      mc: analyte.standardMoisturePercent ?? "—"
                    })}
                  </Typography>
                </Box>
              )
            )}
          </Grid>

          <Grid size={{ xs: 12, sm: 6, md: 3 }}>
            {isResidualSolvents ? (
              <TextField
                fullWidth
                size="small"
                label={t("sst.entryForm.stdConcLabel")}
                value={methodAnalyte?.standardConcentrationUgPerMl ?? "—"}
                disabled
                helperText={t("sst.entryForm.stdConcHelper")}
              />
            ) : (
              <TextField
                fullWidth
                size="small"
                label={t("sst.entryForm.thStdWtLabel")}
                value={methodAnalyte?.theoreticalWeightStdMg ?? "—"}
                disabled
                helperText={t("sst.entryForm.thStdWtHelper")}
              />
            )}
          </Grid>

          <Grid size={{ xs: 12, sm: 6, md: 3 }}>
            <TextField
              fullWidth
              size="small"
              label={t("sst.entryForm.actualStdWtLabel")}
              type="number"
              required={standardIsWeighed}
              disabled={disabled}
              error={showMissing && standardIsWeighed && !(formValue.standardWeightMg > 0)}
              value={formValue.standardWeightMg === 0 ? "" : formValue.standardWeightMg}
              onChange={(e) => onChange({ standardWeightMg: e.target.value === "" ? 0 : Number(e.target.value) })}
              helperText={standardIsWeighed ? t("sst.entryForm.actualStdWtWeighed") : t("sst.entryForm.actualStdWtOptional")}
            />
          </Grid>
        </Grid>

        {/* Row 2: Standard Injections Responses */}
        <Box>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              {t("sst.entryForm.standardInjections", { count: injectionCount })}
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {t("sst.entryForm.rsdHint")}
            </Typography>
          </Box>

          <Grid container spacing={1.5}>
            {Array.from({ length: injectionCount }).map((_, i) =>
              hasInternalStandard ? (
                <Grid size={{ xs: 12, sm: 6, md: 4 }} key={i}>
                  <Box sx={{ p: 1.5, border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
                    <Typography variant="caption" sx={{ fontWeight: 700, mb: 1, display: "block" }}>
                      {t("sst.entryForm.injectionNumber", { num: i + 1 })}
                    </Typography>
                    <Stack spacing={1}>
                      <TextField
                        fullWidth
                        size="small"
                        label={t("sst.entryForm.analyteArea")}
                        type="number"
                        disabled={disabled}
                        error={showMissing && !((responses[i] ?? 0) > 0)}
                        value={responses[i] !== undefined && responses[i] !== 0 ? responses[i] : ""}
                        onChange={(e) => handleResponseChange(i, e.target.value)}
                        placeholder={t("sst.entryForm.analyteArea")}
                        slotProps={{
                          htmlInput: {
                            min: 0,
                            step: "any",
                            "aria-label": t("sst.entryForm.analyteAreaAria", { num: i + 1 })
                          }
                        }}
                      />
                      <TextField
                        fullWidth
                        size="small"
                        label={t("sst.entryForm.isArea")}
                        type="number"
                        disabled={disabled}
                        error={showMissing && !((isResponses[i] ?? 0) > 0)}
                        value={isResponses[i] !== undefined && isResponses[i] !== 0 ? isResponses[i] : ""}
                        onChange={(e) => handleIsResponseChange(i, e.target.value)}
                        placeholder={t("sst.entryForm.isArea")}
                        slotProps={{
                          htmlInput: {
                            min: 0,
                            step: "any",
                            "aria-label": t("sst.entryForm.isAreaAria", { num: i + 1 })
                          }
                        }}
                      />
                    </Stack>
                  </Box>
                </Grid>
              ) : (
                <Grid size={{ xs: 6, sm: 4, md: 2.4 }} key={i}>
                  <TextField
                    fullWidth
                    size="small"
                    label={t("sst.entryForm.injectionNumber", { num: i + 1 })}
                    type="number"
                    disabled={disabled}
                    error={showMissing && !((responses[i] ?? 0) > 0)}
                    value={responses[i] !== undefined && responses[i] !== 0 ? responses[i] : ""}
                    onChange={(e) => handleResponseChange(i, e.target.value)}
                    placeholder={t("sst.entryForm.peakArea")}
                    slotProps={{
                      htmlInput: {
                        min: 0,
                        step: "any",
                        "aria-label": t("sst.entryForm.injectionNumber", { num: i + 1 })
                      }
                    }}
                  />
                </Grid>
              )
            )}
          </Grid>
        </Box>

        {/* Row 3: Transcribed CDS Report Criteria Values */}
        <Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
            {t("sst.entryForm.transcribedTitle")}
          </Typography>

          <Grid container spacing={2}>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <TextField
                fullWidth
                size="small"
                label={t("sst.entryForm.reportedRsd")}
                type="number"
                disabled={disabled}
                value={formValue.reportedRsdPercent ?? ""}
                onChange={(e) => handleNumChange("reportedRsdPercent", e.target.value)}
                helperText={methodAnalyte?.sstMaxRsdPercent != null ? t("sst.entryForm.criterionHelper", { criterion: `≤ ${methodAnalyte.sstMaxRsdPercent}%` }) : undefined}
              />
            </Grid>

            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <TextField
                fullWidth
                size="small"
                label={t("sst.entryForm.resolution")}
                type="number"
                disabled={disabled}
                error={showMissing && methodAnalyte?.sstMinResolution != null && formValue.resolution == null}
                value={formValue.resolution ?? ""}
                onChange={(e) => handleNumChange("resolution", e.target.value)}
                helperText={methodAnalyte?.sstMinResolution != null ? t("sst.entryForm.criterionHelper", { criterion: `≥ ${methodAnalyte.sstMinResolution}` }) : undefined}
              />
            </Grid>

            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <TextField
                fullWidth
                size="small"
                label={t("sst.entryForm.tailingFactor")}
                type="number"
                disabled={disabled}
                error={showMissing && methodAnalyte?.sstMaxTailingFactor != null && formValue.tailingFactor == null}
                value={formValue.tailingFactor ?? ""}
                onChange={(e) => handleNumChange("tailingFactor", e.target.value)}
                helperText={methodAnalyte?.sstMaxTailingFactor != null ? t("sst.entryForm.criterionHelper", { criterion: `≤ ${methodAnalyte.sstMaxTailingFactor}` }) : undefined}
              />
            </Grid>

            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <TextField
                fullWidth
                size="small"
                label={t("sst.entryForm.theoreticalPlates")}
                type="number"
                disabled={disabled}
                error={showMissing && methodAnalyte?.sstMinTheoreticalPlates != null && formValue.theoreticalPlates == null}
                value={formValue.theoreticalPlates ?? ""}
                onChange={(e) => handleNumChange("theoreticalPlates", e.target.value)}
                helperText={methodAnalyte?.sstMinTheoreticalPlates != null ? t("sst.entryForm.criterionHelper", { criterion: `≥ ${methodAnalyte.sstMinTheoreticalPlates}` }) : undefined}
              />
            </Grid>

            {/* Optional criteria if method or value has them */}
            {(methodAnalyte?.sstMinRetentionFactor != null || formValue.retentionFactor != null) && (
              <Grid size={{ xs: 12, sm: 6, md: 4 }}>
                <TextField
                  fullWidth
                  size="small"
                  label={t("sst.entryForm.retentionFactor")}
                  type="number"
                  disabled={disabled}
                  error={showMissing && methodAnalyte?.sstMinRetentionFactor != null && formValue.retentionFactor == null}
                  value={formValue.retentionFactor ?? ""}
                  onChange={(e) => handleNumChange("retentionFactor", e.target.value)}
                  helperText={methodAnalyte?.sstMinRetentionFactor != null ? t("sst.entryForm.criterionHelper", { criterion: `≥ ${methodAnalyte.sstMinRetentionFactor}` }) : undefined}
                />
              </Grid>
            )}

            {(methodAnalyte?.sstMinSignalToNoise != null || formValue.signalToNoise != null) && (
              <Grid size={{ xs: 12, sm: 6, md: 4 }}>
                <TextField
                  fullWidth
                  size="small"
                  label={t("sst.entryForm.signalToNoise")}
                  type="number"
                  disabled={disabled}
                  error={showMissing && methodAnalyte?.sstMinSignalToNoise != null && formValue.signalToNoise == null}
                  value={formValue.signalToNoise ?? ""}
                  onChange={(e) => handleNumChange("signalToNoise", e.target.value)}
                  helperText={methodAnalyte?.sstMinSignalToNoise != null ? t("sst.entryForm.criterionHelper", { criterion: `≥ ${methodAnalyte.sstMinSignalToNoise}` }) : undefined}
                />
              </Grid>
            )}

            {(methodAnalyte?.sstMinPeakToValley != null || formValue.peakToValley != null) && (
              <Grid size={{ xs: 12, sm: 6, md: 4 }}>
                <TextField
                  fullWidth
                  size="small"
                  label={t("sst.entryForm.peakToValley")}
                  type="number"
                  disabled={disabled}
                  error={showMissing && methodAnalyte?.sstMinPeakToValley != null && formValue.peakToValley == null}
                  value={formValue.peakToValley ?? ""}
                  onChange={(e) => handleNumChange("peakToValley", e.target.value)}
                  helperText={methodAnalyte?.sstMinPeakToValley != null ? t("sst.entryForm.criterionHelper", { criterion: `≥ ${methodAnalyte.sstMinPeakToValley}` }) : undefined}
                />
              </Grid>
            )}
          </Grid>
        </Box>
      </Stack>
    </Paper>
  );
}
