import { useCallback, useEffect, useState } from "react";
import { Alert, Box } from "@mui/material";
import { PageHeader } from "../../components/PageHeader";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { IncidentFilterBar } from "./components/IncidentFilterBar";
import { IncidentTable } from "./components/IncidentTable";
import { ErrorMonitoringService } from "./services/ErrorMonitoringService";
import type { IncidentFilterState, IncidentListResult } from "./types/errorMonitoringTypes";
import { INITIAL_INCIDENT_FILTERS } from "./types/errorMonitoringTypes";

export function ErrorMonitoringPage() {
  const [filters, setFilters] = useState<IncidentFilterState>(INITIAL_INCIDENT_FILTERS);
  const [result, setResult] = useState<IncidentListResult | null>(null);
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(25);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setResult(await ErrorMonitoringService.search(filters, page, rowsPerPage));
    } catch (err: any) {
      setError(err?.response?.data?.message ?? "Could not load incidents.");
    } finally {
      setLoading(false);
    }
  }, [filters, page, rowsPerPage]);

  useEffect(() => {
    void load();
  }, [load]);

  const applyFilters = (next: IncidentFilterState) => {
    setFilters(next);
    // A filter change with the old page number can land on a page that no
    // longer exists and show an empty table over a non-empty result.
    setPage(0);
    setExpandedId(null);
  };

  const handleExport = async () => {
    setExporting(true);
    setError(null);
    try {
      await ErrorMonitoringService.exportCsv(filters);
    } catch (err: any) {
      setError(err?.response?.data?.message ?? "The export could not be generated.");
    } finally {
      setExporting(false);
    }
  };

  return (
    <Box>
      <PageHeader
        title="Error Monitoring"
        subtitle="Technical incidents captured from the application, the API and the database. Operational data only - this is not part of the GxP record."
      />

      <IncidentFilterBar
        filters={filters}
        onChange={applyFilters}
        onReset={() => applyFilters(INITIAL_INCIDENT_FILTERS)}
        onExport={handleExport}
        exporting={exporting}
        disabled={loading}
      />

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {loading && !result ? (
        <LoadingSpinner />
      ) : (
        <IncidentTable
          items={result?.items ?? []}
          totalCount={result?.totalCount ?? 0}
          page={page}
          rowsPerPage={rowsPerPage}
          expandedId={expandedId}
          onToggleExpand={(id) => setExpandedId((current) => (current === id ? null : id))}
          onPageChange={setPage}
          onRowsPerPageChange={(size) => { setRowsPerPage(size); setPage(0); }}
          onChanged={load}
        />
      )}
    </Box>
  );
}
