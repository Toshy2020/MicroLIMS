import { useEffect, useMemo, useState } from "react";
import {
  Paper,
  Box,
  Grid,
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
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import CategoryOutlinedIcon from "@mui/icons-material/CategoryOutlined";
import { Link } from "react-router-dom";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { PrintButton } from "../../../components/PrintButton";
import { PrintableTable } from "../../../components/PrintableTable";
import { RegisterTable } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { TableToolbar } from "../../../components/lab/table/TableToolbar";
import { useTableState } from "../../../components/lab/table/useTableState";
import { formatLabDate, formatLabCalendarDate } from "../../../utils/formatDate";
import { useTranslation } from "../../../i18n";
import { masterDataOptions, mediaClassLabel } from "../../../services/masterDataOptions";
import { UserService, UserRecord } from "../../users/services/UserService";
import type { MediaLot } from "../../laboratoryConfiguration/mediaEvaluation/types/mediaEvaluationTypes";
import { StatusTone } from "../../../theme/statusTokens";

// GET /media/released returns MediaResponse. The three extra fields are read by
// this page but the backend does not send them (they are always undefined).
type ReleasedMediaRow = MediaLot & {
  mediaType?: { class?: string };
  preparedByName?: string;
  preparedByUser?: { fullName?: string };
};

interface ApprovedMediaFilterState {
  search: string;
  mediaType: string;
  manufacturer: string;
  status: string;
  expiryRange: string; // "" | "expiring_30" | "expiring_60" | "valid"
}

const TABLE_KEY = "inventory.approvedMedia";

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

export function ApprovedMediaListPage({ embedded = false }: { embedded?: boolean }) {
  const { t } = useTranslation("inventory");
  const theme = useTheme();
  const { action, purple } = theme.custom.status;

  // Data states
  const [media, setMedia] = useState<ReleasedMediaRow[]>([]);
  const [userMap, setUserMap] = useState<Record<number, string>>({});
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // The search, dropdowns and page live in the address and in saved views; the columns in the column chooser.
  const tableState = useTableState(TABLE_KEY);
  const { setFilter, setSearch, applyState } = tableState;
  const stateFilters = tableState.state.filters;
  const stateSearch = tableState.state.search;
  const filters = useMemo<ApprovedMediaFilterState>(() => ({
    search: stateSearch,
    mediaType: textFilter(stateFilters, "mediaType"),
    manufacturer: textFilter(stateFilters, "manufacturer"),
    status: textFilter(stateFilters, "status"),
    expiryRange: textFilter(stateFilters, "expiryRange")
  }), [stateFilters, stateSearch]);

  const loadData = async () => {
    try {
      setLoading(true);
      setError(null);
      const [releasedMedia, users] = await Promise.all([
        masterDataOptions.getReleasedMedia(),
        // "all": only resolves stored preparer ids to names (historical attribution), never a picker.
        UserService.getAll("all").catch(() => [] as UserRecord[])
      ]);

      const map: Record<number, string> = {};
      if (Array.isArray(users)) {
        users.forEach((u) => {
          if (u.id) map[u.id] = u.fullName;
        });
      }
      setUserMap(map);
      setMedia(releasedMedia || []);
    } catch {
      setError(t("media.loadError"));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  // Unique filter dropdown options
  const uniqueMediaTypes = useMemo(() => {
    const map = new Map<string, string>();
    media.forEach((m) => {
      const cls = m.mediaType?.class;
      if (cls) {
        map.set(cls, mediaClassLabel(cls));
      }
    });
    return Array.from(map.entries()).sort((a, b) => a[1].localeCompare(b[1]));
  }, [media]);

  const uniqueManufacturers = useMemo(() => {
    const set = new Set<string>();
    media.forEach((m) => {
      const mfg = m.manufacturerName || m.material?.manufacturerName;
      if (mfg?.trim()) set.add(mfg.trim());
    });
    return Array.from(set).sort();
  }, [media]);

  const handleFilterChange = (field: keyof ApprovedMediaFilterState, value: string) => {
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

  // Filtered dataset combining text search and compact dropdown filters
  const filteredMedia = useMemo(() => {
    return media.filter((m) => {
      // 1. Text search
      if (filters.search.trim()) {
        const q = filters.search.toLowerCase().trim();
        const lotMatch = (m.lotNumber ?? "").toLowerCase().includes(q);
        const typeLabel = mediaClassLabel(m.mediaType?.class).toLowerCase();
        const typeMatch = typeLabel.includes(q);
        const matNameMatch = (m.material?.materialName ?? "").toLowerCase().includes(q);
        const batchMatch = (m.material?.batchNumber ?? "").toLowerCase().includes(q);
        const mfgLotMatch = (m.manufacturerLot ?? "").toLowerCase().includes(q);
        const mfgMatch = (m.manufacturerName || m.material?.manufacturerName || "").toLowerCase().includes(q);

        if (!lotMatch && !typeMatch && !matNameMatch && !batchMatch && !mfgLotMatch && !mfgMatch) {
          return false;
        }
      }

      // 2. Media Type filter
      if (filters.mediaType && m.mediaType?.class !== filters.mediaType) {
        return false;
      }

      // 3. Manufacturer filter
      if (filters.manufacturer) {
        const mfg = m.manufacturerName || m.material?.manufacturerName;
        if (mfg !== filters.manufacturer) return false;
      }

      // 4. Status filter
      if (filters.status) {
        if (filters.status === "Released" && (!m.isReleasedForUse || m.status !== "Active")) return false;
        if (filters.status === "OutOfStock" && m.status !== "OutOfStock") return false;
      }

      // 5. Expiry filter
      if (filters.expiryRange) {
        const now = new Date();
        const expiry = new Date(m.expiryDate);
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
  }, [media, filters]);

  const hasActiveFilters = Boolean(
    filters.search.trim() ||
      filters.mediaType ||
      filters.manufacturer ||
      filters.status ||
      filters.expiryRange
  );

  // Real KPI calculations from loaded released media dataset
  const kpiData = useMemo(() => {
    const totalCount = media.length;
    const inStockCount = media.filter((m) => m.status === "Active" || m.isReleasedForUse).length;
    const expiringSoonCount = media.filter((m) => isExpiringSoon(m.expiryDate, 30)).length;
    const uniqueTypesCount = new Set(
      media.map((m) => m.materialId || m.material?.materialName).filter(Boolean)
    ).size;

    return [
      {
        label: t("media.kpi.total"),
        description: t("media.kpi.totalDesc"),
        count: totalCount,
        icon: <ScienceOutlinedIcon sx={{ fontSize: 20 }} />,
        tone: "purple" as StatusTone
      },
      {
        label: t("media.kpi.inStock"),
        description: t("media.kpi.inStockDesc"),
        count: inStockCount,
        icon: <CheckCircleOutlineIcon sx={{ fontSize: 20 }} />,
        tone: "notDetected" as StatusTone
      },
      {
        label: t("media.kpi.expiring"),
        description: t("media.kpi.expiringDesc"),
        count: expiringSoonCount,
        icon: <WarningAmberOutlinedIcon sx={{ fontSize: 20 }} />,
        tone: "action" as StatusTone
      },
      {
        label: t("media.kpi.formulations"),
        description: t("media.kpi.formulationsDesc"),
        count: uniqueTypesCount,
        icon: <CategoryOutlinedIcon sx={{ fontSize: 20 }} />,
        tone: "info" as StatusTone
      }
    ];
  }, [media, t]);

  // User name resolution helper
  const getPreparedByName = (m: ReleasedMediaRow, english = false) => {
    if (m.preparedByName) return m.preparedByName;
    if (m.preparedByUser?.fullName) return m.preparedByUser.fullName;
    if (m.preparedByUserId && userMap[m.preparedByUserId]) return userMap[m.preparedByUserId];
    if (!m.preparedByUserId) return "—";
    // The printed register is English whatever the interface language.
    return english ? `User #${m.preparedByUserId}` : t("media.userFallback", { id: m.preparedByUserId });
  };

  const columns: RegisterColumn<ReleasedMediaRow>[] = [
    {
      key: "lotNumber",
      label: t("media.columns.lotNumber"),
      render: (m) => (
        <>
          <Typography
            sx={{ fontSize: 13, fontWeight: 700, fontFamily: "monospace", color: theme.palette.primary.main }}
          >
            {m.lotNumber}
          </Typography>
          {m.material?.batchNumber && (
            <Typography sx={{ fontSize: 12, color: "text.secondary" }} noWrap>
              {t("media.columns.batch", { batch: m.material.batchNumber })}
            </Typography>
          )}
        </>
      )
    },
    {
      key: "mediaType",
      label: t("media.columns.mediaType"),
      render: (m) => (
        <>
          <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
            {m.material?.materialName ?? mediaClassLabel(m.mediaType?.class) ?? "—"}
          </Typography>
          {m.material?.materialName && m.mediaType?.class && (
            <Typography sx={{ fontSize: 12, color: "text.secondary" }} noWrap>
              {mediaClassLabel(m.mediaType?.class)}
            </Typography>
          )}
        </>
      )
    },
    {
      key: "ph",
      label: t("media.columns.ph"),
      render: (m) => (
        <Typography sx={{ fontSize: 12, fontWeight: 700, fontFamily: "monospace", color: "text.primary" }}>
          {m.ph != null ? Number(m.ph).toFixed(2) : "—"}
        </Typography>
      )
    },
    {
      key: "preparedBy",
      label: t("media.columns.preparedBy"),
      render: (m) => <Typography sx={{ fontSize: 12, color: "text.primary" }}>{getPreparedByName(m)}</Typography>
    },
    {
      key: "volume",
      label: t("media.columns.volume"),
      render: (m) => <Typography sx={{ fontSize: 12, color: "text.primary" }}>{m.totalVolume || "—"}</Typography>
    },
    {
      key: "weight",
      label: t("media.columns.weight"),
      render: (m) => (
        <Typography sx={{ fontSize: 12, color: "text.primary" }}>
          {m.totalWeight != null ? `${m.totalWeight} g` : "—"}
        </Typography>
      )
    },
    {
      key: "mfgLot",
      label: t("media.columns.mfgLot"),
      render: (m) => (
        <Typography sx={{ fontSize: 12, color: "text.primary", fontFamily: "monospace" }}>
          {m.manufacturerLot || "—"}
        </Typography>
      )
    },
    {
      key: "mfgName",
      label: t("media.columns.manufacturer"),
      render: (m) => (
        <Typography sx={{ fontSize: 12, color: "text.primary" }}>
          {m.manufacturerName || m.material?.manufacturerName || "—"}
        </Typography>
      )
    },
    {
      key: "preparedOn",
      label: t("media.columns.preparedOn"),
      render: (m) => <Typography sx={{ fontSize: 12, color: "text.primary" }}>{formatLabDate(m.preparedAt)}</Typography>
    },
    {
      key: "expiry",
      label: t("media.columns.expiry"),
      render: (m) => {
        const expiring = isExpiringSoon(m.expiryDate, 30);
        return (
          <>
            <Typography
              sx={{ fontSize: 12, fontWeight: expiring ? 700 : 500, color: expiring ? action.text : "text.primary" }}
            >
              {formatLabCalendarDate(m.expiryDate)}
            </Typography>
            {expiring && (
              <Typography sx={{ fontSize: 12, color: action.text, fontWeight: 600 }}>{t("media.columns.expiringSoon")}</Typography>
            )}
          </>
        );
      }
    },
    {
      key: "status",
      label: t("media.columns.status"),
      render: (m) => (
        <StatusBadge
          status={m.status === "OutOfStock" ? "OutOfStock" : "Released"}
          label={m.status === "OutOfStock" ? t("media.status.outOfStock") : t("media.status.released")}
        />
      )
    }
  ];

  // Printable table columns
  const printColumns = [
    { label: "Lot Number", render: (m: ReleasedMediaRow) => m.lotNumber },
    {
      label: "Media Type",
      render: (m: ReleasedMediaRow) => m.material?.materialName ?? mediaClassLabel(m.mediaType?.class) ?? "—"
    },
    {
      label: "pH Result",
      render: (m: ReleasedMediaRow) => (m.ph != null ? Number(m.ph).toFixed(2) : "—")
    },
    { label: "Prepared By", render: (m: ReleasedMediaRow) => getPreparedByName(m, true) },
    { label: "Total Volume", render: (m: ReleasedMediaRow) => m.totalVolume || "—" },
    { label: "Total Weight", render: (m: ReleasedMediaRow) => (m.totalWeight != null ? `${m.totalWeight} g` : "—") },
    { label: "Manufacturer Lot", render: (m: ReleasedMediaRow) => m.manufacturerLot || "—" },
    { label: "Manufacturer", render: (m: ReleasedMediaRow) => m.manufacturerName || m.material?.manufacturerName || "—" },
    { label: "Prepared On", render: (m: ReleasedMediaRow) => formatLabDate(m.preparedAt, { english: true }) },
    { label: "Expiry", render: (m: ReleasedMediaRow) => formatLabCalendarDate(m.expiryDate, { english: true }) },
    {
      label: "Status",
      render: (m: ReleasedMediaRow) => (m.status === "OutOfStock" ? "Out of Stock" : "Released")
    }
  ];

  return (
    <>
      <Box className="no-print">
        {!embedded && (
          <PageHeader
            title={t("media.title")}
            subtitle={t("media.subtitle")}
          />
        )}

        {error && (
          <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        {/* KPI Row */}
        <Grid container spacing={1.5} sx={{ mb: 2.5 }}>
          {kpiData.map((card) => {
            const tokens = theme.custom.status[card.tone];
            return (
              <Grid
                key={card.label}
                size={{
                  xs: 12,
                  sm: 6,
                  md: 3
                }}>
                <Paper
                  elevation={0}
                  sx={{
                    p: 1.75,
                    border: "1px solid",
                    borderColor: "divider",
                    borderRadius: 2,
                    bgcolor: "background.paper",
                    boxShadow: "0 1px 3px rgba(0,0,0,0.04)"
                  }}
                >
                  <Box
                    sx={{
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      mb: 0.75
                    }}
                  >
                    <Box
                      sx={{
                        width: 36,
                        height: 36,
                        borderRadius: 1.5,
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        bgcolor: tokens.bg,
                        color: tokens.text
                      }}
                    >
                      {card.icon}
                    </Box>
                    <Typography
                      sx={{
                        fontSize: 24,
                        fontWeight: 800,
                        color: tokens.text,
                        lineHeight: 1
                      }}
                    >
                      {card.count}
                    </Typography>
                  </Box>
                  <Typography
                    sx={{ fontSize: 13, fontWeight: 700, color: "text.primary", lineHeight: 1.2 }}
                    noWrap
                  >
                    {card.label}
                  </Typography>
                  <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.25 }} noWrap>
                    {card.description}
                  </Typography>
                </Paper>
              </Grid>
            );
          })}
        </Grid>

        {/* Search & Filter Bar */}
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
            {/* Search Field */}
            <TextField
              fullWidth
              size="small"
              placeholder={t("media.filters.searchPlaceholder")}
              value={filters.search}
              onChange={(e) => handleFilterChange("search", e.target.value)}
              sx={{
                "& .MuiOutlinedInput-root": {
                  borderRadius: 1.5,
                  bgcolor: "background.default"
                }
              }}
              slotProps={{ htmlInput: { "aria-label": t("media.filters.searchAria") },
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <SearchIcon sx={{ color: "text.secondary", fontSize: 20 }} />
                    </InputAdornment>
                  )
                }
              }}
            />

            {/* Compact Filters */}
            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: {
                  xs: "1fr",
                  sm: "repeat(2, 1fr)",
                  md: "1.5fr 1.5fr 1.2fr 1.2fr auto"
                },
                gap: 1.5,
                alignItems: "center"
              }}
            >
              {/* Media Type Filter */}
              <FormControl size="small" fullWidth>
                <InputLabel id="media-type-label">{t("media.filters.mediaType")}</InputLabel>
                <Select
                  labelId="media-type-label"
                  label={t("media.filters.mediaType")}
                  value={filters.mediaType}
                  onChange={(e) => handleFilterChange("mediaType", e.target.value)}
                >
                  <MenuItem value="">
                    <em>{t("media.filters.allMediaTypes")}</em>
                  </MenuItem>
                  {uniqueMediaTypes.map(([cls, label]) => (
                    <MenuItem key={cls} value={cls}>
                      {label}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              {/* Manufacturer Filter */}
              <FormControl size="small" fullWidth>
                <InputLabel id="media-manufacturer-label">{t("media.filters.manufacturer")}</InputLabel>
                <Select
                  labelId="media-manufacturer-label"
                  label={t("media.filters.manufacturer")}
                  value={filters.manufacturer}
                  onChange={(e) => handleFilterChange("manufacturer", e.target.value)}
                >
                  <MenuItem value="">
                    <em>{t("media.filters.allManufacturers")}</em>
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
                <InputLabel id="media-status-label">{t("media.filters.status")}</InputLabel>
                <Select
                  labelId="media-status-label"
                  label={t("media.filters.status")}
                  value={filters.status}
                  onChange={(e) => handleFilterChange("status", e.target.value)}
                >
                  <MenuItem value="">
                    <em>{t("media.filters.allStatuses")}</em>
                  </MenuItem>
                  <MenuItem value="Released">{t("media.filters.releasedActive")}</MenuItem>
                  <MenuItem value="OutOfStock">{t("media.filters.outOfStock")}</MenuItem>
                </Select>
              </FormControl>

              {/* Expiry Range Filter */}
              <FormControl size="small" fullWidth>
                <InputLabel id="media-expiry-label">{t("media.filters.expiryDate")}</InputLabel>
                <Select
                  labelId="media-expiry-label"
                  label={t("media.filters.expiryDate")}
                  value={filters.expiryRange}
                  onChange={(e) => handleFilterChange("expiryRange", e.target.value)}
                >
                  <MenuItem value="">
                    <em>{t("media.filters.allExpiry")}</em>
                  </MenuItem>
                  <MenuItem value="expiring_30">{t("media.filters.expiring30")}</MenuItem>
                  <MenuItem value="expiring_60">{t("media.filters.expiring60")}</MenuItem>
                  <MenuItem value="valid">{t("media.filters.valid")}</MenuItem>
                </Select>
              </FormControl>

              {/* Reset Button */}
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
                {t("media.filters.reset")}
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
            <SectionTitle>{t("media.section")}</SectionTitle>
            {!loading && (
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                {t("media.found", { count: filteredMedia.length })}
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
          rows={filteredMedia}
          getRowId={(m) => m.id}
          tableKey={TABLE_KEY}
          tableState={tableState}
          toolbar={<TableToolbar tableKey={TABLE_KEY} tableState={tableState} showSearch={false} />}
          loading={loading}
          trailingCell={{
            label: t("media.columns.actions"),
            render: (m) => (
              <Tooltip title={t("media.recordTooltip")}>
                <Button
                  component={Link}
                  to={`/media/${m.id}/report`}
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
                  {t("media.record")}
                </Button>
              </Tooltip>
            )
          }}
          empty={{
            title: hasActiveFilters
              ? t("media.emptyFiltered")
              : t("media.empty"),
            action: hasActiveFilters ? (
              <Button
                size="small"
                variant="outlined"
                onClick={handleReset}
                startIcon={<RotateLeftIcon fontSize="small" />}
                sx={{ borderColor: "divider", color: "text.secondary", mt: 0.5 }}
              >
                {t("media.filters.resetFilters")}
              </Button>
            ) : undefined
          }}
        />
      </Box>

      {/* Printable version rendered only in print dialog */}
      <PrintableTable
        title="Approved Media List"
        subtitle="Prepared media lots that passed GPT and are released for routine use."
        rows={filteredMedia}
        getRowId={(m) => m.id}
        columns={printColumns}
      />
    </>
  );
}

