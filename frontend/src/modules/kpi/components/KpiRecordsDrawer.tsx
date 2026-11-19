import { useEffect, useState } from "react";
import {
  Alert, Drawer, IconButton, LinearProgress, Stack, Table, TableBody, TableCell, TableHead, TableRow,
  TablePagination, Typography
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import type { KpiRecordPage } from "../types/kpiTypes";
import { formatKpiValue } from "../utils/kpiFormat";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { useTranslation } from "../../../i18n";

const PAGE_SIZE = 50;

interface KpiRecordsDrawerProps {
  title: string;
  onClose(): void;
  // Memoised by the caller (it changes only when a different figure is chosen).
  load(page: number, pageSize: number): Promise<KpiRecordPage>;
}

// The records behind one figure. The parent mounts this only while a figure is
// selected, so choosing another figure starts again on page 1.
export function KpiRecordsDrawer({ title, onClose, load }: KpiRecordsDrawerProps) {
  const { t } = useTranslation("kpi");
  const [page, setPage] = useState(0); // 0-based for TablePagination; the server pages from 1
  const [data, setData] = useState<KpiRecordPage | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let live = true;
    setLoading(true);
    setError(null);
    load(page + 1, PAGE_SIZE)
      .then((d) => { if (live) setData(d); })
      .catch((e: unknown) => {
        if (live) {
          setData(null);
          const err = e as { response?: { data?: { message?: string } } } | undefined;
          setError(err?.response?.data?.message ?? t("records.loadError"));
        }
      })
      .finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [load, page, t]);

  return (
    <Drawer anchor="right" open onClose={onClose} slotProps={{ paper: { sx: { width: { xs: "100%", md: 760 }, p: 2 } } }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
        <Typography component="h2" sx={{ fontSize: 18, fontWeight: 700 }}>{title}</Typography>
        <IconButton aria-label={t("records.close")} onClick={onClose}><CloseIcon /></IconButton>
      </Stack>
      {loading && <LinearProgress sx={{ mb: 1 }} />}
      {error && <Alert severity="error">{error}</Alert>}
      {data && (data.items.length === 0 ? (
        <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{t("records.none")}</Typography>
      ) : (
        <>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>{t("records.columns.controlNo")}</TableCell>
                <TableCell>{t("records.columns.test")}</TableCell>
                <TableCell>{t("records.columns.category")}</TableCell>
                <TableCell>{t("records.columns.analyst")}</TableCell>
                <TableCell>{t("records.columns.event")}</TableCell>
                <TableCell align="right">{t("records.columns.value")}</TableCell>
                <TableCell>{t("records.columns.inFigure")}</TableCell>
                <TableCell>{t("records.columns.note")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data.items.map((r, i) => (
                <TableRow key={`${r.testOrderId ?? r.sampleId ?? "r"}-${i}`}>
                  <TableCell>{r.controlNumber ?? "—"}</TableCell>
                  <TableCell>{r.testCode ?? "—"}</TableCell>
                  <TableCell>{r.category ?? "—"}</TableCell>
                  <TableCell>{r.analystName ?? "—"}</TableCell>
                  <TableCell>{r.eventAt ? formatDateTimeWithTimeZone(r.eventAt) : "—"}</TableCell>
                  <TableCell align="right">{r.value == null ? "—" : r.unit ? formatKpiValue(r.unit, r.value, t) : String(r.value)}</TableCell>
                  <TableCell>{r.inclusion}</TableCell>
                  <TableCell>{r.note ?? ""}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <TablePagination
            component="div"
            count={data.total}
            page={page}
            rowsPerPage={PAGE_SIZE}
            rowsPerPageOptions={[PAGE_SIZE]}
            onPageChange={(_, p) => setPage(p)}
          />
        </>
      ))}
    </Drawer>
  );
}
