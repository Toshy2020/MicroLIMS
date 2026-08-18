import { useEffect, useState } from "react";
import { Box, Alert, Typography } from "@mui/material";
import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { AuditHistoryDialog } from "../../components/AuditHistoryDialog";
import { AuditSearchService } from "./services/AuditSearchService";
import type { AuditLogItem, AuditSearchFilterState } from "./types/auditTypes";
import { AuditFilterBar } from "./components/AuditFilterBar";
import { AuditResultsTable } from "./components/AuditResultsTable";
import { AuditEventDrawer } from "./components/AuditEventDrawer";

const INITIAL_FILTERS: AuditSearchFilterState = {
  fromDate: "",
  toDate: "",
  batchNumber: "",
  controlNumber: "",
  sampleReferenceNumber: "",
  mediaLotNumber: "",
  referenceStrainCode: "",
  cryovialCode: "",
  entityName: "",
  action: "",
  userId: ""
};

export function AuditSearchPage() {
  const [filters, setFilters] = useState<AuditSearchFilterState>(INITIAL_FILTERS);
  const [results, setResults] = useState<AuditLogItem[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Pagination state
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(25);

  // Selected event for detail drawer
  const [selectedEvent, setSelectedEvent] = useState<AuditLogItem | null>(null);

  // Entity history modal state
  const [historyTarget, setHistoryTarget] = useState<{ entityName: string; entityId: string | number } | null>(null);

  const executeSearch = async (appliedFilters: AuditSearchFilterState = filters) => {
    setLoading(true);
    setError(null);
    try {
      const payload: Record<string, any> = { Take: 300 };

      if (appliedFilters.fromDate) payload.FromDate = new Date(appliedFilters.fromDate).toISOString();
      if (appliedFilters.toDate) {
        const d = new Date(appliedFilters.toDate);
        d.setHours(23, 59, 59, 999);
        payload.ToDate = d.toISOString();
      }
      if (appliedFilters.batchNumber.trim()) payload.BatchNumber = appliedFilters.batchNumber.trim();
      if (appliedFilters.controlNumber.trim()) payload.ControlNumber = appliedFilters.controlNumber.trim();
      if (appliedFilters.sampleReferenceNumber.trim()) payload.SampleReferenceNumber = appliedFilters.sampleReferenceNumber.trim();
      if (appliedFilters.mediaLotNumber.trim()) payload.MediaLotNumber = appliedFilters.mediaLotNumber.trim();
      if (appliedFilters.referenceStrainCode.trim()) payload.ReferenceStrainCode = appliedFilters.referenceStrainCode.trim();
      if (appliedFilters.cryovialCode.trim()) payload.CryovialCode = appliedFilters.cryovialCode.trim();
      if (appliedFilters.entityName.trim()) payload.EntityName = appliedFilters.entityName.trim();
      if (appliedFilters.action.trim()) payload.Action = appliedFilters.action.trim();
      if (appliedFilters.userId.trim()) {
        const uid = parseInt(appliedFilters.userId.trim(), 10);
        if (!isNaN(uid)) payload.UserId = uid;
      }

      const res = await AuditSearchService.search(payload);
      setResults(res);
      setPage(0);
    } catch {
      setError("Failed to fetch audit records from the server.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    executeSearch(INITIAL_FILTERS);
  }, []);

  const handleReset = () => {
    setFilters(INITIAL_FILTERS);
    executeSearch(INITIAL_FILTERS);
  };

  return (
    <>
      <PageHeader
        title="Audit Trail"
        subtitle="Search and trace immutable change events, actor identities, and laboratory records across MicroLIMS."
      />

      {error && (
        <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {/* Filter Area */}
      <AuditFilterBar
        filters={filters}
        onFilterChange={setFilters}
        onSearch={() => executeSearch(filters)}
        onReset={handleReset}
        loading={loading}
      />

      {/* Results Header */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
        <SectionTitle>
          {results
            ? `Audit Events (${results.length}${results.length >= 300 ? " — capped at 300 latest" : ""})`
            : "Audit Events"}
        </SectionTitle>
      </Box>

      {/* Results Table */}
      {loading && !results ? (
        <LoadingSpinner />
      ) : results ? (
        <AuditResultsTable
          items={results}
          totalCount={results.length}
          page={page}
          rowsPerPage={rowsPerPage}
          onPageChange={setPage}
          onRowsPerPageChange={(nr) => {
            setRowsPerPage(nr);
            setPage(0);
          }}
          onSelectEvent={(ev) => setSelectedEvent(ev)}
        />
      ) : null}

      {/* Audit Event Detail Drawer */}
      <AuditEventDrawer
        open={selectedEvent != null}
        event={selectedEvent}
        onClose={() => setSelectedEvent(null)}
        onViewRecordHistory={(name, id) => setHistoryTarget({ entityName: name, entityId: id })}
      />

      {/* Record History Dialog */}
      {historyTarget && (
        <AuditHistoryDialog
          open={historyTarget != null}
          entityName={historyTarget.entityName}
          entityId={historyTarget.entityId}
          onClose={() => setHistoryTarget(null)}
        />
      )}
    </>
  );
}
