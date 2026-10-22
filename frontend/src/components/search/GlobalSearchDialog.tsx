import React, { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import {
  Dialog, DialogContent, Box, InputBase, Typography, Chip, CircularProgress,
  List, ListItemButton, ListItemText, Divider, IconButton
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import ClearIcon from "@mui/icons-material/Clear";
import HistoryIcon from "@mui/icons-material/History";
import ArrowForwardIosIcon from "@mui/icons-material/ArrowForwardIos";
import { globalSearchApi, GlobalSearchResultDto, GlobalSearchItemDto } from "../../api/globalSearchApi";
import { useRecentSearches } from "./useRecentSearches";

interface GlobalSearchDialogProps {
  open: boolean;
  onClose: () => void;
}

export function GlobalSearchDialog({ open, onClose }: GlobalSearchDialogProps) {
  const navigate = useNavigate();
  const [query, setQuery] = useState("");
  const [activeCategory, setActiveCategory] = useState<string>("All");
  const [loading, setLoading] = useState(false);
  const [results, setResults] = useState<GlobalSearchResultDto | null>(null);
  const [selectedIndex, setSelectedIndex] = useState(0);
  const { recentSearches, addSearch, clearSearches } = useRecentSearches();

  const abortControllerRef = useRef<AbortController | null>(null);

  // Flattened items for keyboard navigation
  const flatItems: GlobalSearchItemDto[] = (results?.categories ?? []).flatMap(c => c.items);

  useEffect(() => {
    if (!open) {
      setQuery("");
      setResults(null);
      setSelectedIndex(0);
      setActiveCategory("All");
      return;
    }
  }, [open]);

  useEffect(() => {
    if (query.trim().length < 2) {
      setResults(null);
      setLoading(false);
      return;
    }

    setLoading(true);
    abortControllerRef.current?.abort();
    const controller = new AbortController();
    abortControllerRef.current = controller;

    const timer = setTimeout(async () => {
      try {
        const cat = activeCategory === "All" ? undefined : activeCategory;
        const res = await globalSearchApi.search(query, cat, 5, controller.signal);
        setResults(res);
        setSelectedIndex(0);
      } catch (err: unknown) {
        if ((err as Error)?.name !== "CanceledError" && (err as Error)?.name !== "AbortError") {
          setResults(null);
        }
      } finally {
        setLoading(false);
      }
    }, 300);

    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [query, activeCategory]);

  const handleSelectItem = (item: GlobalSearchItemDto) => {
    addSearch(query);
    onClose();
    navigate(item.navigationTarget);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setSelectedIndex(prev => (prev + 1 < flatItems.length ? prev + 1 : prev));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setSelectedIndex(prev => (prev > 0 ? prev - 1 : 0));
    } else if (e.key === "Enter" && flatItems[selectedIndex]) {
      e.preventDefault();
      handleSelectItem(flatItems[selectedIndex]);
    }
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
          placeholder="Search MicroLIMS (samples, orders, materials, equipment, docs)..."
          value={query}
          onChange={e => setQuery(e.target.value)}
          onKeyDown={handleKeyDown}
          sx={{ fontSize: 16 }}
        />
        {loading && <CircularProgress size={20} />}
        {query && !loading && (
          <IconButton size="small" onClick={() => setQuery("")} aria-label="Clear search input">
            <ClearIcon fontSize="small" />
          </IconButton>
        )}
      </Box>

      <DialogContent sx={{ p: 2 }}>
        {query.trim().length < 2 && recentSearches.length > 0 && (
          <Box sx={{ mb: 2 }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
              <Typography variant="caption" color="text.secondary" sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                <HistoryIcon sx={{ fontSize: 14 }} /> RECENT SEARCHES
              </Typography>
              <Typography variant="caption" color="primary" sx={{ cursor: "pointer" }} onClick={clearSearches}>
                Clear
              </Typography>
            </Box>
            <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap" }}>
              {recentSearches.map(term => (
                <Chip key={term} label={term} size="small" onClick={() => setQuery(term)} />
              ))}
            </Box>
          </Box>
        )}

        {results && results.categories.length === 0 && !loading && (
          <Box sx={{ py: 6, textAlign: "center", color: "text.secondary" }}>
            <Typography variant="body1">No matches found for "{query}"</Typography>
            <Typography variant="caption">Try searching by sample reference, batch number, material name, or equipment code.</Typography>
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
                        <ArrowForwardIosIcon sx={{ fontSize: 12, color: "action.disabled" }} />
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
    </Dialog>
  );
}
