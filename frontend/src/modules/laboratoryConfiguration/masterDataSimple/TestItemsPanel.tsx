import { useEffect, useRef, useState } from "react";
import { Alert, Box, CircularProgress, Link as MuiLink, Stack, TablePagination, TextField, Tooltip, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import { SectionTitle } from "../../../components/SectionTitle";
import { ToneBadge } from "../../../components/StatusBadge";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";
import type { StatusTone } from "../../../theme/statusTokens";
import { TestItemsService, type TestItemRow, type TestItemSopStatus } from "./services/TestItemsService";

const PAGE_SIZE = 10;
const SEARCH_DELAY_MS = 300;

const SOP_STATUS_TONES: Record<TestItemSopStatus, StatusTone> = {
  Linked: "notDetected",
  NotFound: "action",
  NoNumber: "pale"
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
  const { t } = useTranslation("masterData");
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
        setError(err?.response?.data?.message ?? t("testItemsPanel.loadError"));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [testId, search, page, retryKey, t]);

  return (
    <Box component="section" aria-label={t("testItemsPanel.ariaLabel")} sx={{ minWidth: 0, position: "relative" }}>
      <SectionTitle>{t("testItemsPanel.title", { count: total ?? "..." })}</SectionTitle>
      <Typography variant="caption" color="text.secondary" component="p" sx={{ mb: 1 }}>
        {t("testItemsPanel.guidanceOnly")}
      </Typography>
      <TextField
        size="small"
        fullWidth
        label={t("testItemsPanel.searchLabel")}
        placeholder={t("testItemsPanel.searchPlaceholder")}
        value={searchText}
        onChange={(e) => setSearchText(e.target.value)}
        sx={{ mb: 1 }}
      />
      <Box role="status" aria-live="polite" sx={visuallyHidden}>
        {loading ? t("testItemsPanel.loadingItems") : t("testItemsPanel.itemsFound", { count: total ?? 0 })}
      </Box>
      {error && (
        <Alert severity="error" action={<MuiLink component="button" type="button" onClick={() => setRetryKey((k) => k + 1)}>{t("testItemsPanel.retry")}</MuiLink>}>
          {error}
        </Alert>
      )}
      {loading && rows.length === 0 && !error && <CircularProgress size={20} aria-label={t("testItemsPanel.loadingItems")} />}
      {!loading && !error && rows.length === 0 && (
        <Typography variant="body2" color="text.secondary">
          {search ? t("testItemsPanel.emptySearch") : t("testItemsPanel.emptyNone")}
        </Typography>
      )}
      {rows.length > 0 && (
        <Stack component="ul" spacing={1} sx={{ listStyle: "none", m: 0, p: 0, opacity: loading ? 0.6 : 1 }} aria-busy={loading}>
          {rows.map((r) => {
            const tone = SOP_STATUS_TONES[r.sopStatus];
            const sopLabel = t(`testItemsPanel.sopStatus.${r.sopStatus}`);
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
                    {r.sopNumber ?? t("testItemsPanel.noSopNumber")}
                  </Typography>
                  <ToneBadge tone={tone} label={t("testItemsPanel.sopBadge", { label: sopLabel })} />
                  {r.effectiveRevisionNumber && (
                    <Typography component="span" variant="caption" color="text.secondary">{t("testItemsPanel.revision", { rev: r.effectiveRevisionNumber })}</Typography>
                  )}
                </Stack>
                <Stack direction="row" spacing={2} useFlexGap sx={{ flexWrap: "wrap", mt: 0.5 }}>
                  <MuiLink component={RouterLink} to={`/laboratory-configuration/items?item=${r.itemId}&tab=documents`} variant="body2" aria-label={t("testItemsPanel.itemDocumentsAria", { code: r.itemCode })}>
                    {t("testItemsPanel.itemDocuments")}
                  </MuiLink>
                  {r.sopStatus === "Linked" && r.documentMasterId != null && (
                    <MuiLink component={RouterLink} to={`/document-control/documents/${r.documentMasterId}`} variant="body2" aria-label={t("testItemsPanel.openSopAria", { code: r.itemCode })}>
                      {t("testItemsPanel.openSop")}
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
          labelRowsPerPage={t("testItemsPanel.rowsPerPage")}
        />
      )}
    </Box>
  );
}
