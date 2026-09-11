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
  TablePagination,
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
import { tableHeadSx } from "../../theme";

// Receiving Components & Dialogs
import { SampleRecord, TestOrderSummary as ReceivingTestOrderSummary } from "../receiving/types/receivingTypes";
import { ReceiveService, TestingWorkspaceFilter, WorkspaceTileCounts } from "../receiving/services/ReceiveService";
import {
  SampleStatusKpiCards,
  WorkloadFilterKey
} from "../receiving/components/SampleStatusKpiCards";
import { SelectionSummaryBar } from "../receiving/components/SelectionSummaryBar";
import { SampleFilterBar } from "../receiving/components/SampleFilterBar";
import { SampleRegisterTable } from "../receiving/components/SampleRegisterTable";
import { NewSampleDialog } from "../receiving/dialogs/NewSampleDialog";
import { EditSampleDetailsDialog } from "../receiving/dialogs/EditSampleDetailsDialog";
import { AssignAnalystDialog } from "../receiving/dialogs/AssignAnalystDialog";

// Testing Workspace Components & Dialogs
import { SampleCard as WorkspaceSampleCard, TestOrderSummary as WorkspaceTestOrderSummary } from "../testingWorkspace/types/workspaceTypes";
import { SampleTableRow } from "../testingWorkspace/SampleTableRow";
import { SampleCardView } from "../testingWorkspace/SampleCardView";
import { SampleKanbanView } from "../testingWorkspace/SampleKanbanView";
import { SelectedSampleTestingPanel } from "../testingWorkspace/SelectedSampleTestingPanel";
import { GroupedActionsPanel } from "../testingWorkspace/components/GroupedActionsPanel";
import { TestWorkflowDialogRouter } from "../testingWorkspace/FloatingDialogs";
import { SampleSummaryDialog } from "../testingWorkspace/SampleSummaryDialog";
import { PreparationDialog } from "../testPreparation/PreparationDialog";
import { VoidSampleConfirmationDialog } from "../receiving/dialogs/VoidSampleConfirmationDialog";

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
  const { role } = useAuth();
  const [searchParams] = useSearchParams();

  // Core Data State
  const [records, setRecords] = useState<SampleRecord[] | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(() => {
    const p = searchParams.get("page");
    return p ? Math.max(1, parseInt(p, 10)) : 1;
  });
  const [pageSize, setPageSize] = useState(() => {
    const ps = searchParams.get("pageSize");
    return ps ? Math.min(200, Math.max(1, parseInt(ps, 10))) : 50;
  });
  const [workloadCounts, setWorkloadCounts] = useState<WorkspaceTileCounts | null>(null);
  const [loading, setLoading] = useState(false);
  const [notification, setNotification] = useState<{ text: string; severity: "success" | "error" | "info" | "warning" } | null>(null);

  // Display View State
  const [viewMode, setViewMode] = useState<WorkspaceDisplayView>(() => {
    const v = searchParams.get("view");
    return v === "table" || v === "card" || v === "kanban" ? v : "table";
  });

  // Selection for Master-Detail Split Pane
  const [selectedSampleId, setSelectedSampleId] = useState<number | null>(() => {
    const s = searchParams.get("sampleId");
    return s ? Number(s) : null;
  });
  const [extraSelectedSample, setExtraSelectedSample] = useState<SampleRecord | null>(null);

  // Multi-select Sample Checkboxes for Grouped Actions
  const [checkedSampleIds, setCheckedSampleIds] = useState<Set<number>>(() => {
    const paramSampleIds = searchParams.get("sampleIds");
    if (paramSampleIds) {
      const ids = paramSampleIds.split(",").map(Number).filter((id) => !isNaN(id) && id > 0);
      if (ids.length >= 2) return new Set(ids);
    }
    return new Set();
  });
  const checkedSamplesCache = useRef<Map<number, SampleRecord>>(new Map());

  // Reveals every checked sample regardless of the active filters, so nobody
  // signs a grouped action over a set they cannot see in full.
  const [showSelectedOnly, setShowSelectedOnly] = useState(false);

  const handleToggleCheckSample = (sampleId: number, checked: boolean) => {
    setCheckedSampleIds((prev) => {
      const next = new Set(prev);
      if (checked) {
        next.add(sampleId);
        const s = records?.find((r) => r.sampleId === sampleId) ||
          (extraSelectedSample?.sampleId === sampleId ? extraSelectedSample : null);
        if (s) {
          checkedSamplesCache.current.set(sampleId, s);
        } else {
          ReceiveService.getSample(sampleId).then((fetched) => {
            if (fetched) checkedSamplesCache.current.set(sampleId, fetched);
          }).catch(() => {});
        }
      } else {
        next.delete(sampleId);
        checkedSamplesCache.current.delete(sampleId);
      }
      return next;
    });
  };

  // Select/deselect every row the register is currently showing on this page.
  const handleToggleCheckMany = (sampleIds: number[], checked: boolean) => {
    setCheckedSampleIds((prev) => {
      const next = new Set(prev);
      for (const id of sampleIds) {
        if (checked) {
          next.add(id);
          const s = records?.find((r) => r.sampleId === id) ||
            (extraSelectedSample?.sampleId === id ? extraSelectedSample : null);
          if (s) checkedSamplesCache.current.set(id, s);
        } else {
          next.delete(id);
          checkedSamplesCache.current.delete(id);
        }
      }
      return next;
    });
  };

  const handleDeselectAllChecked = () => {
    setCheckedSampleIds(new Set());
    checkedSamplesCache.current.clear();
    setShowSelectedOnly(false);
  };

  // Filter State initialized from URL query params
  const [workloadFilter, setWorkloadFilter] = useState<WorkloadFilterKey | null>(() => {
    const status = searchParams.get("status");
    if (status === "Active") return null;
    const scope = searchParams.get("scope");
    if (scope === "mine") return "mine";
    return null;
  });

  const [search, setSearch] = useState(() => searchParams.get("search") || "");
  const [debouncedSearch, setDebouncedSearch] = useState(() => searchParams.get("search") || "");
  const [categoryFilter, setCategoryFilter] = useState(() => searchParams.get("category") || "ALL");
  const [sampleStatusFilter, setSampleStatusFilter] = useState(() => {
    const status = searchParams.get("status");
    if (status && status !== "Active") return status;
    return searchParams.get("sampleStatus") || "ALL";
  });
  const [testStatusFilter, setTestStatusFilter] = useState(() => searchParams.get("testStatus") || "ALL");
  const [analystIdFilter, setAnalystIdFilter] = useState<number | null>(() => {
    const a = searchParams.get("analystId");
    return a ? Number(a) : null;
  });
  const [urgencyFilter, setUrgencyFilter] = useState<string>(() => searchParams.get("urgency") || "");
  const [fromDate, setFromDate] = useState(() => searchParams.get("fromDate") || "");
  const [toDate, setToDate] = useState(() => searchParams.get("toDate") || "");

  // Debounce search input by 300 ms while keeping input responsive
  useEffect(() => {
    if (search === debouncedSearch) return;
    const timer = setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [search, debouncedSearch]);

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

  const filterRef = useRef<TestingWorkspaceFilter>({});
  filterRef.current = {
    page,
    pageSize,
    search: debouncedSearch,
    category: categoryFilter,
    sampleStatus: sampleStatusFilter,
    testStatus: testStatusFilter,
    analystId: analystIdFilter,
    urgency: urgencyFilter,
    fromDate,
    toDate,
    workloadFilter
  };

  const loadRecords = async (silent = false) => {
    if (!silent) setLoading(true);
    try {
      const currentFilter = filterRef.current;
      const [pagedData, countsData] = await Promise.all([
        ReceiveService.getRecordsPaged(currentFilter),
        ReceiveService.getWorkloadCounts()
      ]);
      setRecords(pagedData.items);
      setTotalCount(pagedData.totalCount);
      setWorkloadCounts(countsData);

      // If a sample is selected, refresh its details if present in the reloaded page
      if (selectedSampleId) {
        const matching = pagedData.items.find((s) => s.sampleId === selectedSampleId);
        if (matching) {
          setExtraSelectedSample(matching);
        }
      }
    } catch (err: any) {
      setNotification({
        text: err?.response?.data?.message || "Failed to load laboratory sample records.",
        severity: "error"
      });
    } finally {
      if (!silent) setLoading(false);
    }
  };

  // Re-fetch whenever page, pageSize, or any filter changes
  useEffect(() => {
    loadRecords();
  }, [
    page,
    pageSize,
    debouncedSearch,
    workloadFilter,
    categoryFilter,
    sampleStatusFilter,
    testStatusFilter,
    analystIdFilter,
    urgencyFilter,
    fromDate,
    toDate
  ]);

  // Deep-Link URL Parameter Processing (Strict Backward Compatibility)
  useEffect(() => {
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
        setWorkloadFilter(null);
        setSampleStatusFilter("ALL");
      } else {
        setSampleStatusFilter(paramStatus);
      }
    }
    if (paramTestStatus) setTestStatusFilter(paramTestStatus);
    if (paramAnalystId) setAnalystIdFilter(Number(paramAnalystId));
    if (paramUrgency) setUrgencyFilter(paramUrgency);
    if (paramScope === "mine") setWorkloadFilter("mine");
    if (paramView === "table" || paramView === "card" || paramView === "kanban") setViewMode(paramView);
    if (paramSearch && paramSearch !== search) {
      setSearch(paramSearch);
      setDebouncedSearch(paramSearch);
    }

    const currentDeepLinkKey = `${paramSampleId ?? ""}:${paramTestOrderId ?? ""}:${paramOpenSummary ?? ""}`;
    if (processedDeepLinkKeyRef.current === currentDeepLinkKey) {
      return;
    }
    processedDeepLinkKeyRef.current = currentDeepLinkKey;

    const sId = paramSampleId ? Number(paramSampleId) : null;
    const tId = paramTestOrderId ? Number(paramTestOrderId) : null;

    if (sId) {
      setSelectedSampleId(sId);
      if (paramOpenSummary === "true") {
        setSummarySampleId(sId);
      }

      const foundInRecords = records?.find((s) => s.sampleId === sId);
      if (foundInRecords) {
        setExtraSelectedSample(foundInRecords);
        if (tId) {
          const foundTest = foundInRecords.assignedTests?.find((t) => t.testOrderId === tId);
          if (foundTest) {
            setActiveTest(foundTest as unknown as WorkspaceTestOrderSummary);
            setActiveSampleForTest(foundInRecords as unknown as WorkspaceSampleCard);
          }
        }
      } else {
        ReceiveService.getSample(sId).then((fetched) => {
          if (fetched) {
            setExtraSelectedSample(fetched);
            if (tId) {
              const foundTest = fetched.assignedTests?.find((t) => t.testOrderId === tId);
              if (foundTest) {
                setActiveTest(foundTest as unknown as WorkspaceTestOrderSummary);
                setActiveSampleForTest(fetched as unknown as WorkspaceSampleCard);
              }
            }
          }
        }).catch(() => {});
      }
    } else if (tId && records) {
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

    const paramSampleIds = searchParams.get("sampleIds");
    if (paramSampleIds) {
      const ids = paramSampleIds.split(",").map(Number).filter((id) => !isNaN(id) && id > 0);
      if (ids.length >= 2) {
        setCheckedSampleIds(new Set(ids));
        ids.forEach(async (id) => {
          if (!checkedSamplesCache.current.has(id)) {
            try {
              const s = await ReceiveService.getSample(id);
              if (s) checkedSamplesCache.current.set(id, s);
            } catch {
              // ignore
            }
          }
        });
      }
    }
  }, [records, searchParams, search]);

  // Ensure selectedSampleId always resolves to a full sample even if not on page 1
  useEffect(() => {
    if (!selectedSampleId) return;
    const inRecords = records?.find((s) => s.sampleId === selectedSampleId);
    if (inRecords) {
      setExtraSelectedSample(inRecords);
      return;
    }
    if (extraSelectedSample?.sampleId === selectedSampleId) return;
    ReceiveService.getSample(selectedSampleId).then((sample) => {
      if (sample) setExtraSelectedSample(sample);
    }).catch(() => {});
  }, [selectedSampleId, records, extraSelectedSample]);

  // Workload tiles are a toggle: clicking the active one clears it.
  const handleSelectWorkload = (key: WorkloadFilterKey) => {
    setWorkloadFilter((prev) => (prev === key ? null : key));
    setPage(1);
  };

  const handleCategoryFilterChange = (cat: string) => {
    setCategoryFilter(cat);
    setPage(1);
  };

  const handleSampleStatusFilterChange = (status: string) => {
    setSampleStatusFilter(status);
    setPage(1);
  };

  const handleTestStatusFilterChange = (ts: string) => {
    setTestStatusFilter(ts);
    setPage(1);
  };

  const handleFromDateChange = (from: string) => {
    setFromDate(from);
    setPage(1);
  };

  const handleToDateChange = (to: string) => {
    setToDate(to);
    setPage(1);
  };

  const handleResetFilters = () => {
    setSearch("");
    setDebouncedSearch("");
    setShowSelectedOnly(false);
    setWorkloadFilter(null);
    setCategoryFilter("ALL");
    setSampleStatusFilter("ALL");
    setTestStatusFilter("ALL");
    setAnalystIdFilter(null);
    setUrgencyFilter("");
    setFromDate("");
    setToDate("");
    setPage(1);
  };

  const hasActiveFilters = Boolean(
    search ||
    workloadFilter ||
    categoryFilter !== "ALL" ||
    sampleStatusFilter !== "ALL" ||
    testStatusFilter !== "ALL" ||
    analystIdFilter !== null ||
    urgencyFilter ||
    fromDate ||
    toDate
  );

  // Filtered/Displayed Samples Computation:
  // When showSelectedOnly is true, surface all checked samples from cache/records.
  // Otherwise, display server-filtered and paged records.
  const displayRecords = useMemo(() => {
    if (showSelectedOnly) {
      const list: SampleRecord[] = [];
      for (const id of checkedSampleIds) {
        const cached = checkedSamplesCache.current.get(id);
        if (cached) {
          list.push(cached);
        } else {
          const inRecords = records?.find((r) => r.sampleId === id);
          if (inRecords) {
            list.push(inRecords);
            checkedSamplesCache.current.set(id, inRecords);
          }
        }
      }
      return list;
    }
    return records || [];
  }, [showSelectedOnly, checkedSampleIds, records]);

  // Checked samples the current filters/page have pushed out of view.
  const hiddenCheckedCount = useMemo(() => {
    if (checkedSampleIds.size === 0) return 0;
    if (showSelectedOnly) return 0;
    const visible = new Set((records || []).map((r) => r.sampleId));
    let hidden = 0;
    checkedSampleIds.forEach((id) => {
      if (!visible.has(id)) hidden += 1;
    });
    return hidden;
  }, [checkedSampleIds, records, showSelectedOnly]);

  // Derive Selected Sample Object
  const selectedSample = useMemo(() => {
    if (!selectedSampleId) return null;
    if (records) {
      const found = records.find((s) => s.sampleId === selectedSampleId);
      if (found) return found;
    }
    if (extraSelectedSample && extraSelectedSample.sampleId === selectedSampleId) {
      return extraSelectedSample;
    }
    if (checkedSamplesCache.current.has(selectedSampleId)) {
      return checkedSamplesCache.current.get(selectedSampleId) || null;
    }
    return null;
  }, [selectedSampleId, records, extraSelectedSample]);

  const handlePageChange = (_: unknown, newPage: number) => {
    setPage(newPage + 1);
  };

  const handleRowsPerPageChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    setPageSize(parseInt(event.target.value, 10));
    setPage(1);
  };

  // Event Handlers
  const handleSelectSample = (sample: SampleRecord | WorkspaceSampleCard) => {
    setSelectedSampleId(sample.sampleId);
    setExtraSelectedSample(sample as SampleRecord);
  };

  const handleDeselectSample = () => {
    setSelectedSampleId(null);
    setExtraSelectedSample(null);
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
            onClick={() => loadRecords()}
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
            color="primary"
            size="medium"
            onClick={() => setNewSampleDialogOpen(true)}
            startIcon={<AddIcon />}
            sx={{
              fontWeight: 600,
              px: 2.5
            }}
          >
            + New Sample
          </Button>
        </Box>
      </Box>

      {/* Unified KPI Status Cards */}
      <SampleStatusKpiCards
        counts={workloadCounts}
        activeKey={workloadFilter}
        onSelect={handleSelectWorkload}
        isSectionHeadOrAdmin={role === "SectionHead" || role === "SystemAdministrator"}
      />

      {/* Unified Filter Bar - search, display-mode toggle, and export all live here */}
      <SampleFilterBar
        search={search}
        onSearchChange={setSearch}
        categoryFilter={categoryFilter}
        onCategoryFilterChange={handleCategoryFilterChange}
        sampleStatusFilter={sampleStatusFilter}
        onSampleStatusFilterChange={handleSampleStatusFilterChange}
        testStatusFilter={testStatusFilter}
        onTestStatusFilterChange={handleTestStatusFilterChange}
        fromDate={fromDate}
        onFromDateChange={handleFromDateChange}
        toDate={toDate}
        onToDateChange={handleToDateChange}
        onResetFilters={handleResetFilters}
        hasActiveFilters={hasActiveFilters}
        viewMode={viewMode}
        onViewModeChange={setViewMode}
        onExport={() => exportSamplesToCsv(displayRecords)}
      />

      {/* Selection summary - rendered above the layout branch so it survives
          the swap into the grouped-actions split view at 2+ selected. */}
      {records && !loading && (
        <SelectionSummaryBar
          selectedCount={checkedSampleIds.size}
          hiddenCount={hiddenCheckedCount}
          showingSelectedOnly={showSelectedOnly}
          onToggleShowSelectedOnly={() => setShowSelectedOnly((prev) => !prev)}
          onClear={handleDeselectAllChecked}
        />
      )}

      {/* Loading State */}
      {!records || loading ? (
        <LoadingSpinner />
      ) : checkedSampleIds.size >= 2 ? (
        /* GROUPED ACTIONS SPLIT-PANE LAYOUT (When 2+ samples are checked) */
        <Box
          sx={{
            display: "flex",
            flexDirection: { xs: "column", md: "row" },
            gap: 2,
            alignItems: "stretch",
            minHeight: "calc(100vh - 280px)"
          }}
        >
          {/* Left Panel: Compact Sample Register with Checkboxes */}
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
                Laboratory Register ({displayRecords.length}
                {!showSelectedOnly && totalCount > displayRecords.length ? ` of ${totalCount}` : ""})
              </Typography>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                {checkedSampleIds.size} checked
                {hiddenCheckedCount > 0 ? ` · ${hiddenCheckedCount} hidden` : ""}
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
                  <TableRow sx={[tableHeadSx, { "& th": { fontWeight: 700, fontSize: 11, py: 1 } }]}>
                    <TableCell>Item / Reference</TableCell>
                    <TableCell sx={{ width: 65 }}>Type</TableCell>
                    <TableCell sx={{ width: 95 }}>Batch/Ctrl</TableCell>
                    <TableCell sx={{ width: 85 }}>Status</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {displayRecords.map((s) => (
                    <SampleTableRow
                      key={s.sampleId}
                      sample={s as unknown as WorkspaceSampleCard}
                      isSelected={selectedSampleId === s.sampleId}
                      isChecked={checkedSampleIds.has(s.sampleId)}
                      onToggleCheck={handleToggleCheckSample}
                      onSelectSample={(sample) => handleSelectSample(sample)}
                      isCompact={true}
                      visibleColumns={new Set(["category", "batch", "control", "status"])}
                      colSpan={4}
                      onNeedsPreparationClick={() => handlePrepareSample(s)}
                      onCorrected={() => loadRecords(true)}
                      onLifecycleBadgeClick={setSummarySampleId}
                    />
                  ))}
                  {displayRecords.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={4} align="center" sx={{ py: 3, color: "text.secondary", fontSize: 12 }}>
                        No matching samples found.
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
              {!showSelectedOnly && totalCount > 0 && (
                <TablePagination
                  component="div"
                  count={totalCount}
                  page={Math.max(0, page - 1)}
                  onPageChange={handlePageChange}
                  rowsPerPage={pageSize}
                  onRowsPerPageChange={handleRowsPerPageChange}
                  rowsPerPageOptions={[25, 50, 100]}
                  sx={{
                    borderTop: "1px solid",
                    borderColor: "divider",
                    "& .MuiTablePagination-selectLabel, & .MuiTablePagination-displayedRows": {
                      fontSize: 11
                    },
                    "& .MuiTablePagination-toolbar": {
                      minHeight: 36,
                      px: 1
                    }
                  }}
                />
              )}
            </Paper>
          </Box>

          {/* Right Panel: Grouped Actions Panel */}
          <Box
            sx={{
              flex: 1,
              minWidth: 0,
              display: "flex",
              flexDirection: "column",
              maxHeight: { xs: "auto", md: "calc(100vh - 290px)" }
            }}
          >
            <GroupedActionsPanel
              selectedSampleIds={Array.from(checkedSampleIds)}
              onDeselectAll={handleDeselectAllChecked}
              onOpenWorkflow={(sampleId, testOrderId) => {
                const sample = displayRecords.find((r) => r.sampleId === sampleId) ||
                  (extraSelectedSample?.sampleId === sampleId ? extraSelectedSample : null);
                const test = sample?.assignedTests?.find((t) => t.testOrderId === testOrderId);
                if (sample && test) {
                  handleTestClick(test as unknown as WorkspaceTestOrderSummary, sample as unknown as WorkspaceSampleCard);
                }
              }}
              onActionComplete={(result) => {
                setNotification({
                  text: `${result.succeededCount} test${result.succeededCount === 1 ? "" : "s"} started in incubation.${result.skippedCount > 0 ? ` (${result.skippedCount} skipped)` : ""}`,
                  severity: result.skippedCount > 0 ? "warning" : "success"
                });
                loadRecords(true);
              }}
              onPreparationComplete={(result) => {
                setNotification({
                  text: `${result.succeededCount} sample${result.succeededCount === 1 ? "" : "s"} prepared and signed.${result.skippedCount > 0 ? ` (${result.skippedCount} skipped)` : ""}`,
                  severity: result.skippedCount > 0 ? "warning" : "success"
                });
                loadRecords(true);
              }}
            />
          </Box>
        </Box>
      ) : selectedSample ? (
        /* MASTER-DETAIL SPLIT-PANE LAYOUT (When a single sample is selected) */
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
                Laboratory Register ({displayRecords.length}
                {!showSelectedOnly && totalCount > displayRecords.length ? ` of ${totalCount}` : ""})
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
                  <TableRow sx={[tableHeadSx, { "& th": { fontWeight: 700, fontSize: 11, py: 1 } }]}>
                    <TableCell>Item / Reference</TableCell>
                    <TableCell sx={{ width: 65 }}>Type</TableCell>
                    <TableCell sx={{ width: 95 }}>Batch/Ctrl</TableCell>
                    <TableCell sx={{ width: 85 }}>Status</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {displayRecords.map((s) => (
                    <SampleTableRow
                      key={s.sampleId}
                      sample={s as unknown as WorkspaceSampleCard}
                      isSelected={selectedSampleId === s.sampleId}
                      isChecked={checkedSampleIds.has(s.sampleId)}
                      onToggleCheck={handleToggleCheckSample}
                      onSelectSample={(sample) => handleSelectSample(sample)}
                      isCompact={true}
                      visibleColumns={new Set(["category", "batch", "control", "status"])}
                      colSpan={4}
                      onNeedsPreparationClick={() => handlePrepareSample(s)}
                      onCorrected={() => loadRecords(true)}
                      onLifecycleBadgeClick={setSummarySampleId}
                    />
                  ))}
                  {displayRecords.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={4} align="center" sx={{ py: 3, color: "text.secondary", fontSize: 12 }}>
                        No matching samples found.
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
              {!showSelectedOnly && totalCount > 0 && (
                <TablePagination
                  component="div"
                  count={totalCount}
                  page={Math.max(0, page - 1)}
                  onPageChange={handlePageChange}
                  rowsPerPage={pageSize}
                  onRowsPerPageChange={handleRowsPerPageChange}
                  rowsPerPageOptions={[25, 50, 100]}
                  sx={{
                    borderTop: "1px solid",
                    borderColor: "divider",
                    "& .MuiTablePagination-selectLabel, & .MuiTablePagination-displayedRows": {
                      fontSize: 11
                    },
                    "& .MuiTablePagination-toolbar": {
                      minHeight: 36,
                      px: 1
                    }
                  }}
                />
              )}
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
              onCorrected={() => loadRecords(true)}
              onViewAuditHistory={(sampleId) => setAuditSampleId(sampleId)}
              onVoid={(sample) => setVoidingSample(sample as unknown as SampleRecord)}
            />
          </Box>
        </Box>
      ) : (
        /* FULL-WIDTH WORKSPACE REGISTER (When no sample is selected) */
        <>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5, flexWrap: "wrap", gap: 1 }}>
            <Typography sx={{ fontSize: 16, fontWeight: 700, color: theme.palette.primary.main }}>
              Laboratory Register {records ? `(${showSelectedOnly ? displayRecords.length : totalCount} ${totalCount === 1 ? "sample" : "samples"})` : ""}
            </Typography>

            {!showSelectedOnly && totalCount > 0 && (
              <TablePagination
                component="div"
                count={totalCount}
                page={Math.max(0, page - 1)}
                onPageChange={handlePageChange}
                rowsPerPage={pageSize}
                onRowsPerPageChange={handleRowsPerPageChange}
                rowsPerPageOptions={[25, 50, 100, 200]}
                sx={{
                  "& .MuiTablePagination-toolbar": { minHeight: 36, px: 0 },
                  "& .MuiTablePagination-selectLabel, & .MuiTablePagination-displayedRows": { fontSize: 12 }
                }}
              />
            )}
          </Box>

          {viewMode === "table" && (
            <SampleRegisterTable
              samples={displayRecords}
              selectedSampleId={selectedSampleId}
              checkedSampleIds={checkedSampleIds}
              onToggleCheck={handleToggleCheckSample}
              onToggleCheckMany={handleToggleCheckMany}
              onSelectSample={handleSelectSample}
              onTestClick={(test, sample) => {
                handleTestClick(test, sample);
              }}
              onViewSummary={(sample) => {
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
              samples={displayRecords as unknown as WorkspaceSampleCard[]}
              selectedSampleId={selectedSampleId}
              onSelectSample={(s) => setSelectedSampleId(s.sampleId)}
              onNeedsPreparationClick={(s) => handlePrepareSample(s)}
              onLifecycleBadgeClick={setSummarySampleId}
            />
          )}

          {viewMode === "kanban" && (
            <SampleKanbanView
              samples={displayRecords as unknown as WorkspaceSampleCard[]}
              onCardClick={(sampleId) => {
                setSelectedSampleId(sampleId);
              }}
            />
          )}

          {!showSelectedOnly && totalCount > pageSize && (
            <Paper
              elevation={0}
              sx={{
                mt: 1.5,
                border: "1px solid",
                borderColor: "divider",
                borderRadius: 2,
                bgcolor: "background.paper"
              }}
            >
              <TablePagination
                component="div"
                count={totalCount}
                page={Math.max(0, page - 1)}
                onPageChange={handlePageChange}
                rowsPerPage={pageSize}
                onRowsPerPageChange={handleRowsPerPageChange}
                rowsPerPageOptions={[25, 50, 100, 200]}
                sx={{
                  "& .MuiTablePagination-selectLabel, & .MuiTablePagination-displayedRows": {
                    fontSize: 12
                  }
                }}
              />
            </Paper>
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
          loadRecords(true);
        }}
      />

      {/* 6. Preparation Dialog (Water, EM, After Cleaning) */}
      <PreparationDialog
        open={Boolean(preparingSample)}
        sample={preparingSample ? {
          sampleId: preparingSample.sampleId,
          category: preparingSample.category,
          itemId: preparingSample.itemId,
          itemName: preparingSample.displayName,
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
