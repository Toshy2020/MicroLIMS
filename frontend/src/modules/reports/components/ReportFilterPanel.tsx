import { RefObject } from "react";
import { Box, Paper, TextField, MenuItem, Select, InputLabel, FormControl, Button, Link, Typography, Stack, useTheme } from "@mui/material";
import { brandColors } from "../../../theme";
import { statusTone } from "../../../theme/statusTokens";
import { categoryLabel } from "../../../components/StatusBadge";
import { FilterOptionsResponse, ResultLevel, ResultRecordSearchParams } from "../types/reportingTypes";
import { DateField } from "../../../components/DateField";
import { useTranslation } from "../../../i18n";

const RESULT_LEVEL_SEGMENTS: { value: ResultLevel | ""; key: string }[] = [
  { value: "", key: "all" },
  { value: "WithinLimit", key: "WithinLimit" },
  { value: "AlertLevel", key: "AlertLevel" },
  { value: "ActionLevel", key: "ActionLevel" },
  { value: "OutOfSpecification", key: "OutOfSpecification" }
];

const SAMPLE_STATUS_OPTIONS = ["Received", "InTesting", "UnderReview", "UnderApproval", "Approved", "Rejected", "RetestRequested"];
const APPROVAL_STATUS_OPTIONS = ["Approved", "Pending", "Rejected"];

// yyyy-MM-dd out of a full ISO timestamp, for <input type="date"> value.
function isoToDateInput(iso?: string): string {
  return iso ? iso.slice(0, 10) : "";
}

interface ReportFilterPanelProps {
  filterOptions: FilterOptionsResponse | null;
  draft: ResultRecordSearchParams;
  onChange: (patch: Partial<ResultRecordSearchParams>) => void;
  onSearch: () => void;
  onReset: () => void;
  searchInputRef?: RefObject<HTMLInputElement | null>;
}

// Left-column filter panel for the Record Search tab. Every field here
// only edits draft state - nothing queries the API until "Search
// Records" is clicked (RecordSearchTab applies the draft then).
export function ReportFilterPanel({ filterOptions, draft, onChange, onSearch, onReset, searchInputRef }: ReportFilterPanelProps) {
  const { t } = useTranslation("reports");
  const theme = useTheme();
  return (
    <Paper sx={{ p: 2.5 }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2 }}>
        <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>{t("search.filters.title")}</Typography>
        <Link component="button" type="button" onClick={onReset} sx={{ fontSize: 13 }} underline="hover">
          {t("search.filters.reset")}
        </Link>
      </Box>

      <Stack spacing={2}>
        <TextField
          fullWidth size="small" label={t("search.filters.searchLabel")}
          placeholder={t("search.filters.searchPlaceholder")}
          value={draft.search ?? ""}
          onChange={(e) => onChange({ search: e.target.value || undefined })}
          inputRef={searchInputRef}
        />

        <FormControl fullWidth size="small">
          <InputLabel>{t("search.filters.resultType")}</InputLabel>
          <Select
            label={t("search.filters.resultType")} value={draft.resultKind ?? ""}
            onChange={(e) => onChange({ resultKind: (e.target.value || undefined) as ResultRecordSearchParams["resultKind"] })}
            inputProps={{ "aria-label": t("search.filters.resultType") }}
          >
            <MenuItem value="">{t("search.filters.all")}</MenuItem>
            <MenuItem value="Quantitative">{t("search.filters.quantitative")}</MenuItem>
            <MenuItem value="Qualitative">{t("search.filters.qualitative")}</MenuItem>
          </Select>
        </FormControl>

        <FormControl fullWidth size="small">
          <InputLabel>{t("search.filters.category")}</InputLabel>
          <Select
            label={t("search.filters.category")} value={draft.category ?? ""}
            onChange={(e) => onChange({ category: (e.target.value || undefined) as ResultRecordSearchParams["category"] })}
            inputProps={{ "aria-label": t("search.filters.category") }}
          >
            <MenuItem value="">{t("search.filters.all")}</MenuItem>
            {(filterOptions?.categories ?? []).map((c) => (
              <MenuItem key={c} value={c}>{categoryLabel(c)}</MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl fullWidth size="small">
          <InputLabel>{t("search.filters.test")}</InputLabel>
          <Select
            label={t("search.filters.test")} value={draft.testCode ?? ""}
            onChange={(e) => onChange({ testCode: e.target.value || undefined })}
            inputProps={{ "aria-label": t("search.filters.test") }}
          >
            <MenuItem value="">{t("search.filters.all")}</MenuItem>
            {(filterOptions?.testCodes ?? []).map((tOpt) => (
              <MenuItem key={tOpt.testCode} value={tOpt.testCode}>{tOpt.testCode} - {tOpt.testDisplayName}</MenuItem>
            ))}
          </Select>
        </FormControl>

        <Box>
          <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.75 }}>{t("search.filters.resultLevel")}</Typography>
          <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.75 }}>
            {RESULT_LEVEL_SEGMENTS.map((seg) => {
              const selected = (draft.resultLevel ?? "") === seg.value;
              const color = seg.value ? theme.custom.status[statusTone(seg.value)].text : brandColors.sectionTitle;
              return (
                <Button
                  key={seg.key} size="small"
                  onClick={() => onChange({ resultLevel: (seg.value || undefined) as ResultRecordSearchParams["resultLevel"] })}
                  sx={{
                    fontSize: 12, minWidth: 0, px: 1.25, py: 0.5, textTransform: "none",
                    color: selected ? "common.white" : color,
                    bgcolor: selected ? color : "transparent",
                    border: `1px solid ${color}`,
                    "&:hover": { bgcolor: selected ? color : `${color}1a` }
                  }}
                >
                  {t(`search.filters.levels.${seg.key}`)}
                </Button>
              );
            })}
          </Box>
        </Box>

        <FormControl fullWidth size="small">
          <InputLabel>{t("search.filters.status")}</InputLabel>
          <Select
            label={t("search.filters.status")} value={draft.sampleStatus ?? ""}
            onChange={(e) => onChange({ sampleStatus: (e.target.value || undefined) as ResultRecordSearchParams["sampleStatus"] })}
            inputProps={{ "aria-label": t("search.filters.status") }}
          >
            <MenuItem value="">{t("search.filters.all")}</MenuItem>
            {SAMPLE_STATUS_OPTIONS.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
          </Select>
        </FormControl>

        <FormControl fullWidth size="small">
          <InputLabel>{t("search.filters.approvalStatus")}</InputLabel>
          <Select
            label={t("search.filters.approvalStatus")} value={draft.approvalStatus ?? ""}
            onChange={(e) => onChange({ approvalStatus: (e.target.value || undefined) as ResultRecordSearchParams["approvalStatus"] })}
            inputProps={{ "aria-label": t("search.filters.approvalStatus") }}
          >
            <MenuItem value="">{t("search.filters.all")}</MenuItem>
            {APPROVAL_STATUS_OPTIONS.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
          </Select>
        </FormControl>

        <Box>
          <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.75 }}>{t("search.filters.dateRange")}</Typography>
          <Box sx={{ display: "flex", gap: 1 }}>
            <DateField
              fullWidth label={t("search.filters.fromDate")} value={isoToDateInput(draft.fromDate)}
              max={isoToDateInput(draft.toDate)}
              onChange={(v) => onChange({ fromDate: v ? `${v}T00:00:00.000Z` : undefined })}
            />
            <DateField
              fullWidth label={t("search.filters.toDate")} value={isoToDateInput(draft.toDate)}
              min={isoToDateInput(draft.fromDate)}
              onChange={(v) => onChange({ toDate: v ? `${v}T23:59:59.999Z` : undefined })}
            />
          </Box>
        </Box>

        <FormControl fullWidth size="small">
          <InputLabel>{t("search.filters.locationPoint")}</InputLabel>
          <Select
            label={t("search.filters.locationPoint")} value={draft.subjectName ?? ""}
            onChange={(e) => onChange({ subjectName: e.target.value || undefined })}
            inputProps={{ "aria-label": t("search.filters.locationPoint") }}
          >
            <MenuItem value="">{t("search.filters.all")}</MenuItem>
            {(filterOptions?.subjectNames ?? []).map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
          </Select>
        </FormControl>

        <Button variant="contained" fullWidth onClick={onSearch}>{t("search.filters.searchRecords")}</Button>
      </Stack>
    </Paper>
  );
}
