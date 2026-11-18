import { useEffect, useState } from "react";
import {
  Box,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import type { UvCalculationMode } from "../../../../services/masterDataOptions";
import { useTranslation } from "react-i18next";
import "../../../../i18n";
import { MaterialMasterService, MaterialMasterEntry } from "../services/MaterialMasterService";

export interface UvTestFieldsProps {
  mode: UvCalculationMode | "";
  onModeChange: (mode: UvCalculationMode) => void;
  specificAbsorbance: string;
  onSpecificAbsorbanceChange: (val: string) => void;
  totalVolumeMl: string;
  onTotalVolumeMlChange: (val: string) => void;
  standardVolumeMl: string;
  onStandardVolumeMlChange: (val: string) => void;
  standardEntryId: number | "";
  onStandardEntryIdChange: (id: number | "") => void;
  sectionId: number | "" | null;
  replicateCount: string;
  onReplicateCountChange: (val: string) => void;
  disabled?: boolean;
}

export const UV_CALCULATION_MODES: { value: UvCalculationMode; label: string; description: string }[] = [
  {
    value: "SpecificAbsorbanceAssay",
    label: "Specific Absorbance Assay (A1%)",
    description: "Calculates % as-is = A × V / (A1% × W)"
  },
  {
    value: "SpecificAbsorbanceValue",
    label: "Specific Absorbance Value",
    description: "Calculates A(1%, 1 cm) = A × V / (W × 100)"
  },
  {
    value: "StandardComparison",
    label: "Assay against Standard",
    description: "Calculates preparation assay compared to a reference standard"
  },
  {
    value: "AbsorbanceLimit",
    label: "Absorbance Limit (Reference)",
    description: "Tests whether absorbance is not greater than a reference solution"
  }
];

export function UvTestFields({
  mode,
  onModeChange,
  specificAbsorbance,
  onSpecificAbsorbanceChange,
  totalVolumeMl,
  onTotalVolumeMlChange,
  standardVolumeMl,
  onStandardVolumeMlChange,
  standardEntryId,
  onStandardEntryIdChange,
  sectionId,
  replicateCount,
  onReplicateCountChange,
  disabled = false
}: UvTestFieldsProps) {
  const { t } = useTranslation("masterData");
  const needsSpecificAbsorbance = mode === "SpecificAbsorbanceAssay";
  const needsTotalVolume =
    mode === "SpecificAbsorbanceAssay" ||
    mode === "SpecificAbsorbanceValue" ||
    mode === "StandardComparison";
  const needsStandardVolume = mode === "StandardComparison";

  const [standards, setStandards] = useState<MaterialMasterEntry[]>([]);
  const [loadError, setLoadError] = useState(false);
  useEffect(() => {
    if (!needsStandardVolume) return;
    let active = true;
    MaterialMasterService.getAll("ReferenceStandard", true)
      .then((s) => active && setStandards(s))
      .catch(() => active && setLoadError(true));
    return () => {
      active = false;
    };
  }, [needsStandardVolume]);
  const standardOptions = standards.filter((s) => sectionId == null || sectionId === "" || s.sectionId === sectionId);

  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
      <Typography sx={{ fontWeight: 600, fontSize: 13, color: "text.secondary", mb: 1.5 }}>
        {t("testMaster.uv.sectionTitle")}
      </Typography>

      <Stack spacing={2}>
        <FormControl size="small" fullWidth required disabled={disabled}>
          <InputLabel id="uv-calculation-mode-label">{t("testMaster.uv.calculationModeLabel")}</InputLabel>
          <Select
            labelId="uv-calculation-mode-label"
            label={t("testMaster.uv.calculationModeLabel")}
            value={mode}
            onChange={(e) => onModeChange(e.target.value as UvCalculationMode)}
          >
            {UV_CALCULATION_MODES.map((m) => (
              <MenuItem key={m.value} value={m.value}>
                <Box>
                  <Typography variant="body2" sx={{ fontWeight: 500 }}>
                    {t(`testMaster.uv.modes.${m.value}.label`)}
                  </Typography>
                  <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                    {t(`testMaster.uv.modes.${m.value}.description`)}
                  </Typography>
                </Box>
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        {needsStandardVolume && (
          <FormControl size="small" fullWidth required disabled={disabled} error={loadError}>
            <InputLabel id="uv-standard-entry-label">{t("testMaster.uv.standardMaterialLabel")}</InputLabel>
            <Select
              labelId="uv-standard-entry-label"
              label={t("testMaster.uv.standardMaterialLabel")}
              value={standardOptions.some((s) => s.id === standardEntryId) ? standardEntryId : ""}
              onChange={(e) => onStandardEntryIdChange(String(e.target.value) === "" ? "" : Number(e.target.value))}
            >
              {standardOptions.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.name} ({s.code})
                </MenuItem>
              ))}
            </Select>
            <FormHelperText>
              {loadError
                ? t("testMaster.uv.standardMaterialError")
                : t("testMaster.uv.standardMaterialHelper")}
            </FormHelperText>
          </FormControl>
        )}

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
          {needsSpecificAbsorbance && (
            <TextField
              size="small"
              label={t("testMaster.uv.specificAbsorbanceLabel")}
              type="number"
              value={specificAbsorbance}
              onChange={(e) => onSpecificAbsorbanceChange(e.target.value)}
              helperText={t("testMaster.uv.specificAbsorbanceHelper")}
              slotProps={{ htmlInput: { step: "any", min: "0" } }}
              required
              disabled={disabled}
              fullWidth
            />
          )}

          {needsTotalVolume && (
            <TextField
              size="small"
              label={t("testMaster.uv.totalVolumeLabel")}
              type="number"
              value={totalVolumeMl}
              onChange={(e) => onTotalVolumeMlChange(e.target.value)}
              helperText={t("testMaster.uv.totalVolumeHelper")}
              slotProps={{ htmlInput: { step: "any", min: "0" } }}
              required
              disabled={disabled}
              fullWidth
            />
          )}

          {needsStandardVolume && (
            <TextField
              size="small"
              label={t("testMaster.uv.standardVolumeLabel")}
              type="number"
              value={standardVolumeMl}
              onChange={(e) => onStandardVolumeMlChange(e.target.value)}
              helperText={t("testMaster.uv.standardVolumeHelper")}
              slotProps={{ htmlInput: { step: "any", min: "0" } }}
              required
              disabled={disabled}
              fullWidth
            />
          )}

          <TextField
            size="small"
            label={t("testMaster.uv.replicateCountLabel")}
            type="number"
            value={replicateCount}
            onChange={(e) => onReplicateCountChange(e.target.value)}
            helperText={t("testMaster.uv.replicateCountHelper")}
            slotProps={{ htmlInput: { step: "1", min: "1", max: "30" } }}
            required
            disabled={disabled}
            fullWidth
          />
        </Box>
      </Stack>
    </Box>
  );
}
