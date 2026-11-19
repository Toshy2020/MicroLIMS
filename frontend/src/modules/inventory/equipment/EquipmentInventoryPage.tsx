import { useEffect, useMemo, useRef, useState } from "react";
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
import { TableSkeleton } from "../../../components/TableSkeleton";
import { formatLabCalendarDate } from "../../../utils/formatDate";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { useLaboratorySections } from "../../../hooks/useLaboratorySections";
import { useTableState } from "../../../components/lab/table/useTableState";
import { TableToolbar } from "../../../components/lab/table/TableToolbar";
import { useTranslation } from "../../../i18n";
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

const TABLE_KEY = "inventory.equipment";
const KPI_FILTERS: readonly EquipmentKpiFilter[] = ["in_service", "out_of_service", "calibration_overdue", "calibration_due_soon"];
const DROPDOWN_FILTERS = ["instrumentType", "status", "location", "calibrationRange"] as const;

function textFilter(filters: Record<string, string | string[]>, name: string): string {
  const value = filters[name];
  return typeof value === "string" ? value : "";
}

export function EquipmentInventoryPage() {
  const theme = useTheme();
  const { t } = useTranslation("inventory");
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
  // The search, dropdowns, KPI shortcut, sort and page live in the address and in saved views.
  const tableState = useTableState(TABLE_KEY);
  const { setFilter, setSearch, applyState } = tableState;
  const stateFilters = tableState.state.filters;
  const stateSearch = tableState.state.search;
  const kpiParam = textFilter(stateFilters, "kpi");
  const kpiFilter: EquipmentKpiFilter = KPI_FILTERS.find((k) => k === kpiParam) ?? "all";
  const filters = useMemo<EquipmentFilterState>(() => ({
    search: stateSearch,
    instrumentType: textFilter(stateFilters, "instrumentType"),
    status: textFilter(stateFilters, "status"),
    location: textFilter(stateFilters, "location"),
    calibrationRange: textFilter(stateFilters, "calibrationRange")
  }), [stateFilters, stateSearch]);

  // Dialog states
  const [isRegisterOpen, setIsRegisterOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<EquipmentItem | null>(null);
  const [detailsItem, setDetailsItem] = useState<EquipmentItem | null>(null);
  const [historyFor, setHistoryFor] = useState<number | null>(null);

  // ?equipmentId= (from Recent / Ctrl+K) opens that record's details once the list is loaded.
  const deepLinkedEquipment = useRef(false);
  const equipmentIdParam = searchParams.get("equipmentId");
  useEffect(() => {
    if (deepLinkedEquipment.current || !items || !equipmentIdParam) return;
    deepLinkedEquipment.current = true;
    const found = items.find((e) => e.id === Number(equipmentIdParam));
    if (found) setDetailsItem(found);
  }, [items, equipmentIdParam]);

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
      setMessage({ text: t("equipment.page.loadError"), ok: false });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleReset = () => {
    applyState(
      { filters: {}, search: "", sort: tableState.state.sort, pageSize: tableState.state.pageSize },
      tableState.viewId
    );
  };

  const handleKpiSelect = (newKpi: EquipmentKpiFilter) => {
    setFilter("kpi", newKpi === "all" ? null : newKpi);
  };

  // The filter bar reports one changed field at a time.
  const handleFilterChange = (newFilters: EquipmentFilterState) => {
    if (newFilters.search !== filters.search) {
      setSearch(newFilters.search);
      return;
    }
    const changed = DROPDOWN_FILTERS.find((name) => newFilters[name] !== filters[name]);
    if (changed) setFilter(changed, newFilters[changed]);
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
    { key: "instrumentType", label: t("equipment.page.columns.type"), sortable: true, render: (eq) => <Box component="span" sx={{ fontWeight: 600, whiteSpace: "nowrap" }}>{eq.instrumentType}</Box> },
    { key: "manufacturerName", label: t("equipment.page.columns.manufacturer"), sortable: true, render: (eq) => eq.manufacturerName || "—" },
    { key: "serialNumber", label: t("equipment.page.columns.serialNumber"), sortable: true, nowrap: true, render: (eq) => <Box component="span" sx={{ fontFamily: monospaceFontFamily }}>{eq.serialNumber || "—"}</Box> },
    { key: "firmwareVersion", label: t("equipment.page.columns.firmware"), sortable: true, render: (eq) => eq.firmwareVersion || "—" },
    {
      key: "code", label: t("equipment.page.columns.code"), sortable: true, nowrap: true,
      render: (eq) => <Box id={`equip-code-link-${eq.id}`} component="span" sx={{ fontFamily: monospaceFontFamily, fontWeight: 700 }}>{eq.code}</Box>
    },
    { key: "location", label: t("equipment.page.columns.location"), sortable: true, render: (eq) => eq.location },
    {
      key: "calibrationDueDate", label: t("equipment.page.columns.calibrationDue"), sortable: true,
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
              {eq.calibrationDueDate ? formatLabCalendarDate(eq.calibrationDueDate) : "—"}
            </Box>
            {isOverdue && <StatusBadge status="Overdue" label={t("equipment.status.overdue")} />}
            {isDueSoon && <StatusBadge status="Due Soon" label={t("equipment.status.dueSoon")} />}
          </Box>
        );
      }
    },
    {
      key: "sectionId", label: t("equipment.page.columns.laboratory"), sortable: true, sortValue: (eq) => (eq.sectionId != null ? sectionNameById.get(eq.sectionId) ?? "" : ""),
      render: (eq) => (eq.sectionId != null ? sectionNameById.get(eq.sectionId) ?? "—" : <StatusBadge status="OnHold" label={t("equipment.status.unassigned")} />)
    },
    { key: "status", label: t("equipment.page.columns.status"), sortable: true, render: (eq) => <StatusBadge status={eq.status} label={t(`equipment.status.${eq.status}`)} /> }
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
      {t("equipment.page.registerButton")}
    </Button>
  );

  const isFiltered = kpiFilter !== "all" || Object.values(filters).some((v) => v !== "");

  return (
    <>
      <Box className="no-print">
        <LabPage
          title={t("equipment.page.title")}
          subtitle={t("equipment.page.subtitle")}
          actions={
            activeTab === 0 ? (
              <>
                <PrintButton label={t("equipment.page.printLabel")} />
                {registerButton}
              </>
            ) : undefined
          }
          kpis={
            <>
              {/* Tabs Navigation: Equipment Register vs Active Equipment */}
              <Box sx={{ borderBottom: 1, borderColor: "divider" }}>
                <Tabs value={activeTab} onChange={(_, val) => setActiveTab(val)}>
                  <Tab label={t("equipment.page.tabs.register")} sx={{ fontWeight: 700, textTransform: "none", fontSize: 15 }} />
                  <Tab label={t("equipment.page.tabs.active")} sx={{ fontWeight: 700, textTransform: "none", fontSize: 15 }} />
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
              tableKey={TABLE_KEY}
              tableState={tableState}
              toolbar={<TableToolbar tableKey={TABLE_KEY} tableState={tableState} showSearch={false} />}
              loading={loading || !items}
              pageSize={25}
              onRowClick={(eq) => setDetailsItem(eq)}
              rowTone={(eq) => (isEquipmentCalibrationOverdue(eq) ? "detected" : isEquipmentCalibrationDueSoon(eq, 30) ? "inconclusive" : undefined)}
              rowActions={(eq) => [
                { label: t("equipment.page.actions.detailsAndDocs"), onClick: () => setDetailsItem(eq) },
                { label: t("equipment.page.actions.edit"), onClick: () => openEdit(eq) },
                ...(canSeeHistory ? [{ label: t("equipment.page.actions.auditHistory"), onClick: () => setHistoryFor(eq.id) }] : [])
              ]}
              empty={
                isFiltered
                  ? { title: t("equipment.page.emptyFiltered.title"), description: t("equipment.page.emptyFiltered.description") }
                  : { title: t("equipment.page.empty.title"), description: t("equipment.page.empty.description"), action: registerButton }
              }
            />
          ) : loading || !items ? (
            <TableSkeleton rows={8} cols={7} hasContainer={false} />
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
          { label: "Calibration Due", render: (eq) => (eq.calibrationDueDate ? formatLabCalendarDate(eq.calibrationDueDate, { english: true }) : "—") }
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
