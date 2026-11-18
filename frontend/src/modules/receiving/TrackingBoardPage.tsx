import { useEffect, useState } from "react";
import {
  Box,
  Paper,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  TablePagination,
  Typography,
  Chip,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  TextField,
  Autocomplete,
  Button,
  useTheme
} from "@mui/material";
import FilterAltOffIcon from "@mui/icons-material/FilterAltOff";

import { PageHeader } from "../../components/PageHeader";
import { TableSkeleton } from "../../components/TableSkeleton";
import { Ltr } from "../../components/Ltr";
import { useTranslation } from "../../i18n";
import { tableHeadSx } from "../../theme";
import { masterDataOptions } from "../../services/masterDataOptions";
import { TrackingService, TrackingRow } from "./services/TrackingService";
import { SampleSummaryDialog } from "../testingWorkspace/SampleSummaryDialog";
import { DateField } from "../../components/DateField";
import { visuallyHidden } from "@mui/utils";

// Matched on section Code, never Id: Ids are database-assigned and differ
// between installations. Codes are never renamed, only labels can change
// (e.g. the FP rename to Physicochemical Laboratory).
const MICRO_SECTION_CODE = "MICRO";
const FP_SECTION_CODE = "FP";

const OVERALL_VALUES = ["ALL", "InProgress", "Approved", "Rejected", "RetestRequested", "Voided", "Cancelled"] as const;
const LAB_VALUES = ["ALL", MICRO_SECTION_CODE, FP_SECTION_CODE] as const;

function overallChipColor(status: string): "error" | "success" | "info" | "default" {
  if (status === "Rejected") return "error";
  if (status === "Approved") return "success";
  if (status === "InProgress") return "info";
  return "default";
}

function LabStageCell({ row, sectionCode }: { row: TrackingRow; sectionCode: string }) {
  const theme = useTheme();
  const { t } = useTranslation("receiving");
  const lab = row.labs.find((l) => l.sectionCode.toUpperCase() === sectionCode);
  if (!lab) {
    // A lab the sample never went to is an absence, not an event: a quiet
    // dash keeps whole columns of it from out-shouting the real stages.
    // The words stay available to screen readers and on hover.
    return (
      <Box component="span" title={t("tracking.notRequested")} sx={{ color: "text.disabled", fontSize: 14 }}>
        <span aria-hidden="true">—</span>
        <Box component="span" sx={visuallyHidden}>{t("tracking.notRequested")}</Box>
      </Box>
    );
  }
  const tone =
    lab.stage === "Approved" ? theme.custom.status.notDetected :
    lab.stage === "Rejected" || lab.stage === "Voided" ? theme.custom.status.detected :
    lab.stage === "RetestRequested" ? theme.custom.status.action :
    theme.custom.status.info;
  return (
    <Box
      component="span"
      sx={{
        display: "inline-block",
        px: 1,
        py: 0.25,
        borderRadius: 5,
        fontSize: 12,
        fontWeight: 700,
        color: tone.text,
        bgcolor: tone.bg,
        border: `1px solid ${tone.border}`
      }}
    >
      {t(`tracking.stages.${lab.stage}`, { defaultValue: lab.stage.replace(/([a-z])([A-Z])/g, "$1 $2") })}
    </Box>
  );
}

// Cross-laboratory tracking board (Samples.TrackAll) - design.md §3.4:
// one row per sample, its overall status, and each laboratory's own stage.
export function TrackingBoardPage() {
  const theme = useTheme();
  const { t, i18n } = useTranslation("receiving");
  const dateLocale = i18n.language === "ar" ? "ar-u-nu-latn-ca-gregory" : "en-GB";
  const [rows, setRows] = useState<TrackingRow[] | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);

  const [labFilter, setLabFilter] = useState("ALL");
  const [overallFilter, setOverallFilter] = useState("ALL");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [items, setItems] = useState<{ id: number; name: string }[]>([]);
  const [itemsLoadFailed, setItemsLoadFailed] = useState(false);
  const [productFilter, setProductFilter] = useState<{ id: number; name: string } | null>(null);

  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(25);
  const [summarySampleId, setSummarySampleId] = useState<number | null>(null);

  useEffect(() => {
    masterDataOptions.getItems().then(setItems).catch(() => {
      setItems([]);
      setItemsLoadFailed(true);
    });
  }, []);

  const loadRows = async () => {
    setLoading(true);
    try {
      const result = await TrackingService.getTracking({
        labSectionCode: labFilter === "ALL" ? undefined : labFilter,
        overall: overallFilter === "ALL" ? undefined : overallFilter,
        from: fromDate || undefined,
        to: toDate || undefined,
        itemId: productFilter?.id,
        page: page + 1,
        pageSize
      });
      setRows(result.items);
      setTotalCount(result.totalCount);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadRows();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [labFilter, overallFilter, fromDate, toDate, productFilter, page, pageSize]);

  const hasActiveFilters = labFilter !== "ALL" || overallFilter !== "ALL" || Boolean(fromDate) || Boolean(toDate) || Boolean(productFilter);

  const resetFilters = () => {
    setLabFilter("ALL");
    setOverallFilter("ALL");
    setFromDate("");
    setToDate("");
    setProductFilter(null);
    setPage(0);
  };

  const formatReceivedDate = (d: string) => {
    try {
      return new Date(d).toLocaleDateString(dateLocale, { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" });
    } catch {
      return d;
    }
  };

  return (
    <Box>
      <PageHeader
        title={t("tracking.title")}
        subtitle={t("tracking.subtitle")}
      />

      <Paper elevation={0} sx={{ p: 2, mb: 2.5, border: "1px solid", borderColor: "divider", borderRadius: 2, bgcolor: "background.paper" }}>
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(5, 1fr) auto" },
            gap: 1.5,
            alignItems: "center"
          }}
        >
          <FormControl size="small" fullWidth>
            <InputLabel id="lab-filter-label">{t("tracking.labFilter")}</InputLabel>
            <Select
              labelId="lab-filter-label"
              label={t("tracking.labFilter")}
              value={labFilter}
              onChange={(e) => { setLabFilter(e.target.value); setPage(0); }}
            >
              {LAB_VALUES.map((val) => (
                <MenuItem key={val} value={val}>{t(`tracking.labOptions.${val}`)}</MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl size="small" fullWidth>
            <InputLabel id="overall-filter-label">{t("tracking.overallFilter")}</InputLabel>
            <Select
              labelId="overall-filter-label"
              label={t("tracking.overallFilter")}
              value={overallFilter}
              onChange={(e) => { setOverallFilter(e.target.value); setPage(0); }}
            >
              {OVERALL_VALUES.map((val) => (
                <MenuItem key={val} value={val}>{t(`tracking.overallOptions.${val}`)}</MenuItem>
              ))}
            </Select>
          </FormControl>

          <Autocomplete
            size="small"
            options={items}
            getOptionLabel={(o) => o.name}
            value={productFilter}
            onChange={(_, v) => { setProductFilter(v); setPage(0); }}
            isOptionEqualToValue={(a, b) => a.id === b.id}
            noOptionsText={itemsLoadFailed ? t("tracking.itemsLoadFailed") : undefined}
            renderInput={(params) => (
              <TextField
                {...params}
                label={t("tracking.productItem")}
                error={itemsLoadFailed}
                helperText={itemsLoadFailed ? t("tracking.itemsLoadFailedHelper") : undefined}
              />
            )}
          />

          <DateField
            label={t("tracking.receivedFrom")}
            value={fromDate}
            onChange={(v) => { setFromDate(v); setPage(0); }}
            max={toDate}
            fullWidth
          />

          <DateField
            label={t("tracking.receivedTo")}
            value={toDate}
            onChange={(v) => { setToDate(v); setPage(0); }}
            min={fromDate}
            fullWidth
          />

          {hasActiveFilters && (
            <Button
              variant="outlined"
              size="small"
              color="inherit"
              onClick={resetFilters}
              startIcon={<FilterAltOffIcon sx={{ fontSize: 18 }} />}
              sx={{ height: 40, borderColor: "divider", color: "text.secondary", whiteSpace: "nowrap" }}
            >
              {t("tracking.resetFilters")}
            </Button>
          )}
        </Box>
      </Paper>

      {rows === null ? (
        <TableSkeleton rows={8} cols={7} />
      ) : (
        <Paper elevation={0} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, overflow: "hidden", bgcolor: "background.paper", opacity: loading ? 0.6 : 1, transition: "opacity 0.15s" }}>
          <Box sx={{ overflowX: "auto", maxHeight: { md: "calc(100vh - 240px)" } }}>
            <Table size="small" stickyHeader sx={{ minWidth: 960 }}>
              <TableHead sx={tableHeadSx}>
                <TableRow>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 140 }}>{t("tracking.columns.reference")}</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 180 }}>{t("tracking.columns.product")}</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 120 }}>{t("tracking.columns.batch")}</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 140 }}>{t("tracking.columns.received")}</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 120 }}>{t("tracking.columns.overall")}</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 140 }}>{t("tracking.columns.microbiology")}</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 140 }}>{t("tracking.columns.physicochemical")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} align="center" sx={{ py: 6 }}>
                      <Typography sx={{ color: "text.secondary", fontSize: 14 }}>
                        {t("tracking.empty")}
                      </Typography>
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((row) => (
                    <TableRow
                      key={row.sampleId}
                      hover
                      onClick={() => setSummarySampleId(row.sampleId)}
                      sx={{ cursor: "pointer", "&:last-child td, &:last-child th": { border: 0 } }}
                    >
                      <TableCell>
                        <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main }}>
                          <Ltr>{row.referenceNumber}</Ltr>
                        </Typography>
                        <Typography sx={{ fontSize: 12, color: "text.secondary" }}><Ltr>#{row.sampleId}</Ltr></Typography>
                      </TableCell>
                      <TableCell sx={{ fontSize: 12 }}>{row.product || "—"}</TableCell>
                      <TableCell sx={{ fontSize: 12 }}>{row.batchNumber ? <Ltr>{row.batchNumber}</Ltr> : "—"}</TableCell>
                      <TableCell sx={{ fontSize: 12, color: "text.secondary", whiteSpace: "nowrap" }}>
                        <Ltr>{formatReceivedDate(row.receivedAt)}</Ltr>
                      </TableCell>
                      <TableCell>
                        <Chip
                          size="small"
                          label={t(`tracking.overallOptions.${row.overallStatus}`, { defaultValue: row.overallStatus })}
                          color={overallChipColor(row.overallStatus)}
                          sx={{ fontSize: 12, fontWeight: 700 }}
                        />
                      </TableCell>
                      <TableCell>
                        <LabStageCell row={row} sectionCode={MICRO_SECTION_CODE} />
                      </TableCell>
                      <TableCell>
                        <LabStageCell row={row} sectionCode={FP_SECTION_CODE} />
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </Box>

          <TablePagination
            component="div"
            count={totalCount}
            page={page}
            rowsPerPage={pageSize}
            onPageChange={(_, newPage) => setPage(newPage)}
            onRowsPerPageChange={(e) => { setPageSize(parseInt(e.target.value, 10)); setPage(0); }}
            rowsPerPageOptions={[25, 50, 100, 200]}
            sx={{ borderTop: "1px solid", borderColor: "divider" }}
          />
        </Paper>
      )}

      <SampleSummaryDialog
        open={Boolean(summarySampleId)}
        sampleId={summarySampleId}
        onClose={() => {
          setSummarySampleId(null);
          loadRows();
        }}
      />
    </Box>
  );
}
