import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Box, Button, Alert, Tabs, Tab, useTheme } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { LabPage, RegisterTable } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { monospaceFontFamily } from "../../../theme/palette";
import { StatusBadge } from "../../../components/StatusBadge";
import { PrintButton } from "../../../components/PrintButton";
import { PrintableTable } from "../../../components/PrintableTable";
import { AuditHistoryDialog } from "../../../components/AuditHistoryDialog";
import { LoadingSpinner } from "../../../components/LoadingSpinner";
import { formatLabDate } from "../../../utils/formatDate";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { useLaboratorySections } from "../../../hooks/useLaboratorySections";
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

const INITIAL_FILTERS: EquipmentFilterState = {
  search: "",
  instrumentType: "",
  status: "",
  location: "",
  calibrationRange: ""
};

export function EquipmentInventoryPage() {
  const theme = useTheme();
  const { permissions } = useAuth();
  const canSeeHistory = permissions.includes(PERMISSIONS.AUDIT_VIEW);

  // Menu links from each lab workspace (Task 11) append ?lab=MICRO|FP - a
  // client-side filter on top of the already section-scoped list, same as
  // the equipment/materials rows below.
  const [searchParams] = useSearchParams();
  const labParam = searchParams.get("lab");
  const { sections } = useLaboratorySections();
  const sectionCodeById = useMemo(
    () => new Map(sections.map((s) => [s.sectionId, s.sectionCode])),
    [sections]
  );
  const sectionNameById = useMemo(
    () => new Map(sections.map((s) => [s.sectionId, s.sectionName])),
    [sections]
  );

  const [activeTab, setActiveTab] = useState(0); // 0 = Equipment Register, 1 = Active Equipment
  const [items, setItems] = useState<EquipmentItem[] | null>(null);
  const [printList, setPrintList] = useState<EquipmentItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  // Filters & KPI state
  const [kpiFilter, setKpiFilter] = useState<EquipmentKpiFilter>("all");
  const [filters, setFilters] = useState<EquipmentFilterState>(INITIAL_FILTERS);

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
  };

  const handleKpiSelect = (newKpi: EquipmentKpiFilter) => {
    setKpiFilter(newKpi);
  };

  const handleFilterChange = (newFilters: EquipmentFilterState) => {
    setFilters(newFilters);
  };

  const filteredItems = useMemo(() => {
    if (!items) return [];

    return items.filter((item) => {
      if (labParam && sectionCodeById.get(item.sectionId ?? -1) !== labParam) return false;

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
  }, [items, kpiFilter, filters, labParam, sectionCodeById]);

  const openEdit = (eq: EquipmentItem) => {
    setEditingItem(eq);
    setIsRegisterOpen(true);
  };

  const columns: RegisterColumn<EquipmentItem>[] = [
    { key: "instrumentType", label: "Type", sortable: true, render: (eq) => <Box component="span" sx={{ fontWeight: 600, whiteSpace: "nowrap" }}>{eq.instrumentType}</Box> },
    { key: "manufacturerName", label: "Manufacturer", sortable: true, render: (eq) => eq.manufacturerName || "—" },
    { key: "serialNumber", label: "Serial No.", sortable: true, nowrap: true, render: (eq) => <Box component="span" sx={{ fontFamily: monospaceFontFamily }}>{eq.serialNumber || "—"}</Box> },
    { key: "firmwareVersion", label: "Firmware", sortable: true, render: (eq) => eq.firmwareVersion || "—" },
    {
      key: "code", label: "Code", sortable: true, nowrap: true,
      render: (eq) => <Box id={`equip-code-link-${eq.id}`} component="span" sx={{ fontFamily: monospaceFontFamily, fontWeight: 700 }}>{eq.code}</Box>
    },
    { key: "location", label: "Location", sortable: true, render: (eq) => eq.location },
    {
      key: "calibrationDueDate", label: "Calibration Due", sortable: true,
      render: (eq) => {
        const isOverdue = isEquipmentCalibrationOverdue(eq);
        const isDueSoon = isEquipmentCalibrationDueSoon(eq, 30);
        return (
          <Box sx={{ display: "flex", alignItems: "center", gap: 1, whiteSpace: "nowrap" }}>
            <Box
              component="span"
              sx={{
                fontVariantNumeric: "tabular-nums",
                fontWeight: isOverdue || isDueSoon ? 600 : "normal",
                color: isOverdue
                  ? theme.custom.status.detected.text
                  : isDueSoon
                  ? theme.custom.status.inconclusive.text
                  : "inherit"
              }}
            >
              {eq.calibrationDueDate ? formatLabDate(eq.calibrationDueDate) : "—"}
            </Box>
            {isOverdue && <StatusBadge status="Overdue" />}
            {isDueSoon && <StatusBadge status="Due Soon" />}
          </Box>
        );
      }
    },
    {
      key: "sectionId", label: "Laboratory", sortable: true, sortValue: (eq) => (eq.sectionId != null ? sectionNameById.get(eq.sectionId) ?? "" : ""),
      render: (eq) => (eq.sectionId != null ? sectionNameById.get(eq.sectionId) ?? "—" : <StatusBadge status="OnHold" label="Unassigned" />)
    },
    { key: "status", label: "Status", sortable: true, render: (eq) => <StatusBadge status={eq.status} /> }
  ];

  const registerButton = (
    <Button
      variant="contained"
      color="primary"
      startIcon={<AddIcon />}
      onClick={() => {
        setEditingItem(null);
        setIsRegisterOpen(true);
      }}
      sx={{ whiteSpace: "nowrap" }}
    >
      Register Equipment
    </Button>
  );

  const isFiltered = kpiFilter !== "all" || Object.values(filters).some((v) => v !== "");

  return (
    <>
      <Box className="no-print">
        <LabPage
          title="Equipment"
          subtitle="QC/Microbiology lab instrument register & active equipment traceability."
          actions={
            activeTab === 0 ? (
              <>
                <PrintButton label="Print (excludes out-of-service / retired)" />
                {registerButton}
              </>
            ) : undefined
          }
          kpis={
            <>
              {/* Tabs Navigation: Equipment Register vs Active Equipment */}
              <Box sx={{ borderBottom: 1, borderColor: "divider" }}>
                <Tabs value={activeTab} onChange={(_, val) => setActiveTab(val)}>
                  <Tab label="Equipment Register" sx={{ fontWeight: 700, textTransform: "none", fontSize: 15 }} />
                  <Tab label="Active Equipment" sx={{ fontWeight: 700, textTransform: "none", fontSize: 15 }} />
                </Tabs>
              </Box>
              {activeTab === 0 && (
                <EquipmentKpiCards
                  items={items ?? []}
                  activeFilter={kpiFilter}
                  onFilterSelect={handleKpiSelect}
                  loading={loading || !items}
                />
              )}
            </>
          }
          filters={
            activeTab === 0 ? (
              <EquipmentFilterBar
                items={items ?? []}
                filters={filters}
                onFilterChange={handleFilterChange}
                onReset={handleReset}
                extraActive={kpiFilter !== "all"}
                resultCount={filteredItems.length}
                onRefresh={loadData}
                refreshing={loading}
              />
            ) : undefined
          }
        >
          {message && (
            <Alert severity={message.ok ? "success" : "error"} onClose={() => setMessage(null)}>
              {message.text}
            </Alert>
          )}

          {activeTab === 0 ? (
            <RegisterTable
              columns={columns}
              rows={filteredItems}
              getRowId={(eq) => eq.id}
              loading={loading || !items}
              pageSize={25}
              onRowClick={(eq) => setDetailsItem(eq)}
              rowTone={(eq) => (isEquipmentCalibrationOverdue(eq) ? "detected" : isEquipmentCalibrationDueSoon(eq, 30) ? "inconclusive" : undefined)}
              rowActions={(eq) => [
                { label: "Details and documents", onClick: () => setDetailsItem(eq) },
                { label: "Edit equipment", onClick: () => openEdit(eq) },
                ...(canSeeHistory ? [{ label: "Audit history", onClick: () => setHistoryFor(eq.id) }] : [])
              ]}
              empty={
                isFiltered
                  ? { title: "No equipment matching the selected filter criteria", description: "Reset the filters to see the whole register." }
                  : { title: "No equipment registered", description: "Register the first instrument.", action: registerButton }
              }
            />
          ) : loading || !items ? (
            <LoadingSpinner />
          ) : (
            /* Active Equipment View (Tab 1) */
            <ActiveEquipmentView
              labSectionId={labParam ? sections.find((s) => s.sectionCode === labParam)?.sectionId ?? -1 : null}
              onOpenDetails={(eqId) => {
                const eq = items.find((e) => e.id === eqId);
                if (eq) setDetailsItem(eq);
              }}
            />
          )}
        </LabPage>
      </Box>

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
        title="Equipment Register: Microbiology Lab"
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
