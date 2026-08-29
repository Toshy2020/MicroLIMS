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
  Typography,
  Tabs,
  Tab,
  useTheme
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
import { EquipmentInventoryService } from "./services/EquipmentInventoryService";
import {
  EquipmentFilterState,
  EquipmentItem,
  EquipmentKpiFilter
} from "./types/equipmentTypes";
import {
  EquipmentKpiCards,
  isEquipmentCalibrationDueSoon,
  isEquipmentCalibrationOverdue
} from "./components/EquipmentKpiCards";
import { EquipmentFilterBar } from "./components/EquipmentFilterBar";
import { RegisterEquipmentDialog } from "./components/RegisterEquipmentDialog";
import { EquipmentDetailsDialog } from "./components/EquipmentDetailsDialog";
import { ActiveEquipmentView } from "./components/ActiveEquipmentView";
import { brandColors } from "../../../theme";

const INITIAL_FILTERS: EquipmentFilterState = {
  search: "",
  instrumentType: "",
  status: "",
  location: "",
  calibrationRange: ""
};

export function EquipmentInventoryPage() {
  const theme = useTheme();
  const { role } = useAuth();
  const canSeeHistory = role === "SectionHead" || role === "SystemAdministrator";

  const [activeTab, setActiveTab] = useState(0); // 0 = Equipment Register, 1 = Active Equipment
  const [items, setItems] = useState<EquipmentItem[] | null>(null);
  const [printList, setPrintList] = useState<EquipmentItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  // Filters & KPI state
  const [kpiFilter, setKpiFilter] = useState<EquipmentKpiFilter>("all");
  const [filters, setFilters] = useState<EquipmentFilterState>(INITIAL_FILTERS);

  // Pagination state
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(15);

  // Dialog states
  const [isRegisterOpen, setIsRegisterOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<EquipmentItem | null>(null);
  const [detailsItem, setDetailsItem] = useState<EquipmentItem | null>(null);
  const [historyFor, setHistoryFor] = useState<number | null>(null);

  const loadData = async () => {
    try {
      setLoading(true);
      const [allEquipment, printEquipment] = await Promise.all([
        EquipmentInventoryService.getAll(),
        EquipmentInventoryService.getForPrint()
      ]);
      setItems(allEquipment);
      setPrintList(printEquipment);

      if (detailsItem) {
        const refreshed = allEquipment.find((e: EquipmentItem) => e.id === detailsItem.id);
        if (refreshed) {
          setDetailsItem(refreshed);
        }
      }
    } catch {
      setMessage({ text: "Failed to load equipment register.", ok: false });
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

  const handleKpiSelect = (newKpi: EquipmentKpiFilter) => {
    setKpiFilter(newKpi);
    setPage(0);
  };

  const handleFilterChange = (newFilters: EquipmentFilterState) => {
    setFilters(newFilters);
    setPage(0);
  };

  const filteredItems = useMemo(() => {
    if (!items) return [];

    return items.filter((item) => {
      if (kpiFilter === "in_service" && item.status !== "InService") return false;
      if (kpiFilter === "out_of_service" && item.status !== "OutOfService" && item.status !== "Retired") return false;
      if (kpiFilter === "calibration_overdue" && !isEquipmentCalibrationOverdue(item)) return false;
      if (kpiFilter === "calibration_due_soon" && !isEquipmentCalibrationDueSoon(item, 30)) return false;

      const q = filters.search.trim().toLowerCase();
      if (q) {
        const matchesType = (item.instrumentType ?? "").toLowerCase().includes(q);
        const matchesMfg = (item.manufacturerName ?? "").toLowerCase().includes(q);
        const matchesSerial = (item.serialNumber ?? "").toLowerCase().includes(q);
        const matchesFw = (item.firmwareVersion ?? "").toLowerCase().includes(q);
        const matchesCode = (item.code ?? "").toLowerCase().includes(q);
        const matchesLoc = (item.location ?? "").toLowerCase().includes(q);
        if (!matchesType && !matchesMfg && !matchesSerial && !matchesFw && !matchesCode && !matchesLoc) {
          return false;
        }
      }

      if (filters.instrumentType && item.instrumentType !== filters.instrumentType) {
        return false;
      }

      if (filters.status && item.status !== filters.status) {
        return false;
      }

      if (filters.location && item.location !== filters.location) {
        return false;
      }

      if (filters.calibrationRange) {
        if (filters.calibrationRange === "overdue" && !isEquipmentCalibrationOverdue(item)) return false;
        if (filters.calibrationRange === "due_30" && !isEquipmentCalibrationDueSoon(item, 30)) return false;
        if (filters.calibrationRange === "due_60" && !isEquipmentCalibrationDueSoon(item, 60)) return false;
        if (filters.calibrationRange === "valid" && isEquipmentCalibrationOverdue(item)) return false;
      }

      return true;
    });
  }, [items, kpiFilter, filters]);

  const paginatedItems = useMemo(() => {
    const start = page * rowsPerPage;
    return filteredItems.slice(start, start + rowsPerPage);
  }, [filteredItems, page, rowsPerPage]);

  return (
    <>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
        <PageHeader
          title="Equipment"
          subtitle="QC/Microbiology lab instrument register & active equipment traceability."
        />
        {activeTab === 0 && (
          <Button
            className="no-print"
            variant="contained"
            startIcon={<AddIcon />}
            onClick={() => {
              setEditingItem(null);
              setIsRegisterOpen(true);
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
            + Register Equipment
          </Button>
        )}
      </Box>

      {/* Tabs Navigation: Equipment Register vs Active Equipment */}
      <Box sx={{ borderBottom: 1, borderColor: "divider", mb: 2.5 }} className="no-print">
        <Tabs value={activeTab} onChange={(_, val) => setActiveTab(val)}>
          <Tab label="Equipment Register" sx={{ fontWeight: 700, textTransform: "none", fontSize: 15 }} />
          <Tab label="Active Equipment" sx={{ fontWeight: 700, textTransform: "none", fontSize: 15 }} />
        </Tabs>
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
      ) : activeTab === 0 ? (
        <Box className="no-print">
          <EquipmentKpiCards
            items={items}
            activeFilter={kpiFilter}
            onFilterSelect={handleKpiSelect}
          />

          <EquipmentFilterBar
            items={items}
            filters={filters}
            onFilterChange={handleFilterChange}
            onReset={handleReset}
          />

          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5, mt: 3 }}>
            <SectionTitle>
              {`Equipment Register (${filteredItems.length}${filteredItems.length !== items.length ? ` filtered from ${items.length}` : ""})`}
            </SectionTitle>
            <PrintButton label="Print (excludes out-of-service / retired)" />
          </Box>

          <Paper sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, overflow: "hidden" }}>
            <TableContainer>
              <Table size="small">
                <TableHead sx={{ bgcolor: "background.default" }}>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Type</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Manufacturer</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Serial No.</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Firmware</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Code</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Location</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Calibration Due</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "center" }}>Status</TableCell>
                    <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "center" }}>Actions</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {paginatedItems.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={9} sx={{ textAlign: "center", py: 4 }}>
                        <Typography color="text.secondary" sx={{ fontSize: 14 }}>
                          No equipment matching the selected filter criteria.
                        </Typography>
                      </TableCell>
                    </TableRow>
                  ) : (
                    paginatedItems.map((eq) => {
                      const isOverdue = isEquipmentCalibrationOverdue(eq);
                      const isDueSoon = isEquipmentCalibrationDueSoon(eq, 30);
                      return (
                        <TableRow
                          key={eq.id}
                          hover
                          sx={{
                            bgcolor: isOverdue
                              ? theme.custom.status.detected.bg
                              : isDueSoon
                              ? theme.custom.status.inconclusive.bg
                              : undefined,
                            "&:last-child td, &:last-child th": { border: 0 }
                          }}
                        >
                          <TableCell sx={{ fontSize: 12, fontWeight: 600, whiteSpace: "nowrap" }}>
                            {eq.instrumentType}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12 }}>
                            {eq.manufacturerName || "—"}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12, fontFamily: "monospace" }}>
                            {eq.serialNumber || "—"}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12 }}>
                            {eq.firmwareVersion || "—"}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12, fontFamily: "monospace", fontWeight: 700 }}>
                            <Typography
                              component="span"
                              id={`equip-code-link-${eq.id}`}
                              onClick={() => setDetailsItem(eq)}
                              sx={{
                                fontSize: 12,
                                fontFamily: "monospace",
                                fontWeight: 700,
                                color: "primary.main",
                                cursor: "pointer",
                                textDecoration: "underline",
                                "&:hover": { color: "primary.dark" }
                              }}
                            >
                              {eq.code}
                            </Typography>
                          </TableCell>
                          <TableCell sx={{ fontSize: 12 }}>
                            {eq.location}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12, whiteSpace: "nowrap" }}>
                            <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                              <span
                                style={{
                                  fontWeight: isOverdue || isDueSoon ? 600 : "normal",
                                  color: isOverdue
                                    ? theme.custom.status.detected.text
                                    : isDueSoon
                                    ? theme.custom.status.inconclusive.text
                                    : "inherit"
                                }}
                              >
                                {eq.calibrationDueDate ? formatLabDate(eq.calibrationDueDate) : "—"}
                              </span>
                              {isOverdue && <StatusBadge status="Overdue" />}
                              {isDueSoon && <StatusBadge status="Due Soon" />}
                            </Box>
                          </TableCell>
                          <TableCell sx={{ textAlign: "center" }}>
                            <StatusBadge status={eq.status} />
                          </TableCell>
                          <TableCell sx={{ textAlign: "center", whiteSpace: "nowrap" }}>
                            <Tooltip title="Edit Equipment">
                              <IconButton
                                size="small"
                                onClick={() => {
                                  setEditingItem(eq);
                                  setIsRegisterOpen(true);
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
                                  onClick={() => setHistoryFor(eq.id)}
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
              rowsPerPageOptions={[15, 30, 50, 100]}
              component="div"
              count={filteredItems.length}
              rowsPerPage={rowsPerPage}
              page={page}
              onPageChange={(_, newPage) => setPage(newPage)}
              onRowsPerPageChange={(e) => {
                setRowsPerPage(parseInt(e.target.value, 10));
                setPage(0);
              }}
              sx={{ borderTop: "1px solid", borderColor: "divider" }}
            />
          </Paper>
        </Box>
      ) : (
        /* Active Equipment View (Tab 1) */
        <Box className="no-print">
          <ActiveEquipmentView
            onOpenDetails={(eqId) => {
              const eq = items.find((e) => e.id === eqId);
              if (eq) setDetailsItem(eq);
            }}
          />
        </Box>
      )}

      {/* Equipment Details & Controlled Documents Dialog */}
      <EquipmentDetailsDialog
        open={detailsItem != null}
        equipment={detailsItem}
        onClose={() => setDetailsItem(null)}
        onEditEquipment={(eq) => {
          setEditingItem(eq);
          setIsRegisterOpen(true);
        }}
        onEquipmentUpdated={loadData}
      />

      {/* Register / Edit Modal Dialog */}
      <RegisterEquipmentDialog
        open={isRegisterOpen}
        onClose={() => {
          setIsRegisterOpen(false);
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
        title="Equipment Register — Microbiology Lab"
        subtitle="Out-of-service and retired instruments are excluded from this list."
        rows={printList}
        getRowId={(eq) => eq.id}
        columns={[
          { label: "Type", render: (eq) => eq.instrumentType },
          { label: "Manufacturer", render: (eq) => eq.manufacturerName || "—" },
          { label: "Serial No.", render: (eq) => eq.serialNumber || "—" },
          { label: "Firmware", render: (eq) => eq.firmwareVersion || "—" },
          { label: "Code", render: (eq) => eq.code },
          { label: "Location", render: (eq) => eq.location },
          { label: "Calibration Due", render: (eq) => (eq.calibrationDueDate ? formatLabDate(eq.calibrationDueDate) : "—") }
        ]}
      />

      {/* Audit History Modal */}
      <AuditHistoryDialog
        open={historyFor != null}
        onClose={() => setHistoryFor(null)}
        entityName="EquipmentInventory"
        entityId={historyFor}
      />
    </>
  );
}
