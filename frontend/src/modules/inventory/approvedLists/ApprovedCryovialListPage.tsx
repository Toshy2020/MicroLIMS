import { useEffect, useMemo, useState } from "react";
import {
  Paper,
  Box,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Button,
  InputAdornment,
  Typography,
  Tooltip,
  Alert,
  useTheme
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import RotateLeftIcon from "@mui/icons-material/RotateLeft";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import AcUnitIcon from "@mui/icons-material/AcUnit";
import { Link } from "react-router-dom";
import { useTranslation } from "../../../i18n";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { PrintButton } from "../../../components/PrintButton";
import { PrintableTable } from "../../../components/PrintableTable";
import { RegisterTable } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { TableToolbar } from "../../../components/lab/table/TableToolbar";
import { useTableState } from "../../../components/lab/table/useTableState";
import { formatLabCalendarDate } from "../../../utils/formatDate";
import { CryovialService } from "../../laboratoryConfiguration/cryovials/services/CryovialService";
import { CryovialItem } from "../../laboratoryConfiguration/cryovials/types/cryovialTypes";
import { ThawVialReasonDialog } from "../../laboratoryConfiguration/cryovials/components/ThawVialReasonDialog";

interface ApprovedCryovialFilterState {
  search: string;
  organism: string;
  storage: string;
  manufacturer: string;
  status: string; // "" | "in_stock" | "depleted"
  expiryRange: string; // "" | "expiring_30" | "expiring_60" | "valid"
}

const TABLE_KEY = "inventory.approvedCryovials";

function textFilter(filters: Record<string, string | string[]>, name: string): string {
  const value = filters[name];
  return typeof value === "string" ? value : "";
}

function isExpiringSoon(expiryDateStr: string | null, daysThreshold: number = 30): boolean {
  if (!expiryDateStr) return false;
  const expiry = new Date(expiryDateStr);
  const now = new Date();
  const diffTime = expiry.getTime() - now.getTime();
  const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
  return diffDays <= daysThreshold;
}

export function ApprovedCryovialListPage({ embedded = false }: { embedded?: boolean }) {
  const { t } = useTranslation("inventory");
  const theme = useTheme();
  const { action, detected, purple } = theme.custom.status;

  // Data states
  const [cryovials, setCryovials] = useState<CryovialItem[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  // The search, dropdowns and page live in the address and in saved views; the columns in the column chooser.
  const tableState = useTableState(TABLE_KEY);
  const { setFilter, setSearch, applyState } = tableState;
  const stateFilters = tableState.state.filters;
  const stateSearch = tableState.state.search;
  const filters = useMemo<ApprovedCryovialFilterState>(() => ({
    search: stateSearch,
    organism: textFilter(stateFilters, "organism"),
    storage: textFilter(stateFilters, "storage"),
    manufacturer: textFilter(stateFilters, "manufacturer"),
    status: textFilter(stateFilters, "status"),
    expiryRange: textFilter(stateFilters, "expiryRange")
  }), [stateFilters, stateSearch]);

  // Thaw action dialog state
  const [thawItem, setThawItem] = useState<CryovialItem | null>(null);

  const loadData = async () => {
    try {
      setLoading(true);
      setError(null);
      const all: CryovialItem[] = await CryovialService.getAll();
      const now = new Date();
      // Approved, non-destroyed cryovials available for use (unexpired)
      const approvedList = (all || []).filter(
        (c) => c.approvalStatus === "Approved" && !c.isDestroyed && new Date(c.expiryDate) > now
      );
      setCryovials(approvedList);
    } catch {
      setError(t("cryovials.loadError"));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  // Extract distinct dynamic filter options from loaded approved cryovials
  const uniqueOrganisms = useMemo(() => {
    const set = new Set<string>();
    cryovials.forEach((c) => {
      const name = c.organism?.scientificName ?? c.organismNameSnapshot;
      if (name?.trim()) set.add(name.trim());
    });
    return Array.from(set).sort();
  }, [cryovials]);

  const uniqueStorageConditions = useMemo(() => {
    const set = new Set<string>();
    cryovials.forEach((c) => {
      if (c.storageCondition?.trim()) set.add(c.storageCondition.trim());
    });
    return Array.from(set).sort();
  }, [cryovials]);

  const uniqueManufacturers = useMemo(() => {
    const set = new Set<string>();
    cryovials.forEach((c) => {
      const mfg = c.manufacturerName || c.material?.manufacturerName;
      if (mfg?.trim()) set.add(mfg.trim());
    });
    return Array.from(set).sort();
  }, [cryovials]);

  const handleFilterChange = (field: keyof ApprovedCryovialFilterState, value: string) => {
    if (field === "search") {
      setSearch(value);
    } else {
      setFilter(field, value);
    }
  };

  const handleReset = () => {
    applyState(
      { filters: {}, search: "", sort: tableState.state.sort, pageSize: tableState.state.pageSize },
      tableState.viewId
    );
  };

  // Filtered dataset combining search and compact dropdown filters
  const filteredCryovials = useMemo(() => {
    return cryovials.filter((c) => {
      // 1. Text search across cryovial code, organism, ATCC no., manufacturer, storage, batch
      if (filters.search.trim()) {
        const q = filters.search.toLowerCase().trim();
        const codeMatch = c.code?.toLowerCase().includes(q);
        const orgName = (c.organism?.scientificName ?? c.organismNameSnapshot ?? "").toLowerCase();
        const orgMatch = orgName.includes(q);
        const atccMatch = (c.organism?.atccNumber ?? "").toLowerCase().includes(q);
        const strainMatch = (c.organism?.strainNumber ?? "").toLowerCase().includes(q);
        const mfg = (c.manufacturerName || c.material?.manufacturerName || "").toLowerCase();
        const mfgMatch = mfg.includes(q);
        const storageMatch = (c.storageCondition ?? "").toLowerCase().includes(q);
        const matNameMatch = (c.material?.materialName ?? "").toLowerCase().includes(q);
        const batchMatch = (c.material?.batchNumber ?? "").toLowerCase().includes(q);

        if (
          !codeMatch &&
          !orgMatch &&
          !atccMatch &&
          !strainMatch &&
          !mfgMatch &&
          !storageMatch &&
          !matNameMatch &&
          !batchMatch
        ) {
          return false;
        }
      }

      // 2. Organism filter
      if (filters.organism) {
        const orgName = c.organism?.scientificName ?? c.organismNameSnapshot;
        if (orgName !== filters.organism) return false;
      }

      // 3. Storage condition filter
      if (filters.storage && c.storageCondition !== filters.storage) {
        return false;
      }

      // 4. Manufacturer filter
      if (filters.manufacturer) {
        const mfg = c.manufacturerName || c.material?.manufacturerName;
        if (mfg !== filters.manufacturer) return false;
      }

      // 5. Status filter (In Stock vs Depleted)
      if (filters.status) {
        if (filters.status === "in_stock" && c.vialsRemaining <= 0) return false;
        if (filters.status === "depleted" && c.vialsRemaining > 0) return false;
      }

      // 6. Expiry filter
      if (filters.expiryRange) {
        const now = new Date();
        const expiry = new Date(c.expiryDate);
        if (filters.expiryRange === "expiring_30") {
          const diffDays = Math.ceil((expiry.getTime() - now.getTime()) / (1000 * 60 * 60 * 24));
          if (diffDays <= 0 || diffDays > 30) return false;
        } else if (filters.expiryRange === "expiring_60") {
          const diffDays = Math.ceil((expiry.getTime() - now.getTime()) / (1000 * 60 * 60 * 24));
          if (diffDays <= 0 || diffDays > 60) return false;
        } else if (filters.expiryRange === "valid") {
          if (expiry <= now) return false;
        }
      }

      return true;
    });
  }, [cryovials, filters]);

  const hasActiveFilters = Boolean(
    filters.search.trim() ||
      filters.organism ||
      filters.storage ||
      filters.manufacturer ||
      filters.status ||
      filters.expiryRange
  );

  const handleThawConfirm = async (reason: string) => {
    if (!thawItem) return;
    await CryovialService.thawVial(thawItem.id, reason);
    setMessage({
      text: t("cryovials.thawed", { code: thawItem.code }),
      ok: true
    });
    setThawItem(null);
    loadData();
  };

  const columns: RegisterColumn<CryovialItem>[] = [
    {
      key: "code",
      label: t("cryovials.columns.code"),
      render: (c) => (
        <>
          <Typography
            sx={{ fontSize: 13, fontWeight: 700, fontFamily: "monospace", color: theme.palette.primary.main }}
          >
            {c.code}
          </Typography>
          {c.material?.batchNumber && (
            <Typography sx={{ fontSize: 12, color: "text.secondary" }} noWrap>
              {t("cryovials.columns.batch", { batch: c.material.batchNumber })}
            </Typography>
          )}
        </>
      )
    },
    {
      key: "organism",
      label: t("cryovials.columns.organism"),
      render: (c) => (
        <>
          <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
            {c.organism?.scientificName ?? c.organismNameSnapshot}
          </Typography>
          {c.material?.materialName && (
            <Typography sx={{ fontSize: 12, color: "text.secondary" }} noWrap>
              {c.material.materialName}
            </Typography>
          )}
        </>
      )
    },
    {
      key: "atcc",
      label: t("cryovials.columns.atcc"),
      render: (c) => (
        <Typography sx={{ fontSize: 12, color: "text.primary", fontFamily: "monospace" }}>
          {c.organism?.atccNumber ?? "—"}
        </Typography>
      )
    },
    {
      key: "manufacturer",
      label: t("cryovials.columns.manufacturer"),
      render: (c) => (
        <Typography sx={{ fontSize: 12, color: "text.primary" }}>
          {c.manufacturerName || c.material?.manufacturerName || "—"}
        </Typography>
      )
    },
    {
      key: "storage",
      label: t("cryovials.columns.storage"),
      render: (c) => <Typography sx={{ fontSize: 12, color: "text.primary" }}>{c.storageCondition || "—"}</Typography>
    },
    {
      key: "vials",
      label: t("cryovials.columns.vials"),
      render: (c) => (
        <Typography
          sx={{ fontSize: 13, fontWeight: 700, color: c.vialsRemaining <= 0 ? detected.text : "text.primary" }}
        >
          {t("cryovials.columns.vialsOf", { remaining: c.vialsRemaining, total: c.numberOfVialsPrepared })}
        </Typography>
      )
    },
    {
      key: "expiry",
      label: t("cryovials.columns.expiry"),
      render: (c) => {
        const expiring = isExpiringSoon(c.expiryDate, 30);
        return (
          <>
            <Typography
              sx={{ fontSize: 12, fontWeight: expiring ? 700 : 500, color: expiring ? action.text : "text.primary" }}
            >
              {formatLabCalendarDate(c.expiryDate)}
            </Typography>
            {expiring && (
              <Typography sx={{ fontSize: 12, color: action.text, fontWeight: 600 }}>{t("cryovials.columns.expiringSoon")}</Typography>
            )}
          </>
        );
      }
    },
    {
      key: "status",
      label: t("cryovials.columns.status"),
      render: (c) =>
        c.vialsRemaining <= 0 ? (
          <StatusBadge status="Depleted" label={t("cryovials.status.depleted")} />
        ) : (
          <StatusBadge status="Approved" label={t("cryovials.status.approved")} />
        )
    }
  ];

  // Printable table columns matching the approved register
  const printColumns = [
    { label: "Cryovial Code", render: (c: CryovialItem) => c.code },
    { label: "Organism", render: (c: CryovialItem) => c.organism?.scientificName ?? c.organismNameSnapshot },
    { label: "ATCC No.", render: (c: CryovialItem) => c.organism?.atccNumber ?? "—" },
    { label: "Manufacturer", render: (c: CryovialItem) => c.manufacturerName || c.material?.manufacturerName || "—" },
    { label: "Storage", render: (c: CryovialItem) => c.storageCondition || "—" },
    {
      label: "Vials Remaining",
      render: (c: CryovialItem) => `${c.vialsRemaining} of ${c.numberOfVialsPrepared}`
    },
    { label: "Expiry", render: (c: CryovialItem) => formatLabCalendarDate(c.expiryDate, { english: true }) },
    {
      label: "Status",
      render: (c: CryovialItem) => (c.vialsRemaining === 0 ? "Depleted" : "Approved")
    }
  ];

  return (
    <>
      <Box className="no-print">
        {!embedded && (
          <PageHeader
            title={t("cryovials.title")}
            subtitle={t("cryovials.subtitle")}
          />
        )}

        {message && (
          <Alert
            severity={message.ok ? "success" : "error"}
            onClose={() => setMessage(null)}
            sx={{ mb: 2 }}
          >
            {message.text}
          </Alert>
        )}

        {error && (
          <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        {/* Search & Filter Panel */}
        <Paper
          elevation={0}
          sx={{
            p: 2,
            mb: 2.5,
            border: "1px solid",
            borderColor: "divider",
            borderRadius: 2,
            bgcolor: "background.paper"
          }}
        >
          <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>
            {/* Prominent Search Bar */}
            <TextField
              fullWidth
              size="small"
              placeholder={t("cryovials.filters.searchPlaceholder")}
              value={filters.search}
              onChange={(e) => handleFilterChange("search", e.target.value)}
              sx={{
                "& .MuiOutlinedInput-root": {
                  borderRadius: 1.5,
                  bgcolor: "background.default"
                }
              }}
              slotProps={{ htmlInput: { "aria-label": t("cryovials.filters.searchAria") },
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <SearchIcon sx={{ color: "text.secondary", fontSize: 20 }} />
                    </InputAdornment>
                  )
                }
              }}
            />

            {/* Filter Dropdown Row & Reset Button */}
            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: {
                  xs: "1fr",
                  sm: "repeat(2, 1fr)",
                  md: "1.4fr 1.2fr 1.3fr 1.1fr 1.1fr auto"
                },
                gap: 1.5,
                alignItems: "center"
              }}
            >
              {/* Organism Filter */}
              <FormControl size="small" fullWidth>
                <InputLabel id="cryovial-organism-label">{t("cryovials.filters.organism")}</InputLabel>
                <Select
                  labelId="cryovial-organism-label"
                  label={t("cryovials.filters.organism")}
                  value={filters.organism}
                  onChange={(e) => handleFilterChange("organism", e.target.value)}
                >
                  <MenuItem value="">
                    <em>{t("cryovials.filters.allOrganisms")}</em>
                  </MenuItem>
                  {uniqueOrganisms.map((org) => (
                    <MenuItem key={org} value={org}>
                      {org}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              {/* Storage Condition Filter */}
              <FormControl size="small" fullWidth>
                <InputLabel id="cryovial-storage-label">{t("cryovials.filters.storage")}</InputLabel>
                <Select
                  labelId="cryovial-storage-label"
                  label={t("cryovials.filters.storage")}
                  value={filters.storage}
                  onChange={(e) => handleFilterChange("storage", e.target.value)}
                >
                  <MenuItem value="">
                    <em>{t("cryovials.filters.allStorage")}</em>
                  </MenuItem>
                  {uniqueStorageConditions.map((cond) => (
                    <MenuItem key={cond} value={cond}>
                      {cond}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              {/* Manufacturer Filter */}
              <FormControl size="small" fullWidth>
                <InputLabel id="cryovial-manufacturer-label">{t("cryovials.filters.manufacturer")}</InputLabel>
                <Select
                  labelId="cryovial-manufacturer-label"
                  label={t("cryovials.filters.manufacturer")}
                  value={filters.manufacturer}
                  onChange={(e) => handleFilterChange("manufacturer", e.target.value)}
                >
                  <MenuItem value="">
                    <em>{t("cryovials.filters.allManufacturers")}</em>
                  </MenuItem>
                  {uniqueManufacturers.map((mfg) => (
                    <MenuItem key={mfg} value={mfg}>
                      {mfg}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              {/* Status Filter */}
              <FormControl size="small" fullWidth>
                <InputLabel id="cryovial-status-label">{t("cryovials.filters.status")}</InputLabel>
                <Select
                  labelId="cryovial-status-label"
                  label={t("cryovials.filters.status")}
                  value={filters.status}
                  onChange={(e) => handleFilterChange("status", e.target.value)}
                >
                  <MenuItem value="">
                    <em>{t("cryovials.filters.allStatuses")}</em>
                  </MenuItem>
                  <MenuItem value="in_stock">{t("cryovials.filters.inStock")}</MenuItem>
                  <MenuItem value="depleted">{t("cryovials.filters.depleted")}</MenuItem>
                </Select>
              </FormControl>

              {/* Expiry Range Filter */}
              <FormControl size="small" fullWidth>
                <InputLabel id="cryovial-expiry-label">{t("cryovials.filters.expiryDate")}</InputLabel>
                <Select
                  labelId="cryovial-expiry-label"
                  label={t("cryovials.filters.expiryDate")}
                  value={filters.expiryRange}
                  onChange={(e) => handleFilterChange("expiryRange", e.target.value)}
                >
                  <MenuItem value="">
                    <em>{t("cryovials.filters.allExpiry")}</em>
                  </MenuItem>
                  <MenuItem value="expiring_30">{t("cryovials.filters.expiring30")}</MenuItem>
                  <MenuItem value="expiring_60">{t("cryovials.filters.expiring60")}</MenuItem>
                  <MenuItem value="valid">{t("cryovials.filters.valid")}</MenuItem>
                </Select>
              </FormControl>

              {/* Reset Action */}
              <Button
                size="small"
                variant="outlined"
                onClick={handleReset}
                disabled={!hasActiveFilters}
                startIcon={<RotateLeftIcon fontSize="small" />}
                sx={{
                  borderColor: "divider",
                  color: "text.secondary",
                  minWidth: 95,
                  height: 40,
                  "&:hover": { bgcolor: "background.default", borderColor: "text.secondary" }
                }}
              >
                {t("cryovials.filters.reset")}
              </Button>
            </Box>
          </Box>
        </Paper>

        {/* Register Summary & Table Toolbar Header */}
        <Box
          sx={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            mb: 1.5,
            flexWrap: "wrap",
            gap: 1
          }}
        >
          <Box sx={{ display: "flex", alignItems: "baseline", gap: 1.5 }}>
            <SectionTitle>{t("cryovials.section")}</SectionTitle>
            {!loading && (
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                {t("cryovials.found", { count: filteredCryovials.length })}
              </Typography>
            )}
          </Box>

          <Box sx={{ display: "flex", alignItems: "center", gap: 1.25 }}>
            {/* Preserved Print Button */}
            <PrintButton />
          </Box>
        </Box>

        <RegisterTable
          columns={columns}
          rows={filteredCryovials}
          getRowId={(c) => c.id}
          tableKey={TABLE_KEY}
          tableState={tableState}
          toolbar={<TableToolbar tableKey={TABLE_KEY} tableState={tableState} showSearch={false} />}
          loading={loading}
          trailingCell={{
            label: t("cryovials.columns.actions"),
            render: (c) => (
              <Box
                sx={{
                  display: "flex",
                  justifyContent: "flex-end",
                  alignItems: "center",
                  gap: 0.75
                }}
              >
                {c.vialsRemaining > 0 && (
                  <Tooltip title={t("cryovials.thawTooltip")}>
                    <Button
                      size="small"
                      variant="outlined"
                      onClick={() => setThawItem(c)}
                      startIcon={<AcUnitIcon fontSize="small" />}
                      sx={{
                        px: 1,
                        py: 0.25,
                        fontSize: 12,
                        fontWeight: 700,
                        borderColor: theme.palette.primary.main,
                        color: theme.palette.primary.main,
                        "&:hover": {
                          borderColor: theme.palette.primary.dark,
                          bgcolor: purple.bg
                        }
                      }}
                    >
                      {t("cryovials.thawVial")}
                    </Button>
                  </Tooltip>
                )}

                <Tooltip title={t("cryovials.reportTooltip")}>
                  <Button
                    component={Link}
                    to={`/cryovials/${c.id}/report`}
                    target="_blank"
                    rel="noopener"
                    size="small"
                    variant="outlined"
                    startIcon={<DescriptionOutlinedIcon fontSize="small" />}
                    sx={{
                      px: 1,
                      py: 0.25,
                      fontSize: 12,
                      borderColor: "divider",
                      color: "text.secondary",
                      "&:hover": { bgcolor: "background.default" }
                    }}
                  >
                    {t("cryovials.report")}
                  </Button>
                </Tooltip>
              </Box>
            )
          }}
          empty={{
            title: hasActiveFilters
              ? t("cryovials.emptyFiltered")
              : t("cryovials.empty"),
            action: hasActiveFilters ? (
              <Button
                size="small"
                variant="outlined"
                onClick={handleReset}
                startIcon={<RotateLeftIcon fontSize="small" />}
                sx={{ borderColor: "divider", color: "text.secondary", mt: 0.5 }}
              >
                {t("cryovials.filters.resetFilters")}
              </Button>
            ) : undefined
          }}
        />
      </Box>

      {/* Printable version rendered only in print dialog */}
      <PrintableTable
        title="Approved Cryovial List"
        subtitle="Approved, non-destroyed, unexpired cryovials available for use."
        rows={filteredCryovials}
        getRowId={(c) => c.id}
        columns={printColumns}
      />

      {/* Thaw Vial Confirmation Dialog */}
      <ThawVialReasonDialog
        open={thawItem != null}
        cryovial={thawItem}
        onCancel={() => setThawItem(null)}
        onConfirm={handleThawConfirm}
      />
    </>
  );
}

