import { useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import {
  Box,
  Button,
  Paper,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Typography,
  Alert,
  Snackbar,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";

import { PageHeader } from "../../components/PageHeader";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { AuditHistoryDialog } from "../../components/AuditHistoryDialog";
import { useAuth } from "../../contexts/AuthContext";
import { brandColors } from "../../theme";

// Receiving Components & Dialogs
import { SampleRecord, TestOrderSummary as ReceivingTestOrderSummary } from "../receiving/types/receivingTypes";
import { ReceiveService } from "../receiving/services/ReceiveService";
import { SampleStatusKpiCards, KpiFilterKey } from "../receiving/components/SampleStatusKpiCards";
import { SampleFilterBar } from "../receiving/components/SampleFilterBar";
import { SampleRegisterTable } from "../receiving/components/SampleRegisterTable";
import { NewSampleDialog } from "../receiving/dialogs/NewSampleDialog";
import { EditSampleDetailsDialog } from "../receiving/dialogs/EditSampleDetailsDialog";
import { AssignAnalystDialog } from "../receiving/dialogs/AssignAnalystDialog";

// Testing Workspace Components & Dialogs
import { SampleCard as WorkspaceSampleCard, TestOrderSummary as WorkspaceTestOrderSummary } from "../testingWorkspace/types/workspaceTypes";
import { WorkspaceService } from "../testingWorkspace/services/WorkspaceService";
import { SampleTableRow } from "../testingWorkspace/SampleTableRow";
import { SampleCardView } from "../testingWorkspace/SampleCardView";
import { SampleKanbanView } from "../testingWorkspace/SampleKanbanView";
import { SelectedSampleTestingPanel } from "../testingWorkspace/SelectedSampleTestingPanel";
import { TestWorkflowDialogRouter } from "../testingWorkspace/FloatingDialogs";
import { SampleSummaryDialog } from "../testingWorkspace/SampleSummaryDialog";
import { PreparationDialog } from "../testPreparation/PreparationDialog";
import { VoidSampleConfirmationDialog } from "../receiving/dialogs/VoidSampleConfirmationDialog";

export type OperationalTab = "all" | "mine" | "needsAction" | "completed";
export type WorkspaceDisplayView = "table" | "card" | "kanban";

function exportSamplesToCsv(samples: SampleRecord[]) {
  const headers = ["Sample ID", "Reference", "Item / Display Name", "Category", "Batch No", "Control No", "Cause of Testing", "Sampled By", "Received At", "Status"];
  const rows = samples.map((s) => [
    s.sampleId,
    s.referenceNumber,
    s.displayName,
    s.category,
    s.batchNumber ?? "",
    s.controlNumber,
    s.causeOfTesting,
    s.sampledBy ?? "",
    new Date(s.receivedAt).toLocaleString(),
    s.status
  ]);
  const csv = [headers, ...rows].map((r) => r.map((v) => `"${String(v).replace(/"/g, '""')}"`).join(",")).join("\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `receiving-testing-workspace-${new Date().toISOString().slice(0, 10)}.csv`;
  link.click();
  URL.revokeObjectURL(url);
}

export function ReceivingTestingWorkspacePage() {
  const theme = useTheme();
  const { userId, role } = useAuth();
  const [searchParams] = useSearchParams();

  // Core Data State
  const [records, setRecords] = useState<SampleRecord[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [notification, setNotification] = useState<{ text: string; severity: "success" | "error" | "info" } | null>(null);

  // Operational Tabs & Display View State
  const [activeTab, setActiveTab] = useState<OperationalTab>("all");
  const [viewMode, setViewMode] = useState<WorkspaceDisplayView>("table");

  // Selection for Master-Detail Split Pane
  const [selectedSampleId, setSelectedSampleId] = useState<number | null>(null);

  // Filter State
  const [activeKpi, setActiveKpi] = useState<KpiFilterKey | null>(null);
  const [search, setSearch] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("ALL");
  const [sampleStatusFilter, setSampleStatusFilter] = useState("ALL");
  const [testStatusFilter, setTestStatusFilter] = useState("ALL");
  const [analystIdFilter, setAnalystIdFilter] = useState<number | null>(null);
  const [urgencyFilter, setUrgencyFilter] = useState<string>("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");

  // Dialogs State
  const [newSampleDialogOpen, setNewSampleDialogOpen] = useState(false);
  const [editSample, setEditSample] = useState<SampleRecord | null>(null);
  const [assigningSample, setAssigningSample] = useState<SampleRecord | null>(null);
  const [summarySampleId, setSummarySampleId] = useState<number | null>(null);
  const [activeTest, setActiveTest] = useState<WorkspaceTestOrderSummary | null>(null);
  const [activeSampleForTest, setActiveSampleForTest] = useState<WorkspaceSampleCard | null>(null);
  const [preparingSample, setPreparingSample] = useState<SampleRecord | null>(null);
  const [auditSampleId, setAuditSampleId] = useState<number | null>(null);
  const [voidingSample, setVoidingSample] = useState<SampleRecord | null>(null);

  const processedDeepLinkKeyRef = useRef<string | null>(null);

  const loadRecords = async () => {
    setLoading(true);
    try {
      const data = await ReceiveService.getRecords();
      setRecords(data);
    } catch (err: any) {
      setNotification({
        text: err?.response?.data?.message || "Failed to load laboratory sample records.",
        severity: "error"
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadRecords();
  }, []);

  // Deep-Link URL Parameter Processing (Strict Backward Compatibility)
  useEffect(() => {
    if (!records) return;

    const paramSampleId = searchParams.get("sampleId");
    const paramTestOrderId = searchParams.get("testOrderId");
    const paramOpenSummary = searchParams.get("openSummary");
    const paramStatus = searchParams.get("status");
    const paramTestStatus = searchParams.get("testStatus");
    const paramAnalystId = searchParams.get("analystId");
    const paramUrgency = searchParams.get("urgency");
    const paramScope = searchParams.get("scope");
    const paramView = searchParams.get("view");
    const paramSearch = searchParams.get("search");

    if (paramStatus) {
      if (paramStatus === "Active") {
        setActiveTab("all");
        setSampleStatusFilter("ALL");
      } else {
        setSampleStatusFilter(paramStatus);
      }
    }
    if (paramTestStatus) setTestStatusFilter(paramTestStatus);
    if (paramAnalystId) setAnalystIdFilter(Number(paramAnalystId));
    if (paramUrgency) setUrgencyFilter(paramUrgency);
    if (paramScope === "mine") setActiveTab("mine");
    if (paramView === "table" || paramView === "card" || paramView === "kanban") setViewMode(paramView);
    if (paramSearch) setSearch(paramSearch);

    const currentDeepLinkKey = `${paramSampleId ?? ""}:${paramTestOrderId ?? ""}:${paramOpenSummary ?? ""}`;
    if (processedDeepLinkKeyRef.current === currentDeepLinkKey) {
      return;
    }
    processedDeepLinkKeyRef.current = currentDeepLinkKey;

    if (paramSampleId) {
      const sId = Number(paramSampleId);
      setSelectedSampleId(sId);
      if (paramOpenSummary === "true") {
        setSummarySampleId(sId);
      }
    }

    if (paramTestOrderId) {
      const tId = Number(paramTestOrderId);
      for (const sample of records) {
        const foundTest = sample.assignedTests?.find((t) => t.testOrderId === tId);
        if (foundTest) {
          setSelectedSampleId(sample.sampleId);
          setActiveTest(foundTest as unknown as WorkspaceTestOrderSummary);
          setActiveSampleForTest(sample as unknown as WorkspaceSampleCard);
          break;
        }
      }
    }
  }, [records, searchParams]);

  // Handle KPI Category Tile Selection
  const handleSelectKpi = (kpi: KpiFilterKey) => {
    if (activeKpi === kpi || kpi === "ALL") {
      setActiveKpi(null);
      setCategoryFilter("ALL");
    } else {
      setActiveKpi(kpi);
      const categoryMapping: Record<string, string> = {
        Product: "FinishedProduct",
        RM: "RawMaterial",
        PM: "PackagingMaterial",
        Water: "Water",
        Aftercleaning: "AfterCleaning",
        EM: "EnvironmentalMonitoring"
      };
      setCategoryFilter(categoryMapping[kpi] || "ALL");
    }
  };

  // Sync Category dropdown changes with KPI cards
  const handleCategoryFilterChange = (cat: string) => {
    setCategoryFilter(cat);
    if (cat === "ALL") {
      setActiveKpi(null);
    } else if (cat === "FinishedProduct" || cat === "Product") {
      setActiveKpi("Product");
    } else if (cat === "RawMaterial" || cat === "RM") {
      setActiveKpi("RM");
    } else if (cat === "PackagingMaterial" || cat === "PM") {
      setActiveKpi("PM");
    } else if (cat === "Water") {
      setActiveKpi("Water");
    } else if (cat === "AfterCleaning" || cat === "Aftercleaning" || cat === "AC") {
      setActiveKpi("Aftercleaning");
    } else if (cat === "EnvironmentalMonitoring" || cat === "EM") {
      setActiveKpi("EM");
    } else {
      setActiveKpi(null);
    }
  };

  // Sync Sample Status Filter changes
  const handleSampleStatusFilterChange = (status: string) => {
    setSampleStatusFilter(status);
  };

  const handleResetFilters = () => {
    setSearch("");
    setActiveKpi(null);
    setCategoryFilter("ALL");
    setSampleStatusFilter("ALL");
    setTestStatusFilter("ALL");
    setAnalystIdFilter(null);
    setUrgencyFilter("");
    setFromDate("");
    setToDate("");
  };

  const hasActiveFilters = Boolean(
    search ||
    activeKpi ||
    categoryFilter !== "ALL" ||
    sampleStatusFilter !== "ALL" ||
    testStatusFilter !== "ALL" ||
    analystIdFilter !== null ||
    urgencyFilter ||
    fromDate ||
    toDate
  );

  // Filtered Samples Computation
  const filteredRecords = useMemo(() => {
    if (!records) return [];
    const now = Date.now();
    const isSectionHeadOrAdmin = role === "SectionHead" || role === "SystemAdministrator";

    return records.filter((r) => {
      // 1. Operational Tab Filter
      if (activeTab === "mine") {
        const isAssigned =
          r.assignedAnalystId === userId ||
          r.assignedTests?.some((t) => t.assignedAnalystId === userId);
        if (!isAssigned) return false;
      } else if (activeTab === "needsAction") {
        const needsPrep = r.preparationStatus === "NeedsPreparation";
        const hasAwaitingReview =
          r.status === "UnderReview" ||
          r.assignedTests?.some((t) => t.status === "ResultEntered" || t.workflowStatus === "PendingReview");
        const hasReadyToRead = r.assignedTests?.some((t) => t.workflowStatus === "ReadyToRead" || t.workflowStatus === "EnterResult");
        const isUnassigned = isSectionHeadOrAdmin && !r.assignedAnalystId && !r.assignedTests?.some((t) => t.assignedAnalystId != null);

        if (!needsPrep && !hasAwaitingReview && !hasReadyToRead && !isUnassigned) {
          return false;
        }
      } else if (activeTab === "completed") {
        const isCompleted =
          r.status === "Approved" ||
          r.status === "Rejected" ||
          r.status === "RetestRequested" ||
          r.status === "Cancelled" ||
          r.status === "Voided";
        if (!isCompleted) return false;
      }

      // 2. Free Text Search
      if (search.trim()) {
        const q = search.trim().toLowerCase();
        const matches =
          String(r.sampleId).includes(q) ||
          r.displayName?.toLowerCase().includes(q) ||
          r.referenceNumber?.toLowerCase().includes(q) ||
          (r.batchNumber && r.batchNumber.toLowerCase().includes(q)) ||
          r.controlNumber?.toLowerCase().includes(q) ||
          r.causeOfTesting?.toLowerCase().includes(q) ||
          (r.sampledBy && r.sampledBy.toLowerCase().includes(q));

        if (!matches) return false;
      }

      // 3. Category / KPI Filter
      if (activeKpi) {
        if (activeKpi === "Product" && r.category !== "FinishedProduct" && r.category !== "Product") return false;
        if (activeKpi === "RM" && r.category !== "RawMaterial" && r.category !== "RM") return false;
        if (activeKpi === "PM" && r.category !== "PackagingMaterial" && r.category !== "PM") return false;
        if (activeKpi === "Water" && r.category !== "Water") return false;
        if (activeKpi === "Aftercleaning" && r.category !== "AfterCleaning" && r.category !== "Aftercleaning" && r.category !== "AC") return false;
        if (activeKpi === "EM" && r.category !== "EnvironmentalMonitoring" && r.category !== "EM") return false;
      } else if (categoryFilter !== "ALL" && r.category !== categoryFilter) {
        return false;
      }

      // 4. Sample Status Filter
      if (sampleStatusFilter !== "ALL") {
        if (sampleStatusFilter === "PendingReview") {
          if (r.status !== "UnderReview" && r.status !== "UnderApproval" && r.status !== "PendingReview") {
            return false;
          }
        } else if (sampleStatusFilter === "RetestRequested") {
          if (r.status !== "RetestRequested" && r.status !== "Cancelled" && r.status !== "Voided") {
            return false;
          }
        } else if (r.status !== sampleStatusFilter) {
          return false;
        }
      }

      // 6. Test Status Filter
      if (testStatusFilter !== "ALL") {
        const tests = r.assignedTests || [];
        if (testStatusFilter === "Waiting" || testStatusFilter === "Pending") {
          if (!tests.some((t) => t.status === "Waiting" || t.status === "NotStarted" || t.status === "Pending")) return false;
        } else if (testStatusFilter === "InProgress") {
          if (!tests.some((t) => t.status === "InProgress" || t.status === "Running" || t.status === "Incubating"))
            return false;
        } else if (testStatusFilter === "ReadyToRead") {
          if (!tests.some((t) => t.workflowStatus === "ReadyToRead" || t.workflowStatus === "EnterResult")) return false;
        } else if (testStatusFilter === "ResultEntered" || testStatusFilter === "UnderReview") {
          if (!tests.some((t) => t.status === "ResultEntered" || t.status === "UnderReview" || t.status === "Reviewed")) return false;
        } else {
          if (!tests.some((t) => t.status === testStatusFilter || t.workflowStatus === testStatusFilter)) return false;
        }
      }

      // 7. Analyst ID Filter
      if (analystIdFilter !== null) {
        const matchesAnalyst =
          r.assignedAnalystId === analystIdFilter ||
          r.assignedTests?.some((t) => t.assignedAnalystId === analystIdFilter);
        if (!matchesAnalyst) return false;
      }

      // 8. Urgency Filter
      if (urgencyFilter === "overdue") {
        const isOverdue = (now - new Date(r.receivedAt).getTime()) > 24 * 3600 * 1000;
        if (!isOverdue) return false;
      }

      // 9. Date Range Filters
      if (r.receivedAt) {
        const dateStr = r.receivedAt.slice(0, 10);
        if (fromDate && dateStr < fromDate) return false;
        if (toDate && dateStr > toDate) return false;
      }

      return true;
    });
  }, [
    records,
    activeTab,
    search,
    activeKpi,
    categoryFilter,
    sampleStatusFilter,
    testStatusFilter,
    analystIdFilter,
    urgencyFilter,
    fromDate,
    toDate,
    userId,
    role
  ]);

  // Derive Selected Sample Object
  const selectedSample = useMemo(() => {
    if (!selectedSampleId || !records) return null;
    return records.find((s) => s.sampleId === selectedSampleId) || null;
  }, [selectedSampleId, records]);

  // Event Handlers
  const handleSelectSample = (sample: SampleRecord | WorkspaceSampleCard) => {
    setSelectedSampleId(sample.sampleId);
  };

  const handleDeselectSample = () => {
    setSelectedSampleId(null);
  };

  const handleTestClick = (test: ReceivingTestOrderSummary | WorkspaceTestOrderSummary, sample: SampleRecord | WorkspaceSampleCard) => {
    setActiveTest(test as unknown as WorkspaceTestOrderSummary);
    setActiveSampleForTest(sample as unknown as WorkspaceSampleCard);
  };

  const handleViewSummary = (sample: SampleRecord | WorkspaceSampleCard) => {
    setSummarySampleId(sample.sampleId);
  };

  const handleEdit = (sample: SampleRecord) => {
    setEditSample(sample);
  };

  const handleViewReport = (sample: SampleRecord | WorkspaceSampleCard) => {
    window.open(`/samples/${sample.sampleId}/report`, "_blank");
  };

  const handleViewAuditHistory = (sample: SampleRecord | WorkspaceSampleCard) => {
    setAuditSampleId(sample.sampleId);
  };

  const handlePrepareSample = (sample: SampleRecord | WorkspaceSampleCard) => {
    setPreparingSample(sample as SampleRecord);
  };

  const handleReceiveSuccess = (count: number) => {
    setNotification({
      text: `Successfully received ${count} sample${count > 1 ? "s" : ""}. Test orders generated automatically.`,
      severity: "success"
    });
    loadRecords();
  };

  const handleEditSuccess = () => {
    setNotification({
      text: "Sample information updated successfully.",
      severity: "success"
    });
    loadRecords();
  };

  return (
    <>
      {/* Header Section */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2, flexWrap: "wrap", gap: 1.5 }}>
        <PageHeader
          title="Receiving & Testing Workspace"
          subtitle="Manage incoming samples, assignments, testing progress, review, and laboratory workflow execution from one workspace."
        />

        <Box sx={{ display: "flex", gap: 1.5, mt: 0.5 }}>
          <Button
            variant="outlined"
            size="medium"
            onClick={loadRecords}
            disabled={loading}
            startIcon={<RefreshIcon />}
            sx={{
              borderColor: "divider",
              color: "text.secondary",
              fontWeight: 600,
              bgcolor: "background.paper",
              "&:hover": { bgcolor: "background.default", borderColor: "text.secondary" }
            }}
          >
            Refresh
          </Button>

          <Button
            variant="contained"
            size="medium"
            onClick={() => setNewSampleDialogOpen(true)}
            startIcon={<AddIcon />}
            sx={{
              bgcolor: brandColors.sectionTitle,
              fontWeight: 600,
              px: 2.5,
              "&:hover": { bgcolor: "#631f74" }
            }}
          >
            + New Sample
          </Button>
        </Box>
      </Box>

      {/* Unified KPI Status Cards */}
      <SampleStatusKpiCards
        samples={records || []}
        activeKpi={activeKpi}
        onSelectKpi={handleSelectKpi}
      />

      {/* Unified Filter Bar - search, display-mode toggle, and export all live here now (the separate All/Mine/Needs Action/Completed tab strip above it was dropped, its counts overlapped this bar's own Sample Status filter) */}
      <SampleFilterBar
        search={search}
        onSearchChange={setSearch}
        categoryFilter={categoryFilter}
        onCategoryFilterChange={handleCategoryFilterChange}
        sampleStatusFilter={sampleStatusFilter}
        onSampleStatusFilterChange={handleSampleStatusFilterChange}
        testStatusFilter={testStatusFilter}
        onTestStatusFilterChange={setTestStatusFilter}
        fromDate={fromDate}
        onFromDateChange={setFromDate}
        toDate={toDate}
        onToDateChange={setToDate}
        onResetFilters={handleResetFilters}
        hasActiveFilters={hasActiveFilters}
        viewMode={viewMode}
        onViewModeChange={setViewMode}
        onExport={() => exportSamplesToCsv(filteredRecords)}
      />

      {/* Loading State */}
      {!records || loading ? (
        <LoadingSpinner />
      ) : selectedSample ? (
        /* MASTER-DETAIL SPLIT-PANE LAYOUT (When a sample is selected) */
        <Box
          sx={{
            display: "flex",
            flexDirection: { xs: "column", md: "row" },
            gap: 2,
            alignItems: "stretch",
            minHeight: "calc(100vh - 280px)"
          }}
        >
          {/* Left Panel: Compact Sample Register */}
          <Box
            sx={{
              width: { xs: "100%", md: "38%" },
              display: "flex",
              flexDirection: "column",
              gap: 1.5,
              flexShrink: 0
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main }}>
                Laboratory Register ({filteredRecords.length})
              </Typography>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                Click a sample to switch
              </Typography>
            </Box>

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
              <Table size="small" stickyHeader>
                <TableHead>
                  <TableRow sx={{ "& th": { bgcolor: "background.default", fontWeight: 700, fontSize: 11, py: 1 } }}>
                    <TableCell>Item / Reference</TableCell>
                    <TableCell sx={{ width: 65 }}>Type</TableCell>
                    <TableCell sx={{ width: 95 }}>Batch/Ctrl</TableCell>
                    <TableCell sx={{ width: 85 }}>Status</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {filteredRecords.map((s) => (
                    <SampleTableRow
                      key={s.sampleId}
                      sample={s as unknown as WorkspaceSampleCard}
                      isSelected={selectedSampleId === s.sampleId}
                      onSelectSample={(sample) => handleSelectSample(sample)}
                      isCompact={true}
                      visibleColumns={new Set(["category", "batch", "control", "status"])}
                      colSpan={4}
                      onNeedsPreparationClick={() => handlePrepareSample(s)}
                      onCorrected={loadRecords}
                      onLifecycleBadgeClick={setSummarySampleId}
                    />
                  ))}
                  {filteredRecords.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={4} align="center" sx={{ py: 3, color: "text.secondary", fontSize: 12 }}>
                        No matching samples found.
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </Paper>
          </Box>

          {/* Right Panel: Selected Sample & Analytical Workflows Panel */}
          <Box
            sx={{
              flex: 1,
              minWidth: 0,
              display: "flex",
              flexDirection: "column",
              maxHeight: { xs: "auto", md: "calc(100vh - 290px)" }
            }}
          >
            <SelectedSampleTestingPanel
              sample={selectedSample as unknown as WorkspaceSampleCard}
              onTestClick={(test, sample) => handleTestClick(test, sample)}
              onClose={handleDeselectSample}
              onNeedsPreparationClick={(sample) => handlePrepareSample(sample)}
              onLifecycleBadgeClick={setSummarySampleId}
              onCorrected={loadRecords}
              onViewAuditHistory={(sampleId) => setAuditSampleId(sampleId)}
              onVoid={(sample) => setVoidingSample(sample as unknown as SampleRecord)}
            />
          </Box>
        </Box>
      ) : (
        /* FULL-WIDTH WORKSPACE REGISTER (When no sample is selected) */
        <>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
            <Typography sx={{ fontSize: 16, fontWeight: 700, color: theme.palette.primary.main }}>
              Laboratory Register {records ? `(${filteredRecords.length}${filteredRecords.length !== records.length ? ` of ${records.length}` : ""})` : ""}
            </Typography>
          </Box>

          {viewMode === "table" && (
            <SampleRegisterTable
              samples={filteredRecords}
              selectedSampleId={selectedSampleId}
              onSelectSample={handleSelectSample}
              onTestClick={(test, sample) => {
                setSelectedSampleId(sample.sampleId);
                handleTestClick(test, sample);
              }}
              onViewSummary={(sample) => {
                setSelectedSampleId(sample.sampleId);
                handleViewSummary(sample);
              }}
              onEdit={handleEdit}
              onViewReport={handleViewReport}
              onViewAuditHistory={handleViewAuditHistory}
              onPrepareSample={handlePrepareSample}
              onAssignAnalyst={(s) => setAssigningSample(s)}
              onVoid={(sample) => setVoidingSample(sample)}
            />
          )}

          {viewMode === "card" && (
            <SampleCardView
              samples={filteredRecords as unknown as WorkspaceSampleCard[]}
              selectedSampleId={selectedSampleId}
              onSelectSample={(s) => setSelectedSampleId(s.sampleId)}
              onNeedsPreparationClick={(s) => handlePrepareSample(s)}
              onLifecycleBadgeClick={setSummarySampleId}
            />
          )}

          {viewMode === "kanban" && (
            <SampleKanbanView
              samples={filteredRecords as unknown as WorkspaceSampleCard[]}
              onCardClick={(sampleId) => {
                setSelectedSampleId(sampleId);
              }}
            />
          )}
        </>
      )}

      {/* Dialogs Ecosystem */}

      {/* 1. New Sample Wizard Dialog */}
      <NewSampleDialog
        open={newSampleDialogOpen}
        onClose={() => setNewSampleDialogOpen(false)}
        onSuccess={handleReceiveSuccess}
      />

      {/* 2. Edit Batch / Control Number Dialog */}
      <EditSampleDetailsDialog
        open={Boolean(editSample)}
        sample={editSample}
        onClose={() => setEditSample(null)}
        onSuccess={handleEditSuccess}
      />

      {/* 3. Assign Analyst Dialog (Section Head & Admin Only) */}
      <AssignAnalystDialog
        open={Boolean(assigningSample)}
        sample={assigningSample}
        onClose={() => setAssigningSample(null)}
        onAssigned={() => {
          setNotification({ text: "Analyst assignment updated successfully.", severity: "success" });
          loadRecords();
        }}
      />

      {/* 4. Detailed Sample Summary & Electronic Signature Dialog */}
      <SampleSummaryDialog
        open={Boolean(summarySampleId)}
        sampleId={summarySampleId}
        onClose={() => {
          setSummarySampleId(null);
          loadRecords();
        }}
      />

      {/* 5. Test Workflow Execution Router Dialog */}
      <TestWorkflowDialogRouter
        open={Boolean(activeTest)}
        test={activeTest}
        sample={activeSampleForTest}
        onClose={() => {
          setActiveTest(null);
          setActiveSampleForTest(null);
          loadRecords();
        }}
      />

      {/* 6. Preparation Dialog (Water, EM, After Cleaning) */}
      <PreparationDialog
        open={Boolean(preparingSample)}
        sample={preparingSample ? {
          sampleId: preparingSample.sampleId,
          category: preparingSample.category,
          departmentId: preparingSample.departmentId,
          machineId: preparingSample.machineId,
          waterDepartmentId: preparingSample.waterDepartmentId,
          assignedAnalystId: preparingSample.assignedAnalystId || preparingSample.assignedTests?.find((t) => t.assignedAnalystId != null)?.assignedAnalystId,
          assignedAnalystName: preparingSample.assignedAnalystName || preparingSample.assignedTests?.find((t) => t.assignedAnalystName)?.assignedAnalystName
        } : null}
        onClose={() => {
          setPreparingSample(null);
          loadRecords();
        }}
      />

      {/* 7. Change History Audit Dialog */}
      <AuditHistoryDialog
        open={Boolean(auditSampleId)}
        entityName="Sample"
        entityId={auditSampleId}
        onClose={() => setAuditSampleId(null)}
      />

      {/* 8. Void Sample Confirmation Dialog */}
      <VoidSampleConfirmationDialog
        open={Boolean(voidingSample)}
        sample={voidingSample}
        onClose={() => setVoidingSample(null)}
        onSuccess={() => {
          setNotification({
            text: `Sample #${voidingSample?.sampleId} (${voidingSample?.displayName}) has been marked as Voided.`,
            severity: "success"
          });
          setVoidingSample(null);
          loadRecords();
        }}
      />

      {/* Toast Notification Snackbar */}
      <Snackbar
        open={Boolean(notification)}
        autoHideDuration={5000}
        onClose={() => setNotification(null)}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
      >
        {notification ? (
          <Alert
            severity={notification.severity}
            onClose={() => setNotification(null)}
            sx={{ borderRadius: 1.5 }}
          >
            {notification.text}
          </Alert>
        ) : undefined}
      </Snackbar>
    </>
  );
}
