import { useEffect, useMemo, useState } from "react";
import {
  Paper,
  Box,
  Button,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  TableContainer,
  TablePagination,
  Alert,
  IconButton,
  Tooltip,
  Typography
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import HistoryIcon from "@mui/icons-material/History";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { PrintButton } from "../../../components/PrintButton";
import { PrintableTable } from "../../../components/PrintableTable";
import { AuditHistoryDialog } from "../../../components/AuditHistoryDialog";
import { LoadingSpinner } from "../../../components/LoadingSpinner";
import { formatLabDate } from "../../../utils/formatDate";
import { useAuth } from "../../../contexts/AuthContext";
import { MaterialService } from "./services/MaterialService";
import {
  MaterialFilterState,
  MaterialItem,
  MaterialKpiFilter
} from "./types/materialTypes";
import {
  MaterialKpiCards,
  isMaterialExpiringSoon,
  isMaterialInStock,
  isMaterialLowStock,
  isMaterialOutOfStock
} from "./components/MaterialKpiCards";
import { MaterialFilterBar } from "./components/MaterialFilterBar";
import { AddMaterialDialog } from "./components/AddMaterialDialog";
import { brandColors } from "../../../theme";

const INITIAL_FILTERS: MaterialFilterState = {
  search: "",
  materialType: "",
  manufacturer: "",
  location: "",
  status: "",
  expiryRange: ""
};

export function MaterialsPage() {
  const { role } = useAuth();
  const canSeeHistory = role === "SectionHead" || role === "SystemAdministrator";

  const [items, setItems] = useState<MaterialItem[] | null>(null);
  const [printList, setPrintList] = useState<MaterialItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  // Filters & KPI state
  const [kpiFilter, setKpiFilter] = useState<MaterialKpiFilter>("all");
  const [filters, setFilters] = useState<MaterialFilterState>(INITIAL_FILTERS);

  // Pagination state
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(25);

  // Dialog states
  const [isAddOpen, setIsAddOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<MaterialItem | null>(null);
  const [historyFor, setHistoryFor] = useState<number | null>(null);

  const loadData = async () => {
    try {
      setLoading(true);
      const [allMaterials, printMaterials] = await Promise.all([
        MaterialService.getAll(),
        MaterialService.getForPrint()
      ]);
      setItems(allMaterials);
      setPrintList(printMaterials);
    } catch {
      setMessage({ text: "Failed to load materials stock.", ok: false });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleReset = () => {
    setKpiFilter("all");
    setFilters(INITIAL_FILTERS);
    setPage(0);
  };

  const handleKpiSelect = (newKpi: MaterialKpiFilter) => {
    setKpiFilter(newKpi);
    setPage(0);
  };

  const handleFilterChange = (newFilters: MaterialFilterState) => {
    setFilters(newFilters);
    setPage(0);
  };

  // Filtered dataset combining KPI shortcuts + form filters
  const filteredItems = useMemo(() => {
    if (!items) return [];

    return items.filter((item) => {
      // 1. KPI Shortcut Filter
      if (kpiFilter === "in_stock" && !isMaterialInStock(item)) return false;
      if (kpiFilter === "low_stock" && !isMaterialLowStock(item)) return false;
      if (kpiFilter === "out_of_stock" && !isMaterialOutOfStock(item)) return false;
      if (kpiFilter === "expiring_soon" && !isMaterialExpiringSoon(item.expiryDate, 30)) return false;

      // 2. Search Text Query
      const q = filters.search.trim().toLowerCase();
      if (q) {
        const matchesName = item.materialName.toLowerCase().includes(q);
        const matchesLot = item.batchNumber.toLowerCase().includes(q);
        const matchesCode = (item.code ?? "").toLowerCase().includes(q);
        const matchesMfg = (item.manufacturerName ?? "").toLowerCase().includes(q);
        const matchesLoc = (item.location ?? "").toLowerCase().includes(q);
        const matchesOrganism = (item.organism?.scientificName ?? "").toLowerCase().includes(q);
        const matchesAtcc = (item.atccNumber ?? "").toLowerCase().includes(q);
        if (!matchesName && !matchesLot && !matchesCode && !matchesMfg && !matchesLoc && !matchesOrganism && !matchesAtcc) {
          return false;
        }
      }

      // 3. Dropdown: Material Type
      if (filters.materialType && item.materialType !== filters.materialType) {
        return false;
      }

      // 4. Dropdown: Manufacturer
      if (filters.manufacturer && item.manufacturerName !== filters.manufacturer) {
        return false;
      }

      // 5. Dropdown: Location
      if (filters.location && item.location !== filters.location) {
        return false;
      }

      // 6. Dropdown: Status
      if (filters.status) {
        if (filters.status === "InStock" && (!isMaterialInStock(item) || isMaterialLowStock(item))) return false;
        if (filters.status === "LowStock" && !isMaterialLowStock(item)) return false;
        if (filters.status === "Depleted" && item.status !== "Depleted") return false;
        if (filters.status === "Expired" && item.status !== "Expired") return false;
      }

      // 7. Dropdown: Expiry Range
      if (filters.expiryRange) {
        if (filters.expiryRange === "expiring_30" && !isMaterialExpiringSoon(item.expiryDate, 30)) return false;
        if (filters.expiryRange === "expiring_60" && !isMaterialExpiringSoon(item.expiryDate, 60)) return false;
        if (filters.expiryRange === "expired" && item.status !== "Expired") return false;
        if (filters.expiryRange === "valid" && item.status === "Expired") return false;
      }

      return true;
    });
  }, [items, kpiFilter, filters]);

  // Paginated slice
  const paginatedItems = useMemo(() => {
    const start = page * rowsPerPage;
    return filteredItems.slice(start, start + rowsPerPage);
  }, [filteredItems, page, rowsPerPage]);

  return (
    <>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
        <PageHeader
          title="Materials Stock"
          subtitle="Media, discs, kits, reagents, chemicals, disposables — receiving, expiry, and quantity received/remaining."
        />
        <Button
          className="no-print"
          variant="contained"
          startIcon={<AddIcon />}
          onClick={() => {
            setEditingItem(null);
            setIsAddOpen(true);
          }}
          sx={{
            bgcolor: brandColors.sectionTitle,
            px: 2.5,
            py: 1,
            fontWeight: 600,
            whiteSpace: "nowrap",
            "&:hover": { bgcolor: brandColors.pageTitle }
          }}
        >
          + Add to Stock
        </Button>
      </Box>

      {message && (
        <Alert
          className="no-print"
          severity={message.ok ? "success" : "error"}
          onClose={() => setMessage(null)}
          sx={{ mb: 2 }}
        >
          {message.text}
        </Alert>
      )}

      {loading || !items ? (
        <LoadingSpinner />
      ) : (
        <Box className="no-print">
          {/* KPI Shortcut Cards */}
          <MaterialKpiCards
            items={items}
            activeFilter={kpiFilter}
            onFilterSelect={handleKpiSelect}
          />

          {/* Compact Search & Filter Bar */}
          <MaterialFilterBar
            items={items}
            filters={filters}
            onFilterChange={handleFilterChange}
            onReset={handleReset}
          />

          {/* Materials Register Section */}
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5, mt: 3 }}>
            <SectionTitle>
              {`Materials in Stock (${filteredItems.length}${filteredItems.length !== items.length ? ` filtered from ${items.length}` : ""})`}
            </SectionTitle>
            <PrintButton label="Print (excludes expired / depleted)" />
          </Box>

          <Paper sx={{ border: "1px solid #e5e7eb", borderRadius: 2, overflow: "hidden" }}>
            <TableContainer>
              <Table size="small">
                <TableHead sx={{ bgcolor: "#f9fafb" }}>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Type</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Material Name</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Manufacturer</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Batch/Lot No.</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Received</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Expiry</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Code</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Organism</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>ATCC</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Location</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>Qty Received</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>Qty Remaining</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>Min Stock</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "center" }}>Status</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "center" }}>Actions</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {paginatedItems.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={15} sx={{ textAlign: "center", py: 4 }}>
                        <Typography color="text.secondary" sx={{ fontSize: 14 }}>
                          No materials matching the selected filter criteria.
                        </Typography>
                      </TableCell>
                    </TableRow>
                  ) : (
                    paginatedItems.map((m) => {
                      const isLow = isMaterialLowStock(m);
                      return (
                        <TableRow
                          key={m.id}
                          hover
                          sx={{
                            bgcolor: isLow ? "#fffbeb" : undefined,
                            "&:last-child td, &:last-child th": { border: 0 }
                          }}
                        >
                          <TableCell sx={{ fontSize: 12, whiteSpace: "nowrap" }}>
                            {m.materialType}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                            {m.materialName}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12 }}>
                            {m.manufacturerName || "—"}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12, fontFamily: "monospace", fontWeight: 600 }}>
                            {m.batchNumber}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12, whiteSpace: "nowrap" }}>
                            {formatLabDate(m.receivingDate)}
                          </TableCell>
                          <TableCell
                            sx={{
                              fontSize: 12,
                              whiteSpace: "nowrap",
                              color: isMaterialExpiringSoon(m.expiryDate) ? "#dc2626" : "inherit",
                              fontWeight: isMaterialExpiringSoon(m.expiryDate) ? 600 : "normal"
                            }}
                          >
                            {m.expiryDate ? formatLabDate(m.expiryDate) : "—"}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12 }}>
                            {m.code ?? "—"}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12, fontStyle: "italic" }}>
                            {m.organism?.scientificName ?? "—"}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12 }}>
                            {m.atccNumber ?? "—"}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12 }}>
                            {m.location}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12, textAlign: "right", whiteSpace: "nowrap" }}>
                            {m.quantityReceived} {m.unit}
                          </TableCell>
                          <TableCell
                            sx={{
                              fontSize: 12,
                              textAlign: "right",
                              whiteSpace: "nowrap",
                              fontWeight: 700,
                              color: m.quantityRemaining <= 0 ? "#dc2626" : isLow ? "#d97706" : "#16a34a"
                            }}
                          >
                            {m.quantityRemaining} {m.unit}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12, textAlign: "right", whiteSpace: "nowrap" }}>
                            {m.minimumStockLevel != null ? `${m.minimumStockLevel} ${m.unit}` : "—"}
                          </TableCell>
                          <TableCell sx={{ textAlign: "center" }}>
                            {isLow ? (
                              <StatusBadge status="LowStock" label="Low Stock" />
                            ) : (
                              <StatusBadge status={m.status} />
                            )}
                          </TableCell>
                          <TableCell sx={{ textAlign: "center", whiteSpace: "nowrap" }}>
                            <Tooltip title="Edit Material">
                              <IconButton
                                size="small"
                                onClick={() => {
                                  setEditingItem(m);
                                  setIsAddOpen(true);
                                }}
                                sx={{ color: "primary.main" }}
                              >
                                <EditOutlinedIcon fontSize="small" />
                              </IconButton>
                            </Tooltip>
                            {canSeeHistory && (
                              <Tooltip title="Audit History">
                                <IconButton
                                  size="small"
                                  onClick={() => setHistoryFor(m.id)}
                                  sx={{ color: "text.secondary" }}
                                >
                                  <HistoryIcon fontSize="small" />
                                </IconButton>
                              </Tooltip>
                            )}
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
              count={filteredItems.length}
              rowsPerPage={rowsPerPage}
              page={page}
              onPageChange={(_, newPage) => setPage(newPage)}
              onRowsPerPageChange={(e) => {
                setRowsPerPage(parseInt(e.target.value, 10));
                setPage(0);
              }}
              sx={{ borderTop: "1px solid #e5e7eb" }}
            />
          </Paper>
        </Box>
      )}

      {/* Add / Edit Modal Dialog */}
      <AddMaterialDialog
        open={isAddOpen}
        onClose={() => {
          setIsAddOpen(false);
          setEditingItem(null);
        }}
        onSuccess={(msg) => {
          setMessage({ text: msg, ok: true });
          loadData();
        }}
        editingItem={editingItem}
      />

      {/* Controlled Printable Document Table */}
      <PrintableTable
        title="Materials in Stock — Microbiology Lab"
        subtitle="Expired and depleted items are excluded from this list."
        rows={printList}
        getRowId={(m) => m.id}
        columns={[
          { label: "Type", render: (m) => m.materialType },
          { label: "Name", render: (m) => m.materialName },
          { label: "Manufacturer", render: (m) => m.manufacturerName },
          { label: "Batch/Lot", render: (m) => m.batchNumber },
          { label: "Received", render: (m) => formatLabDate(m.receivingDate) },
          { label: "Expiry", render: (m) => (m.expiryDate ? formatLabDate(m.expiryDate) : "—") },
          { label: "Code", render: (m) => m.code ?? "—" },
          { label: "Location", render: (m) => m.location },
          { label: "Qty Remaining", render: (m) => `${m.quantityRemaining} ${m.unit}` }
        ]}
      />

      {/* Audit History Modal */}
      <AuditHistoryDialog
        open={historyFor != null}
        onClose={() => setHistoryFor(null)}
        entityName="Material"
        entityId={historyFor}
      />
    </>
  );
}
