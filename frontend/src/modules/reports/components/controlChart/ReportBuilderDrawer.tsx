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
import { ReportFormat, ReportItem } from "../../types/controlChartTypes";

interface ReportBuilderDrawerProps {
  open: boolean;
  items: ReportItem[];
  onClose: () => void;
  onChange: (items: ReportItem[]) => void;
  onGenerate: (title: string, notes: string, includeSummary: boolean, format: ReportFormat) => Promise<void>;
}

// An error from a blob request arrives as a Blob, not parsed JSON.
export async function errorMessage(e: any): Promise<string> {
  const data = e?.response?.data;
  if (data instanceof Blob) {
    try {
      return JSON.parse(await data.text())?.message ?? "The report could not be generated.";
    } catch {
      return "The report could not be generated.";
    }
  }
  return data?.message ?? e?.message ?? "The report could not be generated.";
}

// The charts queued for one combined report: retitle, reorder or remove
// them, then generate a single PDF or Word document on the server.
export function ReportBuilderDrawer({ open, items, onClose, onChange, onGenerate }: ReportBuilderDrawerProps) {
  const [title, setTitle] = useState("Control chart report");
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
          <Typography sx={{ fontSize: 16, fontWeight: 700 }}>Report ({items.length} chart{items.length === 1 ? "" : "s"})</Typography>
          <IconButton aria-label="Close report" onClick={onClose}><CloseIcon /></IconButton>
        </Stack>
        <TextField size="small" label="Report title" required value={title} onChange={(e) => setTitle(e.target.value)} />
        <TextField size="small" label="Notes (optional)" multiline minRows={2} value={notes} onChange={(e) => setNotes(e.target.value)}
          placeholder="e.g. Annual EM limit review 2026 per SOP QC-EM-012" />
        <FormControlLabel label={<Typography sx={{ fontSize: 13 }}>Summary table of all charts at the start</Typography>}
          control={<Checkbox size="small" checked={includeSummary} onChange={(e) => setIncludeSummary(e.target.checked)} />} />
        {error && <Alert severity="error">{error}</Alert>}
        <Divider />

        <Box sx={{ flex: 1, overflowY: "auto" }}>
          {items.length === 0 ? (
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              No charts yet. Use “Add to report” on a chart, or “Add all to report” in the limit review.
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
                        slotProps={{ htmlInput: { "aria-label": `Title of chart ${i + 1}` } }} />
                      <Typography sx={{ fontSize: 11, color: "text.secondary", mt: 0.5 }} noWrap>
                        {i + 1}. {item.chartLabel} · {item.testDisplayName} · {item.subjectName}
                      </Typography>
                    </Box>
                    <Stack>
                      <Tooltip title="Move up"><span><IconButton size="small" disabled={i === 0} onClick={() => move(i, -1)} aria-label="Move up"><ArrowUpwardIcon fontSize="small" /></IconButton></span></Tooltip>
                      <Tooltip title="Move down"><span><IconButton size="small" disabled={i === items.length - 1} onClick={() => move(i, 1)} aria-label="Move down"><ArrowDownwardIcon fontSize="small" /></IconButton></span></Tooltip>
                      <Tooltip title="Remove"><IconButton size="small" onClick={() => onChange(items.filter((x) => x.id !== item.id))} aria-label="Remove"><DeleteOutlineIcon fontSize="small" /></IconButton></Tooltip>
                    </Stack>
                  </Stack>
                </Box>
              ))}
            </Stack>
          )}
        </Box>

        <Divider />
        <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
          The server recalculates every figure in the report from each chart's own settings; the report is recorded in the data export log.
        </Typography>
        <Stack direction="row" spacing={1}>
          <Button fullWidth variant="contained" startIcon={<PictureAsPdfIcon />} disabled={!ready} onClick={() => generate("Pdf")}>
            {busy === "Pdf" ? "Generating..." : "Download PDF"}
          </Button>
          <Button fullWidth variant="outlined" startIcon={<DescriptionIcon />} disabled={!ready} onClick={() => generate("Word")}>
            {busy === "Word" ? "Generating..." : "Download Word"}
          </Button>
        </Stack>
        <Button size="small" color="inherit" disabled={items.length === 0 || busy !== null} onClick={() => onChange([])}>Clear all charts</Button>
      </Stack>
    </Drawer>
  );
}
