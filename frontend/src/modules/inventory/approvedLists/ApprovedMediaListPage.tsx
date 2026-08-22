import { useEffect, useMemo, useState } from "react";
import {
  Paper,
  Box,
  Grid,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  TableContainer,
  TablePagination,
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
  Popover,
  FormGroup,
  FormControlLabel,
  Checkbox,
  useTheme
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import RotateLeftIcon from "@mui/icons-material/RotateLeft";
import ViewColumnIcon from "@mui/icons-material/ViewColumn";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import FilterAltOffIcon from "@mui/icons-material/FilterAltOff";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import CategoryOutlinedIcon from "@mui/icons-material/CategoryOutlined";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { PrintButton } from "../../../components/PrintButton";
import { PrintableTable } from "../../../components/PrintableTable";
import { LoadingSpinner } from "../../../components/LoadingSpinner";
import { formatLabDate } from "../../../utils/formatDate";
import { masterDataOptions, mediaClassLabel } from "../../../services/masterDataOptions";
import { UserService } from "../../users/services/UserService";
import { StatusTone } from "../../../theme/statusTokens";

interface ApprovedMediaFilterState {
  search: string;
  mediaType: string;
  manufacturer: string;
  status: string;
  expiryRange: string; // "" | "expiring_30" | "expiring_60" | "valid"
}

const INITIAL_FILTERS: ApprovedMediaFilterState = {
  search: "",
  mediaType: "",
  manufacturer: "",
  status: "",
  expiryRange: ""
};

type ColumnKey =
  | "mediaType"
  | "ph"
  | "preparedBy"
  | "volume"
  | "weight"
  | "mfgLot"
  | "mfgName"
  | "preparedOn"
  | "expiry"
  | "status";

interface ColumnConfig {
  key: ColumnKey;
  label: string;
}

const TOGGLEABLE_COLUMNS: ColumnConfig[] = [
  { key: "mediaType", label: "Media Type" },
  { key: "ph", label: "pH Result" },
  { key: "preparedBy", label: "Prepared By" },
  { key: "volume", label: "Total Volume" },
  { key: "weight", label: "Total Weight" },
  { key: "mfgLot", label: "Manufacturer Lot" },
  { key: "mfgName", label: "Manufacturer" },
  { key: "preparedOn", label: "Prepared On" },
  { key: "expiry", label: "Expiry" },
  { key: "status", label: "Status" }
];

function isExpiringSoon(expiryDateStr: string | null, daysThreshold: number = 30): boolean {
  if (!expiryDateStr) return false;
  const expiry = new Date(expiryDateStr);
  const now = new Date();
  const diffTime = expiry.getTime() - now.getTime();
  const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
  return diffDays <= daysThreshold;
}

export function ApprovedMediaListPage() {
  const theme = useTheme();
  const { action, purple } = theme.custom.status;

  // Data states
  const [media, setMedia] = useState<any[]>([]);
  const [userMap, setUserMap] = useState<Record<number, string>>({});
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // Filters & Pagination states
  const [filters, setFilters] = useState<ApprovedMediaFilterState>(INITIAL_FILTERS);
  const [page, setPage] = useState<number>(0);
  const [rowsPerPage, setRowsPerPage] = useState<number>(25);

  // Column visibility
  const [visibleColumns, setVisibleColumns] = useState<Set<ColumnKey>>(
    new Set([
      "mediaType",
      "ph",
      "preparedBy",
      "volume",
      "weight",
      "mfgLot",
      "mfgName",
      "preparedOn",
      "expiry",
      "status"
    ])
  );
  const [columnsAnchor, setColumnsAnchor] = useState<HTMLElement | null>(null);

  const loadData = async () => {
    try {
      setLoading(true);
      setError(null);
      const [releasedMedia, users] = await Promise.all([
        masterDataOptions.getReleasedMedia(),
        UserService.getAll().catch(() => [] as any[])
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
      setError("Failed to retrieve approved media register. Please try again.");
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
    setFilters((prev) => ({ ...prev, [field]: value }));
    setPage(0);
  };

  const handleReset = () => {
    setFilters(INITIAL_FILTERS);
    setPage(0);
  };

  const toggleColumn = (key: ColumnKey) => {
    const next = new Set(visibleColumns);
    if (next.has(key)) {
      next.delete(key);
    } else {
      next.add(key);
    }
    setVisibleColumns(next);
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

  // Pagination calculation
  const paginatedMedia = useMemo(() => {
    return filteredMedia.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);
  }, [filteredMedia, page, rowsPerPage]);

  // Real KPI calculations from loaded released media dataset
  const kpiData = useMemo(() => {
    const totalCount = media.length;
    const inStockCount = media.filter((m) => m.status === "Active" || m.isReleasedForUse).length;
    const expiringSoonCount = media.filter((m) => isExpiringSoon(m.expiryDate, 30)).length;
    const uniqueTypesCount = new Set(
      media.map((m) => m.mediaTypeId || m.mediaType?.class).filter(Boolean)
    ).size;

    return [
      {
        label: "Total Media Lots",
        description: "All released lots",
        count: totalCount,
        icon: <ScienceOutlinedIcon sx={{ fontSize: 20 }} />,
        tone: "purple" as StatusTone
      },
      {
        label: "In Stock",
        description: "Active released lots",
        count: inStockCount,
        icon: <CheckCircleOutlineIcon sx={{ fontSize: 20 }} />,
        tone: "notDetected" as StatusTone
      },
      {
        label: "Expiring Soon",
        description: "Within 30 days",
        count: expiringSoonCount,
        icon: <WarningAmberOutlinedIcon sx={{ fontSize: 20 }} />,
        tone: "action" as StatusTone
      },
      {
        label: "Media Formulations",
        description: "Distinct types in use",
        count: uniqueTypesCount,
        icon: <CategoryOutlinedIcon sx={{ fontSize: 20 }} />,
        tone: "info" as StatusTone
      }
    ];
  }, [media]);

  // User name resolution helper
  const getPreparedByName = (m: any) => {
    if (m.preparedByName) return m.preparedByName;
    if (m.preparedByUser?.fullName) return m.preparedByUser.fullName;
    if (m.preparedByUserId && userMap[m.preparedByUserId]) return userMap[m.preparedByUserId];
    return m.preparedByUserId ? `User #${m.preparedByUserId}` : "—";
  };

  // Printable table columns
  const printColumns = [
    { label: "Lot Number", render: (m: any) => m.lotNumber },
    {
      label: "Media Type",
      render: (m: any) => m.material?.materialName ?? mediaClassLabel(m.mediaType?.class) ?? "—"
    },
    {
      label: "pH Result",
      render: (m: any) => (m.ph != null ? Number(m.ph).toFixed(2) : "—")
    },
    { label: "Prepared By", render: (m: any) => getPreparedByName(m) },
    { label: "Total Volume", render: (m: any) => m.totalVolume || "—" },
    { label: "Total Weight", render: (m: any) => (m.totalWeight != null ? `${m.totalWeight} g` : "—") },
    { label: "Manufacturer Lot", render: (m: any) => m.manufacturerLot || "—" },
    { label: "Manufacturer", render: (m: any) => m.manufacturerName || m.material?.manufacturerName || "—" },
    { label: "Prepared On", render: (m: any) => formatLabDate(m.preparedAt) },
    { label: "Expiry", render: (m: any) => formatLabDate(m.expiryDate) },
    {
      label: "Status",
      render: (m: any) => (m.status === "OutOfStock" ? "Out of Stock" : "Released")
    }
  ];

  return (
    <>
      <Box className="no-print">
        <PageHeader
          title="Approved Media List"
          subtitle="Prepared media lots that passed GPT and are released for routine use."
        />

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
              <Grid item xs={12} sm={6} md={3} key={card.label}>
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
                  <Typography sx={{ fontSize: 11, color: "text.secondary", mt: 0.25 }} noWrap>
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
              placeholder="Search by lot number, media type, manufacturer, batch..."
              value={filters.search}
              onChange={(e) => handleFilterChange("search", e.target.value)}
              InputProps={{
                startAdornment: (
                  <InputAdornment position="start">
                    <SearchIcon sx={{ color: "text.secondary", fontSize: 20 }} />
                  </InputAdornment>
                )
              }}
              sx={{
                "& .MuiOutlinedInput-root": {
                  borderRadius: 1.5,
                  bgcolor: "background.default"
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
                <InputLabel id="media-type-label">Media Type</InputLabel>
                <Select
                  labelId="media-type-label"
                  label="Media Type"
                  value={filters.mediaType}
                  onChange={(e) => handleFilterChange("mediaType", e.target.value)}
                >
                  <MenuItem value="">
                    <em>All Media Types</em>
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
                <InputLabel id="media-manufacturer-label">Manufacturer</InputLabel>
                <Select
                  labelId="media-manufacturer-label"
                  label="Manufacturer"
                  value={filters.manufacturer}
                  onChange={(e) => handleFilterChange("manufacturer", e.target.value)}
                >
                  <MenuItem value="">
                    <em>All Manufacturers</em>
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
                <InputLabel id="media-status-label">Status</InputLabel>
                <Select
                  labelId="media-status-label"
                  label="Status"
                  value={filters.status}
                  onChange={(e) => handleFilterChange("status", e.target.value)}
                >
                  <MenuItem value="">
                    <em>All Statuses</em>
                  </MenuItem>
                  <MenuItem value="Released">Released / Active</MenuItem>
                  <MenuItem value="OutOfStock">Out of Stock</MenuItem>
                </Select>
              </FormControl>

              {/* Expiry Range Filter */}
              <FormControl size="small" fullWidth>
                <InputLabel id="media-expiry-label">Expiry Date</InputLabel>
                <Select
                  labelId="media-expiry-label"
                  label="Expiry Date"
                  value={filters.expiryRange}
                  onChange={(e) => handleFilterChange("expiryRange", e.target.value)}
                >
                  <MenuItem value="">
                    <em>All Expiry</em>
                  </MenuItem>
                  <MenuItem value="expiring_30">Expiring in 30 Days</MenuItem>
                  <MenuItem value="expiring_60">Expiring in 60 Days</MenuItem>
                  <MenuItem value="valid">Valid / Unexpired</MenuItem>
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
                Reset
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
            <SectionTitle>Released Media</SectionTitle>
            {!loading && (
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                {filteredMedia.length === 1
                  ? "1 released media lot found"
                  : `${filteredMedia.length} released media lots found`}
              </Typography>
            )}
          </Box>

          <Box sx={{ display: "flex", alignItems: "center", gap: 1.25 }}>
            {/* Columns Customizer */}
            <Button
              size="small"
              variant="outlined"
              startIcon={<ViewColumnIcon fontSize="small" />}
              onClick={(e) => setColumnsAnchor(e.currentTarget)}
              sx={{
                borderColor: "divider",
                color: "text.secondary",
                fontSize: 12,
                "&:hover": { bgcolor: "background.default" }
              }}
            >
              Columns
            </Button>
            <Popover
              open={Boolean(columnsAnchor)}
              anchorEl={columnsAnchor}
              onClose={() => setColumnsAnchor(null)}
              anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
              transformOrigin={{ vertical: "top", horizontal: "right" }}
            >
              <Box sx={{ p: 2, minWidth: 200 }}>
                <Typography sx={{ fontSize: 12, fontWeight: 700, mb: 1, color: "text.secondary" }}>
                  Visible Columns
                </Typography>
                <FormGroup>
                  {TOGGLEABLE_COLUMNS.map((col) => (
                    <FormControlLabel
                      key={col.key}
                      label={<Typography sx={{ fontSize: 13 }}>{col.label}</Typography>}
                      control={
                        <Checkbox
                          size="small"
                          checked={visibleColumns.has(col.key)}
                          onChange={() => toggleColumn(col.key)}
                        />
                      }
                    />
                  ))}
                </FormGroup>
              </Box>
            </Popover>

            {/* Preserved Print Button */}
            <PrintButton />
          </Box>
        </Box>

        {/* Dense Register Table Card */}
        <Paper
          elevation={0}
          sx={{
            width: "100%",
            overflow: "hidden",
            border: "1px solid",
            borderColor: "divider",
            borderRadius: 1.5,
            bgcolor: "background.paper"
          }}
        >
          {loading ? (
            <Box sx={{ py: 8, display: "flex", justifyContent: "center" }}>
              <LoadingSpinner />
            </Box>
          ) : (
            <>
              <TableContainer sx={{ maxHeight: "calc(100vh - 360px)", minHeight: 300 }}>
                <Table size="small" stickyHeader>
                  <TableHead>
                    <TableRow>
                      <TableCell
                        sx={{
                          fontWeight: 700,
                          fontSize: 12,
                          bgcolor: "background.default",
                          color: "text.secondary"
                        }}
                      >
                        Lot Number
                      </TableCell>
                      {visibleColumns.has("mediaType") && (
                        <TableCell
                          sx={{
                            fontWeight: 700,
                            fontSize: 12,
                            bgcolor: "background.default",
                            color: "text.secondary"
                          }}
                        >
                          Media Type
                        </TableCell>
                      )}
                      {visibleColumns.has("ph") && (
                        <TableCell
                          sx={{
                            fontWeight: 700,
                            fontSize: 12,
                            bgcolor: "background.default",
                            color: "text.secondary"
                          }}
                        >
                          pH Result
                        </TableCell>
                      )}
                      {visibleColumns.has("preparedBy") && (
                        <TableCell
                          sx={{
                            fontWeight: 700,
                            fontSize: 12,
                            bgcolor: "background.default",
                            color: "text.secondary"
                          }}
                        >
                          Prepared By
                        </TableCell>
                      )}
                      {visibleColumns.has("volume") && (
                        <TableCell
                          sx={{
                            fontWeight: 700,
                            fontSize: 12,
                            bgcolor: "background.default",
                            color: "text.secondary"
                          }}
                        >
                          Total Volume
                        </TableCell>
                      )}
                      {visibleColumns.has("weight") && (
                        <TableCell
                          sx={{
                            fontWeight: 700,
                            fontSize: 12,
                            bgcolor: "background.default",
                            color: "text.secondary"
                          }}
                        >
                          Total Weight
                        </TableCell>
                      )}
                      {visibleColumns.has("mfgLot") && (
                        <TableCell
                          sx={{
                            fontWeight: 700,
                            fontSize: 12,
                            bgcolor: "background.default",
                            color: "text.secondary"
                          }}
                        >
                          Manufacturer Lot
                        </TableCell>
                      )}
                      {visibleColumns.has("mfgName") && (
                        <TableCell
                          sx={{
                            fontWeight: 700,
                            fontSize: 12,
                            bgcolor: "background.default",
                            color: "text.secondary"
                          }}
                        >
                          Manufacturer
                        </TableCell>
                      )}
                      {visibleColumns.has("preparedOn") && (
                        <TableCell
                          sx={{
                            fontWeight: 700,
                            fontSize: 12,
                            bgcolor: "background.default",
                            color: "text.secondary"
                          }}
                        >
                          Prepared On
                        </TableCell>
                      )}
                      {visibleColumns.has("expiry") && (
                        <TableCell
                          sx={{
                            fontWeight: 700,
                            fontSize: 12,
                            bgcolor: "background.default",
                            color: "text.secondary"
                          }}
                        >
                          Expiry
                        </TableCell>
                      )}
                      {visibleColumns.has("status") && (
                        <TableCell
                          sx={{
                            fontWeight: 700,
                            fontSize: 12,
                            bgcolor: "background.default",
                            color: "text.secondary"
                          }}
                        >
                          Status
                        </TableCell>
                      )}
                      <TableCell
                        align="right"
                        sx={{
                          fontWeight: 700,
                          fontSize: 12,
                          bgcolor: "background.default",
                          color: "text.secondary"
                        }}
                      >
                        Actions
                      </TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {paginatedMedia.length === 0 ? (
                      <TableRow>
                        <TableCell
                          colSpan={2 + visibleColumns.size}
                          align="center"
                          sx={{ py: 6 }}
                        >
                          <Box
                            sx={{
                              display: "flex",
                              flexDirection: "column",
                              alignItems: "center",
                              gap: 1.5
                            }}
                          >
                            <FilterAltOffIcon sx={{ fontSize: 36, color: "text.secondary", opacity: 0.6 }} />
                            <Typography sx={{ color: "text.secondary", fontSize: 14 }}>
                              {hasActiveFilters
                                ? "No approved media lots match the selected filters."
                                : "No approved media lots currently available in inventory."}
                            </Typography>
                            {hasActiveFilters && (
                              <Button
                                size="small"
                                variant="outlined"
                                onClick={handleReset}
                                startIcon={<RotateLeftIcon fontSize="small" />}
                                sx={{ borderColor: "divider", color: "text.secondary", mt: 0.5 }}
                              >
                                Reset Filters
                              </Button>
                            )}
                          </Box>
                        </TableCell>
                      </TableRow>
                    ) : (
                      paginatedMedia.map((m) => {
                        const expiring = isExpiringSoon(m.expiryDate, 30);
                        const mfg = m.manufacturerName || m.material?.manufacturerName || "—";
                        const prepBy = getPreparedByName(m);

                        return (
                          <TableRow
                            key={m.id}
                            hover
                            sx={{
                              "&:nth-of-type(even)": { bgcolor: "background.default" }
                            }}
                          >
                            {/* Lot Number */}
                            <TableCell sx={{ py: 1.25 }}>
                              <Typography
                                sx={{
                                  fontSize: 13,
                                  fontWeight: 700,
                                  fontFamily: "monospace",
                                  color: theme.palette.primary.main
                                }}
                              >
                                {m.lotNumber}
                              </Typography>
                              {m.material?.batchNumber && (
                                <Typography sx={{ fontSize: 11, color: "text.secondary" }} noWrap>
                                  Batch: {m.material.batchNumber}
                                </Typography>
                              )}
                            </TableCell>

                            {/* Media Type */}
                            {visibleColumns.has("mediaType") && (
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                                  {m.material?.materialName ?? mediaClassLabel(m.mediaType?.class) ?? "—"}
                                </Typography>
                                {m.material?.materialName && m.mediaType?.class && (
                                  <Typography sx={{ fontSize: 11, color: "text.secondary" }} noWrap>
                                    {mediaClassLabel(m.mediaType?.class)}
                                  </Typography>
                                )}
                              </TableCell>
                            )}

                            {/* pH Result */}
                            {visibleColumns.has("ph") && (
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography
                                  sx={{
                                    fontSize: 12,
                                    fontWeight: 700,
                                    fontFamily: "monospace",
                                    color: "text.primary"
                                  }}
                                >
                                  {m.ph != null ? Number(m.ph).toFixed(2) : "—"}
                                </Typography>
                              </TableCell>
                            )}

                            {/* Prepared By */}
                            {visibleColumns.has("preparedBy") && (
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 12, color: "text.primary" }}>
                                  {prepBy}
                                </Typography>
                              </TableCell>
                            )}

                            {/* Total Volume */}
                            {visibleColumns.has("volume") && (
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 12, color: "text.primary" }}>
                                  {m.totalVolume || "—"}
                                </Typography>
                              </TableCell>
                            )}

                            {/* Total Weight */}
                            {visibleColumns.has("weight") && (
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 12, color: "text.primary" }}>
                                  {m.totalWeight != null ? `${m.totalWeight} g` : "—"}
                                </Typography>
                              </TableCell>
                            )}

                            {/* Manufacturer Lot */}
                            {visibleColumns.has("mfgLot") && (
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 12, color: "text.primary", fontFamily: "monospace" }}>
                                  {m.manufacturerLot || "—"}
                                </Typography>
                              </TableCell>
                            )}

                            {/* Manufacturer */}
                            {visibleColumns.has("mfgName") && (
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 12, color: "text.primary" }}>
                                  {mfg}
                                </Typography>
                              </TableCell>
                            )}

                            {/* Prepared On */}
                            {visibleColumns.has("preparedOn") && (
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 12, color: "text.primary" }}>
                                  {formatLabDate(m.preparedAt)}
                                </Typography>
                              </TableCell>
                            )}

                            {/* Expiry */}
                            {visibleColumns.has("expiry") && (
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography
                                  sx={{
                                    fontSize: 12,
                                    fontWeight: expiring ? 700 : 500,
                                    color: expiring ? action.text : "text.primary"
                                  }}
                                >
                                  {formatLabDate(m.expiryDate)}
                                </Typography>
                                {expiring && (
                                  <Typography sx={{ fontSize: 10, color: action.text, fontWeight: 600 }}>
                                    Expiring soon
                                  </Typography>
                                )}
                              </TableCell>
                            )}

                            {/* Status */}
                            {visibleColumns.has("status") && (
                              <TableCell sx={{ py: 1.25 }}>
                                <StatusBadge
                                  status={m.status === "OutOfStock" ? "OutOfStock" : "Released"}
                                  label={m.status === "OutOfStock" ? "Out of Stock" : "Released"}
                                />
                              </TableCell>
                            )}

                            {/* Actions */}
                            <TableCell align="right" sx={{ py: 1.25 }}>
                              <Tooltip title="View media lot report record">
                                <Button
                                  size="small"
                                  variant="outlined"
                                  onClick={() => window.open(`/media/${m.id}/report`, "_blank", "noopener")}
                                  startIcon={<DescriptionOutlinedIcon fontSize="small" />}
                                  sx={{
                                    px: 1,
                                    py: 0.25,
                                    fontSize: 11,
                                    borderColor: "divider",
                                    color: "text.secondary",
                                    "&:hover": { bgcolor: "background.default" }
                                  }}
                                >
                                  Record
                                </Button>
                              </Tooltip>
                            </TableCell>
                          </TableRow>
                        );
                      })
                    )}
                  </TableBody>
                </Table>
              </TableContainer>

              <TablePagination
                rowsPerPageOptions={[10, 25, 50, 100]}
                component="div"
                count={filteredMedia.length}
                rowsPerPage={rowsPerPage}
                page={page}
                onPageChange={(_, newPage) => setPage(newPage)}
                onRowsPerPageChange={(e) => {
                  setRowsPerPage(parseInt(e.target.value, 10));
                  setPage(0);
                }}
              />
            </>
          )}
        </Paper>
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

