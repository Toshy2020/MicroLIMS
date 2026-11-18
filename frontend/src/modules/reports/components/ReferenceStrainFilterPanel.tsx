import { RefObject } from "react";
import {
  Box, Paper, TextField, MenuItem, Select, InputLabel, FormControl,
  Button, Link, Typography, Stack, useTheme
} from "@mui/material";
import { ReferenceStrainFilterOptions, ReferenceStrainSearchParams } from "../types/referenceStrainTypes";
import { DateField } from "../../../components/DateField";
import { useTranslation } from "../../../i18n";

interface ReferenceStrainFilterPanelProps {
  filterOptions: ReferenceStrainFilterOptions | null;
  draft: ReferenceStrainSearchParams;
  onChange: (patch: Partial<ReferenceStrainSearchParams>) => void;
  onSearch: () => void;
  onReset: () => void;
  searchInputRef?: RefObject<HTMLInputElement | null>;
}

export function ReferenceStrainFilterPanel({
  filterOptions,
  draft,
  onChange,
  onSearch,
  onReset,
  searchInputRef
}: ReferenceStrainFilterPanelProps) {
  const { t } = useTranslation("reports");
  const theme = useTheme();

  return (
    <Paper sx={{ p: 2.5 }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2 }}>
        <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
          {t("referenceStrain.filters.title")}
        </Typography>
        <Link component="button" type="button" onClick={onReset} sx={{ fontSize: 13 }} underline="hover">
          {t("referenceStrain.filters.reset")}
        </Link>
      </Box>

      <Stack spacing={2}>
        <TextField
          fullWidth
          size="small"
          label={t("referenceStrain.filters.searchLabel")}
          placeholder={t("referenceStrain.filters.searchPlaceholder")}
          value={draft.search ?? ""}
          onChange={(e) => onChange({ search: e.target.value || undefined })}
          inputRef={searchInputRef}
        />

        <FormControl fullWidth size="small">
          <InputLabel>{t("referenceStrain.filters.organismStrain")}</InputLabel>
          <Select
            label={t("referenceStrain.filters.organismStrain")}
            value={draft.organismId ?? ""}
            onChange={(e) => onChange({ organismId: e.target.value ? Number(e.target.value) : undefined })}
            inputProps={{ "aria-label": t("referenceStrain.filters.organismStrain") }}
          >
            <MenuItem value="">{t("referenceStrain.filters.allOrganisms")}</MenuItem>
            {(filterOptions?.organisms ?? []).map((o) => (
              <MenuItem key={o.id} value={o.id}>
                {o.scientificName} {o.atccNumber ? `(ATCC ${o.atccNumber})` : ""}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl fullWidth size="small">
          <InputLabel>{t("referenceStrain.filters.approvalStatus")}</InputLabel>
          <Select
            label={t("referenceStrain.filters.approvalStatus")}
            value={draft.approvalStatus ?? ""}
            onChange={(e) => onChange({ approvalStatus: (e.target.value || undefined) as ReferenceStrainSearchParams["approvalStatus"] })}
            inputProps={{ "aria-label": t("referenceStrain.filters.approvalStatus") }}
          >
            <MenuItem value="">{t("referenceStrain.filters.allStatuses")}</MenuItem>
            <MenuItem value="PendingReview">{t("referenceStrain.filters.pendingReview")}</MenuItem>
            <MenuItem value="Approved">{t("referenceStrain.filters.approved")}</MenuItem>
            <MenuItem value="Rejected">{t("referenceStrain.filters.rejected")}</MenuItem>
          </Select>
        </FormControl>

        <FormControl fullWidth size="small">
          <InputLabel>{t("referenceStrain.filters.activeDestroyed")}</InputLabel>
          <Select<"" | "true" | "false">
            label={t("referenceStrain.filters.activeDestroyed")}
            value={draft.isDestroyed === undefined ? "" : draft.isDestroyed ? "true" : "false"}
            onChange={(e) => onChange({ isDestroyed: e.target.value === "" ? undefined : e.target.value === "true" })}
            inputProps={{ "aria-label": t("referenceStrain.filters.activeDestroyed") }}
          >
            <MenuItem value="">{t("referenceStrain.filters.allBatches")}</MenuItem>
            <MenuItem value="false">{t("referenceStrain.filters.activeOnly")}</MenuItem>
            <MenuItem value="true">{t("referenceStrain.filters.destroyedOnly")}</MenuItem>
          </Select>
        </FormControl>

        <Box>
          <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.5, fontWeight: 600 }}>
            {t("referenceStrain.filters.receiptDateRange")}
          </Typography>
          <Stack direction="row" spacing={1}>
            <DateField
              label={t("referenceStrain.filters.from")}
              value={draft.receiptFromDate?.slice(0, 10) ?? ""}
              onChange={(v) => onChange({ receiptFromDate: v ? `${v}T00:00:00.000Z` : undefined })}
              max={draft.receiptToDate?.slice(0, 10)}
              fullWidth
            />
            <DateField
              label={t("referenceStrain.filters.to")}
              value={draft.receiptToDate?.slice(0, 10) ?? ""}
              onChange={(v) => onChange({ receiptToDate: v ? `${v}T23:59:59.999Z` : undefined })}
              min={draft.receiptFromDate?.slice(0, 10)}
              fullWidth
            />
          </Stack>
        </Box>

        <Box>
          <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.5, fontWeight: 600 }}>
            {t("referenceStrain.filters.gptUsageDateRange")}
          </Typography>
          <Stack direction="row" spacing={1}>
            <DateField
              label={t("referenceStrain.filters.from")}
              value={draft.usageFromDate?.slice(0, 10) ?? ""}
              onChange={(v) => onChange({ usageFromDate: v ? `${v}T00:00:00.000Z` : undefined })}
              max={draft.usageToDate?.slice(0, 10)}
              fullWidth
            />
            <DateField
              label={t("referenceStrain.filters.to")}
              value={draft.usageToDate?.slice(0, 10) ?? ""}
              onChange={(v) => onChange({ usageToDate: v ? `${v}T23:59:59.999Z` : undefined })}
              min={draft.usageFromDate?.slice(0, 10)}
              fullWidth
            />
          </Stack>
        </Box>

        <Stack direction="row" spacing={1} sx={{ pt: 1 }}>
          <Button
            variant="contained"
            color="primary"
            fullWidth
            onClick={onSearch}
            sx={{ fontWeight: 700 }}
          >
            {t("referenceStrain.filters.searchBatches")}
          </Button>
          <Button variant="outlined" onClick={onReset} sx={{ fontWeight: 600 }}>
            {t("referenceStrain.filters.clear")}
          </Button>
        </Stack>
      </Stack>
    </Paper>
  );
}
