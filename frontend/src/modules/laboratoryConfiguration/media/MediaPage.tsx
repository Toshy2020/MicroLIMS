import { useEffect, useMemo, useState } from "react";
import { Box, Button, Alert, Paper } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";
import { PageHeader } from "../../../components/PageHeader";
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
import { masterDataOptions } from "../../../services/masterDataOptions";
import { brandColors } from "../../../theme";

export function MediaPage() {
  const [lots, setLots] = useState<any[]>([]);
  const [evaluations, setEvaluations] = useState<any[]>([]);
  const [awaitingApprovalIds, setAwaitingApprovalIds] = useState<Set<number>>(new Set());
  const [mediaTypes, setMediaTypes] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);

  // Selected Media Lot for Split-Pane Workspace
  const [selectedLotId, setSelectedLotId] = useState<number | null>(null);

  // Dialogs state
  const [prepDialogOpen, setPrepDialogOpen] = useState(false);
  const [evaluationDialogOpen, setEvaluationDialogOpen] = useState(false);
  const [activeEvaluationId, setActiveEvaluationId] = useState<number | null>(null);
  const [auditLotId, setAuditLotId] = useState<number | null>(null);
  const [pendingDecision, setPendingDecision] = useState<{ lot: any; approved: boolean } | null>(null);
  const [outOfStockLot, setOutOfStockLot] = useState<any | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  // Filters & Controls
  const [search, setSearch] = useState("");
  const [selectedMediaTypeId, setSelectedMediaTypeId] = useState("");
  const [selectedStatus, setSelectedStatus] = useState("");
  const [activeKpi, setActiveKpi] = useState<MediaKpiFilterKey | null>(null);

  const loadData = async () => {
    setLoading(true);
    try {
      const [lotsData, awaitingQueue, evalsData, mTypes] = await Promise.all([
        MediaPreparationService.getAll(),
        MediaPreparationService.getAwaitingApproval().catch(() => []),
        MediaEvaluationService.getAll().catch(() => []),
        masterDataOptions.getMediaTypes().catch(() => [])
      ]);

      setLots(lotsData || []);
      setAwaitingApprovalIds(new Set((awaitingQueue || []).map((m: any) => m.id)));
      setEvaluations(evalsData || []);
      setMediaTypes(mTypes || []);
    } catch (err: any) {
      setMessage({ text: err?.response?.data?.message ?? "Failed to load media lots.", ok: false });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleKpiSelect = (kpi: MediaKpiFilterKey) => {
    if (kpi === "ALL" || activeKpi === kpi) {
      setActiveKpi(null);
      setSelectedStatus("");
    } else {
      setActiveKpi(kpi);
      setSelectedStatus(kpi);
    }
  };

  const handleStatusFilterChange = (status: string) => {
    setSelectedStatus(status);
    if (status) {
      setActiveKpi(status as MediaKpiFilterKey);
    } else {
      setActiveKpi(null);
    }
  };

  const handleResetFilters = () => {
    setSearch("");
    setSelectedMediaTypeId("");
    setSelectedStatus("");
    setActiveKpi(null);
  };

  // Filtered lots
  const visibleLots = useMemo(() => {
    const q = search.trim().toLowerCase();

    return lots.filter((lot) => {
      if (q) {
        const matchesLot = lot.lotNumber?.toLowerCase().includes(q);
        const matchesType = lot.mediaType?.class?.toLowerCase().includes(q);
        const matchesMaterial = lot.material?.materialName?.toLowerCase().includes(q);
        const matchesBatch = lot.material?.batchNumber?.toLowerCase().includes(q);
        if (!matchesLot && !matchesType && !matchesMaterial && !matchesBatch) return false;
      }

      if (selectedMediaTypeId && String(lot.mediaTypeId) !== selectedMediaTypeId) {
        return false;
      }

      if (selectedStatus) {
        const lifecycle = lifecycleOf(lot, awaitingApprovalIds);
        if (lifecycle !== selectedStatus) return false;
      }

      return true;
    });
  }, [lots, search, selectedMediaTypeId, selectedStatus, awaitingApprovalIds]);

  const selectedLot = useMemo(() => {
    if (!selectedLotId || !lots) return null;
    return lots.find((l) => l.id === selectedLotId) || null;
  }, [selectedLotId, lots]);

  const handleSelectLot = (lot: any) => {
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
      setMessage({
        text: `Media lot ${pendingDecision.lot.lotNumber} ${pendingDecision.approved ? "released for use" : "rejected"}.`,
        ok: pendingDecision.approved
      });
      setPendingDecision(null);
      await loadData();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Release decision failed.", ok: false });
    }
  };

  const handlePrepSuccess = async (newLot: any) => {
    setMessage({ text: `Media lot ${newLot?.lotNumber ?? ""} successfully prepared.`, ok: true });
    await loadData();
    if (newLot?.id) {
      setSelectedLotId(newLot.id);
    }
  };

  return (
    <>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 1.5, mb: 1 }}>
        <PageHeader
          title="Media Preparation & Evaluation"
          subtitle="Prepare media lots and manage their evaluations (GPT, Sterility, Indication/Inhibition, Enrichment Characteristics)."
        />
        <Box sx={{ display: "flex", gap: 1, pt: 0.5 }}>
          <Button
            size="small"
            variant="outlined"
            startIcon={<RefreshIcon />}
            onClick={loadData}
            disabled={loading}
            sx={{ borderColor: "divider", color: "text.secondary" }}
          >
            Refresh
          </Button>
          <Button
            size="small"
            variant="contained"
            startIcon={<AddIcon />}
            onClick={() => setPrepDialogOpen(true)}
            sx={{ bgcolor: brandColors.sectionTitle, fontWeight: 700, "&:hover": { bgcolor: brandColors.pageTitle } }}
          >
            + Prepare New Media Lot
          </Button>
        </Box>
      </Box>

      {message && (
        <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }} onClose={() => setMessage(null)}>
          {message.text}
        </Alert>
      )}

      {/* KPI Cards */}
      <MediaLotKpiCards
        lots={lots}
        awaitingApprovalIds={awaitingApprovalIds}
        activeKpi={activeKpi}
        onSelectKpi={handleKpiSelect}
      />

      {/* Compact Filter Bar */}
      <MediaLotFilterBar
        search={search}
        onSearchChange={setSearch}
        selectedMediaTypeId={selectedMediaTypeId}
        onMediaTypeChange={setSelectedMediaTypeId}
        selectedStatus={selectedStatus}
        onStatusChange={handleStatusFilterChange}
        mediaTypes={mediaTypes}
        onResetFilters={handleResetFilters}
      />

      {/* Main Workspace Layout */}
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

            <Paper
              elevation={0}
              sx={{
                border: "1px solid",
                borderColor: "divider",
                borderRadius: 2,
                overflowY: "auto",
                maxHeight: { xs: "340px", md: "calc(100vh - 330px)" },
                bgcolor: "background.paper"
              }}
            >
              <MediaLotRegisterTable
                lots={visibleLots}
                awaitingApprovalIds={awaitingApprovalIds}
                selectedLotId={selectedLotId}
                onSelectLot={handleSelectLot}
                isCompact={true}
                onViewRecord={handleViewRecord}
                onViewAuditHistory={handleViewAuditHistory}
                onRequestReleaseDecision={(lot, approved) => setPendingDecision({ lot, approved })}
              />
            </Paper>
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
          <Paper sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, overflowX: "auto" }}>
            <MediaLotRegisterTable
              lots={visibleLots}
              awaitingApprovalIds={awaitingApprovalIds}
              selectedLotId={null}
              onSelectLot={handleSelectLot}
              isCompact={false}
              onViewRecord={handleViewRecord}
              onViewAuditHistory={handleViewAuditHistory}
              onRequestReleaseDecision={(lot, approved) => setPendingDecision({ lot, approved })}
            />
          </Paper>
        </>
      )}

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
        onUpdated={loadData}
      />

      <MarkOutOfStockDialog
        open={Boolean(outOfStockLot)}
        lot={outOfStockLot}
        onClose={() => setOutOfStockLot(null)}
        onSuccess={() => {
          setMessage({ text: `Media lot ${outOfStockLot?.lotNumber ?? ""} marked Out of Stock.`, ok: true });
          loadData();
        }}
      />

      <AuditHistoryDialog
        open={Boolean(auditLotId)}
        entityName="Media"
        entityId={auditLotId}
        onClose={() => setAuditLotId(null)}
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
