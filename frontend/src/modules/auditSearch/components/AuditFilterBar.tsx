import { useState } from "react";
import {
  Paper,
  Box,
  TextField,
  Button,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Collapse,
  FormControlLabel,
  Checkbox
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import RestartAltIcon from "@mui/icons-material/RestartAlt";
import FilterListIcon from "@mui/icons-material/FilterList";
import type { AuditSearchFilterState } from "../types/auditTypes";
import { ENTITY_DISPLAY_NAMES, ACTION_CATEGORIES } from "../types/auditTypes";
import { DateField } from "../../../components/DateField";
import { useTranslation } from "../../../i18n";

interface Props {
  filters: AuditSearchFilterState;
  onFilterChange: (filters: AuditSearchFilterState) => void;
  onSearch: () => void;
  onReset: () => void;
  loading: boolean;
}

export function AuditFilterBar({
  filters,
  onFilterChange,
  onSearch,
  onReset,
  loading
}: Props) {
  const { t } = useTranslation("auditSearch");
  const [showAdvanced, setShowAdvanced] = useState(false);

  const handleChange = (key: keyof AuditSearchFilterState, val: string | boolean) => {
    onFilterChange({ ...filters, [key]: val });
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter") {
      onSearch();
    }
  };

  return (
    <Paper
      sx={{
        p: 2.5,
        mb: 3,
        border: "1px solid",
        borderColor: "divider",
        borderRadius: 2
      }}
    >
      {/* Primary quick filters */}
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            md: "repeat(4, 1fr)"
          },
          gap: 2,
          mb: 2
        }}
        onKeyDown={handleKeyDown}
      >
        <DateField
          label={t("filter.fromDate")}
          value={filters.fromDate}
          onChange={(v) => handleChange("fromDate", v)}
          max={filters.toDate}
        />

        <DateField
          label={t("filter.toDate")}
          value={filters.toDate}
          onChange={(v) => handleChange("toDate", v)}
          min={filters.fromDate}
        />

        <FormControl size="small" fullWidth>
          <InputLabel id="audit-filter-action-label">{t("filter.action")}</InputLabel>
          <Select
            labelId="audit-filter-action-label"
            label={t("filter.action")}
            value={filters.action}
            onChange={(e) => handleChange("action", e.target.value)}
          >
            <MenuItem value="">{t("filter.allActions")}</MenuItem>
            <MenuItem value="Create">Create</MenuItem>
            <MenuItem value="Update">Update</MenuItem>
            <MenuItem value="Delete">Delete</MenuItem>
          </Select>
        </FormControl>

        <FormControl size="small" fullWidth>
          <InputLabel id="audit-filter-entity-label">{t("filter.entityCategory")}</InputLabel>
          <Select
            labelId="audit-filter-entity-label"
            label={t("filter.entityCategory")}
            value={filters.entityName}
            onChange={(e) => handleChange("entityName", e.target.value)}
          >
            <MenuItem value="">{t("filter.allEntities")}</MenuItem>
            {Object.entries(ENTITY_DISPLAY_NAMES).map(([key, label]) => (
              <MenuItem key={key} value={key}>
                {label} ({key})
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      </Box>

      {/* Advanced laboratory reference filters */}
      <Collapse in={showAdvanced}>
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: {
              xs: "1fr",
              sm: "repeat(2, 1fr)",
              md: "repeat(4, 1fr)"
            },
            gap: 2,
            pt: 1,
            pb: 2,
            borderTop: "1px dashed",
            borderColor: "divider"
          }}
          onKeyDown={handleKeyDown}
        >
          <TextField
            size="small"
            label={t("filter.sampleReference")}
            placeholder="e.g. FP0107026"
            value={filters.sampleReferenceNumber}
            onChange={(e) => handleChange("sampleReferenceNumber", e.target.value)}
          />

          <TextField
            size="small"
            label={t("filter.batchNumber")}
            placeholder="e.g. B-2026-088"
            value={filters.batchNumber}
            onChange={(e) => handleChange("batchNumber", e.target.value)}
          />

          <TextField
            size="small"
            label={t("filter.controlNumber")}
            placeholder="e.g. CTRL-904"
            value={filters.controlNumber}
            onChange={(e) => handleChange("controlNumber", e.target.value)}
          />

          <TextField
            size="small"
            label={t("filter.mediaLotNumber")}
            placeholder="e.g. MED-044"
            value={filters.mediaLotNumber}
            onChange={(e) => handleChange("mediaLotNumber", e.target.value)}
          />

          <TextField
            size="small"
            label={t("filter.cryovial")}
            placeholder="e.g. CRYO-019"
            value={filters.cryovialCode}
            onChange={(e) => handleChange("cryovialCode", e.target.value)}
          />

          <TextField
            size="small"
            label={t("filter.userId")}
            placeholder="e.g. 5"
            value={filters.userId}
            onChange={(e) => handleChange("userId", e.target.value)}
          />

          <FormControl size="small" fullWidth>
            <InputLabel id="audit-filter-category-label">{t("filter.actionCategory")}</InputLabel>
            <Select
              labelId="audit-filter-category-label"
              label={t("filter.actionCategory")}
              value={filters.actionCategory}
              onChange={(e) => handleChange("actionCategory", e.target.value)}
            >
              <MenuItem value="">{t("filter.allCategories")}</MenuItem>
              {ACTION_CATEGORIES.map((c) => (
                <MenuItem key={c} value={c}>{c}</MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl size="small" fullWidth>
            <InputLabel id="audit-filter-actor-label">{t("filter.actorType")}</InputLabel>
            <Select
              labelId="audit-filter-actor-label"
              label={t("filter.actorType")}
              value={filters.actorType}
              onChange={(e) => handleChange("actorType", e.target.value)}
            >
              <MenuItem value="">{t("filter.anyActor")}</MenuItem>
              <MenuItem value="User">{t("filter.actorUser")}</MenuItem>
              <MenuItem value="System">{t("filter.actorSystem")}</MenuItem>
            </Select>
          </FormControl>

          <TextField
            size="small"
            label={t("filter.correlationId")}
            placeholder="GUID"
            value={filters.correlationId}
            onChange={(e) => handleChange("correlationId", e.target.value)}
          />

          <TextField
            size="small"
            label={t("filter.reasonContains")}
            value={filters.reason}
            onChange={(e) => handleChange("reason", e.target.value)}
          />

          <FormControlLabel
            control={
              <Checkbox
                size="small"
                checked={filters.negativeEntityId}
                onChange={(e) => handleChange("negativeEntityId", e.target.checked)}
              />
            }
            label={t("filter.negativeId")}
            slotProps={{ typography: { sx: { fontSize: 13 } } }}
          />
        </Box>
      </Collapse>

      {/* Footer controls */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Button
          size="small"
          variant="text"
          startIcon={<FilterListIcon />}
          onClick={() => setShowAdvanced(!showAdvanced)}
          sx={{ textTransform: "none", fontSize: 12, color: "text.secondary" }}
        >
          {showAdvanced ? t("filter.hideIdentifiers") : t("filter.moreIdentifiers")}
        </Button>

        <Box sx={{ display: "flex", gap: 1 }}>
          <Button
            size="small"
            variant="outlined"
            startIcon={<RestartAltIcon />}
            onClick={onReset}
            disabled={loading}
            sx={{ textTransform: "none", color: "text.secondary" }}
          >
            {t("filter.reset")}
          </Button>

          <Button
            id="audit-search-submit-btn"
            size="small"
            variant="contained"
            color="primary"
            startIcon={<SearchIcon />}
            onClick={onSearch}
            disabled={loading}
            sx={{
              px: 2.5,
              textTransform: "none",
              fontWeight: 600
            }}
          >
            {loading ? t("filter.searching") : t("filter.search")}
          </Button>
        </Box>
      </Box>
    </Paper>
  );
}
