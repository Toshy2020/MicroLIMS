import { Select, MenuItem, Button } from "@mui/material";
import FilterAltOffOutlinedIcon from "@mui/icons-material/FilterAltOffOutlined";
import { FilterBar } from "../../../../components/lab";
import { useTranslation } from "../../../../i18n";

interface Props {
  search: string;
  onSearchChange: (v: string) => void;
  selectedMaterialId: string;
  onMaterialChange: (v: string) => void;
  selectedStatus: string;
  onStatusChange: (v: string) => void;
  materials: any[];
  onResetFilters: () => void;
  resultCount?: number;
  onRefresh?: () => void;
  refreshing?: boolean;
}

const STATUS_VALUES = [
  "Pending Evaluation",
  "Awaiting Approval",
  "Released",
  "Rejected",
  "Out of Stock"
] as const;

export function MediaLotFilterBar({
  search,
  onSearchChange,
  selectedMaterialId,
  onMaterialChange,
  selectedStatus,
  onStatusChange,
  materials,
  onResetFilters,
  resultCount,
  onRefresh,
  refreshing
}: Props) {
  const { t } = useTranslation("labConfig");
  const hasActiveFilters = Boolean(search || selectedMaterialId || selectedStatus);
  const materialSelectValue = materials.some((m) => String(m.id) === selectedMaterialId) ? selectedMaterialId : "";

  return (
    <FilterBar
      search={search}
      onSearch={onSearchChange}
      placeholder={t("media.lotFilterBar.searchPlaceholder")}
      resultCount={resultCount}
      onRefresh={onRefresh}
      refreshing={refreshing}
    >
      <Select
        size="small"
        displayEmpty
        value={materialSelectValue}
        onChange={(e) => onMaterialChange(e.target.value)}
        sx={{ minWidth: 180 }}
        inputProps={{ "aria-label": t("media.lotFilterBar.materialAriaLabel") }}
      >
        <MenuItem value="">
          <em>{t("media.lotFilterBar.allMaterials")}</em>
        </MenuItem>
        {materials.map((m) => (
          <MenuItem key={m.id} value={String(m.id)}>
            {m.materialName}
          </MenuItem>
        ))}
      </Select>

      <Select
        size="small"
        displayEmpty
        value={selectedStatus}
        onChange={(e) => onStatusChange(e.target.value)}
        sx={{ minWidth: 180 }}
        inputProps={{ "aria-label": t("media.lotFilterBar.statusAriaLabel") }}
      >
        <MenuItem value="">
          <em>{t("media.lotFilterBar.allStatuses")}</em>
        </MenuItem>
        {STATUS_VALUES.map((value) => (
          <MenuItem key={value} value={value}>
            {t(`media.lotFilterBar.statuses.${value}`)}
          </MenuItem>
        ))}
      </Select>

      {hasActiveFilters && (
        <Button
          size="small"
          variant="outlined"
          onClick={onResetFilters}
          startIcon={<FilterAltOffOutlinedIcon fontSize="small" />}
          sx={{ borderColor: "divider", color: "text.secondary" }}
        >
          {t("media.lotFilterBar.resetFilters")}
        </Button>
      )}
    </FilterBar>
  );
}
