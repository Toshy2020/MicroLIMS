import { useCallback, useEffect, useRef, useState } from "react";
import { Box, Alert, Button, Tab, Tabs } from "@mui/material";
import FileDownloadOutlinedIcon from "@mui/icons-material/FileDownloadOutlined";
import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { TableSkeleton } from "../../components/TableSkeleton";
import { AuditHistoryDialog } from "../../components/AuditHistoryDialog";
import { useAuth } from "../../contexts/AuthContext";
import { AuditSearchService } from "./services/AuditSearchService";
import type { AuditLogItem, AuditSearchFilterState } from "./types/auditTypes";
import { SECURITY_AUDIT_PERMISSION } from "./types/auditTypes";
import { AuditFilterBar } from "./components/AuditFilterBar";
import { AuditResultsTable } from "./components/AuditResultsTable";
import { AuditEventDrawer } from "./components/AuditEventDrawer";
import { SecurityTrailPanel } from "./components/SecurityTrailPanel";
import { INITIAL_AUDIT_FILTERS, buildAuditPayload } from "./utils/auditPayload";

type TabKey = "audit" | "security-events" | "login-history";

export function AuditSearchPage() {
  const { permissions } = useAuth();
  const canViewSecurityAudit = permissions.includes(SECURITY_AUDIT_PERMISSION);

  const [tab, setTab] = useState<TabKey>("audit");
  const [filters, setFilters] = useState<AuditSearchFilterState>(INITIAL_AUDIT_FILTERS);
  // The filters of the last search: paging and export keep using these, not what is typed since.
  const appliedRef = useRef<AuditSearchFilterState>(INITIAL_AUDIT_FILTERS);
  const [results, setResults] = useState<AuditLogItem[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Keyset paging: cursors[i] is the BeforeId that fetches page i (page 0 has none).
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(25);
  const [cursors, setCursors] = useState<(number | null)[]>([null]);
  const [nextCursor, setNextCursor] = useState<number | null>(null);
  const [hasMore, setHasMore] = useState(false);

  const [selectedEvent, setSelectedEvent] = useState<AuditLogItem | null>(null);
  const [historyTarget, setHistoryTarget] = useState<{ entityName: string; entityId: string | number } | null>(null);

  const fetchPage = useCallback(async (applied: AuditSearchFilterState, size: number, before: number | null) => {
    setLoading(true);
    setError(null);
    try {
      const payload = buildAuditPayload(applied, size);
      if (before !== null) payload.BeforeId = before;
      const res = await AuditSearchService.searchPage(payload);
      setResults(res.items);
      setNextCursor(res.nextCursor);
      setHasMore(res.hasMore);
      return true;
    } catch {
      setError("Failed to fetch audit records from the server.");
      return false;
    } finally {
      setLoading(false);
    }
  }, []);

  const executeSearch = async (applied: AuditSearchFilterState = filters, size: number = rowsPerPage) => {
    appliedRef.current = applied;
    setPage(0);
    setCursors([null]);
    await fetchPage(applied, size, null);
  };

  useEffect(() => {
    void executeSearch(INITIAL_AUDIT_FILTERS);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handlePageChange = async (newPage: number) => {
    if (newPage > page) {
      if (nextCursor === null) return;
      const next = [...cursors.slice(0, page + 1), nextCursor];
      if (await fetchPage(appliedRef.current, rowsPerPage, nextCursor)) {
        setCursors(next);
        setPage(newPage);
      }
    } else if (newPage < page) {
      const before = cursors[newPage] ?? null;
      if (await fetchPage(appliedRef.current, rowsPerPage, before)) setPage(newPage);
    }
  };

  const handleRowsPerPageChange = (size: number) => {
    setRowsPerPage(size);
    void executeSearch(appliedRef.current, size);
  };

  const handleReset = () => {
    setFilters(INITIAL_AUDIT_FILTERS);
    void executeSearch(INITIAL_AUDIT_FILTERS);
  };

  const handleExport = async () => {
    setExporting(true);
    setError(null);
    try {
      await AuditSearchService.exportCsv(buildAuditPayload(appliedRef.current));
    } catch {
      setError("Failed to export the audit trail.");
    } finally {
      setExporting(false);
    }
  };

  return (
    <>
      <PageHeader
        title="Audit Trail"
        subtitle="Search and trace immutable change events, actor identities, and laboratory records across MicroLIMS."
      />

      {canViewSecurityAudit && (
        <Tabs value={tab} onChange={(_, v: TabKey) => setTab(v)} sx={{ mb: 2 }}>
          <Tab value="audit" label="Audit Trail" />
          <Tab value="security-events" label="Security Events" />
          <Tab value="login-history" label="Login History" />
        </Tabs>
      )}

      {tab !== "audit" && canViewSecurityAudit ? (
        <SecurityTrailPanel kind={tab} />
      ) : (
        <>
          {error && (
            <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}

          <AuditFilterBar
            filters={filters}
            onFilterChange={setFilters}
            onSearch={() => void executeSearch(filters)}
            onReset={handleReset}
            loading={loading}
          />

          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
            <SectionTitle>Audit Events</SectionTitle>
            <Button
              size="small"
              variant="outlined"
              startIcon={<FileDownloadOutlinedIcon />}
              onClick={() => void handleExport()}
              disabled={exporting || loading}
              sx={{ textTransform: "none" }}
            >
              {exporting ? "Exporting..." : "Export CSV (max 50,000 rows)"}
            </Button>
          </Box>

          {loading && !results ? (
            <TableSkeleton rows={8} cols={6} />
          ) : results ? (
            <AuditResultsTable
              items={results}
              hasMore={hasMore}
              page={page}
              rowsPerPage={rowsPerPage}
              onPageChange={(p) => void handlePageChange(p)}
              onRowsPerPageChange={handleRowsPerPageChange}
              onSelectEvent={(ev) => setSelectedEvent(ev)}
            />
          ) : null}

          <AuditEventDrawer
            open={selectedEvent != null}
            event={selectedEvent}
            onClose={() => setSelectedEvent(null)}
            onViewRecordHistory={(name, id) => setHistoryTarget({ entityName: name, entityId: id })}
          />

          {historyTarget && (
            <AuditHistoryDialog
              open={historyTarget != null}
              entityName={historyTarget.entityName}
              entityId={historyTarget.entityId}
              onClose={() => setHistoryTarget(null)}
            />
          )}
        </>
      )}
    </>
  );
}
