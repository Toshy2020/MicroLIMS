import { Box, TextField, Select, MenuItem, Button, Stack, InputAdornment } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import RestartAltIcon from "@mui/icons-material/RestartAlt";
import { useTranslation } from "../../../../i18n";

interface ItemFilterBarProps {
  searchQuery: string;
  onSearchChange: (value: string) => void;
  categoryFilter: string;
  onCategoryChange: (value: string) => void;
  statusFilter: string;
  onStatusChange: (value: string) => void;
  onReset: () => void;
}

const CATEGORY_VALUES = ["ALL", "FinishedProduct", "RawMaterial", "PackagingMaterial"] as const;
const STATUS_VALUES = ["ALL", "Active", "Frozen"] as const;

export function ItemFilterBar({
  searchQuery,
  onSearchChange,
  categoryFilter,
  onCategoryChange,
  statusFilter,
  onStatusChange,
  onReset,
}: ItemFilterBarProps) {
  const { t } = useTranslation("labConfig");
  const isFiltered = searchQuery !== "" || categoryFilter !== "ALL" || statusFilter !== "ALL";

  const categories = CATEGORY_VALUES.map((value) => ({
    value,
    label: value === "ALL" ? t("items.categories.all") : t(`items.categories.${value}`),
  }));

  const statuses = STATUS_VALUES.map((value) => ({
    value,
    label: value === "ALL" ? t("items.statuses.all") : t(`items.statuses.${value}`),
  }));

  return (
    <Box sx={{ mb: 2.5, p: 2, bgcolor: "background.paper", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{
        alignItems: "center"
      }}>
        <TextField
          size="small"
          placeholder={t("items.filterBar.searchPlaceholder")}
          value={searchQuery}
          onChange={(e) => onSearchChange(e.target.value)}
          sx={{ flexGrow: 1, minWidth: { xs: "100%", sm: 280 } }}
          slotProps={{
            htmlInput: { "aria-label": t("items.filterBar.searchAriaLabel") },
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" sx={{ color: "text.secondary" }} />
                </InputAdornment>
              ),
            }
          }}
        />

        <Select
          size="small"
          value={categoryFilter}
          onChange={(e) => onCategoryChange(e.target.value)}
          inputProps={{ "aria-label": t("items.filterBar.categoryAriaLabel") }}
          sx={{ minWidth: 180, width: { xs: "100%", sm: "auto" } }}
        >
          {categories.map((c) => (
            <MenuItem key={c.value} value={c.value}>
              {c.label}
            </MenuItem>
          ))}
        </Select>

        <Select
          size="small"
          value={statusFilter}
          onChange={(e) => onStatusChange(e.target.value)}
          inputProps={{ "aria-label": t("items.filterBar.statusAriaLabel") }}
          sx={{ minWidth: 150, width: { xs: "100%", sm: "auto" } }}
        >
          {statuses.map((s) => (
            <MenuItem key={s.value} value={s.value}>
              {s.label}
            </MenuItem>
          ))}
        </Select>

        {isFiltered && (
          <Button
            size="small"
            variant="outlined"
            color="inherit"
            startIcon={<RestartAltIcon fontSize="small" />}
            onClick={onReset}
            sx={{ textTransform: "none", height: 40, width: { xs: "100%", sm: "auto" }, flexShrink: 0 }}
          >
            {t("items.filterBar.reset")}
          </Button>
        )}
      </Stack>
    </Box>
  );
}
