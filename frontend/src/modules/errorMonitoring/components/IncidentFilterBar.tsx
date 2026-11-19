import { Box, Button, MenuItem, Paper, Stack, TextField } from "@mui/material";
import FileDownloadOutlinedIcon from "@mui/icons-material/FileDownloadOutlined";
import RestartAltIcon from "@mui/icons-material/RestartAlt";
import type { IncidentFilterState } from "../types/errorMonitoringTypes";
import { SEVERITIES, SOURCES, STATUSES } from "../types/errorMonitoringTypes";
import { DateField } from "../../../components/DateField";
import { useTranslation } from "../../../i18n";

interface Props {
  filters: IncidentFilterState;
  onChange: (filters: IncidentFilterState) => void;
  onReset: () => void;
  onExport: () => void;
  exporting: boolean;
  disabled: boolean;
}

export function IncidentFilterBar({ filters, onChange, onReset, onExport, exporting, disabled }: Props) {
  const { t } = useTranslation("errorMonitoring");
  const set = <K extends keyof IncidentFilterState>(key: K, value: IncidentFilterState[K]) =>
    onChange({ ...filters, [key]: value });

  return (
    <Paper sx={{ p: 2, mb: 2 }}>
      <Stack
        direction="row"
        spacing={2}
        useFlexGap
        sx={{
          flexWrap: "wrap",
          alignItems: "flex-end"
        }}>
        <TextField
          select size="small" label={t("filters.severity")} sx={{ minWidth: 140 }}
          value={filters.severity}
          onChange={(e) => set("severity", e.target.value as IncidentFilterState["severity"])}
        >
          <MenuItem value="">{t("filters.all")}</MenuItem>
          {SEVERITIES.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
        </TextField>

        <TextField
          select size="small" label={t("filters.source")} sx={{ minWidth: 140 }}
          value={filters.source}
          onChange={(e) => set("source", e.target.value as IncidentFilterState["source"])}
        >
          <MenuItem value="">{t("filters.all")}</MenuItem>
          {SOURCES.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
        </TextField>

        <TextField
          select size="small" label={t("filters.status")} sx={{ minWidth: 140 }}
          value={filters.status}
          onChange={(e) => set("status", e.target.value as IncidentFilterState["status"])}
        >
          <MenuItem value="">{t("filters.all")}</MenuItem>
          {STATUSES.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
        </TextField>

        <DateField label={t("filters.activeFrom")} value={filters.fromDate} onChange={(v) => set("fromDate", v)} max={filters.toDate} />
        <DateField label={t("filters.activeTo")} value={filters.toDate} onChange={(v) => set("toDate", v)} min={filters.fromDate} />

        <TextField
          size="small" label={t("filters.search")} sx={{ minWidth: 280, flexGrow: 1 }}
          value={filters.search} onChange={(e) => set("search", e.target.value)}
        />

        <Box sx={{ display: "flex", gap: 1 }}>
          <Button size="small" startIcon={<RestartAltIcon />} onClick={onReset} disabled={disabled}>
            {t("filters.reset")}
          </Button>
          <Button
            size="small" variant="outlined" startIcon={<FileDownloadOutlinedIcon />}
            onClick={onExport} disabled={disabled || exporting}
          >
            {exporting ? t("filters.exporting") : t("filters.exportCsv")}
          </Button>
        </Box>
      </Stack>
    </Paper>
  );
}
