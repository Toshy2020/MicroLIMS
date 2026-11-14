import { useEffect, useRef, useState } from "react";
import { Alert, Box, CircularProgress, Link as MuiLink, Stack, TablePagination, TextField, Tooltip, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import { SectionTitle } from "../../../components/SectionTitle";
import { ToneBadge } from "../../../components/StatusBadge";
import { monospaceFontFamily } from "../../../theme/palette";
import type { StatusTone } from "../../../theme/statusTokens";
import { TestItemsService, type TestItemRow, type TestItemSopStatus } from "./services/TestItemsService";

const PAGE_SIZE = 10;
const SEARCH_DELAY_MS = 300;

const GUIDANCE_ONLY = "Guidance only. Your approved SOPs take precedence.";

const SOP_STATUS: Record<TestItemSopStatus, { tone: StatusTone; label: string }> = {
  Linked: { tone: "notDetected", label: "Linked to Document Control" },
  NotFound: { tone: "action", label: "Not in Document Control" },
  NoNumber: { tone: "pale", label: "No SOP number" }
};

const visuallyHidden = {
  position: "absolute",
  width: 1,
  height: 1,
  overflow: "hidden",
  clip: "rect(0 0 0 0)",
  whiteSpace: "nowrap"
} as const;

/** The items that use a test, with each item's SOP status and routes to its documents. */
export function TestItemsPanel({ testId }: { testId: number }) {
  const [searchText, setSearchText] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(0);
  const [rows, setRows] = useState<TestItemRow[]>([]);
  const [total, setTotal] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [retryKey, setRetryKey] = useState(0);
  const appliedSearch = useRef("");

  useEffect(() => {
    const timer = window.setTimeout(() => {
      const next = searchText.trim();
      if (next === appliedSearch.current) return;
      appliedSearch.current = next;
      setSearch(next);
      setPage(0);
    }, SEARCH_DELAY_MS);
    return () => window.clearTimeout(timer);
  }, [searchText]);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    TestItemsService.getItems(testId, { search, page: page + 1, pageSize: PAGE_SIZE }, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setRows(result.items);
        setTotal(result.totalCount);
      })
      .catch((e: unknown) => {
        if (controller.signal.aborted) return;
        const err = e as { response?: { data?: { message?: string } } };
        setError(err?.response?.data?.message ?? "The items that use this test could not be loaded.");
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [testId, search, page, retryKey]);

  return (
    <Box component="section" aria-label="Items using this test" sx={{ minWidth: 0, position: "relative" }}>
      <SectionTitle>{`Items using this test (${total ?? "..."})`}</SectionTitle>
      <Typography variant="caption" color="text.secondary" component="p" sx={{ mb: 1 }}>
        {GUIDANCE_ONLY}
      </Typography>
      <TextField
        size="small"
        fullWidth
        label="Search items"
        placeholder="Item code or name"
        value={searchText}
        onChange={(e) => setSearchText(e.target.value)}
        sx={{ mb: 1 }}
      />
      <Box role="status" aria-live="polite" sx={visuallyHidden}>
        {loading ? "Loading items" : `${total ?? 0} items found`}
      </Box>
      {error && (
        <Alert severity="error" action={<MuiLink component="button" type="button" onClick={() => setRetryKey((k) => k + 1)}>Retry</MuiLink>}>
          {error}
        </Alert>
      )}
      {loading && rows.length === 0 && !error && <CircularProgress size={20} aria-label="Loading items" />}
      {!loading && !error && rows.length === 0 && (
        <Typography variant="body2" color="text.secondary">
          {search ? "No item matches your search." : "No item uses this test yet."}
        </Typography>
      )}
      {rows.length > 0 && (
        <Stack component="ul" spacing={1} sx={{ listStyle: "none", m: 0, p: 0, opacity: loading ? 0.6 : 1 }} aria-busy={loading}>
          {rows.map((r) => {
            const status = SOP_STATUS[r.sopStatus];
            return (
              <Box key={r.itemId} component="li" sx={{ border: 1, borderColor: "divider", borderRadius: 1, p: 1 }}>
                <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: "baseline" }}>
                  <Typography component="span" variant="body2" sx={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>{r.itemCode}</Typography>
                  <Tooltip title={r.itemName}>
                    <Typography component="span" variant="body2" noWrap sx={{ minWidth: 0, flex: 1 }}>{r.itemName}</Typography>
                  </Tooltip>
                </Stack>
                <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: "center", flexWrap: "wrap", mt: 0.5 }}>
                  <Typography component="span" variant="caption" sx={{ fontFamily: monospaceFontFamily }}>
                    {r.sopNumber ?? "No SOP number"}
                  </Typography>
                  <ToneBadge tone={status.tone} label={`SOP: ${status.label}`} />
                  {r.effectiveRevisionNumber && (
                    <Typography component="span" variant="caption" color="text.secondary">{`Revision ${r.effectiveRevisionNumber}`}</Typography>
                  )}
                </Stack>
                <Stack direction="row" spacing={2} useFlexGap sx={{ flexWrap: "wrap", mt: 0.5 }}>
                  <MuiLink component={RouterLink} to={`/laboratory-configuration/items?item=${r.itemId}&tab=documents`} variant="body2" aria-label={`Item documents for ${r.itemCode}`}>
                    Item documents
                  </MuiLink>
                  {r.sopStatus === "Linked" && r.documentMasterId != null && (
                    <MuiLink component={RouterLink} to={`/document-control/documents/${r.documentMasterId}`} variant="body2" aria-label={`Open SOP for ${r.itemCode}`}>
                      Open SOP
                    </MuiLink>
                  )}
                </Stack>
              </Box>
            );
          })}
        </Stack>
      )}
      {(total ?? 0) > PAGE_SIZE && (
        <TablePagination
          component="div"
          count={total ?? 0}
          page={page}
          onPageChange={(_, p) => setPage(p)}
          rowsPerPage={PAGE_SIZE}
          rowsPerPageOptions={[PAGE_SIZE]}
          labelRowsPerPage="Items per page"
        />
      )}
    </Box>
  );
}
