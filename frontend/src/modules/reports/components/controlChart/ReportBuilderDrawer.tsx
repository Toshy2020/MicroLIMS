import { useState } from "react";
import {
  Alert, Box, Button, Checkbox, Divider, Drawer, FormControlLabel, IconButton, Stack, TextField, Tooltip, Typography
} from "@mui/material";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlineOutlined";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import DescriptionIcon from "@mui/icons-material/Description";
import CloseIcon from "@mui/icons-material/Close";
import { i18n, useTranslation } from "../../../../i18n";
import { ReportFormat, ReportItem } from "../../types/controlChartTypes";

interface ReportBuilderDrawerProps {
  open: boolean;
  items: ReportItem[];
  onClose: () => void;
  onChange: (items: ReportItem[]) => void;
  onGenerate: (title: string, notes: string, includeSummary: boolean, format: ReportFormat) => Promise<void>;
}

// An error from a blob request arrives as a Blob, not parsed JSON.
export async function errorMessage(e: unknown): Promise<string> {
  const fallback = i18n.t("reports:controlChart.reportBuilder.generateError");
  const data = (e as { response?: { data?: unknown } } | null)?.response?.data;
  if (data instanceof Blob) {
    try {
      const body: unknown = JSON.parse(await data.text());
      return (body as { message?: string } | null)?.message ?? fallback;
    } catch {
      return fallback;
    }
  }
  return (data as { message?: string } | undefined)?.message ?? (e instanceof Error ? e.message : undefined) ?? fallback;
}

// The charts queued for one combined report: retitle, reorder or remove
// them, then generate a single PDF or Word document on the server.
export function ReportBuilderDrawer({ open, items, onClose, onChange, onGenerate }: ReportBuilderDrawerProps) {
  const { t } = useTranslation("reports");
  const [title, setTitle] = useState(() => t("controlChart.reportBuilder.defaultTitle"));
  const [notes, setNotes] = useState("");
  const [includeSummary, setIncludeSummary] = useState(true);
  const [busy, setBusy] = useState<ReportFormat | null>(null);
  const [error, setError] = useState<string | null>(null);

  const move = (index: number, delta: number) => {
    const next = [...items];
    const [item] = next.splice(index, 1);
    next.splice(index + delta, 0, item);
    onChange(next);
  };

  const generate = async (format: ReportFormat) => {
    setBusy(format);
    setError(null);
    try {
      await onGenerate(title.trim(), notes, includeSummary, format);
    } catch (e) {
      setError(await errorMessage(e));
    } finally {
      setBusy(null);
    }
  };

  const ready = items.length > 0 && title.trim().length > 0 && busy === null;

  return (
    <Drawer anchor="right" open={open} onClose={onClose} slotProps={{ paper: { sx: { width: { xs: "100%", sm: 460 } } } }}>
      <Stack sx={{ p: 2, height: "100%" }} spacing={1.5}>
        <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between" }}>
          <Typography sx={{ fontSize: 16, fontWeight: 700 }}>{t("controlChart.reportBuilder.titleWithCount", { count: items.length })}</Typography>
          <IconButton aria-label={t("controlChart.reportBuilder.closeAria")} onClick={onClose}><CloseIcon /></IconButton>
        </Stack>
        <TextField size="small" label={t("controlChart.reportBuilder.titleLabel")} required value={title} onChange={(e) => setTitle(e.target.value)} />
        <TextField size="small" label={t("controlChart.reportBuilder.notesLabel")} multiline minRows={2} value={notes} onChange={(e) => setNotes(e.target.value)}
          placeholder={t("controlChart.reportBuilder.notesPlaceholder")} />
        <FormControlLabel label={<Typography sx={{ fontSize: 13 }}>{t("controlChart.reportBuilder.includeSummary")}</Typography>}
          control={<Checkbox size="small" checked={includeSummary} onChange={(e) => setIncludeSummary(e.target.checked)} />} />
        {error && <Alert severity="error">{error}</Alert>}
        <Divider />

        <Box sx={{ flex: 1, overflowY: "auto" }}>
          {items.length === 0 ? (
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              {t("controlChart.reportBuilder.empty")}
            </Typography>
          ) : (
            <Stack spacing={1.5}>
              {items.map((item, i) => (
                <Box key={item.id} sx={{ border: 1, borderColor: "divider", borderRadius: 1, p: 1 }}>
                  <Stack direction="row" spacing={1} sx={{ alignItems: "flex-start" }}>
                    <Box component="img" src={item.image} alt={item.title}
                      sx={{ width: 120, height: 64, objectFit: "contain", bgcolor: "#fff", border: 1, borderColor: "divider", flexShrink: 0 }} />
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                      <TextField size="small" fullWidth value={item.title}
                        onChange={(e) => onChange(items.map((x) => (x.id === item.id ? { ...x, title: e.target.value } : x)))}
                        slotProps={{ htmlInput: { "aria-label": t("controlChart.reportBuilder.chartTitleAria", { index: i + 1 }) } }} />
                      <Typography sx={{ fontSize: 11, color: "text.secondary", mt: 0.5 }} noWrap>
                        {i + 1}. {t(`controlChart.chartKinds.${item.chartKind}.label`, { defaultValue: item.chartLabel })} · {item.testDisplayName} · {item.subjectName}
                      </Typography>
                    </Box>
                    <Stack>
                      <Tooltip title={t("controlChart.reportBuilder.moveUp")}><span><IconButton size="small" disabled={i === 0} onClick={() => move(i, -1)} aria-label={t("controlChart.reportBuilder.moveUp")}><ArrowUpwardIcon fontSize="small" /></IconButton></span></Tooltip>
                      <Tooltip title={t("controlChart.reportBuilder.moveDown")}><span><IconButton size="small" disabled={i === items.length - 1} onClick={() => move(i, 1)} aria-label={t("controlChart.reportBuilder.moveDown")}><ArrowDownwardIcon fontSize="small" /></IconButton></span></Tooltip>
                      <Tooltip title={t("controlChart.reportBuilder.remove")}><IconButton size="small" onClick={() => onChange(items.filter((x) => x.id !== item.id))} aria-label={t("controlChart.reportBuilder.remove")}><DeleteOutlineIcon fontSize="small" /></IconButton></Tooltip>
                    </Stack>
                  </Stack>
                </Box>
              ))}
            </Stack>
          )}
        </Box>

        <Divider />
        <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
          {t("controlChart.reportBuilder.footerNote")}
        </Typography>
        <Stack direction="row" spacing={1}>
          <Button fullWidth variant="contained" startIcon={<PictureAsPdfIcon />} disabled={!ready} onClick={() => generate("Pdf")}>
            {busy === "Pdf" ? t("controlChart.reportBuilder.generating") : t("controlChart.reportBuilder.downloadPdf")}
          </Button>
          <Button fullWidth variant="outlined" startIcon={<DescriptionIcon />} disabled={!ready} onClick={() => generate("Word")}>
            {busy === "Word" ? t("controlChart.reportBuilder.generating") : t("controlChart.reportBuilder.downloadWord")}
          </Button>
        </Stack>
        <Button size="small" color="inherit" disabled={items.length === 0 || busy !== null} onClick={() => onChange([])}>{t("controlChart.reportBuilder.clearAll")}</Button>
      </Stack>
    </Drawer>
  );
}
