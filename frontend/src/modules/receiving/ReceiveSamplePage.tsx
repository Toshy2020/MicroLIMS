import { useEffect, useMemo, useState } from "react";
import { Box, Button, Alert, Snackbar, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";
import { PageHeader } from "../../components/PageHeader";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { AuditHistoryDialog } from "../../components/AuditHistoryDialog";
import { SampleSummaryDialog } from "../testingWorkspace/SampleSummaryDialog";
import { TestWorkflowDialogRouter } from "../testingWorkspace/FloatingDialogs";
import { PreparationDialog } from "../testPreparation/PreparationDialog";
import { SampleRecord, TestOrderSummary } from "./types/receivingTypes";
import { ReceiveService } from "./services/ReceiveService";
import { SampleStatusKpiCards, KpiFilterKey } from "./components/SampleStatusKpiCards";
import { SampleFilterBar } from "./components/SampleFilterBar";
import { SampleRegisterTable } from "./components/SampleRegisterTable";
import { NewSampleDialog } from "./dialogs/NewSampleDialog";
import { EditSampleDetailsDialog } from "./dialogs/EditSampleDetailsDialog";
import { brandColors } from "../../theme";

export function ReceiveSamplePage() {
  const [records, setRecords] = useState<SampleRecord[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [notification, setNotification] = useState<{ text: string; severity: "success" | "error" | "info" } | null>(null);

  // KPI & Filter State
  const [activeKpi, setActiveKpi] = useState<KpiFilterKey | null>(null);
  const [search, setSearch] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("ALL");
  const [sampleStatusFilter, setSampleStatusFilter] = useState("ALL");
  const [testStatusFilter, setTestStatusFilter] = useState("ALL");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");

  // Dialogs State
  const [newSampleDialogOpen, setNewSampleDialogOpen] = useState(false);
  const [editSample, setEditSample] = useState<SampleRecord | null>(null);
  const [summarySampleId, setSummarySampleId] = useState<number | null>(null);
  const [activeTest, setActiveTest] = useState<TestOrderSummary | null>(null);
  const [activeSampleForTest, setActiveSampleForTest] = useState<any | null>(null);
  const [preparingSample, setPreparingSample] = useState<SampleRecord | null>(null);
  const [auditSampleId, setAuditSampleId] = useState<number | null>(null);

  const loadRecords = async () => {
    setLoading(true);
    try {
      const data = await ReceiveService.getRecords();
      setRecords(data);
    } catch (err: any) {
      setNotification({
        text: err?.response?.data?.message || "Failed to load sample records.",
        severity: "error"
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadRecords();
  }, []);

  // Handle KPI Click Filter
  const handleSelectKpi = (kpi: KpiFilterKey) => {
    if (kpi === "ALL" || activeKpi === kpi) {
      setActiveKpi(null);
      setSampleStatusFilter("ALL");
    } else {
      setActiveKpi(kpi);
      setSampleStatusFilter(kpi);
    }
  };

  // Sync Sample Status Filter changes with KPI cards
  const handleSampleStatusFilterChange = (status: string) => {
    setSampleStatusFilter(status);
    if (status === "ALL") {
      setActiveKpi(null);
    } else if (
      status === "InTesting" ||
      status === "PendingReview" ||
      status === "Approved" ||
      status === "Rejected" ||
      status === "RetestRequested"
    ) {
      setActiveKpi(status === "RetestRequested" ? "CancelledVoided" : (status as KpiFilterKey));
    } else {
      setActiveKpi(null);
    }
  };

  const handleResetFilters = () => {
    setSearch("");
    setActiveKpi(null);
    setCategoryFilter("ALL");
    setSampleStatusFilter("ALL");
    setTestStatusFilter("ALL");
    setFromDate("");
    setToDate("");
  };

  const hasActiveFilters = Boolean(
    search ||
    activeKpi ||
    categoryFilter !== "ALL" ||
    sampleStatusFilter !== "ALL" ||
    testStatusFilter !== "ALL" ||
    fromDate ||
    toDate
  );

  // Filtered Samples Computation
  const filteredRecords = useMemo(() => {
    if (!records) return [];

    return records.filter((r) => {
      // Free Text Search
      if (search.trim()) {
        const q = search.trim().toLowerCase();
        const matches =
          r.displayName?.toLowerCase().includes(q) ||
          r.referenceNumber?.toLowerCase().includes(q) ||
          (r.batchNumber && r.batchNumber.toLowerCase().includes(q)) ||
          r.controlNumber?.toLowerCase().includes(q) ||
          r.causeOfTesting?.toLowerCase().includes(q) ||
          (r.sampledBy && r.sampledBy.toLowerCase().includes(q));

        if (!matches) return false;
      }

      // KPI Status Filter
      if (activeKpi && activeKpi !== "ALL") {
        if (activeKpi === "InTesting" && r.status !== "InTesting") return false;
        if (
          activeKpi === "PendingReview" &&
          r.status !== "UnderReview" &&
          r.status !== "UnderApproval" &&
          r.status !== "PendingReview"
        )
          return false;
        if (activeKpi === "Approved" && r.status !== "Approved") return false;
        if (activeKpi === "Rejected" && r.status !== "Rejected") return false;
        if (
          activeKpi === "CancelledVoided" &&
          r.status !== "RetestRequested" &&
          r.status !== "Cancelled" &&
          r.status !== "Voided"
        )
          return false;
      }

      // Category / Item Type Filter
      if (categoryFilter !== "ALL" && r.category !== categoryFilter) {
        return false;
      }

      // Sample Status Filter (if not already handled by KPI)
      if (sampleStatusFilter !== "ALL" && !activeKpi) {
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

      // Test Status Filter
      if (testStatusFilter !== "ALL") {
        const tests = r.assignedTests || [];
        if (testStatusFilter === "Waiting") {
          if (!tests.some((t) => t.status === "Waiting" || t.status === "NotStarted")) return false;
        } else if (testStatusFilter === "InProgress") {
          if (!tests.some((t) => t.status === "InProgress" || t.status === "Running" || t.status === "Incubating"))
            return false;
        } else if (testStatusFilter === "UnderReview") {
          if (!tests.some((t) => t.status === "UnderReview" || t.status === "Reviewed")) return false;
        } else {
          if (!tests.some((t) => t.status === testStatusFilter)) return false;
        }
      }

      // Date Range Filters
      if (r.receivedAt) {
        const dateStr = r.receivedAt.slice(0, 10);
        if (fromDate && dateStr < fromDate) return false;
        if (toDate && dateStr > toDate) return false;
      }

      return true;
    });
  }, [records, search, activeKpi, categoryFilter, sampleStatusFilter, testStatusFilter, fromDate, toDate]);

  // Actions
  const handleTestClick = (test: TestOrderSummary, sample: SampleRecord) => {
    setActiveTest(test);
    setActiveSampleForTest({
      sampleId: sample.sampleId,
      category: sample.category,
      displayName: sample.displayName
    });
  };

  const handleViewSummary = (sample: SampleRecord) => {
    setSummarySampleId(sample.sampleId);
  };

  const handleEdit = (sample: SampleRecord) => {
    setEditSample(sample);
  };

  const handleViewReport = (sample: SampleRecord) => {
    window.open(`/samples/${sample.sampleId}/report`, "_blank");
  };

  const handleViewAuditHistory = (sample: SampleRecord) => {
    setAuditSampleId(sample.sampleId);
  };

  const handlePrepareSample = (sample: SampleRecord) => {
    setPreparingSample(sample);
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
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2, flexWrap: "wrap", gap: 1.5 }}>
        <PageHeader
          title="Sample Receiving"
          subtitle="Log incoming samples and track their testing status."
        />

        <Box sx={{ display: "flex", gap: 1.5, mt: 0.5 }}>
          <Button
            variant="outlined"
            size="medium"
            onClick={loadRecords}
            disabled={loading}
            startIcon={<RefreshIcon />}
            sx={{
              borderColor: "#d1d5db",
              color: "#374151",
              fontWeight: 600,
              bgcolor: "#ffffff",
              "&:hover": { bgcolor: "#f9fafb", borderColor: "#9ca3af" }
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

      {/* KPI Cards */}
      <SampleStatusKpiCards
        samples={records || []}
        activeKpi={activeKpi}
        onSelectKpi={handleSelectKpi}
      />

      {/* Filter Panel */}
      <SampleFilterBar
        search={search}
        onSearchChange={setSearch}
        categoryFilter={categoryFilter}
        onCategoryFilterChange={setCategoryFilter}
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
      />

      {/* Records Count & Table Title */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
        <Typography sx={{ fontSize: 16, fontWeight: 700, color: brandColors.pageTitle }}>
          Received Samples Register {records ? `(${filteredRecords.length}${filteredRecords.length !== records.length ? ` of ${records.length}` : ""})` : ""}
        </Typography>
      </Box>

      {/* Sample Register Table */}
      {!records || loading ? (
        <LoadingSpinner />
      ) : (
        <SampleRegisterTable
          samples={filteredRecords}
          onTestClick={handleTestClick}
          onViewSummary={handleViewSummary}
          onEdit={handleEdit}
          onViewReport={handleViewReport}
          onViewAuditHistory={handleViewAuditHistory}
          onPrepareSample={handlePrepareSample}
        />
      )}

      {/* New Sample Dialog (2 Steps) */}
      <NewSampleDialog
        open={newSampleDialogOpen}
        onClose={() => setNewSampleDialogOpen(false)}
        onSuccess={handleReceiveSuccess}
      />

      {/* Edit Sample Details Dialog */}
      <EditSampleDetailsDialog
        open={Boolean(editSample)}
        sample={editSample}
        onClose={() => setEditSample(null)}
        onSuccess={handleEditSuccess}
      />

      {/* Detailed Sample Summary Dialog */}
      <SampleSummaryDialog
        open={Boolean(summarySampleId)}
        sampleId={summarySampleId}
        onClose={() => {
          setSummarySampleId(null);
          loadRecords();
        }}
      />

      {/* Test Workflow Router Dialog */}
      <TestWorkflowDialogRouter
        open={Boolean(activeTest)}
        test={activeTest as any}
        sample={activeSampleForTest}
        onClose={() => {
          setActiveTest(null);
          setActiveSampleForTest(null);
          loadRecords();
        }}
      />

      {/* Preparation Dialog (for EM / After Cleaning / Needs Preparation) */}
      <PreparationDialog
        open={Boolean(preparingSample)}
        sample={preparingSample ? {
          sampleId: preparingSample.sampleId,
          category: preparingSample.category,
          departmentId: preparingSample.departmentId,
          machineId: preparingSample.machineId,
          waterDepartmentId: preparingSample.waterDepartmentId
        } : null}
        onClose={() => {
          setPreparingSample(null);
          loadRecords();
        }}
      />

      {/* Change History Audit Dialog */}
      <AuditHistoryDialog
        open={Boolean(auditSampleId)}
        entityName="Sample"
        entityId={auditSampleId}
        onClose={() => setAuditSampleId(null)}
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
            sx={{ boxShadow: "0 4px 12px rgba(0,0,0,0.15)", borderRadius: 1.5 }}
          >
            {notification.text}
          </Alert>
        ) : undefined}
      </Snackbar>
    </>
  );
}
