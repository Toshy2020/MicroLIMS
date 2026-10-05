import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Box, Button, Alert, Typography, useTheme } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { LabPage, RegisterTable, NumericCell } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { monospaceFontFamily } from "../../../theme/palette";
import { StatusBadge } from "../../../components/StatusBadge";
import { PrintButton } from "../../../components/PrintButton";
import { PrintableTable } from "../../../components/PrintableTable";
import { AuditHistoryDialog } from "../../../components/AuditHistoryDialog";
import { formatLabDate } from "../../../utils/formatDate";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { useLaboratorySections } from "../../../hooks/useLaboratorySections";
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
import { MaterialFilterBar, MATERIAL_TYPE_OPTIONS } from "./components/MaterialFilterBar";
import { AddMaterialDialog } from "./components/AddMaterialDialog";
import { MaterialLotDetailsDialog } from "./components/MaterialLotDetailsDialog";

const MATERIAL_TYPE_LABEL_MAP = new Map<string, string>(
  MATERIAL_TYPE_OPTIONS.map((opt) => [opt.value, opt.label])
);

function getMaterialTypeDisplay(item: { materialType: string; customType?: string | null }): string {
  if (item.customType && item.customType.trim()) {
    return item.customType.trim();
  }
  return MATERIAL_TYPE_LABEL_MAP.get(item.materialType) ?? item.materialType;
}

const INITIAL_FILTERS: MaterialFilterState = {
  search: "",
  materialType: "",
  manufacturer: "",
  location: "",
  status: "",
  expiryRange: ""
};

export function MaterialsPage() {
  const theme = useTheme();
  const { permissions } = useAuth();
  const canSeeHistory = permissions.includes(PERMISSIONS.AUDIT_VIEW);

  // Menu links from each lab workspace (Task 11) append ?lab=MICRO|FP -
  // Material.SectionId is required server-side, so every row carries one
  // (unlike EquipmentInventory's still-nullable column); this is a
  // client-side filter on top of the already section-scoped list.
  const [searchParams] = useSearchParams();
  const labParam = searchParams.get("lab");
  const { sections } = useLaboratorySections();
  const sectionCodeById = useMemo(
    () => new Map(sections.map((s) => [s.sectionId, s.sectionCode])),
    [sections]
  );

  const activeSectionId = useMemo(() => {
    if (labParam) {
      const found = sections.find((s) => s.sectionCode === labParam);
      if (found) return found.sectionId;
    }
    return undefined;
  }, [labParam, sections]);

  const [items, setItems] = useState<MaterialItem[] | null>(null);
  const [printList, setPrintList] = useState<MaterialItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const printableTableTitle = useMemo(() => {
    let effectiveCode: string | undefined;
    let singleSectionId: number | undefined;

    if (labParam) {
      effectiveCode = labParam;
    } else if (printList.length > 0) {
      const firstSectionId = printList[0].sectionId;
      if (printList.every((row) => row.sectionId === firstSectionId)) {
        singleSectionId = firstSectionId;
        effectiveCode = sectionCodeById.get(firstSectionId);
      }
    }

    if (effectiveCode === "MICRO") {
      return "Materials in Stock: Microbiology Lab";
    }
    if (effectiveCode === "FP") {
      return "Materials in Stock: Physicochemical Lab";
    }
    if (effectiveCode) {
      const sec = sections.find((s) => s.sectionCode === effectiveCode);
      if (sec?.sectionName) {
        return `Materials in Stock: ${sec.sectionName}`;
      }
    }
    if (singleSectionId != null) {
      const sec = sections.find((s) => s.sectionId === singleSectionId);
      if (sec?.sectionName) {
        return `Materials in Stock: ${sec.sectionName}`;
      }
    }

    return "Materials in Stock: All Laboratories";
  }, [labParam, printList, sections, sectionCodeById]);

  // Filters & KPI state
  const [kpiFilter, setKpiFilter] = useState<MaterialKpiFilter>("all");
  const [filters, setFilters] = useState<MaterialFilterState>(INITIAL_FILTERS);

  // Dialog states
  const [isAddOpen, setIsAddOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<MaterialItem | null>(null);
  const [historyFor, setHistoryFor] = useState<number | null>(null);
  const [lotDetailsFor, setLotDetailsFor] = useState<MaterialItem | null>(null);

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
  };

  const handleKpiSelect = (newKpi: MaterialKpiFilter) => {
    setKpiFilter(newKpi);
  };

  const handleFilterChange = (newFilters: MaterialFilterState) => {
    setFilters(newFilters);
  };

  // Filtered dataset combining KPI shortcuts + form filters
  const filteredItems = useMemo(() => {
    if (!items) return [];

    return items.filter((item) => {
      if (labParam && sectionCodeById.get(item.sectionId) !== labParam) return false;

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
      if (filters.materialType) {
        if (filters.materialType.startsWith("custom:")) {
          const customName = filters.materialType.slice("custom:".length);
          if (item.customType !== customName) {
            return false;
          }
        } else if (item.materialType !== filters.materialType) {
          return false;
        }
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
  }, [items, kpiFilter, filters, labParam, sectionCodeById]);

  const openEdit = (m: MaterialItem) => {
    setEditingItem(m);
    setIsAddOpen(true);
  };

  const columns: RegisterColumn<MaterialItem>[] = [
    { key: "materialType", label: "Type", sortable: true, sortValue: (m) => getMaterialTypeDisplay(m), render: (m) => <Box component="span" sx={{ whiteSpace: "nowrap" }}>{getMaterialTypeDisplay(m)}</Box> },
    {
      key: "materialName", label: "Material Name", sortable: true,
      // Material Name + optional organism/ATCC secondary line
      render: (m) => {
        // Canonical ATCC: organism.atccNumber first, material.atccNumber as fallback
        const canonicalAtcc = m.organism?.atccNumber ?? m.atccNumber;
        const organism = m.organism?.scientificName;
        return (
          <>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap", fontWeight: 600 }}>
              <span>{m.materialName}</span>
              {m.materialType === "DehydratedMedia" && m.mediaProductId == null && (
                <StatusBadge status="OnHold" label="Not linked to a media product" />
              )}
              {(m.materialType === "ReferenceStandard" || m.materialType === "PrimaryStandard") && m.purity != null && (
                <StatusBadge status="Assigned" label={`Purity: ${m.purity}%`} />
              )}
            </Box>
            {(organism || canonicalAtcc) && (
              <Typography
                component="div"
                sx={{ fontSize: 12, color: "text.secondary", fontWeight: 400, fontStyle: organism ? "italic" : "normal", mt: 0.25 }}
              >
                {organism && canonicalAtcc ? `${organism} · ATCC ${canonicalAtcc}` : organism ? organism : `ATCC ${canonicalAtcc}`}
              </Typography>
            )}
          </>
        );
      }
    },
    { key: "manufacturerName", label: "Manufacturer", sortable: true, render: (m) => m.manufacturerName || "—" },
    {
      key: "batchNumber", label: "Batch/Lot No.", sortable: true, nowrap: true,
      render: (m) => <Box id={`lot-${m.id}`} component="span" sx={{ fontFamily: monospaceFontFamily, fontWeight: 700 }}>{m.batchNumber}</Box>
    },
    { key: "receivingDate", label: "Received", sortable: true, render: (m) => <Box component="span" sx={{ whiteSpace: "nowrap" }}>{formatLabDate(m.receivingDate)}</Box> },
    {
      key: "expiryDate", label: "Expiry", sortable: true,
      render: (m) => (
        <Box
          component="span"
          sx={{
            whiteSpace: "nowrap",
            color: isMaterialExpiringSoon(m.expiryDate) ? theme.custom.status.detected.text : "inherit",
            fontWeight: isMaterialExpiringSoon(m.expiryDate) ? 600 : "normal"
          }}
        >
          {m.expiryDate ? formatLabDate(m.expiryDate) : "—"}
        </Box>
      )
    },
    { key: "code", label: "Code", sortable: true, nowrap: true, render: (m) => <Box component="span" sx={{ fontFamily: monospaceFontFamily }}>{m.code ?? "—"}</Box> },
    { key: "location", label: "Location", sortable: true, render: (m) => m.location },
    { key: "quantityReceived", label: "Qty Received", numeric: true, sortable: true, render: (m) => <NumericCell value={m.quantityReceived} unit={m.unit} /> },
    {
      key: "quantityRemaining", label: "Qty Remaining", numeric: true, sortable: true,
      render: (m) => (
        <Box
          component="span"
          sx={{
            fontWeight: 700,
            color: m.quantityRemaining <= 0
              ? theme.custom.status.detected.text
              : isMaterialLowStock(m)
              ? theme.custom.status.action.text
              : theme.custom.status.notDetected.text
          }}
        >
          <NumericCell value={m.quantityRemaining} unit={m.unit} />
        </Box>
      )
    },
    { key: "minimumStockLevel", label: "Min Stock", numeric: true, sortable: true, render: (m) => <NumericCell value={m.minimumStockLevel} unit={m.unit} /> },
    {
      key: "status", label: "Status", sortable: true,
      render: (m) => (isMaterialLowStock(m) ? <StatusBadge status="LowStock" label="Low Stock" /> : <StatusBadge status={m.status} />)
    }
  ];

  const addButton = (
    <Button
      variant="contained"
      color="primary"
      startIcon={<AddIcon />}
      onClick={() => {
        setEditingItem(null);
        setIsAddOpen(true);
      }}
      sx={{ whiteSpace: "nowrap" }}
    >
      Add to Stock
    </Button>
  );

  const isFiltered = kpiFilter !== "all" || Object.values(filters).some((v) => v !== "");

  return (
    <>
      <Box className="no-print">
        <LabPage
          title="Materials Stock"
          subtitle="Media, discs, kits, reagents, chemicals, disposables: receiving, expiry, and quantity received/remaining."
          actions={
            <>
              <PrintButton label="Print (excludes expired / depleted)" />
              {addButton}
            </>
          }
          kpis={<MaterialKpiCards items={items ?? []} activeFilter={kpiFilter} onFilterSelect={handleKpiSelect} loading={loading || !items} />}
          filters={
            <MaterialFilterBar
              items={items ?? []}
              filters={filters}
              onFilterChange={handleFilterChange}
              onReset={handleReset}
              extraActive={kpiFilter !== "all"}
              resultCount={filteredItems.length}
              onRefresh={loadData}
              refreshing={loading}
              sectionId={activeSectionId}
            />
          }
        >
          {message && (
            <Alert severity={message.ok ? "success" : "error"} onClose={() => setMessage(null)}>
              {message.text}
            </Alert>
          )}

          <RegisterTable
            columns={columns}
            rows={filteredItems}
            getRowId={(m) => m.id}
            loading={loading || !items}
            onRowClick={(m) => setLotDetailsFor(m)}
            rowTone={(m) => (m.status === "Expired" ? "detected" : isMaterialLowStock(m) ? "inconclusive" : undefined)}
            rowActions={(m) => [
              { label: "Lot details and documents", onClick: () => setLotDetailsFor(m) },
              { label: "Edit material", onClick: () => openEdit(m) },
              ...(canSeeHistory ? [{ label: "Audit history", onClick: () => setHistoryFor(m.id) }] : [])
            ]}
            empty={
              isFiltered
                ? { title: "No materials matching the selected filter criteria", description: "Reset the filters to see all stock." }
                : { title: "No materials in stock", description: "Receive the first material lot.", action: addButton }
            }
          />
        </LabPage>
      </Box>

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

      {/* Lot Details Dialog (documents) */}
      <MaterialLotDetailsDialog
        open={lotDetailsFor != null}
        material={lotDetailsFor}
        onClose={() => setLotDetailsFor(null)}
      />

      {/* Controlled Printable Document Table */}
      <PrintableTable
        title={printableTableTitle}
        subtitle="Expired and depleted items are excluded from this list."
        rows={printList}
        getRowId={(m) => m.id}
        columns={[
          { label: "Type", render: (m) => getMaterialTypeDisplay(m) },
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
