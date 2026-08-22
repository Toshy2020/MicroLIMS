import { useEffect, useMemo, useState } from "react";
import {
  Paper,
  Box,
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
import AcUnitIcon from "@mui/icons-material/AcUnit";
import FilterAltOffIcon from "@mui/icons-material/FilterAltOff";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { PrintButton } from "../../../components/PrintButton";
import { PrintableTable } from "../../../components/PrintableTable";
import { LoadingSpinner } from "../../../components/LoadingSpinner";
import { formatLabDate } from "../../../utils/formatDate";
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

const INITIAL_FILTERS: ApprovedCryovialFilterState = {
  search: "",
  organism: "",
  storage: "",
  manufacturer: "",
  status: "",
  expiryRange: ""
};

type ColumnKey = "atcc" | "manufacturer" | "storage" | "vials" | "expiry" | "status";

interface ColumnConfig {
  key: ColumnKey;
  label: string;
}

const TOGGLEABLE_COLUMNS: ColumnConfig[] = [
  { key: "atcc", label: "ATCC No." },
  { key: "manufacturer", label: "Manufacturer" },
  { key: "storage", label: "Storage" },
  { key: "vials", label: "Vials Remaining" },
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

export function ApprovedCryovialListPage() {
  const theme = useTheme();
  const { action, detected, purple } = theme.custom.status;

  // Data states
  const [cryovials, setCryovials] = useState<CryovialItem[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  // Filter & Pagination states
  const [filters, setFilters] = useState<ApprovedCryovialFilterState>(INITIAL_FILTERS);
  const [page, setPage] = useState<number>(0);
  const [rowsPerPage, setRowsPerPage] = useState<number>(25);

  // Columns visibility state
  const [visibleColumns, setVisibleColumns] = useState<Set<ColumnKey>>(
    new Set(["atcc", "manufacturer", "storage", "vials", "expiry", "status"])
  );
  const [columnsAnchor, setColumnsAnchor] = useState<HTMLElement | null>(null);

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
      setError("Failed to retrieve approved cryovials register. Please try again.");
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

  // Pagination calculation
  const paginatedCryovials = useMemo(() => {
    return filteredCryovials.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);
  }, [filteredCryovials, page, rowsPerPage]);

  const handleThawConfirm = async (reason: string) => {
    if (!thawItem) return;
    await CryovialService.thawVial(thawItem.id, reason);
    setMessage({
      text: `Vial from batch ${thawItem.code} successfully thawed and recorded.`,
      ok: true
    });
    setThawItem(null);
    loadData();
  };

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
    { label: "Expiry", render: (c: CryovialItem) => formatLabDate(c.expiryDate) },
    {
      label: "Status",
      render: (c: CryovialItem) => (c.vialsRemaining === 0 ? "Depleted" : "Approved")
    }
  ];

  return (
    <>
      <Box className="no-print">
        <PageHeader
          title="Approved Cryovial List"
          subtitle="Approved, non-destroyed cryovials available for use."
        />

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
              placeholder="Search by cryovial code, organism, ATCC no., manufacturer..."
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
                <InputLabel id="cryovial-organism-label">Organism</InputLabel>
                <Select
                  labelId="cryovial-organism-label"
                  label="Organism"
                  value={filters.organism}
                  onChange={(e) => handleFilterChange("organism", e.target.value)}
                >
                  <MenuItem value="">
                    <em>All Organisms</em>
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
                <InputLabel id="cryovial-storage-label">Storage</InputLabel>
                <Select
                  labelId="cryovial-storage-label"
                  label="Storage"
                  value={filters.storage}
                  onChange={(e) => handleFilterChange("storage", e.target.value)}
                >
                  <MenuItem value="">
                    <em>All Storage Types</em>
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
                <InputLabel id="cryovial-manufacturer-label">Manufacturer</InputLabel>
                <Select
                  labelId="cryovial-manufacturer-label"
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
                <InputLabel id="cryovial-status-label">Status</InputLabel>
                <Select
                  labelId="cryovial-status-label"
                  label="Status"
                  value={filters.status}
                  onChange={(e) => handleFilterChange("status", e.target.value)}
                >
                  <MenuItem value="">
                    <em>All Statuses</em>
                  </MenuItem>
                  <MenuItem value="in_stock">Available / In Stock</MenuItem>
                  <MenuItem value="depleted">Depleted (0 Vials)</MenuItem>
                </Select>
              </FormControl>

              {/* Expiry Range Filter */}
              <FormControl size="small" fullWidth>
                <InputLabel id="cryovial-expiry-label">Expiry Date</InputLabel>
                <Select
                  labelId="cryovial-expiry-label"
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
            <SectionTitle>Approved Cryovials</SectionTitle>
            {!loading && (
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                {filteredCryovials.length === 1
                  ? "1 approved cryovial found"
                  : `${filteredCryovials.length} approved cryovials found`}
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
              <Box sx={{ p: 2, minWidth: 190 }}>
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
                        Cryovial Code
                      </TableCell>
                      <TableCell
                        sx={{
                          fontWeight: 700,
                          fontSize: 12,
                          bgcolor: "background.default",
                          color: "text.secondary"
                        }}
                      >
                        Organism
                      </TableCell>
                      {visibleColumns.has("atcc") && (
                        <TableCell
                          sx={{
                            fontWeight: 700,
                            fontSize: 12,
                            bgcolor: "background.default",
                            color: "text.secondary"
                          }}
                        >
                          ATCC No.
                        </TableCell>
                      )}
                      {visibleColumns.has("manufacturer") && (
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
                      {visibleColumns.has("storage") && (
                        <TableCell
                          sx={{
                            fontWeight: 700,
                            fontSize: 12,
                            bgcolor: "background.default",
                            color: "text.secondary"
                          }}
                        >
                          Storage
                        </TableCell>
                      )}
                      {visibleColumns.has("vials") && (
                        <TableCell
                          sx={{
                            fontWeight: 700,
                            fontSize: 12,
                            bgcolor: "background.default",
                            color: "text.secondary"
                          }}
                        >
                          Vials Remaining
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
                    {paginatedCryovials.length === 0 ? (
                      <TableRow>
                        <TableCell
                          colSpan={3 + visibleColumns.size}
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
                                ? "No approved cryovials match the selected filters."
                                : "No approved cryovials currently available in inventory."}
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
                      paginatedCryovials.map((c) => {
                        const organismName = c.organism?.scientificName ?? c.organismNameSnapshot;
                        const atcc = c.organism?.atccNumber ?? "—";
                        const mfg = c.manufacturerName || c.material?.manufacturerName || "—";
                        const expiring = isExpiringSoon(c.expiryDate, 30);
                        const depleted = c.vialsRemaining <= 0;

                        return (
                          <TableRow
                            key={c.id}
                            hover
                            sx={{
                              "&:nth-of-type(even)": { bgcolor: "background.default" }
                            }}
                          >
                            {/* Cryovial Code */}
                            <TableCell sx={{ py: 1.25 }}>
                              <Typography
                                sx={{
                                  fontSize: 13,
                                  fontWeight: 700,
                                  fontFamily: "monospace",
                                  color: theme.palette.primary.main
                                }}
                              >
                                {c.code}
                              </Typography>
                              {c.material?.batchNumber && (
                                <Typography sx={{ fontSize: 11, color: "text.secondary" }} noWrap>
                                  Batch: {c.material.batchNumber}
                                </Typography>
                              )}
                            </TableCell>

                            {/* Organism */}
                            <TableCell sx={{ py: 1.25 }}>
                              <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                                {organismName}
                              </Typography>
                              {c.material?.materialName && (
                                <Typography sx={{ fontSize: 11, color: "text.secondary" }} noWrap>
                                  {c.material.materialName}
                                </Typography>
                              )}
                            </TableCell>

                            {/* ATCC No. */}
                            {visibleColumns.has("atcc") && (
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 12, color: "text.primary", fontFamily: "monospace" }}>
                                  {atcc}
                                </Typography>
                              </TableCell>
                            )}

                            {/* Manufacturer */}
                            {visibleColumns.has("manufacturer") && (
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 12, color: "text.primary" }}>
                                  {mfg}
                                </Typography>
                              </TableCell>
                            )}

                            {/* Storage */}
                            {visibleColumns.has("storage") && (
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography sx={{ fontSize: 12, color: "text.primary" }}>
                                  {c.storageCondition || "—"}
                                </Typography>
                              </TableCell>
                            )}

                            {/* Vials Remaining */}
                            {visibleColumns.has("vials") && (
                              <TableCell sx={{ py: 1.25 }}>
                                <Typography
                                  sx={{
                                    fontSize: 13,
                                    fontWeight: 700,
                                    color: depleted ? detected.text : "text.primary"
                                  }}
                                >
                                  {c.vialsRemaining} of {c.numberOfVialsPrepared}
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
                                  {formatLabDate(c.expiryDate)}
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
                                {depleted ? (
                                  <StatusBadge status="Depleted" label="Depleted" />
                                ) : (
                                  <StatusBadge status="Approved" label="Approved" />
                                )}
                              </TableCell>
                            )}

                            {/* Actions */}
                            <TableCell align="right" sx={{ py: 1.25 }}>
                              <Box
                                sx={{
                                  display: "flex",
                                  justifyContent: "flex-end",
                                  alignItems: "center",
                                  gap: 0.75
                                }}
                              >
                                {!depleted && (
                                  <Tooltip title="Thaw a single vial from this approved batch">
                                    <Button
                                      size="small"
                                      variant="outlined"
                                      onClick={() => setThawItem(c)}
                                      startIcon={<AcUnitIcon fontSize="small" />}
                                      sx={{
                                        px: 1,
                                        py: 0.25,
                                        fontSize: 11,
                                        fontWeight: 700,
                                        borderColor: theme.palette.primary.main,
                                        color: theme.palette.primary.main,
                                        "&:hover": {
                                          borderColor: theme.palette.primary.dark,
                                          bgcolor: purple.bg
                                        }
                                      }}
                                    >
                                      Thaw Vial
                                    </Button>
                                  </Tooltip>
                                )}

                                <Tooltip title="View cryovial report record">
                                  <Button
                                    size="small"
                                    variant="outlined"
                                    onClick={() =>
                                      window.open(`/cryovials/${c.id}/report`, "_blank", "noopener")
                                    }
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
                              </Box>
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
                count={filteredCryovials.length}
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

