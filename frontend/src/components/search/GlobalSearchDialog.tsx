import React, { useCallback, useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import {
  Alert, Dialog, DialogContent, Box, InputBase, Typography, Chip, CircularProgress,
  List, ListItemButton, ListItemText, Divider, IconButton
} from "@mui/material";
import { visuallyHidden } from "@mui/utils";
import SearchIcon from "@mui/icons-material/Search";
import ClearIcon from "@mui/icons-material/Clear";
import HistoryIcon from "@mui/icons-material/History";
import ArrowForwardIosIcon from "@mui/icons-material/ArrowForwardIos";
import PhotoCameraOutlinedIcon from "@mui/icons-material/PhotoCameraOutlined";
import axios from "axios";
import { toast } from "sonner";
import { globalSearchApi, GlobalSearchResultDto, GlobalSearchItemDto } from "../../api/globalSearchApi";
import { type ScanMatch } from "../../services/labelService";
import { getErrorMessage } from "../../utils/errorMessage";
import { CameraScanDialog } from "../labels/CameraScanDialog";
import { ENTITY_TYPE_LABELS } from "../labels/entityTypeLabels";
import { useScanResolve } from "../labels/useScanResolve";
import { useRecentSearches } from "./useRecentSearches";
import { SearchFavourites } from "./SearchFavourites";
import { SearchRecents } from "./SearchRecents";
import { DirectionalIcon } from "../DirectionalIcon";
import { useTranslation } from "../../i18n";

interface GlobalSearchDialogProps {
  open: boolean;
  onClose: () => void;
}

const SEARCH_CATEGORIES = ["All", "Samples", "Orders", "Materials", "Equipment", "Documents"] as const;

function isAbortError(err: unknown): boolean {
  return (
    axios.isCancel(err) ||
    ((err instanceof DOMException || err instanceof Error) &&
      (err.name === "AbortError" || err.name === "CanceledError"))
  );
}

function candidatesToSearchResult(query: string, candidates: ScanMatch[]): GlobalSearchResultDto {
  const items: GlobalSearchItemDto[] = candidates.map((c) => {
    const typeLabel = ENTITY_TYPE_LABELS[c.entityType] ?? c.entityType;
    return {
      id: c.id,
      entityType: c.entityType,
      title: c.code,
      subtitle: c.displayName ? `${typeLabel} • ${c.displayName}` : typeLabel,
      status: c.status,
      matchedField: "Code",
      matchedValue: c.code,
      badgeColor: "info",
      navigationTarget: c.navigationTarget
    };
  });

  return {
    query,
    totalMatches: items.length,
    categories: items.length > 0 ? [{ categoryName: "Matching records", count: items.length, items }] : [],
    executionTimeMs: 0
  };
}

export function GlobalSearchDialog({ open, onClose }: GlobalSearchDialogProps) {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [query, setQuery] = useState("");
  const [activeCategory, setActiveCategory] = useState<string>("All");
  const [loading, setLoading] = useState(false);
  const [results, setResults] = useState<GlobalSearchResultDto | null>(null);
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [selectionMoved, setSelectionMoved] = useState(false);
  const [scanHint, setScanHint] = useState<string | null>(null);
  const [cameraOpen, setCameraOpen] = useState(false);
  const { recentSearches, addSearch, clearSearches } = useRecentSearches();
  const { resolve, pending: resolvePending } = useScanResolve();
  const hasCamera = typeof navigator !== "undefined" && typeof navigator.mediaDevices?.getUserMedia === "function";

  const abortControllerRef = useRef<AbortController | null>(null);
  const debounceTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const suppressDebounceQueryRef = useRef<string | null>(null);
  const openRef = useRef(open);
  openRef.current = open;
  const queryRef = useRef(query);
  queryRef.current = query;

  // Flattened items for keyboard navigation
  const flatItems: GlobalSearchItemDto[] = (results?.categories ?? []).flatMap(c => c.items);
  const isSearching = loading || resolvePending;

  const cancelPendingSearch = useCallback(() => {
    if (debounceTimerRef.current !== null) {
      clearTimeout(debounceTimerRef.current);
      debounceTimerRef.current = null;
    }
    abortControllerRef.current?.abort();
    abortControllerRef.current = null;
    setLoading(false);
  }, []);

  const executeSearch = useCallback(async (searchQuery: string, category: string) => {
    abortControllerRef.current?.abort();
    const controller = new AbortController();
    abortControllerRef.current = controller;
    setLoading(true);
    try {
      const cat = category === "All" ? undefined : category;
      const limit = category === "All" ? 5 : 15;
      const res = await globalSearchApi.search(searchQuery, cat, limit, controller.signal);
      if (controller.signal.aborted || abortControllerRef.current !== controller) {
        return;
      }
      setResults(res);
      setSelectedIndex(0);
      setSelectionMoved(false);
    } catch (err: unknown) {
      if (!isAbortError(err) && abortControllerRef.current === controller) {
        setResults(null);
        toast.error(getErrorMessage(err, t("search.failed")));
      }
    } finally {
      if (abortControllerRef.current === controller) {
        abortControllerRef.current = null;
        setLoading(false);
      }
    }
  }, [t]);

  useEffect(() => {
    if (!open) {
      cancelPendingSearch();
      setQuery("");
      setResults(null);
      setSelectedIndex(0);
      setSelectionMoved(false);
      setScanHint(null);
      setCameraOpen(false);
      setActiveCategory("All");
    }
  }, [open, cancelPendingSearch]);

  useEffect(() => {
    if (!open || query.trim().length < 2) {
      cancelPendingSearch();
      setResults(null);
      return;
    }

    if (suppressDebounceQueryRef.current === query) {
      suppressDebounceQueryRef.current = null;
      return;
    }

    cancelPendingSearch();
    setLoading(true);
    debounceTimerRef.current = setTimeout(() => {
      debounceTimerRef.current = null;
      void executeSearch(query, activeCategory);
    }, 300);

    return () => {
      cancelPendingSearch();
    };
  }, [open, query, activeCategory, cancelPendingSearch, executeSearch]);

  const updateQuery = (next: string) => {
    setQuery(next);
    setSelectionMoved(false);
    setScanHint(null);
  };

  const handleCategoryChange = (category: string) => {
    setActiveCategory(category);
    setSelectionMoved(false);
    setScanHint(null);
  };

  const handleSelectItem = (item: GlobalSearchItemDto) => {
    addSearch(query);
    onClose();
    navigate(item.navigationTarget);
  };

  const handleScanEnter = async (scanQuery: string) => {
    cancelPendingSearch();
    setScanHint(null);

    try {
      const res = await resolve(scanQuery);
      if (!res || !openRef.current || queryRef.current !== scanQuery) {
        return;
      }

      if (res.outcome === "Found" && res.match) {
        if (res.layoutCorrected) {
          toast.info(t("search.layoutCorrected"));
        }
        addSearch(res.match.code || scanQuery);
        onClose();
        navigate(res.match.navigationTarget);
        return;
      }

      if (res.outcome === "Ambiguous") {
        if (res.layoutCorrected) {
          toast.info(t("search.layoutCorrected"));
        }
        setResults(candidatesToSearchResult(scanQuery, res.candidates));
        setSelectedIndex(0);
        setSelectionMoved(false);
        return;
      }

      setScanHint(res.hint ?? null);
      await executeSearch(scanQuery, activeCategory);
    } catch (err: unknown) {
      if (!openRef.current || queryRef.current !== scanQuery) {
        return;
      }
      toast.error(getErrorMessage(err, t("search.scanFailed")));
      await executeSearch(scanQuery, activeCategory);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      if (flatItems.length > 0) {
        setSelectedIndex(prev => (prev + 1 < flatItems.length ? prev + 1 : prev));
        setSelectionMoved(true);
      }
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      if (flatItems.length > 0) {
        setSelectedIndex(prev => (prev > 0 ? prev - 1 : 0));
        setSelectionMoved(true);
      }
    } else if (e.key === "Enter") {
      e.preventDefault();
      if (selectionMoved && flatItems[selectedIndex]) {
        handleSelectItem(flatItems[selectedIndex]);
        return;
      }
      if (query.trim().length >= 2) {
        void handleScanEnter(query);
      }
    }
  };

  const handleCameraDetected = (detectedCode: string) => {
    suppressDebounceQueryRef.current = detectedCode;
    queryRef.current = detectedCode;
    updateQuery(detectedCode);
    void handleScanEnter(detectedCode);
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="md"
      fullWidth
      slotProps={{
        paper: {
          sx: {
            borderRadius: 3,
            overflow: "hidden",
            maxHeight: "80vh",
            position: "relative",
            top: { xs: 0, sm: -80 }
          }
        }
      }}
    >
      <Box sx={{ display: "flex", alignItems: "center", px: 2.5, py: 1.5, borderBottom: 1, borderColor: "divider", gap: 1.5 }}>
        <SearchIcon color="action" />
        <InputBase
          autoFocus
          fullWidth
          placeholder={t("search.placeholder")}
          value={query}
          onChange={e => updateQuery(e.target.value)}
          onKeyDown={handleKeyDown}
          inputProps={{
            "aria-label": t("search.inputLabel"),
            role: "combobox",
            "aria-expanded": Boolean(results && flatItems.length > 0),
            "aria-autocomplete": "list"
          }}
          sx={{ fontSize: 16 }}
        />
        {isSearching && <CircularProgress size={20} />}
        {hasCamera && (
          <IconButton size="small" onClick={() => setCameraOpen(true)} aria-label={t("search.scanWithCamera")}>
            <PhotoCameraOutlinedIcon fontSize="small" />
          </IconButton>
        )}
        {query && !isSearching && (
          <IconButton size="small" onClick={() => updateQuery("")} aria-label={t("search.clearInput")}>
            <ClearIcon fontSize="small" />
          </IconButton>
        )}
      </Box>

      {/* Category Filter Facets */}
      <Box
        role="group"
        aria-label={t("search.filterByCategory")}
        sx={{
          display: "flex",
          alignItems: "center",
          gap: 0.75,
          px: 2.5,
          py: 1,
          borderBottom: 1,
          borderColor: "divider",
          bgcolor: "background.default",
          overflowX: "auto"
        }}
      >
        {SEARCH_CATEGORIES.map((category) => {
          const selected = activeCategory === category;
          return (
            <Chip
              key={category}
              label={t(`search.category.${category}`)}
              size="small"
              clickable
              color={selected ? "primary" : "default"}
              variant={selected ? "filled" : "outlined"}
              aria-pressed={selected}
              onClick={() => handleCategoryChange(category)}
              sx={{ fontWeight: selected ? 700 : 500, fontSize: 12 }}
            />
          );
        })}
      </Box>

      {/* Screen reader live announcements for search status */}
      <Box sx={visuallyHidden} aria-live="polite" aria-atomic="true">
        {isSearching
          ? t("search.searching")
          : results && query.trim().length >= 2
          ? t("search.resultsFound", { total: flatItems.length, query })
          : ""}
      </Box>

      <DialogContent sx={{ p: 2 }}>
        {scanHint && (
          <Alert severity="info" role="status" sx={{ mb: 2 }}>
            {scanHint}
          </Alert>
        )}

        {open && query.trim().length < 2 && <SearchFavourites onSelect={onClose} />}
        {open && query.trim().length < 2 && <SearchRecents onSelect={onClose} />}

        {query.trim().length < 2 && recentSearches.length > 0 && (
          <Box sx={{ mb: 2 }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
              <Typography variant="caption" color="text.secondary" sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                <HistoryIcon sx={{ fontSize: 14 }} /> {t("search.recentSearches")}
              </Typography>
              <Typography
                component="button"
                type="button"
                variant="caption"
                color="primary"
                sx={{ cursor: "pointer", bgcolor: "transparent", border: 0, p: 0 }}
                onClick={clearSearches}
              >
                {t("search.clear")}
              </Typography>
            </Box>
            <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap" }}>
              {recentSearches.map(term => (
                <Chip key={term} label={term} size="small" onClick={() => updateQuery(term)} />
              ))}
            </Box>
          </Box>
        )}

        {results && results.categories.length === 0 && !isSearching && (
          <Box sx={{ py: 6, textAlign: "center", color: "text.secondary" }}>
            <Typography variant="body1">{t("search.noMatches", { query })}</Typography>
            <Typography variant="caption">{t("search.noMatchesHint")}</Typography>
          </Box>
        )}

        {results && results.categories.length > 0 && (
          <List disablePadding>
            {results.categories.map(category => (
              <React.Fragment key={category.categoryName}>
                <Typography variant="caption" sx={{ fontWeight: 700, color: "text.secondary", px: 1, py: 0.5, display: "block" }}>
                  {category.categoryName.toUpperCase()} ({category.count})
                </Typography>
                {category.items.map(item => {
                  const itemIndex = flatItems.indexOf(item);
                  const isSelected = itemIndex === selectedIndex;
                  return (
                    <ListItemButton
                      key={`${item.entityType}-${item.id}`}
                      selected={isSelected}
                      onClick={() => handleSelectItem(item)}
                      sx={{ borderRadius: 1.5, my: 0.5, display: "flex", justifyContent: "space-between" }}
                    >
                      <ListItemText
                        primary={item.title}
                        secondary={item.subtitle}
                        slotProps={{
                          primary: { sx: { fontWeight: 600, fontSize: 14 } },
                          secondary: { sx: { fontSize: 12 } }
                        }}
                      />
                      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                        {item.status && <Chip label={item.status} size="small" variant="outlined" />}
                        <DirectionalIcon><ArrowForwardIosIcon sx={{ fontSize: 12, color: "action.disabled" }} /></DirectionalIcon>
                      </Box>
                    </ListItemButton>
                  );
                })}
                <Divider sx={{ my: 1 }} />
              </React.Fragment>
            ))}
          </List>
        )}
      </DialogContent>
      {hasCamera && (
        <CameraScanDialog
          open={cameraOpen}
          onClose={() => setCameraOpen(false)}
          onDetected={handleCameraDetected}
        />
      )}
    </Dialog>
  );
}
