import { Box, ButtonBase, InputAdornment, Paper, Skeleton, Stack, TextField, Typography } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import { ConfigBadge } from "./types";
import { ToneChip } from "./ToneChip";

export interface MasterListItem {
  id: number;
  title: string;
  subtitle?: string;
  badge?: ConfigBadge;
}

interface Props {
  searchLabel: string;
  searchPlaceholder?: string;
  search: string;
  onSearchChange: (value: string) => void;
  items: MasterListItem[];
  selectedId: number | null;
  onSelect: (id: number) => void;
  loading?: boolean;
  emptyText: string;
}

// Left-hand list of a master/detail configuration page (Water systems,
// EM departments): search box plus one selectable row per parent record.
export function ConfigMasterList({
  searchLabel, searchPlaceholder, search, onSearchChange, items, selectedId, onSelect, loading, emptyText
}: Props) {
  return (
    <Paper variant="outlined" sx={{ borderRadius: 2, overflow: "hidden", display: "flex", flexDirection: "column" }}>
      <Box sx={{ p: 2, borderBottom: "1px solid", borderColor: "divider" }}>
        <TextField
          size="small"
          fullWidth
          label={searchLabel}
          placeholder={searchPlaceholder}
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          slotProps={{
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" sx={{ color: "text.secondary" }} />
                </InputAdornment>
              )
            }
          }}
        />
      </Box>
      <Stack component="ul" spacing={0.5} sx={{ listStyle: "none", m: 0, p: 1 }}>
        {loading && items.length === 0
          ? [0, 1, 2].map((i) => (
              <Box component="li" key={i}>
                <Skeleton variant="rounded" height={58} />
              </Box>
            ))
          : items.map((item) => {
              const selected = item.id === selectedId;
              return (
                <Box component="li" key={item.id}>
                  <ButtonBase
                    onClick={() => onSelect(item.id)}
                    aria-current={selected ? "true" : undefined}
                    sx={{
                      width: "100%",
                      justifyContent: "flex-start",
                      textAlign: "left",
                      px: 1.75,
                      py: 1.5,
                      borderRadius: 1.5,
                      border: "1px solid",
                      borderColor: selected ? "primary.main" : "transparent",
                      bgcolor: selected ? "action.selected" : "transparent",
                      transition: "background-color 150ms ease, border-color 150ms ease",
                      "&:hover": { bgcolor: selected ? "action.selected" : "action.hover" },
                      "&.Mui-focusVisible": { outline: "2px solid", outlineColor: "primary.main", outlineOffset: 1 }
                    }}
                  >
                    <Stack spacing={0.75} sx={{ minWidth: 0 }}>
                      <Typography sx={{ fontSize: 15, fontWeight: 600, overflowWrap: "anywhere" }}>{item.title}</Typography>
                      <Stack useFlexGap direction="row" spacing={0.75} sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 0.5 }}>
                        {item.subtitle && <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{item.subtitle}</Typography>}
                        {item.badge && <ToneChip {...item.badge} />}
                      </Stack>
                    </Stack>
                  </ButtonBase>
                </Box>
              );
            })}
        {!loading && items.length === 0 && (
          <Box component="li">
            <Typography sx={{ fontSize: 13, color: "text.secondary", p: 1.5 }}>{emptyText}</Typography>
          </Box>
        )}
      </Stack>
    </Paper>
  );
}
