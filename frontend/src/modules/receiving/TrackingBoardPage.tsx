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
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { tableHeadSx } from "../../theme";
import { masterDataOptions } from "../../services/masterDataOptions";
import { TrackingService, TrackingRow } from "./services/TrackingService";
import { SampleSummaryDialog } from "../testingWorkspace/SampleSummaryDialog";

// Fixed ids per env.md - section Codes are never renamed, only labels can
// change (e.g. the FP rename to Physicochemical Laboratory).
const MICRO_SECTION_ID = 1;
const FP_SECTION_ID = 2;

const OVERALL_OPTIONS = [
  { value: "ALL", label: "All Overall Statuses" },
  { value: "InProgress", label: "In Progress" },
  { value: "Approved", label: "Approved" },
  { value: "Rejected", label: "Rejected" },
  { value: "RetestRequested", label: "Retest Requested" },
  { value: "Voided", label: "Voided" },
  { value: "Cancelled", label: "Cancelled" }
];

const LAB_OPTIONS = [
  { value: "ALL", label: "All Laboratories" },
  { value: String(MICRO_SECTION_ID), label: "Microbiology Laboratory" },
  { value: String(FP_SECTION_ID), label: "Physicochemical Laboratory" }
];

function overallChipColor(status: string): "error" | "success" | "info" | "default" {
  if (status === "Rejected") return "error";
  if (status === "Approved") return "success";
  if (status === "InProgress") return "info";
  return "default";
}

function LabStageCell({ row, sectionId }: { row: TrackingRow; sectionId: number }) {
  const theme = useTheme();
  const lab = row.labs.find((l) => l.sectionId === sectionId);
  if (!lab) {
    return (
      <Chip
        size="small"
        label="Not requested"
        variant="outlined"
        sx={{ fontSize: 11, color: "text.disabled", borderColor: "divider" }}
      />
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
        fontSize: 11,
        fontWeight: 700,
        color: tone.text,
        bgcolor: tone.bg,
        border: `1px solid ${tone.border}`
      }}
    >
      {lab.stage.replace(/([a-z])([A-Z])/g, "$1 $2")}
    </Box>
  );
}

// Cross-laboratory tracking board (Samples.TrackAll) - design.md §3.4:
// one row per sample, its overall status, and each laboratory's own stage.
export function TrackingBoardPage() {
  const theme = useTheme();
  const [rows, setRows] = useState<TrackingRow[] | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);

  const [labFilter, setLabFilter] = useState("ALL");
  const [overallFilter, setOverallFilter] = useState("ALL");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [items, setItems] = useState<{ id: number; name: string }[]>([]);
  const [productFilter, setProductFilter] = useState<{ id: number; name: string } | null>(null);

  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(25);
  const [summarySampleId, setSummarySampleId] = useState<number | null>(null);

  useEffect(() => {
    masterDataOptions.getItems().then(setItems).catch(() => setItems([]));
  }, []);

  const loadRows = async () => {
    setLoading(true);
    try {
      const result = await TrackingService.getTracking({
        labSectionId: labFilter === "ALL" ? undefined : Number(labFilter),
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
      return new Date(d).toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" });
    } catch {
      return d;
    }
  };

  return (
    <Box>
      <PageHeader
        title="Tracking Board"
        subtitle="Every received sample across both laboratories, with each lab's own stage."
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
            <InputLabel id="lab-filter-label">Laboratory</InputLabel>
            <Select
              labelId="lab-filter-label"
              label="Laboratory"
              value={labFilter}
              onChange={(e) => { setLabFilter(e.target.value); setPage(0); }}
            >
              {LAB_OPTIONS.map((o) => (
                <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl size="small" fullWidth>
            <InputLabel id="overall-filter-label">Overall Status</InputLabel>
            <Select
              labelId="overall-filter-label"
              label="Overall Status"
              value={overallFilter}
              onChange={(e) => { setOverallFilter(e.target.value); setPage(0); }}
            >
              {OVERALL_OPTIONS.map((o) => (
                <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>
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
            renderInput={(params) => <TextField {...params} label="Product / Item" />}
          />

          <TextField
            size="small"
            type="date"
            label="Received From"
            value={fromDate}
            onChange={(e) => { setFromDate(e.target.value); setPage(0); }}
            fullWidth
            slotProps={{ inputLabel: { shrink: true } }}
          />

          <TextField
            size="small"
            type="date"
            label="Received To"
            value={toDate}
            onChange={(e) => { setToDate(e.target.value); setPage(0); }}
            fullWidth
            slotProps={{ inputLabel: { shrink: true } }}
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
              Reset Filters
            </Button>
          )}
        </Box>
      </Paper>

      {rows === null ? (
        <LoadingSpinner />
      ) : (
        <Paper elevation={0} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, overflow: "hidden", bgcolor: "background.paper", opacity: loading ? 0.6 : 1, transition: "opacity 0.15s" }}>
          <Box sx={{ overflowX: "auto" }}>
            <Table size="small" sx={{ minWidth: 960 }}>
              <TableHead sx={tableHeadSx}>
                <TableRow>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 140 }}>Reference</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 180 }}>Product</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 120 }}>Batch</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 140 }}>Received</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 120 }}>Overall</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 140 }}>Microbiology</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 140 }}>Physicochemical</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} align="center" sx={{ py: 6 }}>
                      <Typography sx={{ color: "text.secondary", fontSize: 14 }}>
                        No samples found matching the filter criteria.
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
                          {row.referenceNumber}
                        </Typography>
                        <Typography sx={{ fontSize: 11, color: "text.secondary" }}>#{row.sampleId}</Typography>
                      </TableCell>
                      <TableCell sx={{ fontSize: 12 }}>{row.product || "—"}</TableCell>
                      <TableCell sx={{ fontSize: 12 }}>{row.batchNumber || "—"}</TableCell>
                      <TableCell sx={{ fontSize: 12, color: "text.secondary", whiteSpace: "nowrap" }}>
                        {formatReceivedDate(row.receivedAt)}
                      </TableCell>
                      <TableCell>
                        <Chip size="small" label={row.overallStatus} color={overallChipColor(row.overallStatus)} sx={{ fontSize: 11, fontWeight: 700 }} />
                      </TableCell>
                      <TableCell>
                        <LabStageCell row={row} sectionId={MICRO_SECTION_ID} />
                      </TableCell>
                      <TableCell>
                        <LabStageCell row={row} sectionId={FP_SECTION_ID} />
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
