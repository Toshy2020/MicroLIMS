import {
  Box,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  FormHelperText,
  Chip,
  Typography,
  Stack,
  Autocomplete
} from "@mui/material";
import { HplcTechnique, HplcResultMode } from "../services/HplcMethodService";
import { useTranslation } from "react-i18next";
import { LaboratorySection } from "../../../../services/laboratorySectionService";
import { HplcMethodErrors } from "./hplcMethodValidation";
import { DateField } from "../../../../components/DateField";

export interface HplcGeneralSectionProps {
  technique?: HplcTechnique;
  resultMode?: HplcResultMode;
  onTechniqueChange?: (val: HplcTechnique) => void;
  onResultModeChange?: (val: HplcResultMode) => void;
  name: string;
  abbreviation: string;
  effectiveDate: string;
  sectionId: number | "";
  internalStandardName?: string;
  onInternalStandardNameChange?: (val: string) => void;
  productItemId?: number | "";
  productItemName?: string;
  onProductItemIdChange?: (val: number | "") => void;
  finishedProductItems?: Array<{ id: number; name: string }>;
  errors: HplcMethodErrors;
  editingId: number | null;
  mySections: LaboratorySection[];
  onNameChange: (val: string) => void;
  onAbbreviationChange: (val: string) => void;
  onEffectiveDateChange: (val: string) => void;
  onSectionIdChange: (val: number | "") => void;
}

export function HplcGeneralSection({
  technique = "Hplc",
  resultMode = "Assay",
  onTechniqueChange,
  onResultModeChange,
  name,
  abbreviation,
  effectiveDate,
  sectionId,
  internalStandardName = "",
  onInternalStandardNameChange,
  productItemId = "",
  productItemName = "",
  onProductItemIdChange,
  finishedProductItems = [],
  errors,
  editingId,
  mySections,
  onNameChange,
  onAbbreviationChange,
  onEffectiveDateChange,
  onSectionIdChange
}: HplcGeneralSectionProps) {
  const { t } = useTranslation("masterData");
  const isEditing = Boolean(editingId);
  const isGc = technique === "Gc";

  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
      {/* Technique & Result Mode Pickers */}
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 2, alignItems: { sm: "center" } }}>
        {isEditing ? (
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <Typography variant="body2" sx={{ fontWeight: 600, color: "text.secondary" }}>
              {t("hplcMethod.general.techniqueColon")}
            </Typography>
            <Chip
              label={technique.toUpperCase()}
              size="small"
              color={isGc ? "secondary" : "primary"}
              sx={{ fontWeight: 700 }}
            />
          </Stack>
        ) : (
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel id="method-technique-picker-label">{t("hplcMethod.general.techniqueLabel")}</InputLabel>
            <Select
              labelId="method-technique-picker-label"
              label={t("hplcMethod.general.techniqueLabel")}
              value={technique}
              onChange={(e) => onTechniqueChange?.(e.target.value as HplcTechnique)}
            >
              <MenuItem value="Hplc">{t("hplcMethod.general.techniqueHplc")}</MenuItem>
              <MenuItem value="Gc">{t("hplcMethod.general.techniqueGc")}</MenuItem>
            </Select>
          </FormControl>
        )}

        {isGc ? (
          isEditing ? (
            <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
              <Typography variant="body2" sx={{ fontWeight: 600, color: "text.secondary" }}>
                {t("hplcMethod.general.resultModeColon")}
              </Typography>
              <Chip
                label={resultMode === "ResidualSolvents" ? t("hplcMethod.general.resultModeResidualSolvents") : t("hplcMethod.general.resultModeAssay")}
                size="small"
                variant="outlined"
                color={resultMode === "ResidualSolvents" ? "warning" : "default"}
                sx={{ fontWeight: 600 }}
              />
            </Stack>
          ) : (
            <FormControl size="small" sx={{ minWidth: 200 }}>
              <InputLabel id="method-result-mode-picker-label">{t("hplcMethod.general.resultModeLabel")}</InputLabel>
              <Select
                labelId="method-result-mode-picker-label"
                label={t("hplcMethod.general.resultModeLabel")}
                value={resultMode}
                onChange={(e) => onResultModeChange?.(e.target.value as HplcResultMode)}
              >
                <MenuItem value="Assay">{t("hplcMethod.general.resultModeAssay")}</MenuItem>
                <MenuItem value="ResidualSolvents">{t("hplcMethod.general.resultModeResidualSolvents")}</MenuItem>
              </Select>
            </FormControl>
          )
        ) : (
          isEditing ? (
            <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
              <Typography variant="body2" sx={{ fontWeight: 600, color: "text.secondary" }}>
                {t("hplcMethod.general.resultModeColon")}
              </Typography>
              <Chip
                label={
                  resultMode === "RelatedSubstances"
                    ? t("hplcMethod.general.resultModeRelatedSubstances")
                    : resultMode === "CleaningResidue"
                    ? t("hplcMethod.general.resultModeCleaningResidue")
                    : t("hplcMethod.general.resultModeAssay")
                }
                size="small"
                variant="outlined"
                color={
                  resultMode === "RelatedSubstances"
                    ? "warning"
                    : resultMode === "CleaningResidue"
                    ? "info"
                    : "default"
                }
                sx={{ fontWeight: 600 }}
              />
            </Stack>
          ) : (
            <FormControl size="small" sx={{ minWidth: 200 }}>
              <InputLabel id="method-result-mode-picker-label">{t("hplcMethod.general.resultModeLabel")}</InputLabel>
              <Select
                labelId="method-result-mode-picker-label"
                label={t("hplcMethod.general.resultModeLabel")}
                value={resultMode}
                onChange={(e) => onResultModeChange?.(e.target.value as HplcResultMode)}
              >
                <MenuItem value="Assay">{t("hplcMethod.general.resultModeAssay")}</MenuItem>
                <MenuItem value="RelatedSubstances">{t("hplcMethod.general.resultModeRelatedSubstances")}</MenuItem>
                <MenuItem value="CleaningResidue">{t("hplcMethod.general.resultModeCleaningResidue")}</MenuItem>
              </Select>
            </FormControl>
          )
        )}

        {!isGc && resultMode === "Assay" && (
          <TextField
            size="small"
            label={t("hplcMethod.general.internalStandardLabel")}
            placeholder={t("hplcMethod.general.internalStandardPlaceholder")}
            value={internalStandardName}
            onChange={(e) => onInternalStandardNameChange?.(e.target.value)}
            disabled={isEditing}
            error={Boolean(errors.internalStandardName)}
            helperText={errors.internalStandardName ?? (isEditing ? t("hplcMethod.general.internalStandardFixed") : t("hplcMethod.general.internalStandardOptional"))}
            sx={{ minWidth: 220 }}
          />
        )}

        {!isGc && resultMode === "CleaningResidue" && (
          <Autocomplete
            size="small"
            sx={{ minWidth: 260, flex: 1 }}
            options={finishedProductItems}
            getOptionLabel={(o) => (typeof o === "string" ? o : o.name ?? "")}
            value={
              finishedProductItems.find((i) => i.id === productItemId) ??
              (productItemId ? { id: Number(productItemId), name: productItemName || t("hplcMethod.general.productFallback", { id: productItemId }) } : null)
            }
            onChange={(_, val) => {
              onProductItemIdChange?.(val ? val.id : "");
            }}
            isOptionEqualToValue={(a, b) => a.id === b.id}
            renderInput={(params) => (
              <TextField
                {...params}
                label={t("hplcMethod.general.finishedProductLabel")}
                placeholder={t("hplcMethod.general.finishedProductPlaceholder")}
                required
                error={Boolean(errors.productItemId)}
                helperText={errors.productItemId ?? t("hplcMethod.general.finishedProductHelper")}
                slotProps={{
                  ...params.slotProps,
                  htmlInput: {
                    ...params.slotProps?.htmlInput,
                    "aria-label": t("hplcMethod.general.finishedProductAria")
                  }
                }}
              />
            )}
          />
        )}
      </Stack>

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "1.5fr 1fr 1fr" },
          gap: 2,
          mb: !editingId && mySections.length > 1 ? 2 : 0
        }}
      >
        <TextField
          size="small"
          label={t("hplcMethod.general.methodNameLabel")}
          placeholder={isGc ? t("hplcMethod.general.methodNamePlaceholderGc") : t("hplcMethod.general.methodNamePlaceholderHplc")}
          value={name}
          onChange={(e) => onNameChange(e.target.value)}
          required
          error={Boolean(errors.name)}
          helperText={errors.name}
          fullWidth
        />
        <TextField
          size="small"
          label={t("hplcMethod.general.abbreviationLabel")}
          placeholder={isGc ? t("hplcMethod.general.abbreviationPlaceholderGc") : t("hplcMethod.general.abbreviationPlaceholderHplc")}
          value={abbreviation}
          onChange={(e) => onAbbreviationChange(e.target.value.toUpperCase())}
          required
          error={Boolean(errors.abbreviation)}
          helperText={errors.abbreviation ?? t("hplcMethod.general.abbreviationHelper")}
          slotProps={{ htmlInput: { maxLength: 20 } }}
          fullWidth
        />
        <DateField
          label={t("hplcMethod.general.effectiveDateLabel")}
          value={effectiveDate}
          onChange={(v) => onEffectiveDateChange(v)}
          required
          error={Boolean(errors.effectiveDate)}
          helperText={errors.effectiveDate}
          fullWidth
        />
      </Box>

      {!editingId && mySections.length > 1 && (
        <FormControl size="small" fullWidth required error={Boolean(errors.sectionId)}>
          <InputLabel id="method-section-label">{t("hplcMethod.general.sectionLabel")}</InputLabel>
          <Select
            labelId="method-section-label"
            label={t("hplcMethod.general.sectionLabel")}
            value={sectionId}
            onChange={(e) => onSectionIdChange(Number(e.target.value) || "")}
          >
            {mySections.map((sec) => (
              <MenuItem key={sec.sectionId} value={sec.sectionId}>
                {sec.sectionName}
              </MenuItem>
            ))}
          </Select>
          {errors.sectionId && <FormHelperText>{errors.sectionId}</FormHelperText>}
        </FormControl>
      )}
    </Box>
  );
}
