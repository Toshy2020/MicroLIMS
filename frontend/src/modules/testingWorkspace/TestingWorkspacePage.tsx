import { useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import {
  Box,
  Paper,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Typography,
  useTheme
} from "@mui/material";
import { SampleTableRow } from "./SampleTableRow";
import { SampleCardView } from "./SampleCardView";
import { SampleKanbanView } from "./SampleKanbanView";
import { WorkspaceStatTiles } from "./WorkspaceStatTiles";
import { WorkspaceToolbar, WorkspaceView, WORKSPACE_COLUMNS } from "./WorkspaceToolbar";
import { SelectedSampleTestingPanel } from "./SelectedSampleTestingPanel";
import { TestWorkflowDialogRouter } from "./FloatingDialogs";
import { SampleSummaryDialog } from "./SampleSummaryDialog";
import { PreparationDialog } from "../testPreparation/PreparationDialog";
import { AuditHistoryDialog } from "../../components/AuditHistoryDialog";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { WorkspaceService } from "./services/WorkspaceService";
import { SampleCard as SampleCardType, TestOrderSummary } from "./types/workspaceTypes";
import { useAuth } from "../../contexts/AuthContext";
import { brandColors } from "../../theme";

const ALL_COLUMN_KEYS = WORKSPACE_COLUMNS.map((c) => c.key);

function exportToCsv(samples: SampleCardType[]) {
  const headers = ["Reference", "Item", "Category", "Batch No", "Control No", "Cause of Testing", "Received At", "Status"];
  const rows = samples.map((s) => [
    s.referenceNumber, s.displayName, s.category, s.batchNumber ?? "", s.controlNumber,
    s.causeOfTesting, new Date(s.receivedAt).toLocaleString(), s.status
  ]);
  const csv = [headers, ...rows].map((r) => r.map((v) => `"${String(v).replace(/"/g, '""')}"`).join(",")).join("\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `testing-workspace-${new Date().toISOString().slice(0, 10)}.csv`;
  link.click();
  URL.revokeObjectURL(url);
}

export function TestingWorkspacePage() {
  const theme = useTheme();
  const { userId } = useAuth();
  const [searchParams] = useSearchParams();
  const [samples, setSamples] = useState<SampleCardType[] | null>(null);

  // Selected sample for split-pane view
  const [selectedSampleId, setSelectedSampleId] = useState<number | null>(null);

  // Active test for workflow dialog execution
  const [activeTest, setActiveTest] = useState<TestOrderSummary | null>(null);
  const [activeSample, setActiveSample] = useState<SampleCardType | null>(null);

  // Other modal dialogs
  const [preparingSample, setPreparingSample] = useState<SampleCardType | null>(null);
  const [summarySampleId, setSummarySampleId] = useState<number | null>(null);
  const [auditSampleId, setAuditSampleId] = useState<number | null>(null);

  // Filters and controls
  const [search, setSearch] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [testStatusFilter, setTestStatusFilter] = useState("");
  const [analystIdFilter, setAnalystIdFilter] = useState<number | null>(null);
  const [urgencyFilter, setUrgencyFilter] = useState<string>("");
  const [scope, setScope] = useState<"all" | "mine">("all");
  const [view, setView] = useState<WorkspaceView>("table");
  const [visibleColumns, setVisibleColumns] = useState<Set<string>>(new Set(ALL_COLUMN_KEYS));

  const processedDeepLinkKeyRef = useRef<string | null>(null);

  const load = () => WorkspaceService.getActiveSamples().then(setSamples);

  useEffect(() => {
    load();
  }, []);

  // Parse URL query parameters for deep-linking
  useEffect(() => {
    if (!samples) return;
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

    if (paramStatus) setStatusFilter(paramStatus);
    if (paramTestStatus) setTestStatusFilter(paramTestStatus);
    if (paramAnalystId) setAnalystIdFilter(Number(paramAnalystId));
    if (paramUrgency) setUrgencyFilter(paramUrgency);
    if (paramScope === "mine" || paramScope === "all") setScope(paramScope);
    if (paramView === "table" || paramView === "card" || paramView === "kanban") setView(paramView);
    if (paramSearch) setSearch(paramSearch);

    const currentDeepLinkKey = `${paramSampleId ?? ""}:${paramTestOrderId ?? ""}:${paramOpenSummary ?? ""}`;

    // Prevent reopening modals when samples are refreshed after completing a workflow
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
      for (const sample of samples) {
        const foundTest = sample.assignedTests.find((t) => t.testOrderId === tId);
        if (foundTest) {
          setSelectedSampleId(sample.sampleId);
          setActiveTest(foundTest);
          setActiveSample(sample);
          break;
        }
      }
    }
  }, [samples, searchParams]);

  const visibleSamples = useMemo(() => {
    if (!samples) return samples;
    const q = search.trim().toLowerCase();
    const now = Date.now();
    return samples.filter((s) => {
      if (
        q &&
        !s.displayName.toLowerCase().includes(q) &&
        !s.referenceNumber.toLowerCase().includes(q) &&
        !(s.batchNumber ?? "").toLowerCase().includes(q) &&
        !s.controlNumber.toLowerCase().includes(q)
      )
        return false;

      const receivedDate = s.receivedAt.slice(0, 10);
      if (fromDate && receivedDate < fromDate) return false;
      if (toDate && receivedDate > toDate) return false;
      if (statusFilter && s.status !== statusFilter) return false;
      if (testStatusFilter && !s.assignedTests.some((t) => t.status === testStatusFilter)) return false;
      if (analystIdFilter && !s.assignedTests.some((t) => t.assignedAnalystId === analystIdFilter)) return false;
      if (urgencyFilter === "overdue") {
        const isOverdue = (now - new Date(s.receivedAt).getTime()) > 24 * 3600 * 1000;
        if (!isOverdue) return false;
      }
      if (scope === "mine" && !s.assignedTests.some((t) => t.assignedAnalystId === userId)) return false;
      return true;
    });
  }, [samples, search, fromDate, toDate, statusFilter, testStatusFilter, analystIdFilter, urgencyFilter, scope, userId]);

  // Derive the currently selected sample object from latest loaded samples
  const selectedSample = useMemo(() => {
    if (!selectedSampleId || !samples) return null;
    return samples.find((s) => s.sampleId === selectedSampleId) || null;
  }, [selectedSampleId, samples]);

  if (!samples || !visibleSamples) return <LoadingSpinner />;

  const handleSelectSample = (sample: SampleCardType) => {
    setSelectedSampleId(sample.sampleId);
  };

  const handleDeselectSample = () => {
    setSelectedSampleId(null);
  };

  const handleTestClick = (test: TestOrderSummary, sample: SampleCardType) => {
    setActiveTest(test);
    setActiveSample(sample);
  };

  const handleCloseTestWorkflow = () => {
    setActiveTest(null);
    setActiveSample(null);
    load(); // Refresh statuses after workflow interaction
  };

  const handlePreparationClose = () => {
    setPreparingSample(null);
    load(); // Refresh after preparation
  };

  const handleSummaryClose = () => {
    setSummarySampleId(null);
    load(); // Refresh after summary review/approval
  };

  const colSpan = 2 + visibleColumns.size;

  return (
    <>
      <PageHeader
        title="Testing Workspace"
        subtitle="Manage and execute analytical workflows for active microbiology samples."
      />

      <WorkspaceStatTiles samples={samples} />

      <WorkspaceToolbar
        search={search}
        onSearchChange={setSearch}
        fromDate={fromDate}
        onFromDateChange={setFromDate}
        toDate={toDate}
        onToDateChange={setToDate}
        statusFilter={statusFilter}
        onStatusFilterChange={setStatusFilter}
        testStatusFilter={testStatusFilter}
        onTestStatusFilterChange={setTestStatusFilter}
        scope={scope}
        onScopeChange={setScope}
        view={view}
        onViewChange={setView}
        visibleColumns={visibleColumns}
        onVisibleColumnsChange={setVisibleColumns}
        onExport={() => exportToCsv(visibleSamples)}
      />

      {/* Main Workspace Layout */}
      {selectedSample ? (
        /* SPLIT-PANE LAYOUT: Left = Compact Sample Register, Right = Selected Sample Testing Panel */
        <Box
          sx={{
            display: "flex",
            flexDirection: { xs: "column", md: "row" },
            gap: 2,
            alignItems: "stretch",
            minHeight: "calc(100vh - 280px)"
          }}
        >
          {/* Left Panel: Compact Sample Register (approx 38% width on desktop) */}
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
                Active Samples ({visibleSamples.length})
              </Typography>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                Click a sample to view
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
                  {visibleSamples.map((s) => (
                    <SampleTableRow
                      key={s.sampleId}
                      sample={s}
                      isSelected={selectedSampleId === s.sampleId}
                      onSelectSample={handleSelectSample}
                      isCompact={true}
                      visibleColumns={visibleColumns}
                      colSpan={4}
                      onNeedsPreparationClick={() => setPreparingSample(s)}
                      onCorrected={load}
                      onLifecycleBadgeClick={setSummarySampleId}
                    />
                  ))}
                  {visibleSamples.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={4} align="center" sx={{ py: 3, color: "text.secondary", fontSize: 12 }}>
                        No matching samples.
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </Paper>
          </Box>

          {/* Right Panel: Selected Sample & Assigned Testing Workflow (approx 62% width on desktop) */}
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
              sample={selectedSample}
              onTestClick={handleTestClick}
              onClose={handleDeselectSample}
              onNeedsPreparationClick={(s) => setPreparingSample(s)}
              onLifecycleBadgeClick={setSummarySampleId}
              onCorrected={load}
              onViewAuditHistory={setAuditSampleId}
            />
          </Box>
        </Box>
      ) : (
        /* NORMAL STATE: Full-Width Sample Register */
        <>
          <SectionTitle>{`Active Samples (${visibleSamples.length})`}</SectionTitle>

          {view === "table" && (
            <Paper sx={{ overflowX: "auto" }}>
              <Table size="small">
                <TableHead>
                  <TableRow sx={{ bgcolor: "background.default" }}>
                    <TableCell sx={{ width: 36 }} />
                    <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Item / Reference</TableCell>
                    {visibleColumns.has("category") && <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Category</TableCell>}
                    {visibleColumns.has("batch") && <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Batch Number</TableCell>}
                    {visibleColumns.has("control") && <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Control Number</TableCell>}
                    {visibleColumns.has("cause") && <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Cause of Testing</TableCell>}
                    {visibleColumns.has("receivedAt") && <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Received At</TableCell>}
                    {visibleColumns.has("assignedTo") && <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Assigned To</TableCell>}
                    {visibleColumns.has("status") && <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Status</TableCell>}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {visibleSamples.map((s) => (
                    <SampleTableRow
                      key={s.sampleId}
                      sample={s}
                      isSelected={false}
                      onSelectSample={handleSelectSample}
                      isCompact={false}
                      visibleColumns={visibleColumns}
                      colSpan={colSpan}
                      onNeedsPreparationClick={() => setPreparingSample(s)}
                      onCorrected={load}
                      onLifecycleBadgeClick={setSummarySampleId}
                    />
                  ))}
                  {visibleSamples.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={colSpan} align="center" sx={{ py: 4, color: "text.secondary", fontSize: 13 }}>
                        No samples match this filter.
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </Paper>
          )}

          {view === "card" && (
            <SampleCardView
              samples={visibleSamples}
              selectedSampleId={selectedSampleId}
              onSelectSample={handleSelectSample}
              onNeedsPreparationClick={setPreparingSample}
              onLifecycleBadgeClick={setSummarySampleId}
            />
          )}

          {view === "kanban" && (
            <SampleKanbanView
              samples={visibleSamples}
              onCardClick={(sampleId) => {
                const s = samples.find((item) => item.sampleId === sampleId);
                if (s) setSelectedSampleId(s.sampleId);
                else setSummarySampleId(sampleId);
              }}
            />
          )}
        </>
      )}

      {/* Floating & Modal Dialogs */}
      <TestWorkflowDialogRouter
        open={Boolean(activeTest)}
        test={activeTest}
        sample={activeSample}
        onClose={handleCloseTestWorkflow}
      />

      <PreparationDialog
        open={Boolean(preparingSample)}
        sample={preparingSample}
        onClose={handlePreparationClose}
      />

      <SampleSummaryDialog
        open={Boolean(summarySampleId)}
        sampleId={summarySampleId}
        onClose={handleSummaryClose}
      />

      <AuditHistoryDialog
        open={Boolean(auditSampleId)}
        entityName="Sample"
        entityId={auditSampleId}
        onClose={() => setAuditSampleId(null)}
      />
    </>
  );
}
