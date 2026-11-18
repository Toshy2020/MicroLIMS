import { RefObject } from "react";
import {
  Box, Paper, TextField, MenuItem, Select, InputLabel, FormControl,
  Button, Link, Typography, Stack, useTheme
} from "@mui/material";
import { EvaluationOutcome, EvaluationType, MediaGptFilterOptions, MediaGptSearchParams } from "../types/mediaGptTypes";
import { useTranslation } from "../../../i18n";

interface MediaGptFilterPanelProps {
  filterOptions: MediaGptFilterOptions | null;
  draft: MediaGptSearchParams;
  onChange: (patch: Partial<MediaGptSearchParams>) => void;
  onSearch: () => void;
  onReset: () => void;
  searchInputRef?: RefObject<HTMLInputElement | null>;
}

export function MediaGptFilterPanel({
  filterOptions,
  draft,
  onChange,
  onSearch,
  onReset,
  searchInputRef
}: MediaGptFilterPanelProps) {
  const { t } = useTranslation("reports");
  const theme = useTheme();

  return (
    <Paper sx={{ p: 2.5 }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2 }}>
        <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
          {t("mediaGpt.filters.title")}
        </Typography>
        <Link component="button" type="button" onClick={onReset} sx={{ fontSize: 13 }} underline="hover">
          {t("mediaGpt.filters.reset")}
        </Link>
      </Box>

      <Stack spacing={2}>
        <TextField
          fullWidth
          size="small"
          label={t("mediaGpt.filters.searchLabel")}
          placeholder={t("mediaGpt.filters.searchPlaceholder")}
          value={draft.search ?? ""}
          onChange={(e) => onChange({ search: e.target.value || undefined })}
          inputRef={searchInputRef}
        />

        <FormControl fullWidth size="small">
          <InputLabel>{t("mediaGpt.filters.evaluationType")}</InputLabel>
          <Select
            label={t("mediaGpt.filters.evaluationType")}
            value={draft.evaluationType ?? ""}
            onChange={(e) => onChange({ evaluationType: (e.target.value || undefined) as EvaluationType | undefined })}
            inputProps={{ "aria-label": t("mediaGpt.filters.evaluationType") }}
          >
            <MenuItem value="">{t("mediaGpt.filters.allEvaluationTypes")}</MenuItem>
            <MenuItem value="GrowthPromotion">{t("mediaGpt.filters.growthPromotion")}</MenuItem>
            <MenuItem value="IndicationInhibition">{t("mediaGpt.filters.indicationInhibition")}</MenuItem>
            <MenuItem value="EnrichmentCharacteristics">{t("mediaGpt.filters.enrichmentCharacteristics")}</MenuItem>
          </Select>
        </FormControl>

        <FormControl fullWidth size="small">
          <InputLabel>{t("mediaGpt.filters.mediaType")}</InputLabel>
          <Select
            label={t("mediaGpt.filters.mediaType")}
            value={draft.mediaType ?? ""}
            onChange={(e) => onChange({ mediaType: e.target.value || undefined })}
            inputProps={{ "aria-label": t("mediaGpt.filters.mediaType") }}
          >
            <MenuItem value="">{t("mediaGpt.filters.allMediaTypes")}</MenuItem>
            {(filterOptions?.mediaTypes ?? []).map((m) => (
              <MenuItem key={m} value={m}>{m}</MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl fullWidth size="small">
          <InputLabel>{t("mediaGpt.filters.evaluationOutcome")}</InputLabel>
          <Select
            label={t("mediaGpt.filters.evaluationOutcome")}
            value={draft.outcome ?? ""}
            onChange={(e) => onChange({ outcome: (e.target.value || undefined) as EvaluationOutcome | undefined })}
            inputProps={{ "aria-label": t("mediaGpt.filters.evaluationOutcome") }}
          >
            <MenuItem value="">{t("mediaGpt.filters.allOutcomes")}</MenuItem>
            <MenuItem value="Conform">{t("mediaGpt.filters.conform")}</MenuItem>
            <MenuItem value="NonConform">{t("mediaGpt.filters.nonConform")}</MenuItem>
          </Select>
        </FormControl>

        <FormControl fullWidth size="small">
          <InputLabel>{t("mediaGpt.filters.approvalStatus")}</InputLabel>
          <Select
            label={t("mediaGpt.filters.approvalStatus")}
            value={draft.approvalStatus ?? ""}
            onChange={(e) => onChange({ approvalStatus: (e.target.value || undefined) as MediaGptSearchParams["approvalStatus"] })}
            inputProps={{ "aria-label": t("mediaGpt.filters.approvalStatus") }}
          >
            <MenuItem value="">{t("mediaGpt.filters.allStatuses")}</MenuItem>
            <MenuItem value="PendingReview">{t("mediaGpt.filters.pendingReview")}</MenuItem>
            <MenuItem value="Approved">{t("mediaGpt.filters.approved")}</MenuItem>
            <MenuItem value="Rejected">{t("mediaGpt.filters.rejected")}</MenuItem>
          </Select>
        </FormControl>

        <Stack direction="row" spacing={1} sx={{ pt: 1 }}>
          <Button
            variant="contained"
            color="primary"
            fullWidth
            onClick={onSearch}
            sx={{ fontWeight: 700 }}
          >
            {t("mediaGpt.filters.searchLots")}
          </Button>
          <Button variant="outlined" onClick={onReset} sx={{ fontWeight: 600 }}>
            {t("mediaGpt.filters.clear")}
          </Button>
        </Stack>
      </Stack>
    </Paper>
  );
}
