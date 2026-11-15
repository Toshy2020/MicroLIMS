import { useEffect, useMemo, useState } from "react";
import { Box, Button, Alert, Tabs, Tab } from "@mui/material";
import { usePersistedTab } from "../../../hooks/usePersistedTab";
import AddIcon from "@mui/icons-material/Add";
import { LabPage } from "../../../components/lab";
import { useTableState } from "../../../components/lab/table/useTableState";
import { SectionTitle } from "../../../components/SectionTitle";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { AuditHistoryDialog } from "../../../components/AuditHistoryDialog";
import { MediaLotKpiCards, MediaKpiFilterKey, lifecycleOf } from "./components/MediaLotKpiCards";
import { MediaLotFilterBar } from "./components/MediaLotFilterBar";
import { MediaLotRegisterTable } from "./components/MediaLotRegisterTable";
import { SelectedMediaLotWorkspace } from "./components/SelectedMediaLotWorkspace";
import { MediaPreparationDialog } from "./dialogs/MediaPreparationDialog";
import { MediaEvaluationWorkflowDialog } from "./dialogs/MediaEvaluationWorkflowDialog";
import { MarkOutOfStockDialog } from "./dialogs/MarkOutOfStockDialog";
import { MediaPreparationService } from "./services/MediaPreparationService";
import { MediaEvaluationService } from "../mediaEvaluation/services/MediaEvaluationService";
import type { MediaLot } from "../mediaEvaluation/types/mediaEvaluationTypes";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { ApprovedMediaListPage } from "../../inventory/approvedLists/ApprovedMediaListPage";
import { PrintLabelsDialog } from "../../../components/labels/PrintLabelsDialog";
import { usePostCreateLabelPrint } from "../../../components/labels/usePostCreateLabelPrint";

const TABLE_KEY = "labConfig.mediaLots";
const KPI_STATUSES: readonly MediaKpiFilterKey[] = [
  "Pending Evaluation",
  "Awaiting Approval",
  "Released",
  "Rejected",
  "Out of Stock"
];

function textFilter(filters: Record<string, string | string[]>, name: string): string {
  const value = filters[name];
  return typeof value === "string" ? value : "";
}

const PAGE_TABS = ["lots", "approved"] as const;
type PageTab = (typeof PAGE_TABS)[number];

export function MediaPage() {
  const [tab, setTab] = usePersistedTab("media", PAGE_TABS, "tab");
  const [lots, setLots] = useState<any[]>([]);
  const [evaluations, setEvaluations] = useState<any[]>([]);
  const [awaitingApprovalIds, setAwaitingApprovalIds] = useState<Set<number>>(new Set());
  const [materials, setMaterials] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const loadData = async () => {
    setLoading(true);
    try {
      const [lotsData, awaitingQueue, evalsData, materialsData] = await Promise.all([
        MediaPreparationService.getAll(),
        MediaPreparationService.getAwaitingApproval().catch(() => []),
        MediaEvaluationService.getAll().catch(() => []),
        masterDataOptions.getMaterials("DehydratedMedia").catch(() => [])
      ]);

      setLots(lotsData || []);
      setAwaitingApprovalIds(new Set((awaitingQueue || []).map((m: any) => m.id)));
      setEvaluations(evalsData || []);
      setMaterials(materialsData || []);
    } catch (err: any) {
      setMessage({ text: err?.response?.data?.message ?? "Failed to load media lots.", ok: false });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const tabs = (
    <Tabs
      value={tab}
      onChange={(_, v: PageTab) => setTab(v)}
      sx={{ mb: 2, borderBottom: 1, borderColor: "divider" }}
    >
      <Tab value="lots" label="Media Lots" />
      <Tab value="approved" label="Approved Media" />
    </Tabs>
  );

  if (tab === "approved") {
    return (
      <>
        {tabs}
        <ApprovedMediaListPage embedded />
      </>
    );
  }

  return (
    <>
      {tabs}
      <MediaLotsContent
        lots={lots}
        evaluations={evaluations}
        awaitingApprovalIds={awaitingApprovalIds}
        materials={materials}
        loading={loading}
        message={message}
        onDismissMessage={() => setMessage(null)}
        onMessage={setMessage}
        onReload={loadData}
      />
    </>
  );
}

interface MediaLotsContentProps {
  lots: MediaLot[];
  evaluations: unknown[];
  awaitingApprovalIds: Set<number>;
  materials: unknown[];
  loading: boolean;
  message: { text: string; ok: boolean } | null;
  onDismissMessage: () => void;
  onMessage: (msg: { text: string; ok: boolean } | null) => void;
  onReload: () => Promise<void>;
}

function MediaLotsContent({
  lots,
  evaluations,
  awaitingApprovalIds,
  materials,
  loading,
  message,
  onDismissMessage,
  onMessage,
  onReload
}: MediaLotsContentProps) {
  // Selected Media Lot for Split-Pane Workspace
  const [selectedLotId, setSelectedLotId] = useState<number | null>(null);

  // Dialogs state
  const [prepDialogOpen, setPrepDialogOpen] = useState(false);
  const [evaluationDialogOpen, setEvaluationDialogOpen] = useState(false);
  const [activeEvaluationId, setActiveEvaluationId] = useState<number | null>(null);
  const [auditLotId, setAuditLotId] = useState<number | null>(null);
  const [pendingDecision, setPendingDecision] = useState<{ lot: MediaLot; approved: boolean } | null>(null);
  const [outOfStockLot, setOutOfStockLot] = useState<MediaLot | null>(null);
  const postCreatePrint = usePostCreateLabelPrint();

  // Search, material dropdown and status/KPI filter live in the address and in saved views.
  const tableState = useTableState(TABLE_KEY);
  const { setFilter, setSearch, applyState } = tableState;
  const search = tableState.state.search;
  const selectedMaterialId = textFilter(tableState.state.filters, "materialId");
  const selectedStatus = textFilter(tableState.state.filters, "status");
  const activeKpi: MediaKpiFilterKey | null = KPI_STATUSES.find((k) => k === selectedStatus) ?? null;

  const handleKpiSelect = (kpi: MediaKpiFilterKey) => {
    if (kpi === "ALL" || activeKpi === kpi) {
      setFilter("status", null);
    } else {
      setFilter("status", kpi);
    }
  };

  const handleStatusFilterChange = (status: string) => {
    setFilter("status", status || null);
  };

  const handleMaterialChange = (materialId: string) => {
    setFilter("materialId", materialId || null);
  };

  const handleResetFilters = () => {
    applyState(
      { filters: {}, search: "", sort: tableState.state.sort, pageSize: tableState.state.pageSize },
      tableState.viewId
    );
  };

  // Filtered lots
  const visibleLots = useMemo(() => {
    const q = search.trim().toLowerCase();

    return lots.filter((lot) => {
      if (q) {
        const matchesLot = lot.lotNumber?.toLowerCase().includes(q);
        const matchesMaterial = lot.material?.materialName?.toLowerCase().includes(q);
        const matchesBatch = lot.material?.batchNumber?.toLowerCase().includes(q);
        if (!matchesLot && !matchesMaterial && !matchesBatch) return false;
      }

      if (selectedMaterialId && String(lot.materialId) !== selectedMaterialId) {
        return false;
      }

      if (selectedStatus) {
        const lifecycle = lifecycleOf(lot, awaitingApprovalIds);
        if (lifecycle !== selectedStatus) return false;
      }

      return true;
    });
  }, [lots, search, selectedMaterialId, selectedStatus, awaitingApprovalIds]);

  const selectedLot = useMemo(() => {
    if (!selectedLotId || !lots) return null;
    return lots.find((l) => l.id === selectedLotId) || null;
  }, [selectedLotId, lots]);

  const handleSelectLot = (lot: MediaLot) => {
    setSelectedLotId(lot.id);
  };

  const handleDeselectLot = () => {
    setSelectedLotId(null);
  };

  const handleOpenEvaluation = (evaluationId: number) => {
    setActiveEvaluationId(evaluationId);
    setEvaluationDialogOpen(true);
  };

  const handleViewRecord = (lotId: number) => {
    window.open(`/media/${lotId}/report`, "_blank", "noopener");
  };

  const handleViewAuditHistory = (lotId: number) => {
    setAuditLotId(lotId);
  };

  const confirmDecision = async (password: string) => {
    if (!pendingDecision) return;
    try {
      await MediaPreparationService.decideRelease(pendingDecision.lot.id, password, pendingDecision.approved);
      onMessage({
        text: `Media lot ${pendingDecision.lot.lotNumber} ${pendingDecision.approved ? "released for use" : "rejected"}.`,
        ok: pendingDecision.approved
      });
      setPendingDecision(null);
      await onReload();
    } catch (e: unknown) {
      const serverMessage =
        typeof e === "object" && e !== null && "response" in e
          ? (e as { response?: { data?: { message?: string } } }).response?.data?.message
          : undefined;
      onMessage({ text: serverMessage ?? "Release decision failed.", ok: false });
    }
  };

  const handlePrepSuccess = async (newLot: MediaLot) => {
    onMessage({ text: `Media lot ${newLot?.lotNumber ?? ""} successfully prepared.`, ok: true });
    if (newLot?.id && newLot?.lotNumber) {
      postCreatePrint.offerPrint([
        {
          entityType: "MediaLot",
          entityId: newLot.id,
          code: newLot.lotNumber,
          displayName: newLot.material?.materialName ?? "Media Lot"
        }
      ]);
    }
    await onReload();
    if (newLot?.id) {
      setSelectedLotId(newLot.id);
    }
  };

  // Skeletons only on the first load; a refresh keeps the current rows visible.
  const initialLoading = loading && lots.length === 0;
  const isFiltered = Boolean(search || selectedMaterialId || selectedStatus);

  const register = (compact: boolean) => (
    <MediaLotRegisterTable
      lots={visibleLots}
      awaitingApprovalIds={awaitingApprovalIds}
      selectedLotId={compact ? selectedLotId : null}
      onSelectLot={handleSelectLot}
      isCompact={compact}
      loading={initialLoading}
      isFiltered={isFiltered}
      tableKey={TABLE_KEY}
      tableState={tableState}
      onViewRecord={handleViewRecord}
      onViewAuditHistory={handleViewAuditHistory}
      onRequestReleaseDecision={(lot, approved) => setPendingDecision({ lot, approved })}
    />
  );

  return (
    <>
      <LabPage
        title="Media Preparation & Evaluation"
        subtitle="Prepare media lots and manage their evaluations (GPT, Sterility, Indication/Inhibition, Enrichment Characteristics)."
        actions={
          <Button variant="contained" color="primary" startIcon={<AddIcon />} onClick={() => setPrepDialogOpen(true)} sx={{ whiteSpace: "nowrap" }}>
            Prepare New Media Lot
          </Button>
        }
        kpis={
          <MediaLotKpiCards
            lots={lots}
            awaitingApprovalIds={awaitingApprovalIds}
            activeKpi={activeKpi}
            onSelectKpi={handleKpiSelect}
            loading={initialLoading}
          />
        }
        filters={
          <MediaLotFilterBar
            search={search}
            onSearchChange={setSearch}
            selectedMaterialId={selectedMaterialId}
            onMaterialChange={handleMaterialChange}
            selectedStatus={selectedStatus}
            onStatusChange={handleStatusFilterChange}
            materials={materials}
            onResetFilters={handleResetFilters}
            resultCount={visibleLots.length}
            onRefresh={onReload}
            refreshing={loading}
          />
        }
      >
        {message && (
          <Alert severity={message.ok ? "success" : "error"} onClose={onDismissMessage}>
            {message.text}
          </Alert>
        )}

        {selectedLot ? (
          /* SPLIT-PANE LAYOUT: Left = Compact Media Lots, Right = Selected Media Lot Workspace */
          <Box
            sx={{
              display: "flex",
              flexDirection: { xs: "column", md: "row" },
              gap: 2,
              alignItems: "stretch",
              minHeight: "calc(100vh - 280px)"
            }}
          >
            {/* Left Panel: Compact Media Lots Register (approx 38% width) */}
            <Box
              sx={{
                width: { xs: "100%", md: "38%" },
                display: "flex",
                flexDirection: "column",
                gap: 1.5,
                flexShrink: 0
              }}
            >
              <SectionTitle>{`Media Lots (${visibleLots.length})`}</SectionTitle>
              {register(true)}
            </Box>

            {/* Right Panel: Selected Media Lot Workspace (approx 62% width) */}
            <Box
              sx={{
                flex: 1,
                minWidth: 0,
                display: "flex",
                flexDirection: "column",
                maxHeight: { xs: "auto", md: "calc(100vh - 290px)" }
              }}
            >
              <SelectedMediaLotWorkspace
                lot={selectedLot}
                awaitingApprovalIds={awaitingApprovalIds}
                onClose={handleDeselectLot}
                onViewRecord={handleViewRecord}
                onViewAuditHistory={handleViewAuditHistory}
                onOpenEvaluation={handleOpenEvaluation}
                onRequestReleaseDecision={(lot, approved) => setPendingDecision({ lot, approved })}
                onMarkOutOfStock={(lot) => setOutOfStockLot(lot)}
                evaluationsList={evaluations}
              />
            </Box>
          </Box>
        ) : (
          /* NORMAL STATE: Full-Width Media Lots Register */
          <>
            <SectionTitle>{`Media Lots (${visibleLots.length})`}</SectionTitle>
            {register(false)}
          </>
        )}
      </LabPage>

      {/* Modal Dialogs */}
      <MediaPreparationDialog
        open={prepDialogOpen}
        onClose={() => setPrepDialogOpen(false)}
        onSuccess={handlePrepSuccess}
      />

      <MediaEvaluationWorkflowDialog
        open={evaluationDialogOpen}
        evaluationId={activeEvaluationId}
        onClose={() => {
          setEvaluationDialogOpen(false);
          setActiveEvaluationId(null);
        }}
        onUpdated={onReload}
      />

      <MarkOutOfStockDialog
        open={Boolean(outOfStockLot)}
        lot={outOfStockLot}
        onClose={() => setOutOfStockLot(null)}
        onSuccess={() => {
          onMessage({ text: `Media lot ${outOfStockLot?.lotNumber ?? ""} marked Out of Stock.`, ok: true });
          onReload();
        }}
      />

      <AuditHistoryDialog
        open={Boolean(auditLotId)}
        entityName="Media"
        entityId={auditLotId}
        onClose={() => setAuditLotId(null)}
      />

      <PrintLabelsDialog
        open={postCreatePrint.dialogOpen}
        targets={postCreatePrint.targets}
        onClose={postCreatePrint.closeDialog}
      />

      {pendingDecision && (
        <SignatureDialog
          open
          meaningStatement={
            pendingDecision.approved
              ? `I am releasing media lot ${pendingDecision.lot.lotNumber} for use in routine testing.`
              : `I am rejecting media lot ${pendingDecision.lot.lotNumber}.`
          }
          onCancel={() => setPendingDecision(null)}
          onConfirm={confirmDecision}
        />
      )}
    </>
  );
}
