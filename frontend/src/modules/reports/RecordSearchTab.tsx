import { useEffect, useRef, useState } from "react";
import { Box, Stack } from "@mui/material";
import { ReportFilterPanel } from "./components/ReportFilterPanel";
import { ReportResultsTable } from "./components/ReportResultsTable";
import { QuickReportsTiles } from "./components/QuickReportsTiles";
import { ReportingService } from "./services/ReportingService";
import { FilterOptionsResponse, ResultRecordItem, ResultRecordSearchParams, ResultRecordSearchResponse, SampleCategory } from "./types/reportingTypes";

interface RecordSearchTabProps {
  fromDate: string | undefined;
  toDate: string | undefined;
  onAnalyzeTrend?: (testCode: string, subjectName: string) => void;
}

const DEFAULT_PAGE_SIZE = 25;

function baseParams(fromDate: string | undefined, toDate: string | undefined): ResultRecordSearchParams {
  return { fromDate, toDate, page: 1, pageSize: DEFAULT_PAGE_SIZE, sortBy: "ResultEnteredAt", sortDescending: true };
}

// Orchestrates the Record Search tab: loads filter-options once, holds
// draft (form) vs applied (actually queried) filter state, and re-runs
// the search whenever `applied` changes. The quick-period selector
// above the tabs is the one exception to "apply on button click" -
// changing it re-applies immediately, carrying over whatever other
// filters are currently applied.
export function RecordSearchTab({ fromDate, toDate, onAnalyzeTrend }: RecordSearchTabProps) {
  const [filterOptions, setFilterOptions] = useState<FilterOptionsResponse | null>(null);
  const [draft, setDraft] = useState<ResultRecordSearchParams>(baseParams(fromDate, toDate));
  const [applied, setApplied] = useState<ResultRecordSearchParams>(baseParams(fromDate, toDate));
  const [results, setResults] = useState<ResultRecordSearchResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    ReportingService.getFilterOptions().then(setFilterOptions).catch(() => setFilterOptions(null));
  }, []);

  // Quick-period change: immediate apply, carrying over every other
  // currently-applied filter (not the unsaved draft edits).
  useEffect(() => {
    setDraft((d) => ({ ...d, fromDate, toDate }));
    setApplied((a) => ({ ...a, fromDate, toDate, page: 1 }));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fromDate, toDate]);

  useEffect(() => {
    setLoading(true);
    setError(null);
    ReportingService.searchResults(applied)
      .then(setResults)
      .catch(() => setError("Could not load results. Please try again."))
      .finally(() => setLoading(false));
  }, [applied]);

  const handleSearch = () => setApplied({ ...draft, page: 1 });
  const handleReset = () => {
    const reset = baseParams(fromDate, toDate);
    setDraft(reset);
    setApplied(reset);
  };

  // Preset tiles run immediately (same single-click exception as the
  // quick-period selector) - a clean slate except for the current date
  // range, then the chosen category.
  const handlePreset = (category: SampleCategory) => {
    const preset = { ...baseParams(fromDate, toDate), category };
    setDraft(preset);
    setApplied(preset);
  };

  const handleCustomReport = () => {
    handleReset();
    searchInputRef.current?.focus();
  };

  return (
    <Box sx={{ display: "grid", gridTemplateColumns: "300px 1fr", gap: 2, alignItems: "start" }}>
      <Stack spacing={2}>
        <ReportFilterPanel
          filterOptions={filterOptions}
          draft={draft}
          onChange={(patch) => setDraft((d) => ({ ...d, ...patch }))}
          onSearch={handleSearch}
          onReset={handleReset}
          searchInputRef={searchInputRef}
        />
        <QuickReportsTiles onPreset={handlePreset} onCustomReport={handleCustomReport} />
      </Stack>

      <ReportResultsTable
        results={results}
        loading={loading}
        error={error}
        appliedParams={applied}
        onPageChange={(page) => setApplied((a) => ({ ...a, page }))}
        onPageSizeChange={(pageSize) => setApplied((a) => ({ ...a, pageSize, page: 1 }))}
        onClearFilters={handleReset}
        onAnalyzeTrend={onAnalyzeTrend}
      />
    </Box>
  );
}
