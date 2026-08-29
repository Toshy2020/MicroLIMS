import { useEffect, useRef, useState } from "react";
import { Box, Stack } from "@mui/material";
import { MediaGptFilterPanel } from "./components/MediaGptFilterPanel";
import { MediaGptResultsTable } from "./components/MediaGptResultsTable";
import { MediaGptSummaryCard } from "./components/MediaGptSummaryCard";
import { MediaGptReportService } from "./services/MediaGptReportService";
import {
  MediaGptFilterOptions,
  MediaGptSearchParams,
  MediaGptSearchResponse,
  MediaGptSummary
} from "./types/mediaGptTypes";

interface MediaGptReportTabProps {
  fromDate?: string;
  toDate?: string;
}

const DEFAULT_PAGE_SIZE = 25;

function baseParams(fromDate?: string, toDate?: string): MediaGptSearchParams {
  return {
    fromDate,
    toDate,
    evaluationType: "GrowthPromotion", // Default to Growth Promotion only as specified
    page: 1,
    pageSize: DEFAULT_PAGE_SIZE,
    sortBy: "PreparedAt",
    sortDescending: true
  };
}

export function MediaGptReportTab({ fromDate, toDate }: MediaGptReportTabProps) {
  const [filterOptions, setFilterOptions] = useState<MediaGptFilterOptions | null>(null);
  const [draft, setDraft] = useState<MediaGptSearchParams>(baseParams(fromDate, toDate));
  const [applied, setApplied] = useState<MediaGptSearchParams>(baseParams(fromDate, toDate));
  const [results, setResults] = useState<MediaGptSearchResponse | null>(null);
  const [summary, setSummary] = useState<MediaGptSummary | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    MediaGptReportService.getFilterOptions()
      .then(setFilterOptions)
      .catch(() => setFilterOptions(null));
  }, []);

  // Quick period change
  useEffect(() => {
    setDraft((d) => ({ ...d, fromDate, toDate }));
    setApplied((a) => ({ ...a, fromDate, toDate, page: 1 }));
  }, [fromDate, toDate]);

  // Load results and summary when applied filters change
  useEffect(() => {
    setLoading(true);
    setError(null);

    Promise.all([
      MediaGptReportService.search(applied),
      MediaGptReportService.getSummary(applied.fromDate, applied.toDate, applied.mediaType)
    ])
      .then(([resData, sumData]) => {
        setResults(resData);
        setSummary(sumData);
      })
      .catch(() => setError("Could not load Media/GPT records. Please try again."))
      .finally(() => setLoading(false));
  }, [applied]);

  const handleSearch = () => setApplied({ ...draft, page: 1 });
  const handleReset = () => {
    const reset = baseParams(fromDate, toDate);
    setDraft(reset);
    setApplied(reset);
  };

  return (
    <Box sx={{ display: "grid", gridTemplateColumns: "300px 1fr", gap: 2, alignItems: "start" }}>
      <Stack spacing={2}>
        <MediaGptFilterPanel
          filterOptions={filterOptions}
          draft={draft}
          onChange={(patch) => setDraft((d) => ({ ...d, ...patch }))}
          onSearch={handleSearch}
          onReset={handleReset}
          searchInputRef={searchInputRef}
        />
      </Stack>

      <Box>
        <MediaGptSummaryCard summary={summary} loading={loading} />
        <MediaGptResultsTable
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
