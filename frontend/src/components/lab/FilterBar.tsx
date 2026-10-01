import { useEffect, useRef, useState } from "react";
import { IconButton, InputAdornment, Paper, Stack, TextField, Tooltip, Typography } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import ClearIcon from "@mui/icons-material/Clear";
import RefreshIcon from "@mui/icons-material/Refresh";
import type { ReactNode } from "react";

const SEARCH_DEBOUNCE_MS = 250;

interface FilterBarProps {
  search: string;
  onSearch: (value: string) => void;
  placeholder?: string;
  // Extra filter controls (selects, toggles) rendered after the search box.
  children?: ReactNode;
  resultCount?: number;
  onRefresh?: () => void;
  // Disables the refresh button while a load is in flight.
  refreshing?: boolean;
}

export function FilterBar({ search, onSearch, placeholder = "Search", children, resultCount, onRefresh, refreshing }: FilterBarProps) {
  // Local text so typing is instant; the parent only sees the debounced value.
  const [text, setText] = useState(search);
  const onSearchRef = useRef(onSearch);
  useEffect(() => { onSearchRef.current = onSearch; });

  // Follow external resets (e.g. a "clear filters" button in the parent).
  useEffect(() => { setText(search); }, [search]);

  useEffect(() => {
    if (text === search) return;
    const id = window.setTimeout(() => onSearchRef.current(text), SEARCH_DEBOUNCE_MS);
    return () => window.clearTimeout(id);
  }, [text, search]);

  const clear = () => {
    setText("");
    onSearch("");
  };

  return (
    <Paper sx={{ p: 2 }}>
      {/* useFlexGap: spacing by gap, not child margins - with margins a
          control that wrapped to a new line kept its left margin and sat
          indented under the search box. */}
      <Stack direction="row" spacing={1.5} useFlexGap sx={{ alignItems: "center", flexWrap: "wrap" }}>
        <TextField
          size="small"
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder={placeholder}
          sx={{ minWidth: { xs: "100%", sm: 260 } }}
          slotProps={{
            htmlInput: { "aria-label": placeholder },
            input: {
              startAdornment: (
                <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment>
              ),
              endAdornment: text ? (
                <InputAdornment position="end">
                  <IconButton size="small" aria-label="Clear search" onClick={clear} edge="end"><ClearIcon fontSize="small" /></IconButton>
                </InputAdornment>
              ) : undefined
            }
          }}
        />
        {children}
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", ml: "auto" }}>
          {resultCount !== undefined && (
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              {resultCount} {resultCount === 1 ? "result" : "results"}
            </Typography>
          )}
          {onRefresh && (
            <Tooltip title="Refresh">
              <span>
                <IconButton size="small" onClick={onRefresh} disabled={refreshing} aria-label="Refresh"><RefreshIcon /></IconButton>
              </span>
            </Tooltip>
          )}
        </Stack>
      </Stack>
    </Paper>
  );
}
