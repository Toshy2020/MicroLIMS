import { useEffect, useState } from "react";
import { Alert, Button, MenuItem, Paper, Stack, TextField } from "@mui/material";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import TuneIcon from "@mui/icons-material/Tune";
import { ReportingService } from "../../reports/services/ReportingService";
import { CATEGORY_LABELS } from "../../reports/services/OverviewService";
import { filtersError } from "../utils/kpiFilters";
import type { KpiFiltersValue } from "../utils/kpiFilters";
import { DateField } from "../../../components/DateField";
import { useTranslation } from "../../../i18n";

interface KpiFiltersProps {
  value: KpiFiltersValue;
  onChange(v: KpiFiltersValue): void;
  onOpenDefinitions(): void;
  onOpenProfiles?(): void;
  onExport(): void;
  exportDisabled: boolean;
}

export function KpiFilters({ value, onChange, onOpenDefinitions, onOpenProfiles, onExport, exportDisabled }: KpiFiltersProps) {
  const { t } = useTranslation("kpi");
  const [options, setOptions] = useState<{ categories: string[]; locations: string[]; testCodes: string[] }>({
    categories: [], locations: [], testCodes: []
  });
  const [optionsFailed, setOptionsFailed] = useState(false);
  // The date inputs hold their own draft: <input type="date"> reports "" while
  // a date is half typed, and a half-typed range must not reach the server.
  const [from, setFrom] = useState(value.from);
  const [to, setTo] = useState(value.to);
  const dateError = filtersError({ from, to });

  useEffect(() => {
    let live = true;
    ReportingService.getFilterOptions()
      .then((o) => {
        if (live && o) {
          setOptions({
            categories: o.categories ?? [],
            locations: o.subjectNames ?? [],
            testCodes: (o.testCodes ?? []).map((t) => t.testCode)
          });
        }
      })
      .catch(() => { if (live) setOptionsFailed(true); });
    return () => { live = false; };
  }, []);

  const changeDates = (nextFrom: string, nextTo: string) => {
    setFrom(nextFrom);
    setTo(nextTo);
    if (!filtersError({ from: nextFrom, to: nextTo })) onChange({ ...value, from: nextFrom, to: nextTo });
  };

  return (
    <Paper sx={{ p: 2, mb: 2.5 }}>
      <Stack direction={{ xs: "column", md: "row" }} spacing={1.5} useFlexGap sx={{ flexWrap: "wrap", alignItems: { md: "flex-start" } }}>
        <DateField label={t("filters.from")} value={from} onChange={(v) => changeDates(v, to)} error={!!dateError} />
        <DateField
          label={t("filters.to")} value={to} onChange={(v) => changeDates(from, v)}
          error={!!dateError} helperText={dateError ?? undefined}
        />
        <TextField
          select size="small" label={t("filters.category")} value={value.category ?? ""} sx={{ minWidth: 180 }}
          onChange={(e) => onChange({ ...value, category: e.target.value || null })}
        >
          <MenuItem value="">{t("filters.allCategories")}</MenuItem>
          {options.categories.map((c) => <MenuItem key={c} value={c}>{CATEGORY_LABELS[c] ?? c}</MenuItem>)}
        </TextField>
        <TextField
          select size="small" label={t("filters.location")} value={value.location ?? ""} sx={{ minWidth: 180 }}
          onChange={(e) => onChange({ ...value, location: e.target.value || null })}
        >
          <MenuItem value="">{t("filters.allLocations")}</MenuItem>
          {options.locations.map((l) => <MenuItem key={l} value={l}>{l}</MenuItem>)}
        </TextField>
        <TextField
          select size="small" label={t("filters.test")} value={value.testCode ?? ""} sx={{ minWidth: 140 }}
          onChange={(e) => onChange({ ...value, testCode: e.target.value || null })}
        >
          <MenuItem value="">{t("filters.allTests")}</MenuItem>
          {options.testCodes.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
        </TextField>

        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", ml: { md: "auto" } }}>
          <Button size="small" variant="outlined" startIcon={<InfoOutlinedIcon />} onClick={onOpenDefinitions}>{t("filters.definitions")}</Button>
          {onOpenProfiles && (
            <Button size="small" variant="outlined" startIcon={<TuneIcon />} onClick={onOpenProfiles}>{t("filters.profiles")}</Button>
          )}
          <Button size="small" variant="contained" startIcon={<PictureAsPdfIcon />} onClick={onExport} disabled={exportDisabled}>{t("filters.exportPdf")}</Button>
        </Stack>
      </Stack>
      {optionsFailed && (
        <Alert severity="error" sx={{ mt: 1.5 }}>
          {t("filters.optionsFailed")}
        </Alert>
      )}
    </Paper>
  );
}
