import { useEffect, useRef, useState } from "react";
import { Box, Stack } from "@mui/material";
import { ReferenceStrainFilterPanel } from "./components/ReferenceStrainFilterPanel";
import { ReferenceStrainResultsTable } from "./components/ReferenceStrainResultsTable";
import { ReferenceStrainReportService } from "./services/ReferenceStrainReportService";
import {
  ReferenceStrainFilterOptions,
  ReferenceStrainSearchParams,
  ReferenceStrainSearchResponse
} from "./types/referenceStrainTypes";

const DEFAULT_PAGE_SIZE = 25;

function baseParams(): ReferenceStrainSearchParams {
  return {
    page: 1,
    pageSize: DEFAULT_PAGE_SIZE,
    sortBy: "PreparedAt",
    sortDescending: true
  };
}

export function ReferenceStrainReportTab() {
  const [filterOptions, setFilterOptions] = useState<ReferenceStrainFilterOptions | null>(null);
  const [draft, setDraft] = useState<ReferenceStrainSearchParams>(baseParams());
  const [applied, setApplied] = useState<ReferenceStrainSearchParams>(baseParams());
  const [results, setResults] = useState<ReferenceStrainSearchResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    ReferenceStrainReportService.getFilterOptions()
      .then(setFilterOptions)
      .catch(() => setFilterOptions(null));
  }, []);

  // Load results when applied filters change
  useEffect(() => {
    setLoading(true);
    setError(null);

    ReferenceStrainReportService.search(applied)
      .then(setResults)
      .catch(() => setError("Could not load Reference Strain batches. Please try again."))
      .finally(() => setLoading(false));
  }, [applied]);

  const handleSearch = () => setApplied({ ...draft, page: 1 });
  const handleReset = () => {
    const reset = baseParams();
    setDraft(reset);
    setApplied(reset);
  };

  return (
    <Box sx={{ display: "grid", gridTemplateColumns: "300px 1fr", gap: 2, alignItems: "start" }}>
      <Stack spacing={2}>
        <ReferenceStrainFilterPanel
          filterOptions={filterOptions}
          draft={draft}
          onChange={(patch) => setDraft((d) => ({ ...d, ...patch }))}
          onSearch={handleSearch}
          onReset={handleReset}
          searchInputRef={searchInputRef}
        />
      </Stack>

      <Box>
        <ReferenceStrainResultsTable
          results={results}
          loading={loading}
          error={error}
          appliedParams={applied}
          onPageChange={(page) => setApplied((a) => ({ ...a, page }))}
          onPageSizeChange={(pageSize) => setApplied((a) => ({ ...a, pageSize, page: 1 }))}
          onClearFilters={handleReset}
        />
      </Box>
    </Box>
  );
}
