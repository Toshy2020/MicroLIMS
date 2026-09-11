import { monospaceFontFamily } from "../../../theme/palette";
import { compactChipSx, documentCodeSx } from "../documentControlStyles";
import { useState, useEffect, useCallback } from "react";
import {
  Box,
  Typography,
  Paper,
  Button,
  TextField,
  MenuItem,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TablePagination,
  Chip,
  Alert,
  CircularProgress,
  useTheme
} from "@mui/material";
import FileDownloadOutlinedIcon from "@mui/icons-material/FileDownloadOutlined";
import FilterAltOutlinedIcon from "@mui/icons-material/FilterAltOutlined";
import ClearIcon from "@mui/icons-material/Clear";

import { PageHeader } from "../../../components/PageHeader";
import { tableHeadSx } from "../../../theme";
import { documentControlService } from "../services/documentControlService";
import { toast } from "sonner";
import type {
  DocumentAuditItemDto,
  AuditActionCategory
} from "../types/documentControlTypes";

export function DocumentAuditPage() {
  const theme = useTheme();
  const [logs, setLogs] = useState<DocumentAuditItemDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [exportSuccessMsg, setExportSuccessMsg] = useState<string | null>(null);

  // Filters
  const [searchTerm, setSearchTerm] = useState("");
  const [actionCategory, setActionCategory] = useState<AuditActionCategory | "">("");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(20);

  const fetchLogs = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await documentControlService.searchAudit({
        searchTerm: searchTerm.trim() || undefined,
        actionCategory: actionCategory !== "" ? actionCategory : undefined,
        dateFrom: dateFrom ? new Date(dateFrom).toISOString() : undefined,
        dateTo: dateTo ? new Date(dateTo).toISOString() : undefined,
        page: page + 1,
        pageSize
      });
      setLogs(res.items);
      setTotalCount(res.totalCount);
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to load audit trail.");
    } finally {
      setLoading(false);
    }
  }, [searchTerm, actionCategory, dateFrom, dateTo, page, pageSize]);

  useEffect(() => {
    fetchLogs();
  }, [fetchLogs]);

  const handleClear = () => {
    setSearchTerm("");
    setActionCategory("");
    setDateFrom("");
    setDateTo("");
    setPage(0);
  };

  const handleExport = async () => {
    setExporting(true);
    setExportSuccessMsg(null);
    try {
      await documentControlService.exportAuditCsv({
        searchTerm: searchTerm.trim() || undefined,
        actionCategory: actionCategory !== "" ? actionCategory : undefined,
        dateFrom: dateFrom ? new Date(dateFrom).toISOString() : undefined,
        dateTo: dateTo ? new Date(dateTo).toISOString() : undefined
      });
      setExportSuccessMsg("Audit trail exported successfully. (The export operation has been logged in the audit trail per 21 CFR Part 11).");
      fetchLogs(); // refresh to show the export audit event
    } catch (err: any) {
      toast.error("Failed to export audit trail: " + (err.response?.data?.message || err.message));
    } finally {
      setExporting(false);
    }
  };

  return (
    <Box sx={{ pb: 4 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
        <PageHeader
          title="Document Control Audit Trail"
          subtitle="Chronological, append-only regulatory audit log of document life-cycle actions and configuration changes"
        />
        <Button
          variant="outlined"
          startIcon={<FileDownloadOutlinedIcon />}
          onClick={handleExport}
          disabled={exporting || loading}
        >
          {exporting ? "Exporting CSV..." : "Export Audit Trail (CSV)"}
        </Button>
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {exportSuccessMsg && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setExportSuccessMsg(null)}>{exportSuccessMsg}</Alert>}

      {/* Filter Panel */}
      <Paper sx={{ p: 2, mb: 3 }}>
        <Box sx={{ display: "flex", flexWrap: "wrap", gap: 2, alignItems: "center" }}>
          <TextField
            size="small"
            placeholder="Search Event UID, Action, Reason, Entity..."
            value={searchTerm}
            onChange={(e) => {
              setSearchTerm(e.target.value);
              setPage(0);
            }}
            sx={{ minWidth: 260, flexGrow: 1 }}
          />

          <TextField
            select
            size="small"
            label="Action Category"
            value={actionCategory}
            onChange={(e) => {
              setActionCategory(e.target.value as AuditActionCategory | "");
              setPage(0);
            }}
            sx={{ minWidth: 160 }}
          >
            <MenuItem value="">All Categories</MenuItem>
            <MenuItem value="Document">Document</MenuItem>
            <MenuItem value="Workflow">Workflow</MenuItem>
            <MenuItem value="Security">Security</MenuItem>
            <MenuItem value="Configuration">Configuration</MenuItem>
            <MenuItem value="System">System</MenuItem>
          </TextField>

          <TextField
            size="small"
            label="Date From"
            type="date"
            InputLabelProps={{ shrink: true }}
            value={dateFrom}
            onChange={(e) => {
              setDateFrom(e.target.value);
              setPage(0);
            }}
          />

          <TextField
            size="small"
            label="Date To"
            type="date"
            InputLabelProps={{ shrink: true }}
            value={dateTo}
            onChange={(e) => {
              setDateTo(e.target.value);
              setPage(0);
            }}
          />

          <Button
            size="small"
            color="inherit"
            startIcon={<ClearIcon />}
            onClick={handleClear}
            disabled={!searchTerm && actionCategory === "" && !dateFrom && !dateTo}
          >
            Reset
          </Button>
        </Box>
      </Paper>

      {/* Audit Log Table */}
      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead sx={tableHeadSx(theme)}>
            <TableRow>
              <TableCell sx={{ fontWeight: 700 }}>Timestamp (UTC)</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Event UID</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Actor</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Action Code</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Category</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Record Type</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Reason / Description</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Field Changes</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={8} align="center" sx={{ py: 6 }}>
                  <CircularProgress size={32} />
                  <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                    Loading audit trail records...
                  </Typography>
                </TableCell>
              </TableRow>
            ) : logs.length === 0 ? (
              <TableRow>
                <TableCell colSpan={8} align="center" sx={{ py: 6 }}>
                  <FilterAltOutlinedIcon sx={{ fontSize: 40, color: "text.disabled", mb: 1 }} />
                  <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                    No audit records match the selected criteria
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              logs.map((log) => (
                <TableRow
                  key={log.id}
                  hover
                  sx={{
                    bgcolor: log.actionCategory === "Security" ? "rgba(244, 67, 54, 0.04)" : "inherit"
                  }}
                >
                  <TableCell sx={{ whiteSpace: "nowrap" }}>
                    {new Date(log.timestamp).toLocaleString()}
                  </TableCell>
                  <TableCell sx={documentCodeSx}>
                    {log.eventUid ? log.eventUid.substring(0, 16) : `#${log.id}`}
                  </TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>
                    {log.userName || log.systemProcessName || "System"}
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {log.actionCode || log.action}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Chip
                      label={log.actionCategory}
                      size="small"
                      color={log.actionCategory === "Security" ? "error" : "default"}
                      variant="outlined"
                      sx={compactChipSx}
                    />
                  </TableCell>
                  <TableCell>
                    <Typography variant="caption" sx={{ fontFamily: monospaceFontFamily }}>
                      {log.recordType} {log.entityId ? `(#${log.entityId})` : ""}
                    </Typography>
                  </TableCell>
                  <TableCell sx={{ maxWidth: 260 }}>
                    <Typography variant="caption" color="text.secondary">
                      {log.reason || "—"}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    {log.changes.length > 0 ? (
                      <Box sx={{ display: "flex", flexDirection: "column", gap: 0.25 }}>
                        {log.changes.map((c, i) => (
                          <Typography key={i} variant="caption" sx={{ fontFamily: monospaceFontFamily }}>
                            <strong>{c.fieldName}</strong>: {c.previousValue ? `"${c.previousValue}"` : "null"} → "{c.newValue}"
                          </Typography>
                        ))}
                      </Box>
                    ) : (
                      <Typography variant="caption" color="text.secondary">—</Typography>
                    )}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>

        <TablePagination
          component="div"
          count={totalCount}
          page={page}
          onPageChange={(_, newPage) => setPage(newPage)}
          rowsPerPage={pageSize}
          onRowsPerPageChange={(e) => {
            setPageSize(parseInt(e.target.value, 10));
            setPage(0);
          }}
          rowsPerPageOptions={[10, 20, 50, 100]}
        />
      </TableContainer>
    </Box>
  );
}
