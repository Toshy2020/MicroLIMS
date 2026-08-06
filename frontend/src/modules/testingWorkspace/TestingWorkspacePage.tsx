import { useEffect, useMemo, useState } from "react";
import { Paper, Table, TableHead, TableRow, TableCell, TableBody, Typography } from "@mui/material";
import { SampleTableRow } from "./SampleTableRow";
import { SampleCardView } from "./SampleCardView";
import { SampleKanbanView } from "./SampleKanbanView";
import { WorkspaceStatTiles } from "./WorkspaceStatTiles";
import { WorkspaceToolbar, WorkspaceView, WORKSPACE_COLUMNS } from "./WorkspaceToolbar";
import { TestWorkflowDialogRouter } from "./FloatingDialogs";
import { SampleSummaryDialog } from "./SampleSummaryDialog";
import { WorkspaceService } from "./services/WorkspaceService";
import { SampleCard as SampleCardType, TestOrderSummary } from "./types/workspaceTypes";
import { PreparationDialog } from "../testPreparation/PreparationDialog";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { useAuth } from "../../contexts/AuthContext";

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
  const { userId } = useAuth();
  const [samples, setSamples] = useState<SampleCardType[] | null>(null);
  const [activeTest, setActiveTest] = useState<TestOrderSummary | null>(null);
  const [activeSample, setActiveSample] = useState<SampleCardType | null>(null);
  const [preparingSample, setPreparingSample] = useState<SampleCardType | null>(null);
  const [summarySampleId, setSummarySampleId] = useState<number | null>(null);

  const [search, setSearch] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [testStatusFilter, setTestStatusFilter] = useState("");
  const [scope, setScope] = useState<"all" | "mine">("all");
  const [view, setView] = useState<WorkspaceView>("table");
  const [visibleColumns, setVisibleColumns] = useState<Set<string>>(new Set(ALL_COLUMN_KEYS));

  const load = () => WorkspaceService.getActiveSamples().then(setSamples);

  useEffect(() => { load(); }, []);

  const visibleSamples = useMemo(() => {
    if (!samples) return samples;
    const q = search.trim().toLowerCase();
    return samples.filter((s) => {
      if (q &&
        !s.displayName.toLowerCase().includes(q) &&
        !s.referenceNumber.toLowerCase().includes(q) &&
        !(s.batchNumber ?? "").toLowerCase().includes(q) &&
        !s.controlNumber.toLowerCase().includes(q)) return false;

      const receivedDate = s.receivedAt.slice(0, 10); // YYYY-MM-DD, matches <input type="date">
      if (fromDate && receivedDate < fromDate) return false;
      if (toDate && receivedDate > toDate) return false;
      if (statusFilter && s.status !== statusFilter) return false;
      if (testStatusFilter && !s.assignedTests.some((t) => t.status === testStatusFilter)) return false;
      if (scope === "mine" && !s.assignedTests.some((t) => t.assignedAnalystId === userId)) return false;
      return true;
    });
  }, [samples, search, fromDate, toDate, statusFilter, testStatusFilter, scope, userId]);

  if (!samples || !visibleSamples) return <LoadingSpinner />;

  const handleTestClick = (test: TestOrderSummary, sample: SampleCardType) => {
    setActiveTest(test);
    setActiveSample(sample);
  };

  const handleClose = () => {
    setActiveTest(null);
    load(); // refresh statuses after a workflow dialog closes
  };

  const handlePreparationClose = () => {
    setPreparingSample(null);
    load(); // refresh so the row switches from "Needs Preparation" to its test chips
  };

  const handleSummaryClose = () => {
    setSummarySampleId(null);
    load(); // refresh so the badge reflects any review/approval decision just made
  };

  const colSpan = 2 + visibleColumns.size; // expand-icon column + Item/Reference + toggleable columns

  return (
    <>
      <PageHeader title="Testing Workspace" subtitle="Every active sample, one row each. Click a test to open its workflow." />

      <WorkspaceStatTiles samples={samples} />

      <WorkspaceToolbar
        search={search} onSearchChange={setSearch}
        fromDate={fromDate} onFromDateChange={setFromDate}
        toDate={toDate} onToDateChange={setToDate}
        statusFilter={statusFilter} onStatusFilterChange={setStatusFilter}
        testStatusFilter={testStatusFilter} onTestStatusFilterChange={setTestStatusFilter}
        scope={scope} onScopeChange={setScope}
        view={view} onViewChange={setView}
        visibleColumns={visibleColumns} onVisibleColumnsChange={setVisibleColumns}
        onExport={() => exportToCsv(visibleSamples)}
      />

      <SectionTitle>{`Active Samples (${visibleSamples.length})`}</SectionTitle>

      {view === "table" && (
        <Paper sx={{ overflowX: "auto" }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell />
                <TableCell>Item / Reference</TableCell>
                {visibleColumns.has("category") && <TableCell>Category</TableCell>}
                {visibleColumns.has("batch") && <TableCell>Batch Number</TableCell>}
                {visibleColumns.has("control") && <TableCell>Control Number</TableCell>}
                {visibleColumns.has("cause") && <TableCell>Cause of Testing</TableCell>}
                {visibleColumns.has("receivedAt") && <TableCell>Received At</TableCell>}
                {visibleColumns.has("tests") && <TableCell>Tests</TableCell>}
                {visibleColumns.has("assignedTo") && <TableCell>Assigned To</TableCell>}
                {visibleColumns.has("status") && <TableCell>Status</TableCell>}
              </TableRow>
            </TableHead>
            <TableBody>
              {visibleSamples.map((s) => (
                <SampleTableRow
                  key={s.sampleId}
                  sample={s}
                  visibleColumns={visibleColumns}
                  colSpan={colSpan}
                  onTestClick={handleTestClick}
                  onNeedsPreparationClick={() => setPreparingSample(s)}
                  onCorrected={load}
                  onLifecycleBadgeClick={setSummarySampleId}
                />
              ))}
            </TableBody>
          </Table>
          {visibleSamples.length === 0 && (
            <Typography sx={{ color: "#9ca3af", fontSize: 13, p: 2 }}>No samples match this filter.</Typography>
          )}
        </Paper>
      )}

      {view === "card" && (
        <SampleCardView
          samples={visibleSamples}
          onTestClick={handleTestClick}
          onNeedsPreparationClick={setPreparingSample}
          onLifecycleBadgeClick={setSummarySampleId}
        />
      )}

      {view === "kanban" && (
        <SampleKanbanView samples={visibleSamples} onCardClick={setSummarySampleId} />
      )}

      <TestWorkflowDialogRouter open={!!activeTest} test={activeTest} sample={activeSample} onClose={handleClose} />
      <PreparationDialog open={!!preparingSample} sample={preparingSample} onClose={handlePreparationClose} />
      <SampleSummaryDialog open={!!summarySampleId} sampleId={summarySampleId} onClose={handleSummaryClose} />
    </>
  );
}
